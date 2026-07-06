using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.ClassifyItems;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Services.TypeSpec;
using Azure.Sdk.Tools.Cli.Tools.TypeSpec;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;


namespace Azure.Sdk.Tools.Cli.Tests.Tools.TypeSpec;

[TestFixture]
public class CustomizedCodeUpdateToolAutoTests
{
    // --- Shared helpers ---

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "azsdk-test-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateTestTypespecProjectPath()
    {
        // Set up a fake tsp project path for mocked tests
        var _specRepoRoot = Path.Combine(Path.GetTempPath(), "test-spec-repo-" + Guid.NewGuid().ToString("N")[..8], "azure-rest-api-specs");
        var _testTspPath = Path.Combine(_specRepoRoot, "specification", "widget", "Widget.Management");
        Directory.CreateDirectory(_testTspPath);
        // Create the customization guide file that the service expects
        var guidePath = Path.Combine(_specRepoRoot, "eng", "common", "knowledge", "customizing-client-tsp.md");
        Directory.CreateDirectory(Path.GetDirectoryName(guidePath)!);
        File.WriteAllText(guidePath, "# TypeSpec Client Customizations\nTest reference content.");
        // For testing, we can just return a dummy path that the TypeSpecHelper will accept as valid.
        return _testTspPath;
    }
    /// <summary>
    /// Creates a fully-wired <see cref="CustomizedCodeUpdateTool"/> with sensible default mocks.
    /// Callers can customise individual mocks before construction by passing them in.
    /// </summary>
    private static (CustomizedCodeUpdateTool tool, ToolMocks mocks) CreateTool(
        LanguageService? languageService = null,
        Action<Mock<IGitHelper>>? configureGit = null,
        Action<Mock<IClassifyService>>? configureClassifier = null,
        Action<Mock<ITypeSpecCustomizationService>>? configureTspCustomization = null,
        Action<Mock<ITypeSpecHelper>>? configureTypeSpecHelper = null,
        ITspClientHelper? tspHelper = null,
        INpxHelper? npxHelper = null)
    {
        int classifyCallCount = 0;
        var gitHelper = new Mock<IGitHelper>();
        gitHelper.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-java");
        gitHelper.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("/mock/repo/root");
        configureGit?.Invoke(gitHelper);

        var feedbackService = new Mock<IAPIViewFeedbackService>();
        var classifierService = new Mock<IClassifyService>();

        // Default ClassifyItemsAsync: handles both passes via a single mock.
        // - First pass (items is empty): populates the list and returns TSP_APPLICABLE.
        // - Second pass (items already populated): returns TSP_APPLICABLE for existing items.
        classifierService.Setup(c => c.ClassifyItemsAsync(It.IsAny<ClassificationKind>(), It.IsAny<ClassifyRequest>(), It.IsAny<CancellationToken>()))
            .Returns<ClassificationKind, ClassifyRequest, CancellationToken>(
                (classifyType, request, ct) =>
                {
                    classifyCallCount++;
                    var customizationRequest = request as ClassifyCustomizationRequest;
                    var items = customizationRequest!.Items;
                    if (classifyCallCount == 1)
                    {
                        items.Clear();
                    }
                    if (items.Count == 0)
                    {
                        // First pass: gather items - create a default item
                        var item = new FeedbackItem { Text = "Rename FooClient to BarClient" };
                        items.Add(item);
                        return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                        { 
                            new FeedbackClassificationResponse.ItemClassificationDetails
                            {
                                ItemId = item.Id,
                                Classification = "TSP_APPLICABLE",
                                Reason = "Can be fixed via TypeSpec",
                                Text = item.Text
                            }
                        }));
                    }

                    // Second pass: classify already-gathered items
                    var actualId = items.FirstOrDefault()?.Id ?? "1";
                    var actualText = items.FirstOrDefault()?.Text ?? "Rename FooClient to BarClient";
                    return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                    {
                        new FeedbackClassificationResponse.ItemClassificationDetails
                        {
                            ItemId = actualId,
                            Classification = "TSP_APPLICABLE",
                            Reason = "Can be fixed via TypeSpec",
                            Text = actualText
                        }
                    }));
                });

        configureClassifier?.Invoke(classifierService);

        var typeSpecCustomization = new Mock<ITypeSpecCustomizationService>();
        // Default: customization succeeds
        typeSpecCustomization.Setup(t => t.ApplyCustomizationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TypeSpecCustomizationServiceResult
            {
                Success = true,
                ChangesSummary = ["Renamed FooClient to BarClient"]
            });
        configureTspCustomization?.Invoke(typeSpecCustomization);

        var typeSpecHelper = new Mock<ITypeSpecHelper>();
        typeSpecHelper.Setup(t => t.IsValidTypeSpecProjectPath(It.IsAny<string>())).Returns(true);

        // Mock GetSpecRepoRootPath to return the path to 'azure-rest-api-specs' folder
        typeSpecHelper.Setup(x => x.GetSpecRepoRootPath(It.IsAny<string>()))
            .Returns<string>(inputPath =>
            {
                if (string.IsNullOrWhiteSpace(inputPath))
                {
                    throw new ArgumentException("Input path cannot be null or whitespace.", nameof(inputPath));
                }
                // Find the 'azure-rest-api-specs' folder in the input path
                var pathParts = inputPath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                var specsIndex = Array.FindIndex(pathParts, p => p.Equals("azure-rest-api-specs", StringComparison.OrdinalIgnoreCase));

                if (specsIndex >= 0)
                {
                    // Return the path up to and including 'azure-rest-api-specs'
                    var rootParts = pathParts.Take(specsIndex + 1);
                    return Path.Combine(pathParts[0].Contains(':') ? "" : Path.DirectorySeparatorChar.ToString(), Path.Combine(rootParts.ToArray()));
                }

                // Fallback: return a test path if 'azure-rest-api-specs' is not found
                return Path.Combine(Path.GetTempPath(), "test-azure-rest-api-specs");
            });

        configureTypeSpecHelper?.Invoke(typeSpecHelper);

        var svc = languageService ?? new ConfigurableLanguageService();
        var tsp = tspHelper ?? new MockTspHelper();

        var tool = new CustomizedCodeUpdateTool(
            new NullLogger<CustomizedCodeUpdateTool>(),
            [svc],
            gitHelper.Object,
            tsp,
            feedbackService.Object,
            classifierService.Object,
            typeSpecCustomization.Object,
            typeSpecHelper.Object,
            npxHelper ?? new Mock<INpxHelper>().Object);

        return (tool, new ToolMocks(gitHelper, feedbackService, classifierService, typeSpecCustomization, typeSpecHelper));
    }

    private record ToolMocks(
        Mock<IGitHelper> GitHelper,
        Mock<IAPIViewFeedbackService> FeedbackService,
        Mock<IClassifyService> ClassifierService,
        Mock<ITypeSpecCustomizationService> TypeSpecCustomization,
        Mock<ITypeSpecHelper> TypeSpecHelper);

    /// <summary>
    /// Builds a classifier configuration whose first pass returns a single CODE_CUSTOMIZATION item, so the
    /// flow proceeds into the custom-code patch/regen pipeline (used by the optional-tspProjectPath tests).
    /// </summary>
    private static Action<Mock<IClassifyService>> CodeCustomizationClassifier(string text) =>
        c => c.Setup(x => x.ClassifyItemsAsync(It.IsAny<ClassificationKind>(), It.IsAny<ClassifyRequest>(), It.IsAny<CancellationToken>()))
            .Returns<ClassificationKind, ClassifyRequest, CancellationToken>(
                (classifyType, request, ct) =>
                {
                    var customizationRequest = request as ClassifyCustomizationRequest;
                    var items = customizationRequest!.Items;
                    items.Clear();
                    if (items.Count == 0)
                    {
                        var item = new FeedbackItem { Text = text };
                        items.Add(item);
                        return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                        {
                                new FeedbackClassificationResponse.ItemClassificationDetails
                                {
                                    ItemId = item.Id,
                                    Classification = "CODE_CUSTOMIZATION",
                                    Reason = "Fix in customization file",
                                    Text = text
                                }
                        }));
                    }
                    return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>()));
                });

    // ========================================================================
    // Happy-path: TSP fix + build succeeds
    // ========================================================================

    [Test]
    public async Task TspFix_BuildPassesFirstIteration_ReturnsSuccess()
    {
        var (tool, _) = CreateTool();
        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.ErrorCode, Is.Null);
        Assert.That(result.Message, Does.Contain("Build passed"));
    }

    [Test]
    public async Task TspFix_BuildFailsAfterRegen_FallsThroughToPatching()
    {
        var buildCalls = 0;
        var svc = new ConfigurableLanguageService(
            buildFunc: () =>
            {
                buildCalls++;
                // First build (after regen) fails, second build (error context / final) passes
                return buildCalls <= 1
                    ? (false, "error: missing import", null)
                    : (true, null, null);
            },
            hasCustomizations: true,
            patchesFunc: () => [new AppliedPatch("test.java", "Fixed import", 1)]);

        var (tool, _) = CreateTool(languageService: svc);
        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Message, Does.Contain("Build passed after code customization patches."));
    }

    // ========================================================================
    // Input validation
    // ========================================================================

    [Test]
    public async Task PackagePath_DoesNotExist_ReturnsInvalidInput()
    {
        var (tool, _) = CreateTool();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(
            packagePath: Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid().ToString("n")),
            tspProjectPath: tspDir,
            customizationRequest: "test customization",
            ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput));
        Assert.That(result.Message, Does.Contain("does not exist"));
    }

    [Test]
    public async Task TspProjectPath_DoesNotExist_ReturnsInvalidInput()
    {
        var (tool, _) = CreateTool();
        var pkg = CreateTempDir();
        var badTspDir = Path.Combine(Path.GetTempPath(), "nonexistent-tsp-" + Guid.NewGuid().ToString("n"));

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: badTspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput));
        Assert.That(result.Message, Does.Contain("does not exist"));
    }

    // ========================================================================
    // Classification edge cases
    // ========================================================================

    [Test]
    public async Task Classification_ReturnsEmptyList_ReturnsInvalidInput()
    {
        var (tool, _) = CreateTool(configureClassifier: c =>
            c.Setup(x => x.ClassifyItemsAsync(It.IsAny<ClassificationKind>(), It.IsAny<ClassifyRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>())));

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput));
        Assert.That(result.Message, Does.Contain("could not be classified"));
    }

    [Test]
    public async Task Classification_ReturnsNullList_ReturnsInvalidInput()
    {
        var (tool, _) = CreateTool(configureClassifier: c =>
            c.Setup(x => x.ClassifyItemsAsync(It.IsAny<ClassificationKind>(), It.IsAny<ClassifyRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ClassifyResponse(ClassificationKind.Customization, null)));

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput));
    }

    [Test]
    public async Task Classification_CopilotCliNotFound_ReturnsCopilotError()
    {
        var innerEx = new InvalidOperationException(
            "Copilot CLI not found at 'runtimes/win-x64/native/copilot.exe'. Ensure the SDK NuGet package was restored correctly.");
        var copilotEx = new CopilotCliUnavailableException(
            "The GitHub Copilot CLI could not be found or failed to start.", innerEx);

        var (tool, _) = CreateTool(configureClassifier: c =>
            c.Setup(x => x.ClassifyItemsAsync(It.IsAny<ClassificationKind>(), It.IsAny<ClassifyRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(copilotEx));

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.UnexpectedError));
        Assert.That(result.Message, Does.Contain("Copilot CLI"));
    }

    [Test]
    public async Task Classification_UnexpectedException_SurfacesActualError()
    {
        var unexpectedEx = new HttpRequestException("Network timeout connecting to AI service");

        var (tool, _) = CreateTool(configureClassifier: c =>
            c.Setup(x => x.ClassifyItemsAsync(It.IsAny<ClassificationKind>(), It.IsAny<ClassifyRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(unexpectedEx));

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.UnexpectedError));
        Assert.That(result.Message, Does.Contain("Network timeout"));
    }

    [Test]
    public async Task Classification_OnlyNonTspItems_ReturnsSuccess()
    {
        // When all items are SUCCESS or REQUIRES_MANUAL_INTERVENTION,
        // no TSP customizations are attempted. Returns success with manual intervention info.
        var (tool, _) = CreateTool(configureClassifier: c =>
            c.Setup(x => x.ClassifyItemsAsync(It.IsAny<ClassificationKind>(), It.IsAny<ClassifyRequest>(), It.IsAny<CancellationToken>()))
                .Returns<ClassificationKind, ClassifyRequest, CancellationToken>(
                    (classifyType, classifyRequest, cancellationToken) =>
                    {
                        var items = new List<FeedbackItem>();
                        var item1 = new FeedbackItem { Text = "Restructure hierarchy" };
                        var item2 = new FeedbackItem { Text = "Looks good" };
                        items.Add(item1);
                        items.Add(item2);
                        return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                        {
                                new FeedbackClassificationResponse.ItemClassificationDetails
                                {
                                    ItemId = item1.Id, Classification = "REQUIRES_MANUAL_INTERVENTION",
                                    Reason = "Complex change", Text = "Restructure hierarchy"
                                },
                                new FeedbackClassificationResponse.ItemClassificationDetails
                                {
                                    ItemId = item2.Id, Classification = "SUCCESS",
                                    Reason = "Already addressed", Text = "Looks good"
                                }
                        }));
                    }));

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("manual intervention"));
    }

    [Test]
    public async Task Classification_EmptyOnSecondPass_StillBuildsForContext()
    {
        // First pass returns TSP_APPLICABLE, regen succeeds, build fails.
        // Second pass (re-classify with build errors) returns empty — no further action.
        var classifyCalls = 0;
        var buildCalls = 0;
        var svc = new ConfigurableLanguageService(buildFunc: () =>
        {
            buildCalls++;
            return (false, "error: still broken", null);
        });

        var (tool, _) = CreateTool(
            languageService: svc,
            configureClassifier: c =>
                c.Setup(x => x.ClassifyItemsAsync(It.IsAny<ClassificationKind>(), It.IsAny<ClassifyRequest>(), It.IsAny<CancellationToken>()))
                    .Returns<ClassificationKind, ClassifyRequest, CancellationToken>(
                        (classifyType, classifyRequest, cancellationToken) =>
                        {
                            classifyCalls++;
                            var customizationRequest = classifyRequest as ClassifyCustomizationRequest;
                            var items = customizationRequest!.Items;
                            if (classifyCalls == 1)
                            {
                                items.Clear();
                            }
                            if (items.Count == 0)
                            {
                                // First pass: populate items with TSP_APPLICABLE
                                var item = new FeedbackItem { Text = "rename X" };
                                items.Add(item);
                                return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                                {
                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = item.Id, Classification = "TSP_APPLICABLE",
                                            Reason = "fixable", Text = "rename X"
                                        }
                                }));
                            }
                            // Second pass: return empty
                            return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>()));
                        }));

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(classifyCalls, Is.EqualTo(2), "Should classify twice: initial pass + second pass with build context");
        Assert.That(buildCalls, Is.EqualTo(1), "Should build once after regen");
    }

    // ========================================================================
    // TSP customization failures
    // ========================================================================

    [Test]
    public async Task TspCustomization_AllFail_ReclassifiedOnSecondPass()
    {
        var classifyCalls = 0;
        var (tool, _) = CreateTool(
            configureTspCustomization: t =>
                t.Setup(x => x.ApplyCustomizationAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string?>(),
                        It.IsAny<int>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new TypeSpecCustomizationServiceResult
                    {
                        Success = false,
                        ChangesSummary = [],
                        FailureReason = "Could not parse TypeSpec project"
                    }),
            configureClassifier: c =>
                c.Setup(x => x.ClassifyItemsAsync(It.IsAny<ClassificationKind>(), It.IsAny<ClassifyRequest>(), It.IsAny<CancellationToken>()))
                    .Returns<ClassificationKind, ClassifyRequest, CancellationToken>(
                        (classifyType, classifyRequest, cancellationToken) =>
                        {
                            classifyCalls++;
                            var customizationRequest = classifyRequest as ClassifyCustomizationRequest;
                            var items = customizationRequest!.Items;
                            if (classifyCalls == 1)
                            {
                                items.Clear();
                            }
                            if (items.Count == 0)
                            {
                                // First pass: populate items with TSP_APPLICABLE
                                var item = new FeedbackItem { Text = "rename X" };
                                items.Add(item);
                                return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                                {
                                    new FeedbackClassificationResponse.ItemClassificationDetails
                                    {
                                            ItemId = item.Id, Classification = "TSP_APPLICABLE",
                                            Reason = "fixable", Text = "rename X"
                                    }
                                }));
                            }
                            // Second pass: classifier sees failure context and flags manual intervention
                            var actualId = items.FirstOrDefault()?.Id ?? "1";
                            return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                            {
     
                                new FeedbackClassificationResponse.ItemClassificationDetails
                                {
                                    ItemId = actualId, Classification = "REQUIRES_MANUAL_INTERVENTION",
                                    Reason = "TSP customization failed, manual fix needed", Text = "rename X"
                                }
                            }));
                        }));

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(classifyCalls, Is.EqualTo(2), "Should classify twice: pass 1 + pass 2 after TSP failure");
        Assert.That(result.NextSteps, Is.Not.Null.And.Count.GreaterThan(0), "Should include manual intervention from second pass");
    }

    [Test]
    public async Task TspCustomization_PartialFailure_ProceedsToBuild()
    {
        // 2 TSP_APPLICABLE items: first succeeds, second fails.
        // Should proceed to regeneration and build since at least one succeeded.
        var customizeCalls = 0;
        var (tool, _) = CreateTool(
            configureClassifier: c =>
                c.Setup(x => x.ClassifyItemsAsync(It.IsAny<ClassificationKind>(), It.IsAny<ClassifyRequest>(), It.IsAny<CancellationToken>()))
                    .Returns<ClassificationKind, ClassifyRequest, CancellationToken>(
                        (classifyType, classifyRequest, cancellationToken) =>
                        {
                            var items = new List<FeedbackItem>();
                            if (classifyRequest is ClassifyCustomizationRequest customizationRequest)
                            {
                                items = customizationRequest.Items;
                            }
                            if (items.Count == 0)
                            {
                                var item = new FeedbackItem { Text = "rename X to Y" };
                                items.Add(item);
                                return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                                {
                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = item.Id, Classification = "TSP_APPLICABLE",
                                            Reason = "fixable", Text = "rename X to Y"
                                        }
                                }));
                            }
                            var actualId = items.FirstOrDefault()?.Id ?? "1";
                            return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                            {
                                    new FeedbackClassificationResponse.ItemClassificationDetails
                                    {
                                        ItemId = actualId, Classification = "TSP_APPLICABLE",
                                        Reason = "fixable", Text = "rename X to Y"
                                    }
                            }));
                        }),
            configureTspCustomization: t =>
                t.Setup(x => x.ApplyCustomizationAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string?>(),
                        It.IsAny<int>(),
                        It.IsAny<CancellationToken>()))
                    .Returns(() =>
                    {
                        customizeCalls++;
                        return Task.FromResult(customizeCalls == 1
                            ? new TypeSpecCustomizationServiceResult { Success = true, ChangesSummary = ["renamed X to Y"] }
                            : new TypeSpecCustomizationServiceResult { Success = false, ChangesSummary = [], FailureReason = "unsupported" });
                    }));

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        // Build passes (default mock), so overall success
        Assert.That(result.Success, Is.True);
        Assert.That(result.Message, Does.Contain("Build passed"));
    }

    [Test]
    public async Task TspRegeneration_Fails_ReclassifiesOnSecondPass()
    {
        var classifyCalls = 0;
        var failingTsp = new MockTspHelper(updateSuccess: false, updateError: "tsp-client failed: exit code 1");
        var (tool, _) = CreateTool(
            tspHelper: failingTsp,
            configureClassifier: c =>
                c.Setup(x => x.ClassifyItemsAsync(It.IsAny<ClassificationKind>(), It.IsAny<ClassifyRequest>(), It.IsAny<CancellationToken>()))
                    .Returns<ClassificationKind, ClassifyRequest, CancellationToken>(
                        (classifyType, classifyRequest, cancellationToken) =>
                        {
                            classifyCalls++;
                            var customizationRequest = classifyRequest as ClassifyCustomizationRequest;
                            var items = customizationRequest!.Items;
                            if (classifyCalls == 1)
                            {
                                items.Clear();
                            }
                            if (items.Count == 0)
                            {
                                // First pass: populate items with TSP_APPLICABLE
                                var item = new FeedbackItem { Text = "rename X" };
                                items.Add(item);
                                return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                                {
                                    new FeedbackClassificationResponse.ItemClassificationDetails
                                    {
                                            ItemId = item.Id, Classification = "TSP_APPLICABLE",
                                            Reason = "fixable", Text = "rename X"
                                        }
                                }));
                            }
                            // Second pass: reclassify as manual intervention (emitter issue)
                            var actualId = items.FirstOrDefault()?.Id ?? "1";
                            return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                            {
                                    new FeedbackClassificationResponse.ItemClassificationDetails
                                    {
                                        ItemId = actualId, Classification = "REQUIRES_MANUAL_INTERVENTION",
                                        Reason = "Emitter issue, cannot be patched", Text = "rename X"
                                    }
                            }));
                        }));

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        // Regen failure → second pass reclassifies as manual intervention
        Assert.That(classifyCalls, Is.EqualTo(2), "Should classify twice: initial + second pass after regen failure");
        Assert.That(result.NextSteps, Is.Not.Null.And.Count.GreaterThan(0), "Should include manual intervention steps");
    }

    [Test]
    public async Task RegenFails_TspCompiled_EmitterIssue_ClassifierGivesManualGuidance()
    {
        // TSP fix succeeds (compiles), but regen fails — likely an emitter issue we can't patch.
        // Second pass classifier should see the regen failure context and flag manual intervention
        // without retrying, since retries won't fix an emitter problem.
        var classifyCalls = 0;
        var buildCalls = 0;
        string? secondPassContext = null;

        var failingTsp = new MockTspHelper(updateSuccess: false, updateError: "emitter @azure-tools/typespec-java failed: unexpected token");
        var svc = new ConfigurableLanguageService(buildFunc: () =>
        {
            buildCalls++;
            return (true, null, null);
        });

        var (tool, _) = CreateTool(
            languageService: svc,
            tspHelper: failingTsp,
            configureClassifier: c =>
                c.Setup(x => x.ClassifyItemsAsync(It.IsAny<ClassificationKind>(), It.IsAny<ClassifyRequest>(), It.IsAny<CancellationToken>()))
                    .Returns<ClassificationKind, ClassifyRequest, CancellationToken>(
                        (classifyType, classifyRequest, cancellationToken) =>
                        {
                            classifyCalls++;
                            var customizationRequest = classifyRequest as ClassifyCustomizationRequest;
                            if (classifyCalls == 1)
                            {
                                customizationRequest!.Items.Clear();
                            }
                            var items = customizationRequest!.Items;
                            if (items.Count == 0)
                            {
                                // First pass: populate items with TSP_APPLICABLE
                                var item = new FeedbackItem { Text = "rename FooClient to BarClient" };
                                items.Add(item);
                                return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                                {

                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = item.Id, Classification = "TSP_APPLICABLE",
                                            Reason = "rename operation", Text = "rename FooClient to BarClient"
                                        }
                                }));
                            }
                            // Second pass: classifier sees regen failure and determines manual intervention
                            secondPassContext = items.FirstOrDefault()?.Context;
                            var actualId = items.FirstOrDefault()?.Id ?? "1";
                            return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                            {
                                    new FeedbackClassificationResponse.ItemClassificationDetails
                                    {
                                        ItemId = actualId, Classification = "REQUIRES_MANUAL_INTERVENTION",
                                        Reason = "Emitter issue — cannot resolve via TSP or patching",
                                        Text = "rename FooClient to BarClient"
                                    }
                            }));
                        }));

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir,
            customizationRequest: "Rename FooClient to BarClient", ct: CancellationToken.None);

        // Verified: exactly 2 classifier calls (pass 1 + pass 2), no retry loop
        Assert.That(classifyCalls, Is.EqualTo(2), "Should classify twice: pass 1 + pass 2 after regen failure");

        // Regen failed so no build in the regen block, but a "build for error context" call should happen
        Assert.That(buildCalls, Is.EqualTo(1), "Should build once for error context since regen failed");

        // Second pass should have the regen failure context
        Assert.That(secondPassContext, Does.Contain("Regeneration failed"), "Second pass should see regen failure");
        Assert.That(secondPassContext, Does.Contain("emitter @azure-tools/typespec-java failed"), "Should include the emitter error details");

        // Result flags manual intervention
        Assert.That(result.NextSteps, Is.Not.Null.And.Count.GreaterThan(0), "Should include manual intervention steps");
        Assert.That(result.NextSteps![0], Does.Contain("Emitter issue"));
    }

    // ========================================================================
    // Customized code update pipeline (after TSP loop exhausted)
    // ========================================================================

    [Test]
    public async Task BuildFailsBothTries_NoCustomizedCodeUpdateSupport_ReturnsNoLanguageService()
    {
        var svc = new ConfigurableLanguageService(
            buildFunc: () => (false, "error CS1234", null),
            isCustomizedCodeUpdateSupported: false);

        var (tool, _) = CreateTool(languageService: svc);
        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.NoLanguageService));
    }

    [Test]
    public async Task BuildFails_NoCustomizationFiles_ReturnsBuildNoCustomizationsFailed()
    {
        var svc = new ConfigurableLanguageService(
            buildFunc: () => (false, "error CS1234: type not found", null),
            hasCustomizations: false);

        var (tool, _) = CreateTool(languageService: svc);
        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.BuildNoCustomizationsFailed));
        Assert.That(result.BuildResult, Does.Contain("error CS1234"));
    }

    [Test]
    public async Task BuildFails_NoPatchesApplied_ReturnsPatchesFailed()
    {
        var svc = new ConfigurableLanguageService(
            buildFunc: () => (false, "error: unknown symbol", null),
            hasCustomizations: true,
            patchesFunc: () => []);

        var (tool, _) = CreateTool(languageService: svc);
        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.PatchesFailed));
        Assert.That(result.BuildResult, Does.Contain("error: unknown symbol"));
    }

    [Test]
    public async Task BuildFails_PatchesApplied_FinalBuildSucceeds_ReturnsSuccess()
    {
        var buildCalls = 0;
        // Pass 1: build fails (1st call), falls through to patch pipeline, final build (2nd call) passes
        var svc = new ConfigurableLanguageService(
            buildFunc: () =>
            {
                buildCalls++;
                return buildCalls <= 1
                    ? (false, "error: variable already defined", null)
                    : (true, null, null);
            },
            hasCustomizations: true,
            patchesFunc: () => [new AppliedPatch("test.py", "Fixed variable conflict", 1)],
            language: SdkLanguage.Python);

        var (tool, _) = CreateTool(languageService: svc, configureGit: g =>
            g.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-python"));
        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Message, Does.Contain("Build passed after code customization patches."));
        Assert.That(result.AppliedPatches, Is.Not.Null.And.Count.EqualTo(1));
    }

    [Test]
    public async Task BuildFails_PatchesApplied_FinalBuildStillFails_ReturnsBuildAfterPatchesFailed()
    {
        var svc = new ConfigurableLanguageService(
            buildFunc: () => (false, "same error every time", null),
            hasCustomizations: true,
            patchesFunc: () => [new AppliedPatch("test.java", "Applied test patch", 1)]);

        var (tool, _) = CreateTool(languageService: svc);
        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.BuildAfterPatchesFailed));
        Assert.That(result.BuildResult, Is.Not.Null);
        Assert.That(result.AppliedPatches, Is.Not.Null.And.Count.EqualTo(1));
    }

    // ========================================================================
    // CODE_CUSTOMIZATION classification route
    // ========================================================================

    [Test]
    public async Task CodeCustomization_ClassifiedItem_SkipsTsp_PatchesApplied_BuildSucceeds()
    {
        // Scenario: classifier returns CODE_CUSTOMIZATION (not TSP_APPLICABLE).
        // The item is removed from the feedback dictionary and no TSP fixes are attempted.
        // Regen is skipped (no TSP changes) → build runs for error context → falls through
        // to the patch pipeline → patches applied → Java regen → final build passes.
        var buildCalls = 0;
        var classifyCalls = 0;

        var svc = new ConfigurableLanguageService(
            buildFunc: () =>
            {
                buildCalls++;
                // First build (error context for patch agent) fails; second build (after patches) passes
                return buildCalls <= 1
                    ? (false, "error: cannot find symbol maxSpeakers", null)
                    : (true, null, null);
            },
            hasCustomizations: true,
            patchesFunc: () =>
            [
                new AppliedPatch("SpeechTranscriptionCustomization.java", "Renamed maxSpeakers to maxSpeakerCount", 2)
            ],
            language: SdkLanguage.Java);

        var (tool, _) = CreateTool(
            languageService: svc,
            configureClassifier: c =>
                c.Setup(x => x.ClassifyItemsAsync(It.IsAny<ClassificationKind>(), It.IsAny<ClassifyRequest>(), It.IsAny<CancellationToken>()))
                    .Returns<ClassificationKind, ClassifyRequest, CancellationToken>(
                        (classifyType, classifyRequest, cancellationToken) =>
                        {
                            classifyCalls++;
                            var customizationRequest = classifyRequest as ClassifyCustomizationRequest;
                            var items = customizationRequest!.Items;
                            if (classifyCalls == 1)
                            {
                                items.Clear();
                            }
                            if (items.Count == 0)
                            {
                                // First pass: CODE_CUSTOMIZATION classification
                                var item = new FeedbackItem { Text = "Rename maxSpeakers to maxSpeakerCount in customization code" };
                                items.Add(item);
                                return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                                {
                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = item.Id,
                                            Classification = "CODE_CUSTOMIZATION",
                                            Reason = "Build error references generated code; fix is in the customization file",
                                            Text = "Rename maxSpeakers to maxSpeakerCount in customization code"
                                        }
                                }));
                            }
                            // Second iteration: feedback dictionary is empty → return empty
                            return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>()));
                        }));

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir,
            customizationRequest: "Rename maxSpeakers to maxSpeakerCount", ct: CancellationToken.None);

        // Verify the CODE_CUSTOMIZATION route succeeded end-to-end
        Assert.That(result.Success, Is.True);
        Assert.That(result.Message, Does.Contain("Build passed after code customization patches."));
        Assert.That(result.AppliedPatches, Is.Not.Null.And.Count.EqualTo(1));
        Assert.That(result.AppliedPatches![0].FilePath, Is.EqualTo("SpeechTranscriptionCustomization.java"));
        Assert.That(result.AppliedPatches[0].ReplacementCount, Is.EqualTo(2));
        Assert.That(result.ErrorCode, Is.Null);
    }

    // ========================================================================
    // Java-specific: regen after patches
    // ========================================================================

    [Test]
    public async Task Java_RegenAfterPatches_Fails_ReturnsRegenerateAfterPatchesFailed()
    {
        var svc = new ConfigurableLanguageService(
            buildFunc: () => (false, "error in build", null),
            hasCustomizations: true,
            patchesFunc: () => [new AppliedPatch("test.java", "patch", 1)],
            language: SdkLanguage.Java);

        // Pass 1 regen call succeeds (1st call), Java regen-after-patches (2nd) fails
        var failingTspForJavaRegen = new CallCountMockTspHelper(failAfterCall: 1, failError: "regen failed: tsp-client error");
        var (tool, _) = CreateTool(languageService: svc, tspHelper: failingTspForJavaRegen);
        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.RegenerateAfterPatchesFailed));
        Assert.That(result.AppliedPatches, Is.Not.Null.And.Count.EqualTo(1));
    }

    // ========================================================================
    // Iteration context & feedback flow
    // ========================================================================

    [Test]
    public async Task CustomizationRequest_FlowsToClassifier()
    {
        string? capturedFeedbackText = null;
        var (tool, _) = CreateTool(configureClassifier: c =>
            c.Setup(x => x.ClassifyItemsAsync(It.IsAny<ClassificationKind>(), It.IsAny<ClassifyRequest>(), It.IsAny<CancellationToken>()))
                .Returns<ClassificationKind, ClassifyRequest, CancellationToken>(
                    (classifyType, classifyRequest, cancellationToken) =>
                    {
                        if (classifyRequest is ClassifyCustomizationRequest customizationRequest)
                        {
                            var item = customizationRequest!.Items.FirstOrDefault();
                            capturedFeedbackText = item!.Text;
                            return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                            {
                                new FeedbackClassificationResponse.ItemClassificationDetails
                                {
                                    ItemId = item.Id, Classification = "TSP_APPLICABLE",
                                    Reason = "test", Text = item.Text
                                }
                            }));
                        }
                        return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>()));
                    }));

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "Please rename FooClient", ct: CancellationToken.None);

        Assert.That(capturedFeedbackText, Is.EqualTo("Please rename FooClient"));
    }

    [Test]
    public async Task SecondPass_FeedbackIncludesBuildErrorContext()
    {
        // TSP fix applied, regen succeeds, build fails → second pass should see build error context
        var classifyCalls = 0;
        string? secondCallContext = null;

        var svc = new ConfigurableLanguageService(buildFunc: () =>
            (false, "error CS0246: type 'FooClient' not found", null));

        var (tool, _) = CreateTool(
            languageService: svc,
            configureClassifier: c =>
                c.Setup(x => x.ClassifyItemsAsync(It.IsAny<ClassificationKind>(), It.IsAny<ClassifyRequest>(), It.IsAny<CancellationToken>()))
                    .Returns<ClassificationKind, ClassifyRequest, CancellationToken>(
                        (classifyType, classifyRequest, cancellationToken) =>
                        {
                            classifyCalls++;
                            var items = new List<FeedbackItem>();
                            if (classifyRequest is ClassifyCustomizationRequest customizationRequest)
                            {
                                items = customizationRequest.Items;
                            }
                            if (items.Count == 0)
                            {
                                // First pass: populate items with TSP_APPLICABLE
                                var item = new FeedbackItem { Text = "rename FooClient" };
                                items.Add(item);
                                return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                                {

                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = item.Id, Classification = "TSP_APPLICABLE",
                                            Reason = "fixable", Text = "rename FooClient"
                                        }
                                }));
                            }
                            // Second pass: capture context + return TSP_APPLICABLE to stay in loop
                            if (classifyCalls == 2)
                            {
                                secondCallContext = items.FirstOrDefault()?.Context;
                            }

                            var actualId = items.FirstOrDefault()?.Id ?? "1";
                            return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                            {
                                    new FeedbackClassificationResponse.ItemClassificationDetails
                                    {
                                        ItemId = actualId, Classification = "TSP_APPLICABLE",
                                        Reason = "fixable", Text = "rename FooClient"
                                    }
                            }));
                        }));

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir,
            customizationRequest: "Rename FooClient to BarClient", ct: CancellationToken.None);

        Assert.That(classifyCalls, Is.EqualTo(2));
        Assert.That(secondCallContext, Does.Contain("Typespec changes applied"));
        Assert.That(secondCallContext, Does.Contain("Renamed FooClient to BarClient"));
        Assert.That(secondCallContext, Does.Contain("Build Result"));
        Assert.That(secondCallContext, Does.Contain("error CS0246"));
    }

    [Test]
    public async Task TwoPass_ClassifiesExactlyTwiceAndBuildsOnce()
    {
        // Build fails after regen → second pass reclassifies with error context
        var buildCalls = 0;
        var classifyCalls = 0;
        var svc = new ConfigurableLanguageService(
            buildFunc: () =>
            {
                buildCalls++;
                return (false, "persistent error", null);
            });

        var (tool, _) = CreateTool(
            languageService: svc,
            configureClassifier: c =>
                c.Setup(x => x.ClassifyItemsAsync(It.IsAny<ClassificationKind>(), It.IsAny<ClassifyRequest>(), It.IsAny<CancellationToken>()))
                    .Returns<ClassificationKind, ClassifyRequest, CancellationToken>(
                        (classifyType, classifyRequest, cancellationToken) =>
                        {
                            classifyCalls++;
                            var items = new List<FeedbackItem>();
                            if (classifyRequest is ClassifyCustomizationRequest customizationRequest)
                            {
                                items = customizationRequest.Items;
                            }
                            if (items.Count == 0)
                            {
                                // First pass: populate items with TSP_APPLICABLE
                                var item = new FeedbackItem { Text = "rename X" };
                                items.Add(item);
                                return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                                {
                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = item.Id, Classification = "TSP_APPLICABLE",
                                            Reason = "fixable", Text = "rename X"
                                        }
                                }));
                            }
                            // Second pass
                            var actualId = items.FirstOrDefault()?.Id ?? "1";
                            return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                            {
                                    new FeedbackClassificationResponse.ItemClassificationDetails
                                    {
                                        ItemId = actualId, Classification = "TSP_APPLICABLE",
                                        Reason = "fixable", Text = "rename X"
                                    }
                            }));
                        }));

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(classifyCalls, Is.EqualTo(2), "Should classify exactly 2 times (pass 1 + pass 2)");
        Assert.That(buildCalls, Is.EqualTo(1), "Should build exactly once after regen");
    }

    public async Task Regeneration_PassesTspProjectPathAsLocalSpecRepo()
    {
        // Verify that UpdateGenerationAsync receives tspProjectPath (absolute) as localSpecRepoPath.
        // tsp-client syncCommand expects the TypeSpec project directory, not the repo root.
        string? capturedLocalSpecRepo = null;
        var tsp = new Mock<ITspClientHelper>();
        tsp.Setup(t => t.UpdateGenerationAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string?, bool, string?, CancellationToken>(
                (_, _, _, localSpec, _) => capturedLocalSpecRepo = localSpec)
            .ReturnsAsync(new TspToolResponse { IsSuccessful = true, TypeSpecProject = "/pkg" });

        var (tool, _) = CreateTool(tspHelper: tsp.Object);

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        var expectedLocalSpec = Path.GetFullPath(tspDir);
        Assert.That(capturedLocalSpecRepo, Is.EqualTo(expectedLocalSpec),
            "Should pass the local TypeSpec project path as localSpecRepoPath");
    }

    [Test]
    public async Task Java_RegenAfterPatches_PassesTspProjectPathAsLocalSpecRepo()
    {
        // Verify that the Java post-patch regeneration path also uses the local TypeSpec project path.
        var tspDir = CreateTestTypespecProjectPath();

        string? capturedLocalSpecRepo = null;
        var captureOnCall = 2; // Java regen is the 2nd UpdateGenerationAsync call
        var callCount = 0;

        var tsp = new Mock<ITspClientHelper>();
        tsp.Setup(t => t.UpdateGenerationAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string?, bool, string?, CancellationToken>(
                (_, _, _, localSpec, _) =>
                {
                    callCount++;
                    if (callCount == captureOnCall)
                    {
                        capturedLocalSpecRepo = localSpec;
                    }
                })
            .ReturnsAsync(new TspToolResponse { IsSuccessful = true, TypeSpecProject = "/pkg" });

        var svc = new ConfigurableLanguageService(
            buildFunc: () => (false, "build error", null),
            hasCustomizations: true,
            patchesFunc: () => [new AppliedPatch("test.java", "patch", 1)],
            language: SdkLanguage.Java);

        var (tool, _) = CreateTool(
            languageService: svc,
            tspHelper: tsp.Object);

        var pkg = CreateTempDir();
        await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        var expectedLocalSpec = Path.GetFullPath(tspDir);
        Assert.That(callCount, Is.GreaterThanOrEqualTo(2), "Should call UpdateGenerationAsync at least twice for Java");
        Assert.That(capturedLocalSpecRepo, Is.EqualTo(expectedLocalSpec),
            "Java post-patch regen should also receive the local TypeSpec project path");
    }

    // ========================================================================
    // JavaScript-specific: customization apply after regeneration
    // ========================================================================

    [Test]
    public async Task JavaScript_CustomizationApply_CalledAfterRegen()
    {
        var npxHelperMock = new Mock<INpxHelper>();
        npxHelperMock.Setup(p => p.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0 });

        var svc = new ConfigurableLanguageService(
            language: SdkLanguage.JavaScript,
            hasCustomizations: true);

        var (tool, _) = CreateTool(
            languageService: svc,
            npxHelper: npxHelperMock.Object,
            configureGit: g => g.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-js"));

        var pkg = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(pkg, "src"));
        var tspDir = CreateTestTypespecProjectPath();

        await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        npxHelperMock.Verify(p => p.Run(
            It.Is<NpxOptions>(o =>
                o.Args.Contains("dev-tool") &&
                o.Args.Contains("customization") &&
                o.Args.Contains("apply")),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce,
            "Should run 'npx dev-tool customization apply' for JavaScript packages with customizations");
    }

    [Test]
    public async Task JavaScript_NoCustomizations_SkipsCustomizationApply()
    {
        var npxHelperMock = new Mock<INpxHelper>();
        npxHelperMock.Setup(p => p.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0 });

        var svc = new ConfigurableLanguageService(
            language: SdkLanguage.JavaScript,
            hasCustomizations: false);

        var (tool, _) = CreateTool(
            languageService: svc,
            npxHelper: npxHelperMock.Object,
            configureGit: g => g.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-js"));

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        npxHelperMock.Verify(p => p.Run(
            It.Is<NpxOptions>(o =>
                o.Args.Contains("dev-tool") &&
                o.Args.Contains("customization") &&
                o.Args.Contains("apply")),
            It.IsAny<CancellationToken>()), Times.Never,
            "Should NOT run 'npx dev-tool customization apply' when no customizations exist");
    }

    [Test]
    public async Task JavaScript_BuildFailsAfterCustomizationApply_FallsThroughToPatching()
    {
        var buildCalls = 0;
        var npxHelperMock = new Mock<INpxHelper>();
        npxHelperMock.Setup(p => p.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0 });

        var svc = new ConfigurableLanguageService(
            language: SdkLanguage.JavaScript,
            hasCustomizations: true,
            isCustomizedCodeUpdateSupported: true,
            buildFunc: () =>
            {
                buildCalls++;
                // First build fails (after regen + customization apply), second succeeds (after patches)
                return buildCalls <= 1
                    ? (false, "error TS2345: Argument of type 'string' is not assignable", null)
                    : (true, null, null);
            },
            patchesFunc: () => [new AppliedPatch("src/client.ts", "Fixed type mismatch", 1)]);

        var (tool, _) = CreateTool(
            languageService: svc,
            npxHelper: npxHelperMock.Object,
            configureGit: g => g.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-js"));

        var pkg = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(pkg, "src"));
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.AppliedPatches, Has.Count.EqualTo(1));
        Assert.That(result.Message, Does.Contain("Build passed after code customization patches."));
    }

    [Test]
    public async Task NonJavaScript_SkipsCustomizationApply()
    {
        var npxHelperMock = new Mock<INpxHelper>();
        npxHelperMock.Setup(p => p.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0 });

        var svc = new ConfigurableLanguageService(
            language: SdkLanguage.Java,
            hasCustomizations: true);

        var (tool, _) = CreateTool(languageService: svc, npxHelper: npxHelperMock.Object);
        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        npxHelperMock.Verify(p => p.Run(
            It.Is<NpxOptions>(o =>
                o.Args.Contains("dev-tool") &&
                o.Args.Contains("customization") &&
                o.Args.Contains("apply")),
            It.IsAny<CancellationToken>()), Times.Never,
            "Should NOT run 'npx dev-tool customization apply' for non-JavaScript languages");
    }

    // ========================================================================
    // EditScope.CustomCode (custom-code-only; never edits spec inputs)
    // ========================================================================

    [Test]
    public async Task CustomCodeScope_TspApplicableOnly_ReturnsSpecChangeRequired_DoesNotApplyTsp()
    {
        // The default classifier returns a single TSP_APPLICABLE item with no code customizations.
        // With CustomCode scope this must NOT be applied (no spec-input edits); instead it is reported
        // as out of scope with errorCode 'SpecChangeRequired'.
        var (tool, mocks) = CreateTool();
        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(
            packagePath: pkg,
            tspProjectPath: tspDir,
            customizationRequest: "Rename FooClient to BarClient",
            editScope: EditScope.CustomCode,
            ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.SpecChangeRequired));
        Assert.That(result.SpecChangeRequired, Is.Not.Null.And.Count.EqualTo(1));

        // Critically, CustomCode scope must never apply spec-input (client.tsp) customizations.
        mocks.TypeSpecCustomization.Verify(t => t.ApplyCustomizationAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never,
            "CustomCode scope must not apply TypeSpec (spec-input) customizations.");
    }

    [Test]
    public async Task CustomCodeScope_CodeCustomization_PatchesApplied_BuildSucceeds()
    {
        // CustomCode scope still performs custom-code patching: a CODE_CUSTOMIZATION item flows through
        // the patch pipeline exactly like update mode, and no spec-input edits are made.
        var buildCalls = 0;
        var svc = new ConfigurableLanguageService(
            buildFunc: () =>
            {
                buildCalls++;
                return buildCalls <= 1
                    ? (false, "error: cannot find symbol maxSpeakers", null)
                    : (true, null, null);
            },
            hasCustomizations: true,
            patchesFunc: () =>
            [
                new AppliedPatch("SpeechTranscriptionCustomization.java", "Renamed maxSpeakers to maxSpeakerCount", 2)
            ],
            language: SdkLanguage.Java);

        var (tool, mocks) = CreateTool(
            languageService: svc,
            configureClassifier: c =>
                c.Setup(x => x.ClassifyItemsAsync(It.IsAny<ClassificationKind>(), It.IsAny<ClassifyRequest>(), It.IsAny<CancellationToken>()))
                    .Returns<ClassificationKind, ClassifyRequest, CancellationToken>(
                        (classifyType, classifyRequest, cancellationToken) =>
                        {
                            var items = new List<FeedbackItem>();
                            if (classifyRequest is ClassifyCustomizationRequest customizationRequest)
                            {
                                items = customizationRequest.Items;
                                items.Clear();
                            }
                            if (items.Count == 0)
                            {
                                var item = new FeedbackItem { Text = "Rename maxSpeakers to maxSpeakerCount in customization code" };
                                items.Add(item);
                                return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                                {
                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = item.Id,
                                            Classification = "CODE_CUSTOMIZATION",
                                            Reason = "Fix is in the customization file",
                                            Text = "Rename maxSpeakers to maxSpeakerCount in customization code"
                                        }
                                }));
                            }
                            return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>()));
                        }));

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(
            packagePath: pkg,
            tspProjectPath: tspDir,
            customizationRequest: "Rename maxSpeakers to maxSpeakerCount",
            editScope: EditScope.CustomCode,
            ct: CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Message, Does.Contain("Build passed after code customization patches."));
        Assert.That(result.AppliedPatches, Is.Not.Null.And.Count.EqualTo(1));
        Assert.That(result.ErrorCode, Is.Null);

        mocks.TypeSpecCustomization.Verify(t => t.ApplyCustomizationAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never,
            "CustomCode scope must not apply TypeSpec (spec-input) customizations even when patching code.");
    }

    [Test]
    public async Task CustomCodeScope_MixedSpecAndCode_PatchesCode_SurfacesSpecChangeRequired()
    {
        // Mixed feedback: one TSP_APPLICABLE (out of scope) + one CODE_CUSTOMIZATION (in scope).
        // CustomCode scope patches the code, reports the spec item as out of scope, and never edits client.tsp.
        var classifyCalls = 0;
        var buildCalls = 0;
        var svc = new ConfigurableLanguageService(
            buildFunc: () =>
            {
                buildCalls++;
                return buildCalls <= 1
                    ? (false, "error: cannot find symbol foo", null)
                    : (true, null, null);
            },
            hasCustomizations: true,
            patchesFunc: () => [new AppliedPatch("Customization.java", "Fixed reference", 1)],
            language: SdkLanguage.Java);

        var (tool, mocks) = CreateTool(
            languageService: svc,
            configureClassifier: c =>
                c.Setup(x => x.ClassifyItemsAsync(It.IsAny<ClassificationKind>(), It.IsAny<ClassifyRequest>(), It.IsAny<CancellationToken>()))
                    .Returns<ClassificationKind, ClassifyRequest, CancellationToken>(
                        (classifyType, classifyRequest, cancellationToken) =>
                        {
                            classifyCalls++;
                            var customizationRequest = classifyRequest as ClassifyCustomizationRequest;
                            var items = customizationRequest!.Items;
                            if (classifyCalls == 1)
                            {
                                items.Clear();
                            }
                            if (items.Count == 0)
                            {
                                var specItem = new FeedbackItem { Text = "Rename client (spec change)" };
                                var codeItem = new FeedbackItem { Text = "Fix customization reference" };
                                items.Add(specItem);
                                items.Add(codeItem);
                                return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                                {
                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = specItem.Id, Classification = "TSP_APPLICABLE",
                                            Reason = "Needs spec edit", Text = "Rename client (spec change)"
                                        },
                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = codeItem.Id, Classification = "CODE_CUSTOMIZATION",
                                            Reason = "Fix in customization file", Text = "Fix customization reference"
                                        }
                                }));
                            }
                            return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>()));
                        }));

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(
            packagePath: pkg,
            tspProjectPath: tspDir,
            customizationRequest: "mixed feedback",
            editScope: EditScope.CustomCode,
            ct: CancellationToken.None);

        // Code path succeeds; the spec item is surfaced as out of scope rather than applied.
        Assert.That(result.SpecChangeRequired, Is.Not.Null.And.Count.EqualTo(1));
        Assert.That(result.AppliedPatches, Is.Not.Null.And.Count.EqualTo(1));

        mocks.TypeSpecCustomization.Verify(t => t.ApplyCustomizationAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never,
            "CustomCode scope must not apply spec-input customizations even in mixed feedback.");
    }

    [Test]
    public async Task DefaultScope_TspApplicable_AppliesCustomization_BackwardCompatible()
    {
        // Default (Update) mode is unchanged: TSP_APPLICABLE items are applied via the
        // TypeSpec customization service. Guards backward compatibility of the new mode param.
        var (tool, mocks) = CreateTool();
        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(
            packagePath: pkg,
            tspProjectPath: tspDir,
            customizationRequest: "Rename FooClient to BarClient",
            ct: CancellationToken.None);

        Assert.That(result.SpecChangeRequired, Is.Null);
        mocks.TypeSpecCustomization.Verify(t => t.ApplyCustomizationAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce,
            "Update mode must still apply TypeSpec customizations for TSP_APPLICABLE items.");
    }

    // ========================================================================
    // EditScope.All (default: both spec inputs and custom code in scope)
    // ========================================================================

    [Test]
    public async Task AllScope_MixedSpecAndCode_AppliesSpec_AndPatchesCode()
    {
        // Explicit EditScope.All: both axes are in scope. A mixed feedback set (one TSP_APPLICABLE +
        // one CODE_CUSTOMIZATION) must apply the spec-input customization AND patch the custom code,
        // and surface neither out-of-scope list.
        var buildCalls = 0;
        var classifyCalls = 0;
        var svc = new ConfigurableLanguageService(
            buildFunc: () =>
            {
                buildCalls++;
                return buildCalls <= 1
                    ? (false, "error: cannot find symbol foo", null)
                    : (true, null, null);
            },
            hasCustomizations: true,
            patchesFunc: () => [new AppliedPatch("Customization.java", "Fixed reference", 1)],
            language: SdkLanguage.Java);

        var (tool, mocks) = CreateTool(
            languageService: svc,
            configureClassifier: c =>
                c.Setup(x => x.ClassifyItemsAsync(It.IsAny<ClassificationKind>(), It.IsAny<ClassifyRequest>(), It.IsAny<CancellationToken>()))
                    .Returns<ClassificationKind, ClassifyRequest, CancellationToken>(
                        (classifyType, classifyRequest, cancellationToken) =>
                        {
                            classifyCalls++;
                            var customizationRequest = classifyRequest as ClassifyCustomizationRequest;
                            var items = customizationRequest!.Items;
                            if (classifyCalls == 1)
                            {
                                items.Clear();
                            }

                            if (items.Count == 0)
                            {
                                var specItem = new FeedbackItem { Text = "Rename client (spec change)" };
                                var codeItem = new FeedbackItem { Text = "Fix customization reference" };
                                items.Add(specItem);
                                items.Add(codeItem);
                                return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                                {
                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = specItem.Id, Classification = "TSP_APPLICABLE",
                                            Reason = "Needs spec edit", Text = "Rename client (spec change)"
                                        },
                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = codeItem.Id, Classification = "CODE_CUSTOMIZATION",
                                            Reason = "Fix in customization file", Text = "Fix customization reference"
                                        }
                                }));
                            }
                            return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>()));
                        }));

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(
            packagePath: pkg,
            tspProjectPath: tspDir,
            customizationRequest: "mixed feedback",
            editScope: EditScope.All,
            ct: CancellationToken.None);

        // Both axes applied: spec via the TypeSpec customization service, code via patches.
        Assert.That(result.AppliedPatches, Is.Not.Null.And.Count.EqualTo(1));
        Assert.That(result.SpecChangeRequired, Is.Null, "All scope edits spec inputs, so nothing is out of scope.");
        Assert.That(result.CustomCodeChangeRequired, Is.Null, "All scope patches custom code, so nothing is out of scope.");

        mocks.TypeSpecCustomization.Verify(t => t.ApplyCustomizationAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce,
            "All scope must apply TypeSpec (spec-input) customizations for TSP_APPLICABLE items.");
    }

    // ========================================================================
    // EditScope.SpecInputs (spec-inputs-only; never patches custom code)
    // ========================================================================

    [Test]
    public async Task SpecInputsScope_MixedSpecAndCode_AppliesSpec_DoesNotPatchCode()
    {
        // EditScope.SpecInputs: spec inputs are in scope but custom code is NOT. A mixed feedback set must
        // apply the spec-input customization, report the CODE_CUSTOMIZATION item as out of scope, and never
        // invoke custom-code patching.
        var classifyCalls = 0;
        var patchCalls = 0;
        var svc = new ConfigurableLanguageService(
            buildFunc: () => (false, "error: cannot find symbol foo", null),
            hasCustomizations: true,
            patchesFunc: () =>
            {
                patchCalls++;
                return [new AppliedPatch("Customization.java", "Fixed reference", 1)];
            },
            language: SdkLanguage.Java);

        var (tool, mocks) = CreateTool(
            languageService: svc,
            configureClassifier: c =>
                c.Setup(x => x.ClassifyItemsAsync(It.IsAny<ClassificationKind>(), It.IsAny<ClassifyRequest>(), It.IsAny<CancellationToken>()))
                    .Returns<ClassificationKind, ClassifyRequest, CancellationToken>(
                        (classifyType, request, ct) =>
                        {
                            classifyCalls++;
                            var customizationRequest = request as ClassifyCustomizationRequest;
                            var items = customizationRequest!.Items;
                            if (classifyCalls == 1)
                            {
                                items.Clear();
                            }
                            if (items.Count == 0)
                            {
                                var specItem = new FeedbackItem { Text = "Rename client (spec change)" };
                                var codeItem = new FeedbackItem { Text = "Fix customization reference" };
                                items.Add(specItem);
                                items.Add(codeItem);
                                return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>
                                {
                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = specItem.Id, Classification = "TSP_APPLICABLE",
                                            Reason = "Needs spec edit", Text = "Rename client (spec change)"
                                        },
                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = codeItem.Id, Classification = "CODE_CUSTOMIZATION",
                                            Reason = "Fix in customization file", Text = "Fix customization reference"
                                        }
                                }));
                            }
                            return Task.FromResult(new ClassifyResponse(ClassificationKind.Customization, new List<FeedbackClassificationResponse.ItemClassificationDetails>()));
                        }));

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(
            packagePath: pkg,
            tspProjectPath: tspDir,
            customizationRequest: "mixed feedback",
            editScope: EditScope.SpecInputs,
            ct: CancellationToken.None);

        // Spec was applied; the code item is reported as out of scope and never patched.
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.CustomCodeChangeRequired));
        Assert.That(result.CustomCodeChangeRequired, Is.Not.Null.And.Count.EqualTo(1));
        Assert.That(result.SpecChangeRequired, Is.Null, "SpecInputs scope edits spec inputs, so spec items are not out of scope.");
        Assert.That(result.AppliedPatches, Is.Null.Or.Empty, "SpecInputs scope must not patch custom code.");
        Assert.That(patchCalls, Is.EqualTo(0), "SpecInputs scope must never invoke ApplyPatchesAsync.");

        mocks.TypeSpecCustomization.Verify(t => t.ApplyCustomizationAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce,
            "SpecInputs scope must apply TypeSpec (spec-input) customizations for TSP_APPLICABLE items.");
    }

    [Test]
    public async Task InvalidEditScope_ReturnsInvalidInput()
    {
        // editScope is a non-nullable flags enum; an undefined value (e.g. a stray bit outside the All
        // mask) is rejected up front with InvalidInput rather than silently treated as no-scope.
        var (tool, _) = CreateTool();
        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(
            packagePath: pkg,
            tspProjectPath: tspDir,
            customizationRequest: "Rename FooClient to BarClient",
            editScope: (EditScope)99,
            ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput));
    }

    // ========================================================================
    // Optional tspProjectPath (auto-resolve regen from pinned tsp-location.yaml)
    // ========================================================================

    [Test]
    public async Task CustomCodeScope_NoTspProjectPath_Java_RegeneratesFromPinnedCommit()
    {
        // CustomCode scope does not edit spec inputs, so a local TypeSpec checkout is optional. When
        // tspProjectPath is omitted, the Java post-patch regeneration must pass localSpecRepoPath == null,
        // causing tsp-client to regenerate from the commit pinned in the package's tsp-location.yaml.
        string? capturedLocalSpecRepo = "SENTINEL";
        var callCount = 0;
        var tsp = new Mock<ITspClientHelper>();
        tsp.Setup(t => t.UpdateGenerationAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string?, bool, string?, CancellationToken>(
                (_, _, _, localSpec, _) =>
                {
                    callCount++;
                    // CODE_CUSTOMIZATION only (no TSP_APPLICABLE), so the Java post-patch regen is the
                    // first and only UpdateGenerationAsync call.
                    if (callCount == 1)
                    {
                        capturedLocalSpecRepo = localSpec;
                    }
                })
            .ReturnsAsync(new TspToolResponse { IsSuccessful = true, TypeSpecProject = "/pkg" });

        var buildCalls = 0;
        var svc = new ConfigurableLanguageService(
            buildFunc: () =>
            {
                buildCalls++;
                return buildCalls <= 1 ? (false, "error: cannot find symbol foo", null) : (true, null, null);
            },
            hasCustomizations: true,
            patchesFunc: () => [new AppliedPatch("Customization.java", "Fixed reference", 1)],
            language: SdkLanguage.Java);

        var (tool, _) = CreateTool(
            languageService: svc,
            tspHelper: tsp.Object,
            configureClassifier: CodeCustomizationClassifier("Fix customization reference"));

        var pkg = CreateTempDir();

        var result = await tool.UpdateAsync(
            packagePath: pkg,
            customizationRequest: "Fix customization reference",
            editScope: EditScope.CustomCode,
            ct: CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.ErrorCode, Is.Null);
        Assert.That(callCount, Is.GreaterThanOrEqualTo(1), "Java regen should run even without a local spec path.");
        Assert.That(capturedLocalSpecRepo, Is.Null,
            "With tspProjectPath omitted, Java regen must pass localSpecRepoPath == null so tsp-client uses the pinned tsp-location.yaml commit.");
    }

    [Test]
    public async Task CustomCodeScope_NoTspProjectPath_NonJava_SucceedsWithoutValidatingSpecPath()
    {
        // For a non-Java language the custom-code flow never regenerates, so tspProjectPath is unnecessary.
        // When omitted, the tool must not attempt to validate a (non-existent) spec path and must succeed.
        var buildCalls = 0;
        var svc = new ConfigurableLanguageService(
            buildFunc: () =>
            {
                buildCalls++;
                return buildCalls <= 1 ? (false, "error CS0103: name does not exist", null) : (true, null, null);
            },
            hasCustomizations: true,
            patchesFunc: () => [new AppliedPatch("Customization.cs", "Fixed reference", 1)],
            language: SdkLanguage.DotNet);

        var (tool, mocks) = CreateTool(
            languageService: svc,
            configureClassifier: CodeCustomizationClassifier("Fix customization reference"),
            configureGit: g => g.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net"));

        var pkg = CreateTempDir();

        var result = await tool.UpdateAsync(
            packagePath: pkg,
            customizationRequest: "Fix customization reference",
            editScope: EditScope.CustomCode,
            ct: CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.ErrorCode, Is.Null);
        mocks.TypeSpecHelper.Verify(t => t.IsValidTypeSpecProjectPath(It.IsAny<string>()), Times.Never,
            "When tspProjectPath is omitted in CustomCode scope, the tool must not validate a spec path.");
    }

    [Test]
    public async Task CustomCodeScope_WithTspProjectPath_Java_UsesLocalSpecRepo()
    {
        // When a local TypeSpec project path IS provided in CustomCode scope, the Java post-patch regen
        // should use it as localSpecRepoPath (regenerate from the local checkout).
        string? capturedLocalSpecRepo = null;
        var callCount = 0;

        var tsp = new Mock<ITspClientHelper>();
        tsp.Setup(t => t.UpdateGenerationAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string?, bool, string?, CancellationToken>(
                (_, _, _, localSpec, _) =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        capturedLocalSpecRepo = localSpec;
                    }
                })
            .ReturnsAsync(new TspToolResponse { IsSuccessful = true, TypeSpecProject = "/pkg" });

        var buildCalls = 0;
        var svc = new ConfigurableLanguageService(
            buildFunc: () =>
            {
                buildCalls++;
                return buildCalls <= 1 ? (false, "error: cannot find symbol foo", null) : (true, null, null);
            },
            hasCustomizations: true,
            patchesFunc: () => [new AppliedPatch("Customization.java", "Fixed reference", 1)],
            language: SdkLanguage.Java);

        var (tool, _) = CreateTool(
            languageService: svc,
            tspHelper: tsp.Object,
            configureClassifier: CodeCustomizationClassifier("Fix customization reference"));

        var pkg = CreateTempDir();
        var tspDir = CreateTestTypespecProjectPath();

        var result = await tool.UpdateAsync(
            packagePath: pkg,
            customizationRequest: "Fix customization reference",
            tspProjectPath: tspDir,
            editScope: EditScope.CustomCode,
            ct: CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(capturedLocalSpecRepo, Is.EqualTo(Path.GetFullPath(tspDir)),
            "When provided, the local TypeSpec project path should be passed to the Java regen as localSpecRepoPath.");
    }

    [Test]
    public async Task SpecInputsScope_NoTspProjectPath_ReturnsInvalidInput()
    {
        // SpecInputs scope edits local spec inputs (client.tsp), which requires a local TypeSpec project
        // path. Omitting it must fail fast with InvalidInput rather than proceeding.
        var (tool, _) = CreateTool();
        var pkg = CreateTempDir();

        var result = await tool.UpdateAsync(
            packagePath: pkg,
            customizationRequest: "Rename FooClient to BarClient",
            editScope: EditScope.SpecInputs,
            ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput));
        Assert.That(result.ResponseError, Does.Contain("TypeSpec project path").IgnoreCase);
    }

    [Test]
    public async Task AllScope_NoTspProjectPath_ReturnsInvalidInput()
    {
        // The default scope (All) includes spec inputs, so omitting tspProjectPath must fail fast with
        // InvalidInput just like SpecInputs scope.
        var (tool, _) = CreateTool();
        var pkg = CreateTempDir();

        var result = await tool.UpdateAsync(
            packagePath: pkg,
            customizationRequest: "Rename FooClient to BarClient",
            ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput));
    }

    // ========================================================================
    // Mock helpers
    // ========================================================================

    /// <summary>
    /// Flexible language service mock where all behaviors can be configured via constructor.
    /// </summary>
    private class ConfigurableLanguageService : LanguageService
    {
        private readonly Func<(bool Success, string? Error, PackageInfo? Info)> _buildFunc;
        private readonly Func<List<AppliedPatch>>? _patchesFunc;
        private readonly bool _hasCustomizations;
        private readonly bool _isCustomizedCodeUpdateSupported;

        public override SdkLanguage Language { get; }
        public override bool IsCustomizedCodeUpdateSupported => _isCustomizedCodeUpdateSupported;

        public ConfigurableLanguageService(
            Func<(bool, string?, PackageInfo?)>? buildFunc = null,
            bool hasCustomizations = false,
            Func<List<AppliedPatch>>? patchesFunc = null,
            SdkLanguage language = SdkLanguage.Java,
            bool isCustomizedCodeUpdateSupported = true)
        {
            _buildFunc = buildFunc ?? (() => (true, null, null));
            _hasCustomizations = hasCustomizations;
            _patchesFunc = patchesFunc;
            Language = language;
            _isCustomizedCodeUpdateSupported = isCustomizedCodeUpdateSupported;
        }

        public override Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath, CancellationToken ct)
            => Task.FromResult(new List<ApiChange>());

        public override string? HasCustomizations(string packagePath, CancellationToken ct = default)
            => _hasCustomizations ? Path.Combine(packagePath, "customization") : null;

        public override Task<List<AppliedPatch>> ApplyPatchesAsync(string customizationRoot, string packagePath, string buildContext, CancellationToken ct)
            => Task.FromResult(_patchesFunc?.Invoke() ?? new List<AppliedPatch>());

        public override Task<ValidationResult> ValidateAsync(string packagePath, CancellationToken ct)
            => Task.FromResult(ValidationResult.CreateSuccess());

        public override Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo)> BuildAsync(string packagePath, int timeoutMinutes = 30, CancellationToken ct = default)
            => Task.FromResult(_buildFunc());

        public override Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
            => Task.FromResult(new PackageInfo
            {
                PackagePath = packagePath,
                RepoRoot = "/mock/repo",
                RelativePath = "sdk/mock/package",
                PackageName = "mock-package",
                ServiceName = "mock",
                PackageVersion = "1.0.0",
                SamplesDirectory = "/mock/samples",
                Language = Language,
                SdkType = SdkType.Dataplane
            });
    }
}

