using System.CommandLine;
using System.Globalization;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Microsoft.Extensions.Logging.Abstractions;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Services.Repair;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.TypeSpec;
using Azure.Sdk.Tools.Cli.Tools.TypeSpec;


namespace Azure.Sdk.Tools.Cli.Tests.Tools.TypeSpec;

[TestFixture]
public class CustomizedCodeUpdateToolAutoTests
{
    // --- Shared helpers ---

    private readonly List<TempDirectory> _directories = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var directory in _directories) { directory.Dispose(); }
        _directories.Clear();
    }

    private string CreateTempDir()
    {
        var directory = TempDirectory.Create("customized-update");
        _directories.Add(directory);
        return directory.DirectoryPath;
    }

    private string CreatePackageDir(SdkLanguage language, bool hasCustomizations = true)
    {
        var package = CreateTempDir();
        File.WriteAllText(Path.Combine(package, "tsp-location.yaml"), "repo: specs\ncommit: immutable-pin\ndirectory: service");
        if (hasCustomizations)
        {
            var path = CustomFile(package, language);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, language == SdkLanguage.Python
                ? "__all__ = ['OldName']\nclass OldName: pass"
                : "internal class OldName { }");
        }
        return package;
    }

    private static string CustomFile(string package, SdkLanguage language) => Path.Combine(package, language switch
    {
        SdkLanguage.DotNet => Path.Combine("src", "Customization.cs"),
        SdkLanguage.Java => Path.Combine("customization", "src", "main", "java", "Customization.java"),
        SdkLanguage.JavaScript => Path.Combine("src", "customization.ts"),
        SdkLanguage.Python => Path.Combine("package", "_patch.py"),
        _ => throw new ArgumentOutOfRangeException(nameof(language))
    });

    private static void RenameCustomSymbol(string package, SdkLanguage language)
    {
        var path = CustomFile(package, language);
        File.WriteAllText(path, File.ReadAllText(path).Replace("OldName", "NewName"));
    }

    private static void AssertValidatedRepair(CustomizedCodeUpdateResponse result, int attempts)
    {
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, result.ResponseError);
            Assert.That(result.BuildValidated, Is.True);
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.Repair, Is.Not.Null);
            Assert.That(result.Repair!.TerminalReason, Is.EqualTo("repaired"));
            Assert.That(result.Repair.AttemptsUsed, Is.EqualTo(attempts));
            Assert.That(result.Repair.Validation.Succeeded, Is.True);
            Assert.That(result.Repair.Validation.ValidatedTree, Is.EqualTo(result.Repair.FinalState!.Tree));
            Assert.That(result.Repair.Input!.InitialTree, Is.Not.EqualTo(result.Repair.FinalState.Tree));
            Assert.That(result.Repair.Input.TspLocationSha256, Is.EqualTo(result.Repair.FinalState.TspLocationSha256));
            Assert.That(result.Repair.FinalState.ChangedFiles, Is.Not.Empty);
        });
    }

    /// <summary>
    /// Creates a fully-wired <see cref="CustomizedCodeUpdateTool"/> with sensible default mocks.
    /// Callers can customise individual mocks before construction by passing them in.
    /// </summary>
    private (CustomizedCodeUpdateTool tool, ToolMocks mocks) CreateTool(
        LanguageService? languageService = null,
        Mock<IGitHelper>? gitHelper = null,
        Action<Mock<IGitHelper>>? configureGit = null,
        Action<Mock<IFeedbackClassifierService>>? configureClassifier = null,
        Action<Mock<ITypeSpecCustomizationService>>? configureTspCustomization = null,
        Action<Mock<ITypeSpecHelper>>? configureTypeSpecHelper = null,
        ITspClientHelper? tspHelper = null,
        INpxHelper? npxHelper = null)
    {
        if (gitHelper is null)
        {
            gitHelper = new Mock<IGitHelper>();
            gitHelper.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-java");
            gitHelper.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string path, CancellationToken _) => Path.GetFullPath(path));
        }
        configureGit?.Invoke(gitHelper);

        var feedbackService = new Mock<IAPIViewFeedbackService>();
        var classifierService = new Mock<IFeedbackClassifierService>();

        // Default ClassifyItemsAsync: handles both passes via a single mock.
        // - First pass (items is empty): populates the list and returns TSP_APPLICABLE.
        // - Second pass (items already populated): returns TSP_APPLICABLE for existing items.
        classifierService.Setup(c => c.ClassifyItemsAsync(
                It.IsAny<List<FeedbackItem>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<EditScope>(),
                It.IsAny<CancellationToken>()))
            .Returns<List<FeedbackItem>, string, string, string?, string?, string?, string?, int?, EditScope, CancellationToken>(
                (items, _, _, _, plainText, _, _, _, _, _) =>
                {
                    if (items.Count == 0)
                    {
                        // First pass: gather items from plainText input
                        var item = new FeedbackItem { Text = plainText ?? "Rename FooClient to BarClient" };
                        items.Add(item);
                        return Task.FromResult(new FeedbackClassificationResponse
                        {
                            Classifications =
                            [
                                new FeedbackClassificationResponse.ItemClassificationDetails
                                {
                                    ItemId = item.Id,
                                    Classification = "TSP_APPLICABLE",
                                    Reason = "Can be fixed via TypeSpec",
                                    Text = item.Text
                                }
                            ]
                        });
                    }

                    // Second pass: classify already-gathered items
                    var actualId = items.FirstOrDefault()?.Id ?? "1";
                    return Task.FromResult(new FeedbackClassificationResponse
                    {
                        Classifications =
                        [
                            new FeedbackClassificationResponse.ItemClassificationDetails
                            {
                                ItemId = actualId,
                                Classification = "TSP_APPLICABLE",
                                Reason = "Can be fixed via TypeSpec",
                                Text = "Rename FooClient to BarClient"
                            }
                        ]
                    });
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
        configureTypeSpecHelper?.Invoke(typeSpecHelper);

        var svc = languageService ?? new ConfigurableLanguageService();
        var tsp = tspHelper ?? new MockTspHelper();
        var source = new MemoryRepairSourceState();
        var artifacts = new MemoryRepairArtifacts(CreateTempDir());
        var repair = new CustomizedCodeRepairService(gitHelper.Object, typeSpecHelper.Object, tsp,
            classifierService.Object, source, artifacts, TimeProvider.System, NullLogger<CustomizedCodeRepairService>.Instance);

        var tool = new CustomizedCodeUpdateTool(
            new NullLogger<CustomizedCodeUpdateTool>(),
            [svc],
            gitHelper.Object,
            tsp,
            feedbackService.Object,
            classifierService.Object,
            typeSpecCustomization.Object,
            typeSpecHelper.Object,
            npxHelper ?? new Mock<INpxHelper>().Object,
            repair);

        return (tool, new ToolMocks(gitHelper, feedbackService, classifierService, typeSpecCustomization, typeSpecHelper, artifacts));
    }

    private record ToolMocks(
        Mock<IGitHelper> GitHelper,
        Mock<IAPIViewFeedbackService> FeedbackService,
        Mock<IFeedbackClassifierService> ClassifierService,
        Mock<ITypeSpecCustomizationService> TypeSpecCustomization,
        Mock<ITypeSpecHelper> TypeSpecHelper,
        MemoryRepairArtifacts Artifacts);

    /// <summary>
    /// Builds a classifier configuration whose first pass returns a single CODE_CUSTOMIZATION item, so the
    /// flow proceeds into the custom-code patch/regen pipeline (used by the optional-tspProjectPath tests).
    /// </summary>
    private static Action<Mock<IFeedbackClassifierService>> CodeCustomizationClassifier(
        string text, string classification = "CODE_CUSTOMIZATION") =>
        c => c.Setup(x => x.ClassifyItemsAsync(
                It.IsAny<List<FeedbackItem>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<EditScope>(),
                It.IsAny<CancellationToken>()))
            .Returns<List<FeedbackItem>, string, string, string?, string?, string?, string?, int?, EditScope, CancellationToken>(
                (items, _, _, _, _, _, _, _, _, _) =>
                {
                    if (items.Count == 0)
                    {
                        var item = new FeedbackItem { Text = text };
                        items.Add(item);
                        return Task.FromResult(new FeedbackClassificationResponse
                        {
                            Classifications =
                            [
                                new FeedbackClassificationResponse.ItemClassificationDetails
                                {
                                    ItemId = item.Id,
                                    Classification = classification,
                                    Reason = "Fix in customization file",
                                    Text = text
                                }
                            ]
                        });
                    }
                    return Task.FromResult(new FeedbackClassificationResponse { Classifications = [] });
                });

    // ========================================================================
    // Happy-path: TSP fix + build succeeds
    // ========================================================================

    [Test]
    public async Task TspFix_BuildPassesFirstIteration_ReturnsSuccess()
    {
        var (tool, _) = CreateTool();
        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

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
        var tspDir = CreateTempDir();

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
        var tspDir = CreateTempDir();

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
            c.Setup(x => x.ClassifyItemsAsync(
                    It.IsAny<List<FeedbackItem>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<int?>(),
                    It.IsAny<EditScope>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FeedbackClassificationResponse { Classifications = [] }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput));
        Assert.That(result.Message, Does.Contain("could not be classified"));
    }

    [Test]
    public async Task Classification_ReturnsNullList_ReturnsInvalidInput()
    {
        var (tool, _) = CreateTool(configureClassifier: c =>
            c.Setup(x => x.ClassifyItemsAsync(
                    It.IsAny<List<FeedbackItem>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<int?>(),
                    It.IsAny<EditScope>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FeedbackClassificationResponse { Classifications = null }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

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
            c.Setup(x => x.ClassifyItemsAsync(
                    It.IsAny<List<FeedbackItem>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<int?>(),
                    It.IsAny<EditScope>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(copilotEx));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

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
            c.Setup(x => x.ClassifyItemsAsync(
                    It.IsAny<List<FeedbackItem>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<int?>(),
                    It.IsAny<EditScope>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(unexpectedEx));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

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
            c.Setup(x => x.ClassifyItemsAsync(
                    It.IsAny<List<FeedbackItem>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<int?>(),
                    It.IsAny<EditScope>(),
                    It.IsAny<CancellationToken>()))
                .Returns<List<FeedbackItem>, string, string, string?, string?, string?, string?, int?, EditScope, CancellationToken>(
                    (items, _, _, _, _, _, _, _, _, _) =>
                    {
                        var item1 = new FeedbackItem { Text = "Restructure hierarchy" };
                        var item2 = new FeedbackItem { Text = "Looks good" };
                        items.Add(item1);
                        items.Add(item2);
                        return Task.FromResult(new FeedbackClassificationResponse
                        {
                            Classifications =
                            [
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
                            ]
                        });
                    }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

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
                c.Setup(x => x.ClassifyItemsAsync(
                        It.IsAny<List<FeedbackItem>>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<int?>(),
                        It.IsAny<EditScope>(),
                        It.IsAny<CancellationToken>()))
                    .Returns<List<FeedbackItem>, string, string, string?, string?, string?, string?, int?, EditScope, CancellationToken>(
                        (items, _, _, _, _, _, _, _, _, _) =>
                        {
                            classifyCalls++;
                            if (items.Count == 0)
                            {
                                // First pass: populate items with TSP_APPLICABLE
                                var item = new FeedbackItem { Text = "rename X" };
                                items.Add(item);
                                return Task.FromResult(new FeedbackClassificationResponse
                                {
                                    Classifications =
                                    [
                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = item.Id, Classification = "TSP_APPLICABLE",
                                            Reason = "fixable", Text = "rename X"
                                        }
                                    ]
                                });
                            }
                            // Second pass: return empty
                            return Task.FromResult(new FeedbackClassificationResponse { Classifications = [] });
                        }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

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
                c.Setup(x => x.ClassifyItemsAsync(
                        It.IsAny<List<FeedbackItem>>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<int?>(),
                        It.IsAny<EditScope>(),
                        It.IsAny<CancellationToken>()))
                    .Returns<List<FeedbackItem>, string, string, string?, string?, string?, string?, int?, EditScope, CancellationToken>(
                        (items, _, _, _, _, _, _, _, _, _) =>
                        {
                            classifyCalls++;
                            if (items.Count == 0)
                            {
                                // First pass: populate items with TSP_APPLICABLE
                                var item = new FeedbackItem { Text = "rename X" };
                                items.Add(item);
                                return Task.FromResult(new FeedbackClassificationResponse
                                {
                                    Classifications =
                                    [
                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = item.Id, Classification = "TSP_APPLICABLE",
                                            Reason = "fixable", Text = "rename X"
                                        }
                                    ]
                                });
                            }
                            // Second pass: classifier sees failure context and flags manual intervention
                            var actualId = items.FirstOrDefault()?.Id ?? "1";
                            return Task.FromResult(new FeedbackClassificationResponse
                            {
                                Classifications =
                                [
                                    new FeedbackClassificationResponse.ItemClassificationDetails
                                    {
                                        ItemId = actualId, Classification = "REQUIRES_MANUAL_INTERVENTION",
                                        Reason = "TSP customization failed, manual fix needed", Text = "rename X"
                                    }
                                ]
                            });
                        }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

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
                c.Setup(x => x.ClassifyItemsAsync(
                        It.IsAny<List<FeedbackItem>>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<int?>(),
                        It.IsAny<EditScope>(),
                        It.IsAny<CancellationToken>()))
                    .Returns<List<FeedbackItem>, string, string, string?, string?, string?, string?, int?, EditScope, CancellationToken>(
                        (items, _, _, _, _, _, _, _, _, _) =>
                        {
                            if (items.Count == 0)
                            {
                                var item = new FeedbackItem { Text = "rename X to Y" };
                                items.Add(item);
                                return Task.FromResult(new FeedbackClassificationResponse
                                {
                                    Classifications =
                                    [
                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = item.Id, Classification = "TSP_APPLICABLE",
                                            Reason = "fixable", Text = "rename X to Y"
                                        }
                                    ]
                                });
                            }
                            var actualId = items.FirstOrDefault()?.Id ?? "1";
                            return Task.FromResult(new FeedbackClassificationResponse
                            {
                                Classifications =
                                [
                                    new FeedbackClassificationResponse.ItemClassificationDetails
                                    {
                                        ItemId = actualId, Classification = "TSP_APPLICABLE",
                                        Reason = "fixable", Text = "rename X to Y"
                                    }
                                ]
                            });
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
        var tspDir = CreateTempDir();

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
                c.Setup(x => x.ClassifyItemsAsync(
                        It.IsAny<List<FeedbackItem>>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<int?>(),
                        It.IsAny<EditScope>(),
                        It.IsAny<CancellationToken>()))
                    .Returns<List<FeedbackItem>, string, string, string?, string?, string?, string?, int?, EditScope, CancellationToken>(
                        (items, _, _, _, _, _, _, _, _, _) =>
                        {
                            classifyCalls++;
                            if (items.Count == 0)
                            {
                                // First pass: populate items with TSP_APPLICABLE
                                var item = new FeedbackItem { Text = "rename X" };
                                items.Add(item);
                                return Task.FromResult(new FeedbackClassificationResponse
                                {
                                    Classifications =
                                    [
                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = item.Id, Classification = "TSP_APPLICABLE",
                                            Reason = "fixable", Text = "rename X"
                                        }
                                    ]
                                });
                            }
                            // Second pass: reclassify as manual intervention (emitter issue)
                            var actualId = items.FirstOrDefault()?.Id ?? "1";
                            return Task.FromResult(new FeedbackClassificationResponse
                            {
                                Classifications =
                                [
                                    new FeedbackClassificationResponse.ItemClassificationDetails
                                    {
                                        ItemId = actualId, Classification = "REQUIRES_MANUAL_INTERVENTION",
                                        Reason = "Emitter issue, cannot be patched", Text = "rename X"
                                    }
                                ]
                            });
                        }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

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
                c.Setup(x => x.ClassifyItemsAsync(
                        It.IsAny<List<FeedbackItem>>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<int?>(),
                        It.IsAny<EditScope>(),
                        It.IsAny<CancellationToken>()))
                    .Returns<List<FeedbackItem>, string, string, string?, string?, string?, string?, int?, EditScope, CancellationToken>(
                        (items, _, _, _, _, _, _, _, _, _) =>
                        {
                            classifyCalls++;
                            if (items.Count == 0)
                            {
                                // First pass: populate items with TSP_APPLICABLE
                                var item = new FeedbackItem { Text = "rename FooClient to BarClient" };
                                items.Add(item);
                                return Task.FromResult(new FeedbackClassificationResponse
                                {
                                    Classifications =
                                    [
                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = item.Id, Classification = "TSP_APPLICABLE",
                                            Reason = "rename operation", Text = "rename FooClient to BarClient"
                                        }
                                    ]
                                });
                            }
                            // Second pass: classifier sees regen failure and determines manual intervention
                            secondPassContext = items.FirstOrDefault()?.Context;
                            var actualId = items.FirstOrDefault()?.Id ?? "1";
                            return Task.FromResult(new FeedbackClassificationResponse
                            {
                                Classifications =
                                [
                                    new FeedbackClassificationResponse.ItemClassificationDetails
                                    {
                                        ItemId = actualId, Classification = "REQUIRES_MANUAL_INTERVENTION",
                                        Reason = "Emitter issue — cannot resolve via TSP or patching",
                                        Text = "rename FooClient to BarClient"
                                    }
                                ]
                            });
                        }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir,
            customizationRequest: "Rename FooClient to BarClient", ct: CancellationToken.None);

        // Verified: exactly 2 classifier calls (pass 1 + pass 2), no retry loop
        Assert.That(classifyCalls, Is.EqualTo(2), "Should classify twice: pass 1 + pass 2 after regen failure");

        Assert.That(buildCalls, Is.Zero, "A required regeneration failure must never validate stale generated code.");
        Assert.That(result.BuildValidated, Is.False);
        Assert.That(result.ExitCode, Is.Not.Zero);

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
        var tspDir = CreateTempDir();

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
        var tspDir = CreateTempDir();

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
        var tspDir = CreateTempDir();

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
        var tspDir = CreateTempDir();

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
        var tspDir = CreateTempDir();

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
                c.Setup(x => x.ClassifyItemsAsync(
                        It.IsAny<List<FeedbackItem>>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<int?>(),
                        It.IsAny<EditScope>(),
                        It.IsAny<CancellationToken>()))
                    .Returns<List<FeedbackItem>, string, string, string?, string?, string?, string?, int?, EditScope, CancellationToken>(
                        (items, _, _, _, _, _, _, _, _, _) =>
                        {
                            classifyCalls++;
                            if (items.Count == 0)
                            {
                                // First pass: CODE_CUSTOMIZATION classification
                                var item = new FeedbackItem { Text = "Rename maxSpeakers to maxSpeakerCount in customization code" };
                                items.Add(item);
                                return Task.FromResult(new FeedbackClassificationResponse
                                {
                                    Classifications =
                                    [
                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = item.Id,
                                            Classification = "CODE_CUSTOMIZATION",
                                            Reason = "Build error references generated code; fix is in the customization file",
                                            Text = "Rename maxSpeakers to maxSpeakerCount in customization code"
                                        }
                                    ]
                                });
                            }
                            // Second iteration: feedback dictionary is empty → return empty
                            return Task.FromResult(new FeedbackClassificationResponse { Classifications = [] });
                        }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

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
    // Regeneration after patches
    // ========================================================================

    [TestCase(SdkLanguage.Java, "azure-sdk-for-java", EditScope.All)]
    [TestCase(SdkLanguage.Java, "azure-sdk-for-java", EditScope.CustomCode)]
    [TestCase(SdkLanguage.DotNet, "azure-sdk-for-net", EditScope.All)]
    [TestCase(SdkLanguage.DotNet, "azure-sdk-for-net", EditScope.CustomCode)]
    public async Task RegenAfterPatches_Fails_ReturnsRegenerateAfterPatchesFailed(
        SdkLanguage language, string repoName, EditScope editScope)
    {
        var buildCalls = 0;
        var svc = new ConfigurableLanguageService(
            buildFunc: () =>
            {
                buildCalls++;
                return (false, "error in build", null);
            },
            hasCustomizations: true,
            patchesFunc: () => [new AppliedPatch("customization", "patch", 1)],
            language: language,
            repairPatch: (package, _) => RenameCustomSymbol(package, language));

        var failingTsp = new CallCountMockTspHelper(
            failAfterCall: 1,
            failError: "regen failed: tsp-client error");
        var (tool, _) = CreateTool(
            languageService: svc,
            tspHelper: failingTsp,
            configureClassifier: editScope == EditScope.CustomCode ? CodeCustomizationClassifier("Fix customization") : null,
            configureGit: g => g.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(repoName));
        var pkg = CreatePackageDir(language);
        var tspDir = editScope == EditScope.All ? CreateTempDir() : null;

        var result = await tool.UpdateAsync(
            packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization",
            editScope: editScope, ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.RegenerateAfterPatchesFailed));
        if (editScope == EditScope.CustomCode)
        {
            Assert.That(result.Repair!.TerminalReason, Is.EqualTo("generation_failed"));
            Assert.That(result.Repair.AttemptsUsed, Is.EqualTo(1));
            Assert.That(result.Repair.Stages.Single(s => s.Id == "attempt-1-generate").DiagnosticSummary,
                Is.EqualTo("regen failed: tsp-client error"));
            Assert.That(result.ResponseError, Is.EqualTo("regen failed: tsp-client error"));
            Assert.That(result.BuildResult, Is.EqualTo("error in build"), "Only the baseline build ran.");
            Assert.That(result.BuildValidated, Is.False);
        }
        else
        {
            Assert.That(result.BuildResult, Is.EqualTo("regen failed: tsp-client error"));
        }
        Assert.That(result.AppliedPatches, Is.Not.Null.And.Count.EqualTo(1));
        Assert.That(result.TypeSpecChangesSummary, editScope == EditScope.All ? Has.Count.EqualTo(1) : Is.Null,
            "CustomCode does not run the TypeSpec-edit stage or populate a spec-edit summary.");
        Assert.That(buildCalls, Is.EqualTo(1), "A failed regeneration must prevent the final build.");
    }

    // ========================================================================
    // Iteration context & feedback flow
    // ========================================================================

    [Test]
    public async Task CustomizationRequest_FlowsToClassifier()
    {
        string? capturedFeedbackText = null;
        var (tool, _) = CreateTool(configureClassifier: c =>
            c.Setup(x => x.ClassifyItemsAsync(
                    It.IsAny<List<FeedbackItem>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<int?>(),
                    It.IsAny<EditScope>(),
                    It.IsAny<CancellationToken>()))
                .Returns<List<FeedbackItem>, string, string, string?, string?, string?, string?, int?, EditScope, CancellationToken>(
                    (items, _, _, _, plainText, _, _, _, _, _) =>
                    {
                        capturedFeedbackText = plainText;
                        var item = new FeedbackItem { Text = plainText ?? "" };
                        items.Add(item);
                        return Task.FromResult(new FeedbackClassificationResponse
                        {
                            Classifications =
                            [
                                new FeedbackClassificationResponse.ItemClassificationDetails
                                {
                                    ItemId = item.Id, Classification = "TSP_APPLICABLE",
                                    Reason = "test", Text = item.Text
                                }
                            ]
                        });
                    }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

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
                c.Setup(x => x.ClassifyItemsAsync(
                        It.IsAny<List<FeedbackItem>>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<int?>(),
                        It.IsAny<EditScope>(),
                        It.IsAny<CancellationToken>()))
                    .Returns<List<FeedbackItem>, string, string, string?, string?, string?, string?, int?, EditScope, CancellationToken>(
                        (items, _, _, _, _, _, _, _, _, _) =>
                        {
                            classifyCalls++;
                            if (items.Count == 0)
                            {
                                // First pass: populate items with TSP_APPLICABLE
                                var item = new FeedbackItem { Text = "rename FooClient" };
                                items.Add(item);
                                return Task.FromResult(new FeedbackClassificationResponse
                                {
                                    Classifications =
                                    [
                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = item.Id, Classification = "TSP_APPLICABLE",
                                            Reason = "fixable", Text = "rename FooClient"
                                        }
                                    ]
                                });
                            }
                            // Second pass: capture context + return TSP_APPLICABLE to stay in loop
                            if (classifyCalls == 2)
                                secondCallContext = items.FirstOrDefault()?.Context;

                            var actualId = items.FirstOrDefault()?.Id ?? "1";
                            return Task.FromResult(new FeedbackClassificationResponse
                            {
                                Classifications =
                                [
                                    new FeedbackClassificationResponse.ItemClassificationDetails
                                    {
                                        ItemId = actualId, Classification = "TSP_APPLICABLE",
                                        Reason = "fixable", Text = "rename FooClient"
                                    }
                                ]
                            });
                        }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

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
                c.Setup(x => x.ClassifyItemsAsync(
                        It.IsAny<List<FeedbackItem>>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<int?>(),
                        It.IsAny<EditScope>(),
                        It.IsAny<CancellationToken>()))
                    .Returns<List<FeedbackItem>, string, string, string?, string?, string?, string?, int?, EditScope, CancellationToken>(
                        (items, _, _, _, _, _, _, _, _, _) =>
                        {
                            classifyCalls++;
                            if (items.Count == 0)
                            {
                                // First pass: populate items with TSP_APPLICABLE
                                var item = new FeedbackItem { Text = "rename X" };
                                items.Add(item);
                                return Task.FromResult(new FeedbackClassificationResponse
                                {
                                    Classifications =
                                    [
                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = item.Id, Classification = "TSP_APPLICABLE",
                                            Reason = "fixable", Text = "rename X"
                                        }
                                    ]
                                });
                            }
                            // Second pass
                            var actualId = items.FirstOrDefault()?.Id ?? "1";
                            return Task.FromResult(new FeedbackClassificationResponse
                            {
                                Classifications =
                                [
                                    new FeedbackClassificationResponse.ItemClassificationDetails
                                    {
                                        ItemId = actualId, Classification = "TSP_APPLICABLE",
                                        Reason = "fixable", Text = "rename X"
                                    }
                                ]
                            });
                        }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

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
        var tspDir = CreateTempDir();

        await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        var expectedLocalSpec = Path.GetFullPath(tspDir);
        Assert.That(capturedLocalSpecRepo, Is.EqualTo(expectedLocalSpec),
            "Should pass the local TypeSpec project path as localSpecRepoPath");
    }

    [Test]
    public async Task Java_RegenAfterPatches_PassesTspProjectPathAsLocalSpecRepo()
    {
        // Verify that the Java post-patch regeneration path also uses the local TypeSpec project path.
        var tspDir = CreateTempDir();

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
                        capturedLocalSpecRepo = localSpec;
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
        var tspDir = CreateTempDir();

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
        var tspDir = CreateTempDir();

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
        var tspDir = CreateTempDir();

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
        var tspDir = CreateTempDir();

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
        var pkg = CreatePackageDir(SdkLanguage.Java);
        var tspDir = CreateTempDir();

        var result = await tool.UpdateAsync(
            packagePath: pkg,
            tspProjectPath: tspDir,
            customizationRequest: "Rename FooClient to BarClient",
            editScope: EditScope.CustomCode,
            ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.SpecChangeRequired));
        Assert.That(result.SpecChangeRequired, Is.Not.Null.And.Count.EqualTo(1));
        Assert.That(result.Repair!.TerminalReason, Is.EqualTo("spec_change_required"));
        Assert.That(result.Repair.AttemptsUsed, Is.Zero);
        Assert.That(result.Repair.Stages.Select(s => s.Name), Is.EqualTo(new[] { "prepare", "generate", "build", "classify" }));
        Assert.That(result.BuildValidated, Is.False, "A green baseline cannot satisfy a spec-level request.");
        Assert.That(result.Repair.Input!.InitialTree, Is.EqualTo(result.Repair.FinalState!.Tree));

        // Critically, CustomCode scope must never apply spec-input (client.tsp) customizations.
        mocks.TypeSpecCustomization.Verify(t => t.ApplyCustomizationAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never,
            "CustomCode scope must not apply TypeSpec (spec-input) customizations.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CustomCodeScope_CodeCustomization_PatchesApplied_BuildSucceeds(bool baselineGreen)
    {
        // CustomCode scope still performs custom-code patching: a CODE_CUSTOMIZATION item flows through
        // the patch pipeline exactly like update mode, and no spec-input edits are made.
        var buildCalls = 0;
        var svc = new ConfigurableLanguageService(
            buildFunc: () =>
            {
                buildCalls++;
                return buildCalls <= 1 && !baselineGreen
                    ? (false, "error: cannot find symbol maxSpeakers", null)
                    : (true, null, null);
            },
            hasCustomizations: true,
            patchesFunc: () =>
            [
                new AppliedPatch("SpeechTranscriptionCustomization.java", "Renamed maxSpeakers to maxSpeakerCount", 2)
            ],
            language: SdkLanguage.Java,
            repairPatch: (package, _) => RenameCustomSymbol(package, SdkLanguage.Java));

        var (tool, mocks) = CreateTool(
            languageService: svc,
            configureClassifier: c =>
                c.Setup(x => x.ClassifyItemsAsync(
                        It.IsAny<List<FeedbackItem>>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<int?>(),
                        It.IsAny<EditScope>(),
                        It.IsAny<CancellationToken>()))
                    .Returns<List<FeedbackItem>, string, string, string?, string?, string?, string?, int?, EditScope, CancellationToken>(
                        (items, _, _, _, _, _, _, _, _, _) =>
                        {
                            if (items.Count == 0)
                            {
                                var item = new FeedbackItem { Text = "Rename maxSpeakers to maxSpeakerCount in customization code" };
                                items.Add(item);
                                return Task.FromResult(new FeedbackClassificationResponse
                                {
                                    Classifications =
                                    [
                                        new FeedbackClassificationResponse.ItemClassificationDetails
                                        {
                                            ItemId = item.Id,
                                            Classification = "CODE_CUSTOMIZATION",
                                            Reason = "Fix is in the customization file",
                                            Text = "Rename maxSpeakers to maxSpeakerCount in customization code"
                                        }
                                    ]
                                });
                            }
                            return Task.FromResult(new FeedbackClassificationResponse { Classifications = [] });
                        }));

        var pkg = CreatePackageDir(SdkLanguage.Java);
        var tspDir = CreateTempDir();

        var result = await tool.UpdateAsync(
            packagePath: pkg,
            tspProjectPath: tspDir,
            customizationRequest: "Rename maxSpeakers to maxSpeakerCount",
            editScope: EditScope.CustomCode,
            ct: CancellationToken.None);

        AssertValidatedRepair(result, 1);
        Assert.That(buildCalls, Is.EqualTo(2), "The baseline never suppresses a requested code edit, even when green.");
        Assert.That(svc.RepairSessions, Is.EqualTo(1));
        Assert.That(svc.PatchTurns, Is.EqualTo(1));
        Assert.That(File.ReadAllText(CustomFile(pkg, SdkLanguage.Java)), Does.Contain("NewName").And.Not.Contain("OldName"));
        Assert.That(mocks.Artifacts.Content["attempt-1/patch.diff"], Does.Contain("OldName").And.Contain("NewName"));
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
    public async Task CustomCodeScope_MixedSpecAndCode_StopsBeforePatching_SurfacesSpecChangeRequired()
    {
        // Mixed feedback: one TSP_APPLICABLE (out of scope) + one CODE_CUSTOMIZATION (in scope).
        // A spec prerequisite stops the entire request before any patch, rather than reporting partial success.
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
            language: SdkLanguage.Java,
            repairPatch: (package, _) => RenameCustomSymbol(package, SdkLanguage.Java));

        var (tool, mocks) = CreateTool(
            languageService: svc,
            configureClassifier: c =>
                c.Setup(x => x.ClassifyItemsAsync(
                        It.IsAny<List<FeedbackItem>>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<int?>(),
                        It.IsAny<EditScope>(),
                        It.IsAny<CancellationToken>()))
                    .Returns<List<FeedbackItem>, string, string, string?, string?, string?, string?, int?, EditScope, CancellationToken>(
                        (items, _, _, _, _, _, _, _, _, _) =>
                        {
                            if (items.Count == 0)
                            {
                                var specItem = new FeedbackItem { Text = "Rename client (spec change)" };
                                var codeItem = new FeedbackItem { Text = "Fix customization reference" };
                                items.Add(specItem);
                                items.Add(codeItem);
                                return Task.FromResult(new FeedbackClassificationResponse
                                {
                                    Classifications =
                                    [
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
                                    ]
                                });
                            }
                            return Task.FromResult(new FeedbackClassificationResponse { Classifications = [] });
                        }));

        var pkg = CreatePackageDir(SdkLanguage.Java);
        var tspDir = CreateTempDir();

        var result = await tool.UpdateAsync(
            packagePath: pkg,
            tspProjectPath: tspDir,
            customizationRequest: "mixed feedback",
            editScope: EditScope.CustomCode,
            ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.BuildValidated, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.SpecChangeRequired));
        Assert.That(result.SpecChangeRequired, Is.Not.Null.And.Count.EqualTo(1));
        Assert.That(result.AppliedPatches, Is.Empty);
        Assert.That(result.Repair!.TerminalReason, Is.EqualTo("spec_change_required"));
        Assert.That(result.Repair.AttemptsUsed, Is.Zero);
        Assert.That(svc.RepairSessions, Is.Zero);
        Assert.That(buildCalls, Is.EqualTo(1));
        Assert.That(File.ReadAllText(CustomFile(pkg, SdkLanguage.Java)), Does.Contain("OldName").And.Not.Contain("NewName"));
        Assert.That(result.Repair.Input!.InitialTree, Is.EqualTo(result.Repair.FinalState!.Tree));

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
        var tspDir = CreateTempDir();

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
                c.Setup(x => x.ClassifyItemsAsync(
                        It.IsAny<List<FeedbackItem>>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<int?>(),
                        It.IsAny<EditScope>(),
                        It.IsAny<CancellationToken>()))
                    .Returns<List<FeedbackItem>, string, string, string?, string?, string?, string?, int?, EditScope, CancellationToken>(
                        (items, _, _, _, _, _, _, _, _, _) =>
                        {
                            if (items.Count == 0)
                            {
                                var specItem = new FeedbackItem { Text = "Rename client (spec change)" };
                                var codeItem = new FeedbackItem { Text = "Fix customization reference" };
                                items.Add(specItem);
                                items.Add(codeItem);
                                return Task.FromResult(new FeedbackClassificationResponse
                                {
                                    Classifications =
                                    [
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
                                    ]
                                });
                            }
                            return Task.FromResult(new FeedbackClassificationResponse { Classifications = [] });
                        }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

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
                c.Setup(x => x.ClassifyItemsAsync(
                        It.IsAny<List<FeedbackItem>>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<int?>(),
                        It.IsAny<EditScope>(),
                        It.IsAny<CancellationToken>()))
                    .Returns<List<FeedbackItem>, string, string, string?, string?, string?, string?, int?, EditScope, CancellationToken>(
                        (items, _, _, _, _, _, _, _, _, _) =>
                        {
                            if (items.Count == 0)
                            {
                                var specItem = new FeedbackItem { Text = "Rename client (spec change)" };
                                var codeItem = new FeedbackItem { Text = "Fix customization reference" };
                                items.Add(specItem);
                                items.Add(codeItem);
                                return Task.FromResult(new FeedbackClassificationResponse
                                {
                                    Classifications =
                                    [
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
                                    ]
                                });
                            }
                            return Task.FromResult(new FeedbackClassificationResponse { Classifications = [] });
                        }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

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
        Assert.That(result.TypeSpecChangesSummary, Is.Not.Null.And.Count.EqualTo(1));

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
        var tspDir = CreateTempDir();

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
    // Bounded CustomCode dispatch and public attempt limits
    // ========================================================================

    [TestCase(false, 1, true)]
    [TestCase(true, 1, true)]
    [TestCase(false, 1, false)]
    [TestCase(true, 1, false)]
    [TestCase(false, 2, false)]
    [TestCase(true, 2, false)]
    public async Task CustomCodeScope_CliAndMcp_UseRealBoundedSession(bool useCli, int attempts, bool omitOption)
    {
        var allowRetry = attempts > 1;
        var package = CreatePackageDir(SdkLanguage.Java);
        var builds = 0;
        var service = new ConfigurableLanguageService(
            buildFunc: () =>
            {
                builds++;
                return File.ReadAllText(CustomFile(package, SdkLanguage.Java)).Contains("RequestedName")
                    ? (true, null, null) : (false, $"cannot find symbol after build {builds}", null);
            },
            hasCustomizations: true,
            patchesFunc: () => [new AppliedPatch("Customization.java", "Address requested symbol", 1)],
            repairPatch: (path, attempt) =>
            {
                var file = CustomFile(path, SdkLanguage.Java);
                File.WriteAllText(file, File.ReadAllText(file).Replace(
                    attempt == 1 ? "OldName" : "CandidateOne", attempt == 1 ? "CandidateOne" : "RequestedName"));
            });
        var (tool, mocks) = CreateTool(languageService: service,
            configureClassifier: CodeCustomizationClassifier("Rename OldName to RequestedName"));
        CustomizedCodeUpdateResponse result;
        if (useCli)
        {
            List<string> arguments = ["--customization-request", "Rename OldName to RequestedName",
                "--package-path", package, "--edit-scope", "CustomCode"];
            if (!omitOption) { arguments.AddRange(["--max-attempts", attempts.ToString(CultureInfo.InvariantCulture)]); }
            var parsed = tool.GetCommandInstances().Single().Parse(arguments.ToArray());
            Assert.That(parsed.Errors, Is.Empty);
            result = (CustomizedCodeUpdateResponse)await tool.HandleCommand(parsed, CancellationToken.None);
        }
        else
        {
            result = !omitOption
                ? await tool.UpdateAsync("Rename OldName to RequestedName", package, editScope: EditScope.CustomCode, maxAttempts: attempts)
                : await tool.UpdateAsync("Rename OldName to RequestedName", package, editScope: EditScope.CustomCode);
        }

        Assert.That(result.Repair!.MaxAttempts, Is.EqualTo(attempts));
        Assert.That(result.Repair.AttemptsUsed, Is.EqualTo(attempts));
        Assert.That(service.RepairSessions, Is.EqualTo(1), "Retries must not create separate language sessions.");
        Assert.That(service.LegacyPatchCalls, Is.Zero, "Neither default-one nor explicit attempt limits may use the legacy patch route.");
        Assert.That(service.PatchTurns, Is.EqualTo(attempts));
        Assert.That(builds, Is.EqualTo(attempts + 1), "Baseline build is outside the patch attempt budget.");
        Assert.That(service.RetryPrompts, Has.Count.EqualTo(allowRetry ? 1 : 0));
        Assert.That(mocks.ClassifierService.Invocations, Has.Count.EqualTo(1));
        Assert.That(mocks.TypeSpecCustomization.Invocations, Is.Empty);
        if (allowRetry)
        {
            AssertValidatedRepair(result, 2);
            Assert.That(service.RetryPrompts.Single(), Does.Contain("cannot find symbol after build 2")
                .And.Contain("CandidateOne").And.Contain("candidate 1"));
            Assert.That(File.ReadAllText(CustomFile(package, SdkLanguage.Java)), Does.Contain("RequestedName"));
        }
        else
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.BuildValidated, Is.False);
            Assert.That(result.Repair.TerminalReason, Is.EqualTo("attempt_limit"));
            Assert.That(result.ErrorCode, Is.EqualTo("AttemptLimit"));
            Assert.That(result.ExitCode, Is.Not.Zero);
            Assert.That(File.ReadAllText(CustomFile(package, SdkLanguage.Java)), Does.Contain("CandidateOne"));
        }
    }

    [Test]
    public async Task CustomCodeScope_GreenBaselineStillClassifiesBeforeReturningAlreadyGreen()
    {
        var builds = 0;
        var service = new ConfigurableLanguageService(buildFunc: () =>
        {
            builds++;
            return (true, null, null);
        });
        var (tool, mocks) = CreateTool(languageService: service,
            configureClassifier: CodeCustomizationClassifier("Requested state already exists", "SUCCESS"));
        var package = CreatePackageDir(SdkLanguage.Java, hasCustomizations: false);

        var result = await tool.UpdateAsync("Confirm requested state", package, editScope: EditScope.CustomCode);

        Assert.That(result.Success, Is.True, result.ResponseError);
        Assert.That(result.BuildValidated, Is.True);
        Assert.That(result.Repair!.TerminalReason, Is.EqualTo("already_green"));
        Assert.That(result.Repair.Stages.Select(s => s.Name), Is.EqualTo(new[] { "prepare", "generate", "build", "classify" }));
        Assert.That(result.Repair.AttemptsUsed, Is.Zero);
        Assert.That(result.Repair.Input!.InitialTree, Is.EqualTo(result.Repair.FinalState!.Tree));
        Assert.That(result.Repair.Validation.ValidatedTree, Is.EqualTo(result.Repair.FinalState.Tree));
        Assert.That(builds, Is.EqualTo(1));
        Assert.That(service.RepairSessions, Is.Zero);
        Assert.That(mocks.ClassifierService.Invocations, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task CustomCodeScope_RealDotnetMissingPlugin_FailsStrictPreparationBeforeGenerationOrClassification()
    {
        var process = new Mock<IProcessHelper>(MockBehavior.Strict);
        var runner = new Mock<ICopilotAgentRunner>(MockBehavior.Strict);
        var service = new DotnetLanguageService(process.Object, Mock.Of<IPowershellHelper>(), runner.Object,
            Mock.Of<IGitHelper>(), NullLogger<LanguageService>.Instance, Mock.Of<ICommonValidationHelpers>(),
            Mock.Of<IPackageInfoHelper>(), Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>());
        var tsp = new Mock<ITspClientHelper>(MockBehavior.Strict);
        var (tool, mocks) = CreateTool(languageService: service, tspHelper: tsp.Object,
            configureGit: g => g.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("azure-sdk-for-net"),
            configureClassifier: CodeCustomizationClassifier("Rename customization"));
        var package = CreatePackageDir(SdkLanguage.DotNet);

        var result = await tool.UpdateAsync("Rename customization", package, editScope: EditScope.CustomCode);

        Assert.That(result.Success, Is.False);
        Assert.That(result.BuildValidated, Is.False);
        Assert.That(result.Repair!.TerminalReason, Is.EqualTo("preparation_failed"));
        Assert.That(result.ResponseError, Does.Contain("Plugin directory not found").And.Contain("Client.Plugin"));
        Assert.That(result.Repair.Stages.Select(s => s.Name), Is.EqualTo(new[] { "prepare" }));
        Assert.That(result.Repair.Stages.Single().Status, Is.EqualTo("failed"));
        Assert.That(result.Repair.AttemptsUsed, Is.Zero);
        Assert.That(result.Repair.Input!.InitialTree, Is.EqualTo(result.Repair.FinalState!.Tree));
        Assert.That(mocks.ClassifierService.Invocations, Is.Empty);
        tsp.VerifyNoOtherCalls();
        process.VerifyNoOtherCalls();
        runner.VerifyNoOtherCalls();
    }

    // ========================================================================
    // Optional tspProjectPath (auto-resolve regen from pinned tsp-location.yaml)
    // ========================================================================

    [TestCase(SdkLanguage.Java, "azure-sdk-for-java")]
    [TestCase(SdkLanguage.DotNet, "azure-sdk-for-net")]
    public async Task CustomCodeScope_NoTspProjectPath_RegeneratesFromPinnedCommit(SdkLanguage language, string repoName)
    {
        // CustomCode scope does not edit spec inputs, so a local TypeSpec checkout is optional. When
        // tspProjectPath is omitted, post-patch regeneration must pass localSpecRepoPath == null,
        // causing tsp-client to regenerate from the commit pinned in the package's tsp-location.yaml.
        var capturedLocalSpecRepos = new List<string?>();
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
                    capturedLocalSpecRepos.Add(localSpec);
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
            patchesFunc: () => [new AppliedPatch("customization", "Fixed reference", 1)],
            language: language,
            repairPatch: (package, _) => RenameCustomSymbol(package, language));

        var (tool, _) = CreateTool(
            languageService: svc,
            tspHelper: tsp.Object,
            configureClassifier: CodeCustomizationClassifier("Fix customization reference"),
            configureGit: g => g.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(repoName));

        var pkg = CreatePackageDir(language);
        var pin = File.ReadAllText(Path.Combine(pkg, "tsp-location.yaml"));

        var result = await tool.UpdateAsync(
            packagePath: pkg,
            customizationRequest: "Fix customization reference",
            editScope: EditScope.CustomCode,
            ct: CancellationToken.None);

        AssertValidatedRepair(result, 1);
        Assert.That(result.ErrorCode, Is.Null);
        Assert.That(callCount, Is.EqualTo(2), "Both baseline and post-patch generation use the unchanged pin.");
        Assert.That(capturedLocalSpecRepos, Is.EqualTo(new string?[] { null, null }));
        Assert.That(File.ReadAllText(Path.Combine(pkg, "tsp-location.yaml")), Is.EqualTo(pin));
        tsp.Verify(t => t.UpdateGenerationAsync(pkg, null, false, null,
            It.Is<CancellationToken>(token => token.CanBeCanceled)), Times.Exactly(2));
    }

    [TestCase(SdkLanguage.JavaScript, "azure-sdk-for-js")]
    [TestCase(SdkLanguage.Python, "azure-sdk-for-python")]
    public async Task CustomCodeScope_NoTspProjectPath_OtherLanguages_DoNotRegenerate(SdkLanguage language, string repoName)
    {
        var tsp = new Mock<ITspClientHelper>(MockBehavior.Strict);
        var buildCalls = 0;
        var svc = new ConfigurableLanguageService(
            buildFunc: () =>
            {
                buildCalls++;
                return buildCalls <= 1 ? (false, "error CS0103: name does not exist", null) : (true, null, null);
            },
            hasCustomizations: true,
            patchesFunc: () => [new AppliedPatch("customization", "Fixed reference", 1)],
            language: language,
            repairPatch: (package, _) => RenameCustomSymbol(package, language));

        var (tool, mocks) = CreateTool(
            languageService: svc,
            tspHelper: tsp.Object,
            configureClassifier: CodeCustomizationClassifier("Fix customization reference"),
            configureGit: g => g.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(repoName));

        var pkg = CreatePackageDir(language);

        var result = await tool.UpdateAsync(
            packagePath: pkg,
            customizationRequest: "Fix customization reference",
            editScope: EditScope.CustomCode,
            ct: CancellationToken.None);

        AssertValidatedRepair(result, 1);
        Assert.That(buildCalls, Is.EqualTo(2));
        Assert.That(result.Repair!.Stages.Where(s => s.Name is "prepare" or "generate").Select(s => s.Status),
            Is.All.EqualTo("not_required"));
        Assert.That(result.ErrorCode, Is.Null);
        mocks.TypeSpecHelper.Verify(t => t.IsValidTypeSpecProjectPath(It.IsAny<string>()), Times.Never,
            "When tspProjectPath is omitted in CustomCode scope, the tool must not validate a spec path.");
        tsp.VerifyNoOtherCalls();
    }

    [TestCase(SdkLanguage.Java, "azure-sdk-for-java", false)]
    [TestCase(SdkLanguage.Java, "azure-sdk-for-java", true)]
    [TestCase(SdkLanguage.DotNet, "azure-sdk-for-net", false)]
    [TestCase(SdkLanguage.DotNet, "azure-sdk-for-net", true)]
    public async Task CustomCodeScope_WithTspProjectPath_UsesLocalSpecRepo(
        SdkLanguage language, string repoName, bool useRelativePath)
    {
        // When a local TypeSpec project path IS provided in CustomCode scope, post-patch regen
        // should use it as localSpecRepoPath (regenerate from the local checkout).
        var capturedLocalSpecRepos = new List<string?>();
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
                    capturedLocalSpecRepos.Add(localSpec);
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
            patchesFunc: () => [new AppliedPatch("customization", "Fixed reference", 1)],
            language: language,
            repairPatch: (package, _) => RenameCustomSymbol(package, language));

        var (tool, _) = CreateTool(
            languageService: svc,
            tspHelper: tsp.Object,
            configureClassifier: CodeCustomizationClassifier("Fix customization reference"),
            configureGit: g => g.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(repoName));

        var pkg = CreatePackageDir(language);
        var tspDir = CreateTempDir();
        File.WriteAllText(Path.Combine(tspDir, "client.tsp"), "// unchanged local spec inputs");

        var result = await tool.UpdateAsync(
            packagePath: pkg,
            customizationRequest: "Fix customization reference",
            tspProjectPath: useRelativePath ? Path.GetRelativePath(Environment.CurrentDirectory, tspDir) : tspDir,
            editScope: EditScope.CustomCode,
            ct: CancellationToken.None);

        AssertValidatedRepair(result, 1);
        Assert.That(callCount, Is.EqualTo(2));
        Assert.That(capturedLocalSpecRepos, Is.EqualTo(new[] { Path.GetFullPath(tspDir), Path.GetFullPath(tspDir) }));
        Assert.That(result.Repair!.Input!.LocalSpec!.ProjectPath, Is.EqualTo(Path.GetFullPath(tspDir)));
        Assert.That(result.Repair.Input.LocalSpec.InitialTree, Is.EqualTo(result.Repair.Input.LocalSpec.FinalTree));
        Assert.That(File.ReadAllText(Path.Combine(tspDir, "client.tsp")), Is.EqualTo("// unchanged local spec inputs"));
        tsp.Verify(t => t.UpdateGenerationAsync(pkg, null, false, Path.GetFullPath(tspDir),
            It.Is<CancellationToken>(token => token.CanBeCanceled)), Times.Exactly(2));
    }

    [TestCase(EditScope.CustomCode, true)]
    [TestCase(EditScope.CustomCode, false)]
    [TestCase(EditScope.All, true)]
    [TestCase(EditScope.All, false)]
    public async Task DotNet_Patches_AreRegeneratedBeforeFinalBuild(EditScope editScope, bool finalBuildSucceeds)
    {
        using var cts = new CancellationTokenSource();
        var pkg = CreatePackageDir(SdkLanguage.DotNet);
        var tspDir = editScope == EditScope.All ? CreateTempDir() : null;
        var repoRoot = editScope == EditScope.CustomCode ? pkg : CreateTempDir();
        var generated = Path.Combine(pkg, "src", "Generated", "Token.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(generated)!);
        File.WriteAllText(generated, "internal partial class Token { }");
        var operations = new List<string>();
        var customTypeIsPublic = false;
        var generatedTypeIsPublic = false;
        var svc = new ConfigurableLanguageService(
            buildFunc: () =>
            {
                operations.Add("build");
                if (customTypeIsPublic != generatedTypeIsPublic)
                {
                    return (false, "CS0262: Partial declarations have conflicting accessibility", null);
                }
                if (!generatedTypeIsPublic)
                {
                    return (false, "CS0050: Inconsistent accessibility", null);
                }
                return finalBuildSucceeds ? (true, null, null) : (false, "Unresolved build error", null);
            },
            hasCustomizations: true,
            patchesFunc: () =>
            {
                operations.Add("patch");
                customTypeIsPublic = true;
                return [new AppliedPatch("Models/Token.cs", "Make the token public", 1)];
            },
            language: SdkLanguage.DotNet,
            preGenerateFunc: async (root, ct) =>
            {
                Assert.That(root, Is.EqualTo(repoRoot));
                Assert.That(ct.CanBeCanceled, Is.True);
                Assert.That(ct == cts.Token, Is.EqualTo(editScope == EditScope.All),
                    "CustomCode shares an invocation-wide linked deadline token; All preserves its original token.");
                await Task.Yield();
                operations.Add("prepare");
            },
            repairPatch: (package, _) =>
            {
                var path = CustomFile(package, SdkLanguage.DotNet);
                File.WriteAllText(path, File.ReadAllText(path).Replace("internal", "public"));
            });
        var tsp = new Mock<ITspClientHelper>(MockBehavior.Strict);
        tsp.Setup(t => t.UpdateGenerationAsync(pkg, null, false, tspDir, It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                operations.Add("regenerate");
                generatedTypeIsPublic = customTypeIsPublic;
                File.WriteAllText(generated, $"{(generatedTypeIsPublic ? "public" : "internal")} partial class Token {{ }}");
            })
            .ReturnsAsync(new TspToolResponse { IsSuccessful = true, TypeSpecProject = pkg });
        var (tool, mocks) = CreateTool(
            languageService: svc,
            tspHelper: tsp.Object,
            configureClassifier: CodeCustomizationClassifier("Fix token accessibility"),
            configureGit: g =>
            {
                g.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net");
                g.Setup(x => x.DiscoverRepoRootAsync(pkg, It.IsAny<CancellationToken>())).ReturnsAsync(repoRoot);
            });

        var result = await tool.UpdateAsync("Fix token accessibility", pkg, tspDir, editScope, cts.Token);

        Assert.That(operations, Is.EqualTo(editScope == EditScope.CustomCode
            ? new[] { "prepare", "regenerate", "build", "patch", "prepare", "regenerate", "build" }
            : new[] { "build", "patch", "prepare", "regenerate", "build" }));
        Assert.That(result.Success, Is.EqualTo(finalBuildSucceeds));
        Assert.That(result.ErrorCode, Is.EqualTo(finalBuildSucceeds ? null : editScope == EditScope.CustomCode
            ? "AttemptLimit" : CustomizedCodeUpdateResponse.KnownErrorCodes.BuildAfterPatchesFailed));
        Assert.That(result.BuildResult, Is.EqualTo(finalBuildSucceeds ? null : "Unresolved build error"));
        Assert.That(result.AppliedPatches, Has.Count.EqualTo(1));
        if (editScope == EditScope.CustomCode)
        {
            Assert.That(svc.OperationTokens.Distinct().Count(), Is.EqualTo(1));
            Assert.That(svc.OperationTokens[0], Is.Not.EqualTo(cts.Token));
            Assert.That(result.Repair!.AttemptsUsed, Is.EqualTo(1));
            Assert.That(File.ReadAllText(CustomFile(pkg, SdkLanguage.DotNet)), Does.Contain("public"));
            Assert.That(File.ReadAllText(generated), Does.StartWith("public"));
            if (finalBuildSucceeds) { AssertValidatedRepair(result, 1); }
            else
            {
                Assert.That(result.Repair.TerminalReason, Is.EqualTo("attempt_limit"));
                Assert.That(result.BuildValidated, Is.False);
                Assert.That(result.Repair.Validation.Succeeded, Is.False);
            }
        }
        else
        {
            Assert.That(result.Repair, Is.Null, "All remains the legacy one-pass path.");
            Assert.That(svc.RepairSessions, Is.Zero);
        }
        tsp.VerifyAll();
        mocks.TypeSpecCustomization.Verify(t => t.ApplyCustomizationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(true, false)]
    [TestCase(false, false)]
    [TestCase(true, true)]
    public async Task DotNet_NoEffectivePatch_OnlyBaselineRegenerates(bool hasCustomizations, bool reportsPatch)
    {
        var buildCalls = 0;
        var preparationCalls = 0;
        var svc = new ConfigurableLanguageService(
            buildFunc: () =>
            {
                buildCalls++;
                return (false, "Build failed", null);
            },
            hasCustomizations: hasCustomizations,
            patchesFunc: () => reportsPatch ? [new AppliedPatch("Customization.cs", "Claimed edit", 1)] : [],
            language: SdkLanguage.DotNet,
            preGenerateFunc: (_, _) =>
            {
                preparationCalls++;
                return Task.CompletedTask;
            });
        var tsp = new Mock<ITspClientHelper>(MockBehavior.Strict);
        tsp.Setup(t => t.UpdateGenerationAsync(It.IsAny<string>(), null, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, string? _, bool _, string? _, CancellationToken _) =>
                new TspToolResponse { IsSuccessful = true, TypeSpecProject = path });
        var (tool, _) = CreateTool(
            languageService: svc,
            tspHelper: tsp.Object,
            configureClassifier: CodeCustomizationClassifier("Fix customization"),
            configureGit: g => g.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net"));

        var package = CreatePackageDir(SdkLanguage.DotNet, hasCustomizations);
        var result = await tool.UpdateAsync("Fix customization", package, editScope: EditScope.CustomCode);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(hasCustomizations
            ? CustomizedCodeUpdateResponse.KnownErrorCodes.PatchesFailed
            : CustomizedCodeUpdateResponse.KnownErrorCodes.BuildNoCustomizationsFailed));
        Assert.That(buildCalls, Is.EqualTo(1));
        Assert.That(preparationCalls, Is.EqualTo(1));
        Assert.That(result.Repair!.AttemptsUsed, Is.EqualTo(hasCustomizations ? 1 : 0));
        Assert.That(result.Repair.TerminalReason, Is.EqualTo(hasCustomizations ? "no_progress" : "no_customizations"));
        Assert.That(svc.PatchTurns, Is.EqualTo(hasCustomizations ? 1 : 0));
        Assert.That(result.Repair.Input!.InitialTree, Is.EqualTo(result.Repair.FinalState!.Tree));
        Assert.That(result.BuildValidated, Is.False);
        Assert.That(result.AppliedPatches, Has.Count.EqualTo(reportsPatch ? 1 : 0));
        tsp.Verify(t => t.UpdateGenerationAsync(package, null, false, null, It.IsAny<CancellationToken>()), Times.Once);
        tsp.VerifyNoOtherCalls();
    }

    [TestCase(true, false)]
    [TestCase(false, false)]
    [TestCase(true, true)]
    [TestCase(false, true)]
    public async Task DotNet_RequiredPreparationFailure_StopsRegeneration(bool cancel, bool failBaseline)
    {
        using var cts = new CancellationTokenSource();
        var buildCalls = 0;
        var preparationCalls = 0;
        var svc = new ConfigurableLanguageService(
            buildFunc: () =>
            {
                buildCalls++;
                return (false, "Build failed", null);
            },
            hasCustomizations: true,
            patchesFunc: () => [new AppliedPatch("Customization.cs", "Fixed reference", 1)],
            language: SdkLanguage.DotNet,
            preGenerateFunc: (_, ct) =>
            {
                preparationCalls++;
                if (!failBaseline && preparationCalls == 1) { return Task.CompletedTask; }
                if (cancel)
                {
                    cts.Cancel();
                    throw new OperationCanceledException(ct);
                }
                throw new InvalidOperationException("Preparation failed");
            },
            repairPatch: (package, _) => RenameCustomSymbol(package, SdkLanguage.DotNet));
        var tsp = new Mock<ITspClientHelper>(MockBehavior.Strict);
        tsp.Setup(t => t.UpdateGenerationAsync(It.IsAny<string>(), null, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, string? _, bool _, string? _, CancellationToken _) =>
                new TspToolResponse { IsSuccessful = true, TypeSpecProject = path });
        var (tool, _) = CreateTool(
            languageService: svc,
            tspHelper: tsp.Object,
            configureClassifier: CodeCustomizationClassifier("Fix customization"),
            configureGit: g => g.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net"));
        var pkg = CreatePackageDir(SdkLanguage.DotNet);
        var result = await tool.UpdateAsync("Fix customization", pkg, editScope: EditScope.CustomCode, ct: cts.Token);
        Assert.That(result.Success, Is.False);
        Assert.That(result.BuildValidated, Is.False);
        Assert.That(result.Repair!.TerminalReason, Is.EqualTo(cancel ? "cancelled" : "preparation_failed"));
        Assert.That(result.ErrorCode, Is.EqualTo(cancel ? "Cancelled" : "PreparationFailed"));
        Assert.That(result.ResponseError, Does.Contain(cancel ? "cancelled" : "Preparation failed"));
        Assert.That(result.Repair.AttemptsUsed, Is.EqualTo(failBaseline ? 0 : 1));
        Assert.That(result.Repair.Stages.Last().Status, Is.EqualTo(cancel ? "cancelled" : "failed"));
        Assert.That(buildCalls, Is.EqualTo(failBaseline ? 0 : 1));
        Assert.That(preparationCalls, Is.EqualTo(failBaseline ? 1 : 2));
        tsp.Verify(t => t.UpdateGenerationAsync(pkg, null, false, null, It.IsAny<CancellationToken>()),
            Times.Exactly(failBaseline ? 0 : 1));
        tsp.VerifyNoOtherCalls();
    }

    [Test]
    public async Task DotNet_PostPatchRegenerationCancellation_ReturnsStructuredCancelledResult()
    {
        using var cts = new CancellationTokenSource();
        var pkg = CreatePackageDir(SdkLanguage.DotNet);
        var buildCalls = 0;
        var svc = new ConfigurableLanguageService(
            buildFunc: () =>
            {
                buildCalls++;
                return (false, "Build failed", null);
            },
            hasCustomizations: true,
            patchesFunc: () => [new AppliedPatch("Customization.cs", "Fixed reference", 1)],
            language: SdkLanguage.DotNet,
            repairPatch: (package, _) => RenameCustomSymbol(package, SdkLanguage.DotNet));
        var tsp = new Mock<ITspClientHelper>(MockBehavior.Strict);
        var generationCalls = 0;
        tsp.Setup(t => t.UpdateGenerationAsync(pkg, null, false, null, It.IsAny<CancellationToken>()))
            .Returns((string _, string? _, bool _, string? _, CancellationToken token) =>
            {
                generationCalls++;
                if (generationCalls == 2)
                {
                    cts.Cancel();
                    Assert.That(token.IsCancellationRequested, Is.True, "Caller cancellation must reach the shared linked token.");
                    throw new OperationCanceledException(token);
                }
                return Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProject = pkg });
            });
        var (tool, _) = CreateTool(
            languageService: svc,
            tspHelper: tsp.Object,
            configureClassifier: CodeCustomizationClassifier("Fix customization"),
            configureGit: g => g.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net"));

        var result = await tool.UpdateAsync("Fix customization", pkg, editScope: EditScope.CustomCode, ct: cts.Token);
        Assert.That(result.Success, Is.False);
        Assert.That(result.BuildValidated, Is.False);
        Assert.That(result.Repair!.TerminalReason, Is.EqualTo("cancelled"));
        Assert.That(result.ErrorCode, Is.EqualTo("Cancelled"));
        Assert.That(result.ResponseError, Does.Contain("cancelled"));
        Assert.That(result.Repair.AttemptsUsed, Is.EqualTo(1));
        Assert.That(result.Repair.Stages.Last().Name, Is.EqualTo("generate"));
        Assert.That(result.Repair.Stages.Last().Status, Is.EqualTo("cancelled"));
        Assert.That(generationCalls, Is.EqualTo(2));
        Assert.That(buildCalls, Is.EqualTo(1));
        tsp.VerifyAll();
    }

    [Test]
    public async Task SpecInputsScope_NoTspProjectPath_ReturnsInvalidInput()
    {
        // SpecInputs scope edits local spec inputs (client.tsp), which requires a local TypeSpec project
        // path. Omitting it must fail fast with InvalidInput rather than proceeding.
        var (tool, _) = CreateTool();
        var pkg = CreateTempDir();

        var result = await tool.UpdateAsync(
            customizationRequest: "Rename FooClient to BarClient",
            packagePath: pkg,
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
            customizationRequest: "Rename FooClient to BarClient",
            packagePath: pkg,
            ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput));
    }

    [Test]
    public async Task SpecInputsScope_InvalidPackageProjectPath_NotReturnInvalidInput()
    {
        // SpecInputs scope, package path is optional. The tool should not fail fast with InvalidInput if the package path is invalid.
        var gitHelper = MockGitHelper();
        var (tool, _) = CreateTool(gitHelper: gitHelper);
        var pkg = "invalid-path";
        var tspDir = CreateTempDir();

        var result = await tool.UpdateAsync(
            customizationRequest: "Rename FooClient to BarClient",
            packagePath: pkg,
            tspProjectPath: tspDir,
            editScope: EditScope.SpecInputs,
            ct: CancellationToken.None);

        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task SpecInputsScope_NoPackageProjectPath_NotReturnInvalidInput()
    {
        // SpecInputs scope, package path is optional. The tool should not fail fast with InvalidInput if the package path is invalid.
        var gitHelper = MockGitHelper();
        var (tool, _) = CreateTool(gitHelper: gitHelper);
        var tspDir = CreateTempDir();

        var result = await tool.UpdateAsync(
            customizationRequest: "Rename FooClient to BarClient",
            tspProjectPath: tspDir,
            editScope: EditScope.SpecInputs,
            ct: CancellationToken.None);

        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task CustomCodeScope_InvalidPackageProjectPath_ReturnInvalidInput()
    {
        // CustomCode scope edits local custom code, which requires a package path to locate the customization files.
        // Invalid package path must fail fast with InvalidInput.
        var (tool, _) = CreateTool(gitHelper: MockGitHelper());
        var pkg = "invalid-path";

        var result = await tool.UpdateAsync(
            customizationRequest: "Rename FooClient to BarClient",
            packagePath: pkg,
            editScope: EditScope.CustomCode,
            ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput));
        Assert.That(result.ResponseError, Does.Contain("existing SDK package directory"));
        Assert.That(result.Repair!.TerminalReason, Is.EqualTo("invalid_input"));
        Assert.That(result.Repair.Stages, Is.Empty);
        Assert.That(result.Repair.AttemptsUsed, Is.Zero);
        Assert.That(result.ExitCode, Is.Not.Zero);
    }

    [Test]
    public async Task CustomCodeScope_NoPackageProjectPath_ReturnInvalidInput()
    {
        // CustomCode scope edits local custom code, which requires a package path to locate the customization files.
        // Invalid package path must fail fast with InvalidInput.
        var (tool, _) = CreateTool(gitHelper: MockGitHelper());

        var result = await tool.UpdateAsync(
            customizationRequest: "Rename FooClient to BarClient",
            editScope: EditScope.CustomCode,
            ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput));
        Assert.That(result.ResponseError, Does.Contain("existing SDK package directory"));
        Assert.That(result.Repair!.TerminalReason, Is.EqualTo("invalid_input"));
        Assert.That(result.Repair.Stages, Is.Empty);
        Assert.That(result.Repair.AttemptsUsed, Is.Zero);
        Assert.That(result.ExitCode, Is.Not.Zero);
    }

    [Test]
    public async Task AllScope_InvalidPackageProjectPath_ReturnInvalidInput()
    {
        // All scope edits both spec inputs and local custom code, which requires a package path to locate the customization files.
        // Invalid package path must fail fast with InvalidInput.
        var (tool, _) = CreateTool(gitHelper: MockGitHelper());
        var pkg = "invalid-path";

        var result = await tool.UpdateAsync(
            customizationRequest: "Rename FooClient to BarClient",
            packagePath: pkg,
            editScope: EditScope.All,
            ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput));
        Assert.That(result.ResponseError, Does.Contain("Package path").IgnoreCase); ;
    }

    [Test]
    public async Task AllScope_NoPackageProjectPath_ReturnInvalidInput()
    {
        // All scope edits both spec inputs and local custom code, which requires a package path to locate the customization files.
        // Invalid package path must fail fast with InvalidInput.
        var (tool, _) = CreateTool(gitHelper: MockGitHelper());

        var result = await tool.UpdateAsync(
            customizationRequest: "Rename FooClient to BarClient",
            editScope: EditScope.All,
            ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput));
        Assert.That(result.ResponseError, Does.Contain("Package path").IgnoreCase); ;
    }
    [Test]
    public async Task SpecInputsScope_InvalidPackageProjectPath_ReturnSuccess()
    {
        // SpecInputs scope edits local spec inputs, package path is optional.
        // invalid package path will not cause failure. and the typespec customization will resolve the request.
        var (tool, _) = CreateTool(gitHelper: MockGitHelper());
        var pkg = "invalid-path";
        var tspDir = CreateTempDir();
        var result = await tool.UpdateAsync(
            customizationRequest: "Rename FooClient to BarClient",
            packagePath: pkg,
            tspProjectPath: tspDir,
            editScope: EditScope.SpecInputs,
            ct: CancellationToken.None);
        Assert.That(result.Success, Is.True);
        Assert.That(result.TypeSpecChangesSummary, Is.Not.Null);
        Assert.That(result.TypeSpecChangesSummary.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task SpecInputsScope_NoPackageProjectPath_ReturnSuccess()
    {
        // SpecInputs scope edits local spec inputs, package path is optional.
        // invalid package path will not cause failure. and the typespec customization will resolve the request.
        var (tool, _) = CreateTool(gitHelper: MockGitHelper());
        var tspDir = CreateTempDir();
        var result = await tool.UpdateAsync(
            customizationRequest: "Rename FooClient to BarClient",
            tspProjectPath: tspDir,
            editScope: EditScope.SpecInputs,
            ct: CancellationToken.None);
        Assert.That(result.Success, Is.True);
        Assert.That(result.TypeSpecChangesSummary, Is.Not.Null);
        Assert.That(result.TypeSpecChangesSummary.Count, Is.GreaterThan(0));
    }
    // ========================================================================
    // Mock helpers
    // ========================================================================
    private static Mock<IGitHelper> MockGitHelper()
    {
        var gitHelper = new Mock<IGitHelper>();
        gitHelper.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns<string, CancellationToken>((path, ct) =>
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path cannot be null or empty", nameof(path));
            }
            if (!Directory.Exists(path))
            {
                throw new InvalidOperationException($"The directory '{path}' does not exist.");
            }
            return Task.FromResult("azure-sdk-for-java");
        });
        gitHelper.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns<string, CancellationToken>((path, ct) =>
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path cannot be null or empty", nameof(path));
            }
            if (!Directory.Exists(path))
            {
                throw new InvalidOperationException($"The directory '{path}' does not exist.");
            }
            return Task.FromResult(Path.GetFullPath(path));
        });

        return gitHelper;
    }
    /// <summary>
    /// Flexible language service mock where all behaviors can be configured via constructor.
    /// </summary>
    private class ConfigurableLanguageService : LanguageService
    {
        private readonly Func<(bool Success, string? Error, PackageInfo? Info)> _buildFunc;
        private readonly Func<List<AppliedPatch>>? _patchesFunc;
        private readonly bool _hasCustomizations;
        private readonly bool _isCustomizedCodeUpdateSupported;
        private readonly Func<string, CancellationToken, Task>? _preGenerateFunc;
        private readonly Action<string, int>? _repairPatch;
        public int RepairSessions { get; private set; }
        public int LegacyPatchCalls { get; private set; }
        public int PatchTurns { get; private set; }
        public List<string> RetryPrompts { get; } = [];
        public List<CancellationToken> OperationTokens { get; } = [];

        public override SdkLanguage Language { get; }
        public override bool IsCustomizedCodeUpdateSupported => _isCustomizedCodeUpdateSupported;

        public ConfigurableLanguageService(
            Func<(bool, string?, PackageInfo?)>? buildFunc = null,
            bool hasCustomizations = false,
            Func<List<AppliedPatch>>? patchesFunc = null,
            SdkLanguage language = SdkLanguage.Java,
            bool isCustomizedCodeUpdateSupported = true,
            Func<string, CancellationToken, Task>? preGenerateFunc = null,
            Action<string, int>? repairPatch = null)
        {
            _buildFunc = buildFunc ?? (() => (true, null, null));
            _hasCustomizations = hasCustomizations;
            _patchesFunc = patchesFunc;
            Language = language;
            _isCustomizedCodeUpdateSupported = isCustomizedCodeUpdateSupported;
            _preGenerateFunc = preGenerateFunc;
            _repairPatch = repairPatch;
        }

        public override Task PreGenerateAsync(string repoRoot, CancellationToken ct)
            => _preGenerateFunc?.Invoke(repoRoot, ct) ?? Task.CompletedTask;

        public override async Task<GenerationPreparationResult> PrepareForGenerationAsync(string repoRoot, CancellationToken ct)
        {
            OperationTokens.Add(ct);
            await PreGenerateAsync(repoRoot, ct);
            return new GenerationPreparationResult(Language == SdkLanguage.DotNet, true, null);
        }

        public override Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath, CancellationToken ct)
            => Task.FromResult(new List<ApiChange>());

        public override string? HasCustomizations(string packagePath, CancellationToken ct = default)
            => _hasCustomizations ? Path.GetDirectoryName(CustomFile(packagePath, Language)) : null;

        public override Task<List<AppliedPatch>> ApplyPatchesAsync(string customizationRoot, string packagePath, string buildContext, CancellationToken ct)
        {
            LegacyPatchCalls++;
            return Task.FromResult(_patchesFunc?.Invoke() ?? new List<AppliedPatch>());
        }

        public override async Task RunRepairSessionAsync(string customizationRoot, string packagePath, string buildContext,
            int maxAttempts, Func<CancellationToken, Task> onTurnStarting,
            Func<string?, CancellationToken, Task<CopilotAgentTurnResult<string>>> onTurnCompleted,
            Action<AppliedPatch> onPatchApplied, CancellationToken ct)
        {
            RepairSessions++;
            OperationTokens.Add(ct);
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                await onTurnStarting(ct);
                PatchTurns++;
                foreach (var patch in _patchesFunc?.Invoke() ?? [])
                {
                    onPatchApplied(patch);
                }
                _repairPatch?.Invoke(packagePath, attempt);
                ct.ThrowIfCancellationRequested();
                var turn = await onTurnCompleted($"candidate {attempt}", ct);
                if (!turn.Continue) { return; }
                RetryPrompts.Add(turn.Prompt!);
            }
        }

        public override Task<ValidationResult> ValidateAsync(string packagePath, CancellationToken ct)
            => Task.FromResult(ValidationResult.CreateSuccess());

        public override Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo)> BuildAsync(string packagePath, int timeoutMinutes = 30, CancellationToken ct = default)
        {
            OperationTokens.Add(ct);
            return Task.FromResult(_buildFunc());
        }

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
            return Task.FromResult(new TspToolResponse { IsSuccessful = false, TypeSpecProject = tspLocationDirectory, ResponseError = _failError });
        return Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProject = tspLocationDirectory });
    }

    public Task<TspToolResponse> InitializeGenerationAsync(string workingDirectory, string tspConfigPath, string[]? additionalArgs = null, CancellationToken ct = default)
        => Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProject = workingDirectory });
}
