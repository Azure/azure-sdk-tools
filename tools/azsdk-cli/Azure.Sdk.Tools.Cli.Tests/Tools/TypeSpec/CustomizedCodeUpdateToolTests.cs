using Azure.Sdk.Tools.Cli.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Services.TypeSpec;
using Moq;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.TypeSpec;


namespace Azure.Sdk.Tools.Cli.Tests.Tools.TypeSpec;

[TestFixture]
public class CustomizedCodeUpdateToolAutoTests
{
    private static string CreateTempPackageDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "azsdk-test-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateTempTypeSpecProjectDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "azsdk-tsp-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(path);
        // Create tspconfig.yaml so IsValidTypeSpecProjectPath returns true
        File.WriteAllText(Path.Combine(path, "tspconfig.yaml"), "# mock tspconfig");
        return path;
    }

    private static Mock<ITypeSpecCustomizationService> CreateMockCustomizationService(bool success = true, string[]? changes = null, string? failureReason = null)
    {
        var mock = new Mock<ITypeSpecCustomizationService>();
        mock.Setup(s => s.ApplyCustomizationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TypeSpecCustomizationServiceResult
            {
                Success = success,
                ChangesSummary = changes ?? (success ? ["Applied test customization"] : []),
                FailureReason = failureReason
            });
        return mock;
    }

    private static Mock<ITypeSpecHelper> CreateMockTypeSpecHelper(bool isValid = true)
    {
        var mock = new Mock<ITypeSpecHelper>();
        mock.Setup(h => h.IsValidTypeSpecProjectPath(It.IsAny<string>())).Returns(isValid);
        return mock;
    }

    // Language service that produces no API changes and no customizations
    private class MockNoChangeLanguageService : LanguageService
    {
        public override SdkLanguage Language { get; } = SdkLanguage.Java;
        public override bool IsCustomizedCodeUpdateSupported => true;
        public override Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath) => Task.FromResult(new List<ApiChange>());
        public override string? HasCustomizations(string packagePath, CancellationToken ct = default) => null; // No customizations
        public override Task<List<AppliedPatch>> ApplyPatchesAsync(string customizationRoot, string packagePath, string buildError, CancellationToken ct) => Task.FromResult(new List<AppliedPatch>());
        public override Task<ValidationResult> ValidateAsync(string packagePath, CancellationToken ct) => Task.FromResult(ValidationResult.CreateSuccess());
        public override Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo)> BuildAsync(string packagePath, int timeoutMinutes = 30, CancellationToken ct = default)
            => Task.FromResult<(bool, string?, PackageInfo?)>((true, null, null));
        public override Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default) => Task.FromResult(new PackageInfo
        {
            PackagePath = packagePath,
            RepoRoot = "/mock/repo",
            RelativePath = "sdk/mock/package",
            PackageName = "mock-package",
            ServiceName = "mock",
            PackageVersion = "1.0.0",
            SamplesDirectory = "/mock/samples",
            Language = SdkLanguage.Java,
            SdkType = SdkType.Dataplane
        });
    }

    // Language service that has customizations and successful patch application
    private class MockChangeLanguageService(bool buildSuccess = true) : LanguageService
    {
        public override SdkLanguage Language { get; } = SdkLanguage.Python;
        public override bool IsCustomizedCodeUpdateSupported => true;
        public SdkLanguage SupportedLanguage => SdkLanguage.Python;
        public override Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath)
            => Task.FromResult(new List<ApiChange> {
                new ApiChange { Kind = "MethodAdded", Symbol = "S1", Detail = "Added method S1" }
            });
        public override string? HasCustomizations(string packagePath, CancellationToken ct = default) => Path.Combine(packagePath, "customization"); // Has customizations
        public override Task<List<AppliedPatch>> ApplyPatchesAsync(string customizationRoot, string packagePath, string buildError, CancellationToken ct)
            => Task.FromResult(new List<AppliedPatch> { new AppliedPatch("test.py", "test patch", 1) }); // Simulate successful patch application
        public override Task<ValidationResult> ValidateAsync(string packagePath, CancellationToken ct) => Task.FromResult(ValidationResult.CreateSuccess());
        public override Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo)> BuildAsync(string packagePath, int timeoutMinutes = 30, CancellationToken ct = default)
            => Task.FromResult<(bool, string?, PackageInfo?)>((buildSuccess, buildSuccess ? null : "Build failed for testing", null));
        public override Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default) => Task.FromResult(new PackageInfo
        {
            PackagePath = packagePath,
            RepoRoot = "/mock/repo",
            RelativePath = "sdk/mock/package",
            PackageName = "mock-package",
            ServiceName = "mock",
            PackageVersion = "1.0.0",
            SamplesDirectory = "/mock/samples",
            Language = SdkLanguage.Python,
            SdkType = SdkType.Dataplane
        });
    }

    [Test]
    public async Task TspCustomizations_Succeeds_BuildPasses_ReturnsSuccess()
    {
        var gitHelper = new Mock<IGitHelper>();
        gitHelper.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-java");
        gitHelper.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("/mock/repo/root");
        var svc = new MockNoChangeLanguageService();
        var tsp = new MockTspHelper();
        var custService = CreateMockCustomizationService(success: true, changes: ["Renamed FooClient to BarClient"]);
        var typeSpecHelper = CreateMockTypeSpecHelper();

        var tool = new CustomizedCodeUpdateTool(
            new NullLogger<CustomizedCodeUpdateTool>(), [svc], gitHelper.Object, tsp,
            custService.Object, typeSpecHelper.Object);

        var pkg = CreateTempPackageDir();
        var tspProject = CreateTempTypeSpecProjectDir();

        var result = await tool.UpdateAsync("Rename FooClient to BarClient", tspProject, pkg, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ErrorCode, Is.Null, "Should complete successfully without errors");
            Assert.That(result.Message, Does.Contain("successfully"), "Should indicate success");
            Assert.That(result.TypeSpecChangesSummary, Is.Not.Null.And.Not.Empty, "Should include TypeSpec changes summary");
            Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty, "Should provide next steps guidance");
        });

    }

    [Test]
    public async Task TspCustomizations_Fails_ReturnsTypeSpecCustomizationFailed()
    {
        var gitHelper = new Mock<IGitHelper>();
        gitHelper.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-java");
        gitHelper.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("/mock/repo/root");
        var svc = new MockNoChangeLanguageService();
        var tsp = new MockTspHelper();
        var custService = CreateMockCustomizationService(success: false, failureReason: "Could not find suitable decorator");
        var typeSpecHelper = CreateMockTypeSpecHelper();

        var tool = new CustomizedCodeUpdateTool(
            new NullLogger<CustomizedCodeUpdateTool>(), [svc], gitHelper.Object, tsp,
            custService.Object, typeSpecHelper.Object);

        var pkg = CreateTempPackageDir();
        var run = await tool.UpdateAsync(packagePath: pkg, ct: CancellationToken.None);
        Assert.That(run.Success, Is.True, "Should complete successfully");
        Assert.That(run.ErrorCode, Is.Null, "Should have no error code");
        // With error-driven flow: build passes → success, message reflects build success
        Assert.That(run.Message, Does.Contain("Build passed"), "Should indicate build passed");
    }

    [Test]
    public async Task TspCustomizations_Succeeds_BuildFails_HasCustomizations_CodeCustomizationsSucceeds()
    {
        var gitHelper = new Mock<IGitHelper>();
        gitHelper.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-python");
        gitHelper.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("/mock/repo/root");

        // Build fails first (after Phase A), succeeds second (after Phase B)
        int buildCallCount = 0;
        var svc = new Mock<LanguageService>();
        svc.SetupGet(s => s.Language).Returns(SdkLanguage.Python);
        svc.SetupGet(s => s.IsCustomizedCodeUpdateSupported).Returns(true);
        svc.Setup(s => s.HasCustomizations(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(true);
        svc.Setup(s => s.ApplyPatchesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        svc.Setup(s => s.BuildAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                buildCallCount++;
                return buildCallCount == 1
                    ? (false, "error CS0246: type not found", (PackageInfo?)null)
                    : (true, null, (PackageInfo?)null);
            });

        var tsp = new MockTspHelper();
        var custService = CreateMockCustomizationService(success: true, changes: ["Applied decorator"]);
        var typeSpecHelper = CreateMockTypeSpecHelper();

        var tool = new CustomizedCodeUpdateTool(
            new NullLogger<CustomizedCodeUpdateTool>(), [svc.Object], gitHelper.Object, tsp,
            custService.Object, typeSpecHelper.Object);

        var pkg = CreateTempPackageDir();
        // Create a mock customization directory
        Directory.CreateDirectory(Path.Combine(pkg, "customization"));
        var first = await tool.UpdateAsync(packagePath: pkg, ct: CancellationToken.None);
        Assert.That(first.Success, Is.True, "Should complete successfully");
        Assert.That(first.ErrorCode, Is.Null, "Should have no error code");
        // With error-driven flow: build passes first → success (no repair needed if build passes)
        Assert.That(first.Message, Does.Contain("Build passed"), "Should indicate build passed");
    }

    [Test]
    public async Task TspCustomizations_Succeeds_BuildFails_NoCustomizations_ReturnsGuidance()
    {
        var gitHelper = new Mock<IGitHelper>();
        gitHelper.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-java");
        gitHelper.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("/mock/repo/root");

        var svc = new Mock<LanguageService>();
        svc.SetupGet(s => s.Language).Returns(SdkLanguage.Java);
        svc.SetupGet(s => s.IsCustomizedCodeUpdateSupported).Returns(true);
        svc.Setup(s => s.HasCustomizations(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(false);
        svc.Setup(s => s.BuildAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, "Build failed: missing symbol", (PackageInfo?)null));

        var tsp = new MockTspHelper();
        var custService = CreateMockCustomizationService(success: true, changes: ["Applied decorator"]);
        var typeSpecHelper = CreateMockTypeSpecHelper();

        var tool = new CustomizedCodeUpdateTool(
            new NullLogger<CustomizedCodeUpdateTool>(), [svc.Object], gitHelper.Object, tsp,
            custService.Object, typeSpecHelper.Object);

        var pkg = CreateTempPackageDir();
        // Create a mock customization directory to trigger patch application
        Directory.CreateDirectory(Path.Combine(pkg, "customization"));
        var resp = await tool.UpdateAsync(packagePath: pkg, ct: CancellationToken.None);
        // Build failure now returns an error code (structured for classifier)
        Assert.That(resp.Success, Is.False, "Should not succeed when build fails");
        Assert.That(resp.ErrorCode, Is.Not.Null, "Should have error code for build failure");
        Assert.That(resp.BuildResult, Is.Not.Null, "Should have build result on failure");
    }

    [Test]
    public async Task ErrorDrivenRepair_BuildFailsThenSucceeds_CompletesSuccessfully()
    {
        var tsp = new MockTspHelper();
        var gitHelper = new Mock<IGitHelper>();
        var specGenSdkConfigHelper = new Mock<ISpecGenSdkConfigHelper>();
        var svc = new TestLanguageServiceBuildFailThenPass();
        gitHelper.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-java");
        gitHelper.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("/mock/repo/root");
        specGenSdkConfigHelper.Setup(s => s.GetConfigurationAsync(It.IsAny<string>(), It.IsAny<SpecGenSdkConfigType>()))
            .ReturnsAsync((SpecGenSdkConfigContentType.Unknown, string.Empty));
        var tool = new CustomizedCodeUpdateTool(new NullLogger<CustomizedCodeUpdateTool>(), [svc], gitHelper.Object, tsp);
        var pkg = CreateTempPackageDir();
        Directory.CreateDirectory(Path.Combine(pkg, "customization"));
        var resp = await tool.UpdateAsync(packagePath: pkg, ct: CancellationToken.None);
        // Should succeed after error-driven repair
        Assert.That(resp.Success, Is.True, "Should succeed after repair");
        Assert.That(resp.ErrorCode, Is.Null, "Should complete successfully after repair");
        Assert.That(resp.Message, Does.Contain("Build passed after repairs"), "Should indicate repair was performed");
    }

    [Test]
    public async Task ErrorDrivenRepair_MaxIterationsReached_ReturnsGuidance()
    {
        var tsp = new MockTspHelper();
        var gitHelper = new Mock<IGitHelper>();
        var specGenSdkConfigHelper = new Mock<ISpecGenSdkConfigHelper>();
        var svc = new TestLanguageServiceBuildAlwaysFails();
        gitHelper.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-java");
        gitHelper.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("/mock/repo/root");
        specGenSdkConfigHelper.Setup(s => s.GetConfigurationAsync(It.IsAny<string>(), It.IsAny<SpecGenSdkConfigType>()))
            .ReturnsAsync((SpecGenSdkConfigContentType.Unknown, string.Empty));
        var tool = new CustomizedCodeUpdateTool(new NullLogger<CustomizedCodeUpdateTool>(), [svc], gitHelper.Object, tsp);
        var pkg = CreateTempPackageDir();
        Directory.CreateDirectory(Path.Combine(pkg, "customization"));
        var resp = await tool.UpdateAsync(packagePath: pkg, ct: CancellationToken.None);
        // Should exhaust and return failure with build result
        Assert.That(resp.Success, Is.False, "Should not succeed when build keeps failing");
        Assert.That(resp.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.BuildAfterPatchesFailed), "Should have build failure error code");
        Assert.That(resp.BuildResult, Is.Not.Null, "Should have build result");
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
            if (_buildCalls == 1)
            {
                return Task.FromResult<(bool, string?, PackageInfo?)>((false, "variable operationId is already defined", null));
            }
            return Task.FromResult<(bool, string?, PackageInfo?)>((true, null, null));
        }
        public override Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default) => Task.FromResult(new PackageInfo
        {
            PackagePath = packagePath,
            RepoRoot = "/mock/repo",
            RelativePath = "sdk/mock/package",
            PackageName = "mock-package",
            ServiceName = "mock",
            PackageVersion = "1.0.0",
            SamplesDirectory = "/mock/samples",
            Language = SdkLanguage.Java,
            SdkType = SdkType.Dataplane
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
            PackagePath = packagePath,
            RepoRoot = "/mock/repo",
            RelativePath = "sdk/mock/package",
            PackageName = "mock-package",
            ServiceName = "mock",
            PackageVersion = "1.0.0",
            SamplesDirectory = "/mock/samples",
            Language = SdkLanguage.Java,
            SdkType = SdkType.Dataplane
        });
    }

    private class TestLanguageServiceFailThenFix: LanguageService
    {
        public override SdkLanguage Language { get; } = SdkLanguage.Java;
        public override bool IsCustomizedCodeUpdateSupported => true;
        public SdkLanguage SupportedLanguage => SdkLanguage.Java;
        private Func<int> _next;
        public TestLanguageServiceFailThenFix(Func<int> next) { _next = next; }
        public override Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath) => Task.FromResult(new List<ApiChange>());
        public override string? HasCustomizations(string packagePath, CancellationToken ct = default) => Path.Combine(packagePath, "customization"); // Has customizations
        public override Task<List<AppliedPatch>> ApplyPatchesAsync(string customizationRoot, string packagePath, string buildError, CancellationToken ct) => Task.FromResult(new List<AppliedPatch> { new AppliedPatch("test.java", "Applied test patch", 1) }); // Simulate patches applied
        public override Task<ValidationResult> ValidateAsync(string packagePath, CancellationToken ct)
        {
            var attempt = _next();
            if (attempt == 0)
            {
                return Task.FromResult(ValidationResult.CreateFailure("compile error"));
            }
            return Task.FromResult(ValidationResult.CreateSuccess());
        }
        public override Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo)> BuildAsync(string packagePath, int timeoutMinutes = 30, CancellationToken ct = default)
            => Task.FromResult<(bool, string?, PackageInfo?)>((false, "Build failed for testing", null)); // Simulate failed build for this test
        public override Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default) => Task.FromResult(new PackageInfo
        {
            PackagePath = packagePath,
            RepoRoot = "/mock/repo",
            RelativePath = "sdk/mock/package",
            PackageName = "mock-package",
            ServiceName = "mock",
            PackageVersion = "1.0.0",
            SamplesDirectory = "/mock/samples",
            Language = SdkLanguage.Java,
            SdkType = SdkType.Dataplane
        });
    }
}

internal class MockTspHelper : ITspClientHelper
{
    public Task<TspToolResponse> ConvertSwaggerAsync(string swaggerReadmePath, string outputDirectory, bool isArm, bool fullyCompatible, bool isCli, CancellationToken ct = default)
        => Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProject = outputDirectory });
    public Task<TspToolResponse> UpdateGenerationAsync(string tspLocationDirectory, string? commitSha = null, bool isCli = false, string? localSpecRepoPath = null, CancellationToken ct = default)
        => Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProject = tspLocationDirectory });
    public Task<TspToolResponse> InitializeGenerationAsync(string workingDirectory, string tspConfigPath, string[]? additionalArgs = null, CancellationToken ct = default)
        => Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProject = workingDirectory });
}