internal class MockTspHelper : ITspClientHelper
{
    private readonly bool _updateSuccess;
    private readonly string? _updateError;

    public MockTspHelper(bool updateSuccess = true, string? updateError = null)
    {
        _updateSuccess = updateSuccess;
        _updateError = updateError;
    }

    public Task<TspToolResponse> ConvertSwaggerAsync(string swaggerReadmePath, string outputDirectory, bool isArm, bool fullyCompatible, bool isCli, CancellationToken ct = default)
        => Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProject = outputDirectory });

    public Task<TspToolResponse> UpdateGenerationAsync(string tspLocationDirectory, string? commitSha = null, bool isCli = false, string? localSpecRepoPath = null, CancellationToken ct = default)
        => Task.FromResult(new TspToolResponse { IsSuccessful = _updateSuccess, TypeSpecProject = tspLocationDirectory, ResponseError = _updateError });

    public Task<TspToolResponse> InitializeGenerationAsync(string workingDirectory, string tspConfigPath, string[]? additionalArgs = null, CancellationToken ct = default)
        => Task.FromResult(new TspToolResponse { IsSuccessful = _updateSuccess, TypeSpecProject = workingDirectory, ResponseError = _updateError });
}

/// <summary>
/// Mock that succeeds for the first N UpdateGenerationAsync calls then fails.
/// Useful for testing scenarios where TSP regen in the loop succeeds but Java regen-after-patches fails.
/// </summary>
internal class CallCountMockTspHelper : ITspClientHelper
{
    private readonly int _failAfterCall;
    private readonly string? _failError;
    private int _updateCalls;

    public CallCountMockTspHelper(int failAfterCall, string? failError = null)
    {
        _failAfterCall = failAfterCall;
        _failError = failError;
    }

    public Task<TspToolResponse> ConvertSwaggerAsync(string swaggerReadmePath, string outputDirectory, bool isArm, bool fullyCompatible, bool isCli, CancellationToken ct = default)
        => Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProject = outputDirectory });

    public Task<TspToolResponse> UpdateGenerationAsync(string tspLocationDirectory, string? commitSha = null, bool isCli = false, string? localSpecRepoPath = null, CancellationToken ct = default)
    {
        _updateCalls++;
        if (_updateCalls > _failAfterCall)
        {
            return Task.FromResult(new TspToolResponse { IsSuccessful = false, TypeSpecProject = tspLocationDirectory, ResponseError = _failError });
        }
        return Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProject = tspLocationDirectory });
    }

    public Task<TspToolResponse> InitializeGenerationAsync(string workingDirectory, string tspConfigPath, string[]? additionalArgs = null, CancellationToken ct = default)
        => Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProject = workingDirectory });
}


