using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Microsoft.Extensions.Logging.Abstractions;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Microsoft.Extensions.Logging;
using Moq;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.TypeSpec;
using Azure.Sdk.Tools.Cli.Tools.TypeSpec;


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

    /// <summary>
    /// Creates a fully-wired <see cref="CustomizedCodeUpdateTool"/> with sensible default mocks.
    /// Callers can customise individual mocks before construction by passing them in.
    /// </summary>
    private static (CustomizedCodeUpdateTool tool, ToolMocks mocks) CreateTool(
        LanguageService? languageService = null,
        Action<Mock<IGitHelper>>? configureGit = null,
        Action<Mock<IFeedbackClassifierService>>? configureClassifier = null,
        Action<Mock<ITypeSpecCustomizationService>>? configureTspCustomization = null,
        ITspClientHelper? tspHelper = null)
    {
        var gitHelper = new Mock<IGitHelper>();
        gitHelper.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-java");
        gitHelper.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("/mock/repo/root");
        configureGit?.Invoke(gitHelper);

        var feedbackService = new Mock<IAPIViewFeedbackService>();
        var classifierService = new Mock<IFeedbackClassifierService>();

        // Default classifier: return a single TSP_APPLICABLE item with non-null text
        classifierService.Setup(c => c.ClassifyItemsAsync(
                It.IsAny<List<FeedbackItem>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeedbackClassificationResponse
            {
                Classifications =
                [
                    new FeedbackClassificationResponse.ItemClassificationDetails
                    {
                        ItemId = "1",
                        Classification = "TSP_APPLICABLE",
                        Reason = "Can be fixed via TypeSpec",
                        Text = "Rename FooClient to BarClient"
                    }
                ]
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
            typeSpecHelper.Object);

        return (tool, new ToolMocks(gitHelper, feedbackService, classifierService, typeSpecCustomization, typeSpecHelper));
    }

    private record ToolMocks(
        Mock<IGitHelper> GitHelper,
        Mock<IAPIViewFeedbackService> FeedbackService,
        Mock<IFeedbackClassifierService> ClassifierService,
        Mock<ITypeSpecCustomizationService> TypeSpecCustomization,
        Mock<ITypeSpecHelper> TypeSpecHelper);

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
    public async Task TspFix_BuildFailsFirstTry_PassesOnRetry_ReturnsSuccess()
    {
        var buildCalls = 0;
        var svc = new ConfigurableLanguageService(buildFunc: () =>
        {
            buildCalls++;
            return buildCalls == 1
                ? (false, "error: missing import", null)
                : (true, null, null);
        });

        var (tool, _) = CreateTool(languageService: svc);
        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Message, Does.Contain("Build passed"));
        Assert.That(buildCalls, Is.EqualTo(2), "Should have attempted build twice");
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
                    It.IsAny<int?>(),
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
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FeedbackClassificationResponse { Classifications = null }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput));
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
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FeedbackClassificationResponse
                {
                    Classifications =
                    [
                        new FeedbackClassificationResponse.ItemClassificationDetails
                        {
                            ItemId = "1", Classification = "SUCCESS",
                            Reason = "Already addressed", Text = "Looks good"
                        },
                        new FeedbackClassificationResponse.ItemClassificationDetails
                        {
                            ItemId = "2", Classification = "REQUIRES_MANUAL_INTERVENTION",
                            Reason = "Complex change", Text = "Restructure hierarchy"
                        }
                    ]
                }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Message, Does.Contain("manual intervention"));
    }

    [Test]
    public async Task Classification_EmptyOnSecondIteration_BreaksLoop()
    {
        // First call returns TSP_APPLICABLE, second call returns empty (nothing more to fix)
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
                        It.IsAny<int?>(),
                        It.IsAny<CancellationToken>()))
                    .Returns(() =>
                    {
                        classifyCalls++;
                        if (classifyCalls == 1)
                        {
                            return Task.FromResult(new FeedbackClassificationResponse
                            {
                                Classifications =
                                [
                                    new FeedbackClassificationResponse.ItemClassificationDetails
                                    {
                                        ItemId = "1", Classification = "TSP_APPLICABLE",
                                        Reason = "fixable", Text = "rename X"
                                    }
                                ]
                            });
                        }
                        return Task.FromResult(new FeedbackClassificationResponse { Classifications = [] });
                    }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(buildCalls, Is.EqualTo(1), "Should have only built once before second classify returned empty");
    }

    // ========================================================================
    // TSP customization failures
    // ========================================================================

    [Test]
    public async Task TspCustomization_AllFail_FirstIteration_ReturnsTypeSpecCustomizationFailed()
    {
        var (tool, _) = CreateTool(configureTspCustomization: t =>
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
                }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.TypeSpecCustomizationFailed));
        Assert.That(result.Message, Does.Contain("Could not parse TypeSpec project"));
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
                        It.IsAny<int?>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new FeedbackClassificationResponse
                    {
                        Classifications =
                        [
                            new FeedbackClassificationResponse.ItemClassificationDetails
                            {
                                ItemId = "1", Classification = "TSP_APPLICABLE",
                                Reason = "fixable", Text = "rename X to Y"
                            },
                            new FeedbackClassificationResponse.ItemClassificationDetails
                            {
                                ItemId = "2", Classification = "TSP_APPLICABLE",
                                Reason = "fixable", Text = "add decorator Z"
                            }
                        ]
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
    public async Task TspRegeneration_Fails_ContinuesLoopAndAppendsContext()
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
                        It.IsAny<int?>(),
                        It.IsAny<CancellationToken>()))
                    .Returns<List<FeedbackItem>, string, string, string?, string?, int?, CancellationToken>(
                        (items, _, _, _, _, _, _) =>
                        {
                            classifyCalls++;
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

        // Regen failure should not short-circuit; loop continues for re-classification
        Assert.That(result.Success, Is.False);
        Assert.That(classifyCalls, Is.EqualTo(2), "Should classify twice (maxTries) even when regen fails");
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
        // TSP loop: 2 builds fail, then falls through to patch pipeline, final build (3rd call) passes
        var svc = new ConfigurableLanguageService(
            buildFunc: () =>
            {
                buildCalls++;
                return buildCalls <= 2
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
        Assert.That(result.Message, Does.Contain("Build passed after repairs"));
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

        // TSP loop regen calls succeed (2x), Java regen-after-patches (3rd) fails
        var failingTspForJavaRegen = new CallCountMockTspHelper(failAfterCall: 2, failError: "regen failed: tsp-client error");
        var (tool, _) = CreateTool(languageService: svc, tspHelper: failingTspForJavaRegen);
        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

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
            c.Setup(x => x.ClassifyItemsAsync(
                    It.IsAny<List<FeedbackItem>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .Callback<List<FeedbackItem>, string, string, string?, string?, int?, CancellationToken>(
                    (items, _, _, _, _, _, _) => capturedFeedbackText = items.FirstOrDefault()?.Text)
                .ReturnsAsync(new FeedbackClassificationResponse
                {
                    Classifications =
                    [
                        new FeedbackClassificationResponse.ItemClassificationDetails
                        {
                            ItemId = "1", Classification = "TSP_APPLICABLE",
                            Reason = "test", Text = "Rename client"
                        }
                    ]
                }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

        await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "Please rename FooClient", ct: CancellationToken.None);

        Assert.That(capturedFeedbackText, Is.EqualTo("Please rename FooClient"));
    }

    [Test]
    public async Task RetryLoop_SecondIteration_FeedbackIncludesContextFromFirstIteration()
    {
        // Build fails first time, so second iteration should include context
        var buildCalls = 0;
        var classifyCalls = 0;
        string? secondCallContext = null;

        var svc = new ConfigurableLanguageService(buildFunc: () =>
        {
            buildCalls++;
            return buildCalls == 1
                ? (false, "error CS0246: type 'FooClient' not found", null)
                : (true, null, null);
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
                        It.IsAny<int?>(),
                        It.IsAny<CancellationToken>()))
                    .Returns<List<FeedbackItem>, string, string, string?, string?, int?, CancellationToken>(
                        (items, _, _, _, _, _, _) =>
                        {
                            classifyCalls++;
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
        Assert.That(secondCallContext, Does.Contain("Iteration 1"));
        Assert.That(secondCallContext, Does.Contain("Typespec changes applied"));
        Assert.That(secondCallContext, Does.Contain("Renamed FooClient to BarClient"));
        Assert.That(secondCallContext, Does.Contain("Build Result"));
        Assert.That(secondCallContext, Does.Contain("error CS0246"));
    }

    [Test]
    public async Task RetryLoop_MaxTriesRespected_DoesNotExceedTwoIterations()
    {
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
                        It.IsAny<int?>(),
                        It.IsAny<CancellationToken>()))
                    .Callback(() => classifyCalls++)
                    .ReturnsAsync(new FeedbackClassificationResponse
                    {
                        Classifications =
                        [
                            new FeedbackClassificationResponse.ItemClassificationDetails
                            {
                                ItemId = "1", Classification = "TSP_APPLICABLE",
                                Reason = "fixable", Text = "rename X"
                            }
                        ]
                    }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "test customization", ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(classifyCalls, Is.EqualTo(2), "Should classify exactly 2 times (maxTries)");
        Assert.That(buildCalls, Is.EqualTo(2), "Should build exactly 2 times (maxTries)");
    }

    [Test]
    public async Task Regeneration_UsesTspProjectPath()
    {
        // Verify that UpdateGenerationAsync receives the tspProjectPath as localSpecRepoPath
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

        Assert.That(capturedLocalSpecRepo, Is.EqualTo(tspDir),
            "Should pass the tspProjectPath as localSpecRepoPath");
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

        public override Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath)
            => Task.FromResult(new List<ApiChange>());

        public override string? HasCustomizations(string packagePath, CancellationToken ct = default)
            => _hasCustomizations ? Path.Combine(packagePath, "customization") : null;

        public override Task<List<AppliedPatch>> ApplyPatchesAsync(string customizationRoot, string packagePath, string buildError, CancellationToken ct)
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
        => Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProject = workingDirectory });
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
