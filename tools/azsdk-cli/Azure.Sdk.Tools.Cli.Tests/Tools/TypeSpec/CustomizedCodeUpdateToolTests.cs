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

        var svc = languageService ?? new MockNoChangeLanguageService();
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
    // Happy-path tests (TSP fix + build succeeds)
    // ========================================================================

    [Test]
    public async Task TspFix_BuildPassesFirstIteration_ReturnsSuccess()
    {
        var (tool, _) = CreateTool(languageService: new MockNoChangeLanguageService());
        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, ct: CancellationToken.None);

        Assert.That(result.Success, Is.True, "Should succeed when build passes on first try");
        Assert.That(result.ErrorCode, Is.Null);
        Assert.That(result.Message, Does.Contain("Build passed"));
    }

    [Test]
    public async Task TspFix_BuildPassesFirstIteration_WithCustomizations_ReturnsSuccess()
    {
        var svc = new MockChangeLanguageService();
        var (tool, _) = CreateTool(languageService: svc, configureGit: g =>
            g.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-python"));
        var pkg = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(pkg, "customization"));
        var tspDir = CreateTempDir();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, ct: CancellationToken.None);

        Assert.That(result.Success, Is.True, "Should succeed when build passes");
        Assert.That(result.ErrorCode, Is.Null);
        Assert.That(result.Message, Does.Contain("Build passed"));
    }

    [Test]
    public async Task TspFix_BuildFailsFirstTry_PassesOnRetry_ReturnsSuccess()
    {
        // Build fails first time (during first classify+fix+build loop), passes second time
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

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, ct: CancellationToken.None);

        Assert.That(result.Success, Is.True, "Should succeed after retry");
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

    // ========================================================================
    // Classification failures
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

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, ct: CancellationToken.None);

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

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput));
    }

    [Test]
    public void TspProjectPath_DoesNotExist_ThrowsDirectoryNotFoundException()
    {
        // Classify checks Directory.Exists(tspProjectPath) and throws DirectoryNotFoundException
        var (tool, _) = CreateTool();
        var pkg = CreateTempDir();
        var badTspDir = Path.Combine(Path.GetTempPath(), "nonexistent-tsp-" + Guid.NewGuid().ToString("n"));

        Assert.ThrowsAsync<DirectoryNotFoundException>(async () =>
            await tool.UpdateAsync(packagePath: pkg, tspProjectPath: badTspDir, ct: CancellationToken.None));
    }

    // ========================================================================
    // ApplyTypeSpecFixesAndRegenerate failures
    // ========================================================================

    [Test]
    public async Task TspCustomization_Fails_ReturnsTypeSpecCustomizationFailed()
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

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.TypeSpecCustomizationFailed));
        Assert.That(result.BuildResult, Does.Contain("Could not parse TypeSpec project"));
    }

    [Test]
    public async Task TspRegeneration_Fails_ReturnsTypeSpecCustomizationFailed()
    {
        var failingTsp = new MockTspHelper(updateSuccess: false, updateError: "tsp-client failed: exit code 1");
        var (tool, _) = CreateTool(tspHelper: failingTsp);
        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.TypeSpecCustomizationFailed));
        Assert.That(result.BuildResult, Does.Contain("Regeneration failed"));
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

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, ct: CancellationToken.None);

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

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, ct: CancellationToken.None);

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
            patchesFunc: () => []); // No patches can be applied

        var (tool, _) = CreateTool(languageService: svc);
        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, ct: CancellationToken.None);

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
            patchesFunc: () => [new AppliedPatch("test.java", "Fixed variable conflict", 1)],
            language: SdkLanguage.Python); // non-Java to skip Java regen

        var (tool, _) = CreateTool(languageService: svc, configureGit: g =>
            g.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-python"));
        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, ct: CancellationToken.None);

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

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, ct: CancellationToken.None);

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

        // First 2 UpdateGenerationAsync calls succeed (TSP regen in loop), 3rd fails (Java regen after patches)
        var failingTspForJavaRegen = new CallCountMockTspHelper(failAfterCall: 2, failError: "regen failed: tsp-client error");
        var (tool, _) = CreateTool(languageService: svc, tspHelper: failingTspForJavaRegen);
        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, ct: CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.RegenerateAfterPatchesFailed));
        Assert.That(result.AppliedPatches, Is.Not.Null.And.Count.EqualTo(1));
    }

    // ========================================================================
    // Classification branch coverage
    // ========================================================================

    [Test]
    public async Task Classification_SuccessItems_AreLoggedAndSkipped()
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
                .ReturnsAsync(new FeedbackClassificationResponse
                {
                    Classifications =
                    [
                        new FeedbackClassificationResponse.ItemClassificationDetails
                        {
                            ItemId = "1",
                            Classification = "SUCCESS",
                            Reason = "Already addressed",
                            Text = "Looks good"
                        }
                    ]
                }));

        // TSP customization will receive empty text (no TSP_APPLICABLE items)
        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

        // Should not crash; proceeds through the flow
        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, ct: CancellationToken.None);
        // Result depends on build outcome (defaults to success via MockNoChangeLanguageService)
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task Classification_ManualInterventionItems_AreLoggedAndSkipped()
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
                .ReturnsAsync(new FeedbackClassificationResponse
                {
                    Classifications =
                    [
                        new FeedbackClassificationResponse.ItemClassificationDetails
                        {
                            ItemId = "1",
                            Classification = "REQUIRES_MANUAL_INTERVENTION",
                            Reason = "Complex change needed",
                            Text = "Restructure entire client hierarchy"
                        }
                    ]
                }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, ct: CancellationToken.None);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task Classification_TspApplicableWithNullText_IsSkipped()
    {
        var (tool, mocks) = CreateTool(configureClassifier: c =>
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
                            ItemId = "1",
                            Classification = "TSP_APPLICABLE",
                            Reason = "Can be fixed",
                            Text = null // Null text should be skipped
                        }
                    ]
                }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

        // Should not crash; null-text items are skipped
        var result = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, ct: CancellationToken.None);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task CustomizationRequest_FlowsToClassifier()
    {
        string? capturedFeedbackText = null;
        var (tool, mocks) = CreateTool(configureClassifier: c =>
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
                            ItemId = "1",
                            Classification = "TSP_APPLICABLE",
                            Reason = "test",
                            Text = "Rename client"
                        }
                    ]
                }));

        var pkg = CreateTempDir();
        var tspDir = CreateTempDir();

        await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, customizationRequest: "Please rename FooClient", ct: CancellationToken.None);

        Assert.That(capturedFeedbackText, Is.EqualTo("Please rename FooClient"), "customizationRequest should flow as FeedbackItem text");
    }

    // ========================================================================
    // Original tests (updated for new signatures)
    // ========================================================================

    [Test]
    public async Task Validation_Failure_Provides_Guidance()
    {
        int calls = 0;
        var svc = new TestLanguageServiceFailThenFix(() => calls++);
        var (tool, _) = CreateTool(languageService: svc);
        var pkg = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(pkg, "customization"));
        var tspDir = CreateTempDir();

        var resp = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, ct: CancellationToken.None);

        Assert.That(resp.Success, Is.False, "Should not succeed when build fails");
        Assert.That(resp.ErrorCode, Is.Not.Null, "Should have error code for build failure");
        Assert.That(resp.BuildResult, Is.Not.Null, "Should have build result on failure");
    }

    [Test]
    public async Task ErrorDrivenRepair_BuildFailsThenSucceeds_CompletesSuccessfully()
    {
        var svc = new TestLanguageServiceBuildFailThenPass();
        var (tool, _) = CreateTool(languageService: svc);
        var pkg = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(pkg, "customization"));
        var tspDir = CreateTempDir();

        var resp = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, ct: CancellationToken.None);

        Assert.That(resp.Success, Is.True, "Should succeed after repair");
        Assert.That(resp.ErrorCode, Is.Null, "Should complete successfully after repair");
        Assert.That(resp.Message, Does.Contain("Build passed after repairs"), "Should indicate repair was performed");
    }

    [Test]
    public async Task ErrorDrivenRepair_MaxIterationsReached_ReturnsGuidance()
    {
        var svc = new TestLanguageServiceBuildAlwaysFails();
        var (tool, _) = CreateTool(languageService: svc);
        var pkg = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(pkg, "customization"));
        var tspDir = CreateTempDir();

        var resp = await tool.UpdateAsync(packagePath: pkg, tspProjectPath: tspDir, ct: CancellationToken.None);

        Assert.That(resp.Success, Is.False, "Should not succeed when build keeps failing");
        Assert.That(resp.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.BuildAfterPatchesFailed), "Should have build failure error code");
        Assert.That(resp.BuildResult, Is.Not.Null, "Should have build result");
    }

    // ========================================================================
    // Mock language services
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

    // Language service that produces no API changes and no customizations
    private class MockNoChangeLanguageService : LanguageService
    {
        public override SdkLanguage Language { get; } = SdkLanguage.Java;
        public override bool IsCustomizedCodeUpdateSupported => true;
        public override Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath) => Task.FromResult(new List<ApiChange>());
        public override string? HasCustomizations(string packagePath, CancellationToken ct = default) => null;
        public override Task<List<AppliedPatch>> ApplyPatchesAsync(string customizationRoot, string packagePath, string buildError, CancellationToken ct) => Task.FromResult(new List<AppliedPatch>());
        public override Task<ValidationResult> ValidateAsync(string packagePath, CancellationToken ct) => Task.FromResult(ValidationResult.CreateSuccess());
        public override Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo)> BuildAsync(string packagePath, int timeoutMinutes = 30, CancellationToken ct = default)
            => Task.FromResult<(bool, string?, PackageInfo?)>((true, null, null));
        public override Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default) => Task.FromResult(new PackageInfo
        {
            PackagePath = packagePath, RepoRoot = "/mock/repo", RelativePath = "sdk/mock/package",
            PackageName = "mock-package", ServiceName = "mock", PackageVersion = "1.0.0",
            SamplesDirectory = "/mock/samples", Language = SdkLanguage.Java, SdkType = SdkType.Dataplane
        });
    }

    // Language service that has customizations and successful patch application
    private class MockChangeLanguageService : LanguageService
    {
        public override SdkLanguage Language { get; } = SdkLanguage.Python;
        public override bool IsCustomizedCodeUpdateSupported => true;
        public override Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath)
            => Task.FromResult(new List<ApiChange> { new ApiChange { Kind = "MethodAdded", Symbol = "S1", Detail = "Added method S1" } });
        public override string? HasCustomizations(string packagePath, CancellationToken ct = default) => Path.Combine(packagePath, "customization");
        public override Task<List<AppliedPatch>> ApplyPatchesAsync(string customizationRoot, string packagePath, string buildError, CancellationToken ct)
            => Task.FromResult(new List<AppliedPatch> { new AppliedPatch("test.py", "test patch", 1) });
        public override Task<ValidationResult> ValidateAsync(string packagePath, CancellationToken ct) => Task.FromResult(ValidationResult.CreateSuccess());
        public override Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo)> BuildAsync(string packagePath, int timeoutMinutes = 30, CancellationToken ct = default)
            => Task.FromResult<(bool, string?, PackageInfo?)>((true, null, null));
        public override Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default) => Task.FromResult(new PackageInfo
        {
            PackagePath = packagePath, RepoRoot = "/mock/repo", RelativePath = "sdk/mock/package",
            PackageName = "mock-package", ServiceName = "mock", PackageVersion = "1.0.0",
            SamplesDirectory = "/mock/samples", Language = SdkLanguage.Python, SdkType = SdkType.Dataplane
        });
    }

    // Language service that fails first build, then passes second build (successful repair)
    private class TestLanguageServiceBuildFailThenPass : LanguageService
    {
        public override SdkLanguage Language { get; } = SdkLanguage.Java;
        public override bool IsCustomizedCodeUpdateSupported => true;
        private int _buildCalls = 0;
        public override Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath) => Task.FromResult(new List<ApiChange>());
        public override string? HasCustomizations(string packagePath, CancellationToken ct = default) => Path.Combine(packagePath, "customization");
        public override Task<List<AppliedPatch>> ApplyPatchesAsync(string customizationRoot, string packagePath, string buildError, CancellationToken ct) => Task.FromResult(new List<AppliedPatch> { new AppliedPatch("test.java", "Applied test patch", 1) });
        public override Task<ValidationResult> ValidateAsync(string packagePath, CancellationToken ct) => Task.FromResult(ValidationResult.CreateSuccess());
        public override Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo)> BuildAsync(string packagePath, int timeoutMinutes = 30, CancellationToken ct = default)
        {
            _buildCalls++;
            // First 2 builds fail (TSP loop), 3rd passes (final build after patches)
            if (_buildCalls <= 2) return Task.FromResult<(bool, string?, PackageInfo?)>((false, "variable operationId is already defined", null));
            return Task.FromResult<(bool, string?, PackageInfo?)>((true, null, null));
        }
        public override Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default) => Task.FromResult(new PackageInfo
        {
            PackagePath = packagePath, RepoRoot = "/mock/repo", RelativePath = "sdk/mock/package",
            PackageName = "mock-package", ServiceName = "mock", PackageVersion = "1.0.0",
            SamplesDirectory = "/mock/samples", Language = SdkLanguage.Java, SdkType = SdkType.Dataplane
        });
    }

    // Language service that always fails build with same error (stall scenario)
    private class TestLanguageServiceBuildAlwaysFails : LanguageService
    {
        public override SdkLanguage Language { get; } = SdkLanguage.Java;
        public override bool IsCustomizedCodeUpdateSupported => true;
        public override Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath) => Task.FromResult(new List<ApiChange>());
        public override string? HasCustomizations(string packagePath, CancellationToken ct = default) => Path.Combine(packagePath, "customization");
        public override Task<List<AppliedPatch>> ApplyPatchesAsync(string customizationRoot, string packagePath, string buildError, CancellationToken ct) => Task.FromResult(new List<AppliedPatch> { new AppliedPatch("test.java", "Applied test patch", 1) });
        public override Task<ValidationResult> ValidateAsync(string packagePath, CancellationToken ct) => Task.FromResult(ValidationResult.CreateSuccess());
        public override Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo)> BuildAsync(string packagePath, int timeoutMinutes = 30, CancellationToken ct = default)
            => Task.FromResult<(bool, string?, PackageInfo?)>((false, "same error every time", null));
        public override Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default) => Task.FromResult(new PackageInfo
        {
            PackagePath = packagePath, RepoRoot = "/mock/repo", RelativePath = "sdk/mock/package",
            PackageName = "mock-package", ServiceName = "mock", PackageVersion = "1.0.0",
            SamplesDirectory = "/mock/samples", Language = SdkLanguage.Java, SdkType = SdkType.Dataplane
        });
    }

    private class TestLanguageServiceFailThenFix : LanguageService
    {
        public override SdkLanguage Language { get; } = SdkLanguage.Java;
        public override bool IsCustomizedCodeUpdateSupported => true;
        private Func<int> _next;
        public TestLanguageServiceFailThenFix(Func<int> next) { _next = next; }
        public override Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath) => Task.FromResult(new List<ApiChange>());
        public override string? HasCustomizations(string packagePath, CancellationToken ct = default) => Path.Combine(packagePath, "customization");
        public override Task<List<AppliedPatch>> ApplyPatchesAsync(string customizationRoot, string packagePath, string buildError, CancellationToken ct) => Task.FromResult(new List<AppliedPatch> { new AppliedPatch("test.java", "Applied test patch", 1) });
        public override Task<ValidationResult> ValidateAsync(string packagePath, CancellationToken ct)
        {
            var attempt = _next();
            if (attempt == 0) return Task.FromResult(ValidationResult.CreateFailure("compile error"));
            return Task.FromResult(ValidationResult.CreateSuccess());
        }
        public override Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo)> BuildAsync(string packagePath, int timeoutMinutes = 30, CancellationToken ct = default)
            => Task.FromResult<(bool, string?, PackageInfo?)>((false, "Build failed for testing", null));
        public override Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default) => Task.FromResult(new PackageInfo
        {
            PackagePath = packagePath, RepoRoot = "/mock/repo", RelativePath = "sdk/mock/package",
            PackageName = "mock-package", ServiceName = "mock", PackageVersion = "1.0.0",
            SamplesDirectory = "/mock/samples", Language = SdkLanguage.Java, SdkType = SdkType.Dataplane
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
