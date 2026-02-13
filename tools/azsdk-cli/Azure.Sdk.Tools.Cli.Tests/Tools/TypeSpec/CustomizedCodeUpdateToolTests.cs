using Azure.Sdk.Tools.Cli.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec;
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
        public override bool HasCustomizations(string packagePath, CancellationToken ct) => false;
        public override Task<bool> ApplyPatchesAsync(string commitSha, string customizationRoot, string packagePath, CancellationToken ct) => Task.FromResult(false);
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
        public override bool HasCustomizations(string packagePath, CancellationToken ct) => true; // Has customizations
        public override Task<bool> ApplyPatchesAsync(string commitSha, string customizationRoot, string packagePath, CancellationToken ct) => Task.FromResult(true); // Simulate successful patch application
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
        var tspProject = CreateTempTypeSpecProjectDir();

        var result = await tool.UpdateAsync("Do something impossible", tspProject, pkg, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ErrorCode, Is.EqualTo("TypeSpecCustomizationFailed"));
            Assert.That(result.Message, Does.Contain("TypeSpec customization failed"));
        });

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
        var tspProject = CreateTempTypeSpecProjectDir();

        var result = await tool.UpdateAsync("Fix build error", tspProject, pkg, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ErrorCode, Is.Null, "Should complete successfully after code customizations");
            Assert.That(result.Message, Does.Contain("patches applied"), "Should indicate code patches were applied");
            Assert.That(result.TypeSpecChangesSummary, Is.Not.Null.And.Not.Empty, "Should include TypeSpec customization changes");
        });

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
        var tspProject = CreateTempTypeSpecProjectDir();

        var result = await tool.UpdateAsync("Fix something", tspProject, pkg, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ErrorCode, Is.EqualTo("BuildNoCustomizationsFailed"));
            Assert.That(result.TypeSpecChangesSummary, Is.Not.Null.And.Not.Empty, "Should include TypeSpec customization changes even on failure");
            Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty, "Should provide manual guidance");
        });

    }

    [Test]
    public async Task InvalidInput_EmptyCustomizationRequest_ReturnsError()
    {
        var gitHelper = new Mock<IGitHelper>();
        gitHelper.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("/mock/repo/root");
        var svc = new MockNoChangeLanguageService();
        var tsp = new MockTspHelper();
        var custService = CreateMockCustomizationService();
        var typeSpecHelper = CreateMockTypeSpecHelper();

        var tool = new CustomizedCodeUpdateTool(
            new NullLogger<CustomizedCodeUpdateTool>(), [svc], gitHelper.Object, tsp,
            custService.Object, typeSpecHelper.Object);

        var pkg = CreateTempPackageDir();

        var result = await tool.UpdateAsync("", "/some/tsp/path", pkg, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(result.ErrorCode, Is.EqualTo("InvalidInput"));
            Assert.That(result.Message, Does.Contain("Plain text feedback is required"));
        });

    }

    [Test]
    public async Task InvalidInput_BadTypeSpecProjectPath_ReturnsError()
    {
        var gitHelper = new Mock<IGitHelper>();
        gitHelper.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("/mock/repo/root");
        var svc = new MockNoChangeLanguageService();
        var tsp = new MockTspHelper();
        var custService = CreateMockCustomizationService();
        var typeSpecHelper = CreateMockTypeSpecHelper(isValid: false);

        var tool = new CustomizedCodeUpdateTool(
            new NullLogger<CustomizedCodeUpdateTool>(), [svc], gitHelper.Object, tsp,
            custService.Object, typeSpecHelper.Object);

        var pkg = CreateTempPackageDir();

        var result = await tool.UpdateAsync("Do something", "/invalid/path", pkg, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(result.ErrorCode, Is.EqualTo("InvalidInput"));
            Assert.That(result.Message, Does.Contain("Invalid TypeSpec project path"));
        });

    }

    [Test]
    public async Task TspCustomizations_Succeeds_BuildFails_CodeCustomizationsFails_ReturnsError()
    {
        var gitHelper = new Mock<IGitHelper>();
        gitHelper.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-python");
        gitHelper.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("/mock/repo/root");

        var svc = new MockChangeLanguageService(buildSuccess: false);
        var tsp = new MockTspHelper();
        var custService = CreateMockCustomizationService(success: true, changes: ["Applied decorator"]);
        var typeSpecHelper = CreateMockTypeSpecHelper();

        var tool = new CustomizedCodeUpdateTool(
            new NullLogger<CustomizedCodeUpdateTool>(), [svc], gitHelper.Object, tsp,
            custService.Object, typeSpecHelper.Object);

        var pkg = CreateTempPackageDir();
        var tspProject = CreateTempTypeSpecProjectDir();

        var result = await tool.UpdateAsync("Fix something", tspProject, pkg, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ErrorCode, Is.EqualTo("BuildAfterPatchesFailed"));
            Assert.That(result.TypeSpecChangesSummary, Is.Not.Null.And.Not.Empty, "Should include TypeSpec customization changes");
            Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty);
        });

    }

    [Test]
    public async Task RegenerationFails_ReturnsError()
    {
        var gitHelper = new Mock<IGitHelper>();
        gitHelper.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-java");
        gitHelper.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("/mock/repo/root");
        var svc = new MockNoChangeLanguageService();

        var tsp = new Mock<ITspClientHelper>();
        tsp.Setup(t => t.UpdateGenerationAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TspToolResponse { IsSuccessful = false, ResponseError = "tsp-client update failed", TypeSpecProject = "mock" });

        var custService = CreateMockCustomizationService(success: true, changes: ["Applied decorator"]);
        var typeSpecHelper = CreateMockTypeSpecHelper();

        var tool = new CustomizedCodeUpdateTool(
            new NullLogger<CustomizedCodeUpdateTool>(), [svc], gitHelper.Object, tsp.Object,
            custService.Object, typeSpecHelper.Object);

        var pkg = CreateTempPackageDir();
        var tspProject = CreateTempTypeSpecProjectDir();

        var result = await tool.UpdateAsync("Rename something", tspProject, pkg, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ErrorCode, Is.EqualTo("RegenerateFailed"));
            Assert.That(result.TypeSpecChangesSummary, Is.Not.Null.And.Not.Empty, "Should include TypeSpec customization changes even on regen failure");
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
