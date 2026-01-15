using Azure.Sdk.Tools.Cli.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec;
using Azure.Sdk.Tools.Cli.Services.Languages;
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

    // Language service that produces no API changes and no customizations
    private class MockNoChangeLanguageService : LanguageService
    {
        public override SdkLanguage Language { get; } = SdkLanguage.Java;
        public override bool IsCustomizedCodeUpdateSupported => true;
        public SdkLanguage SupportedLanguage => SdkLanguage.Java;
        public override Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath) => Task.FromResult(new List<ApiChange>());
        public override string? GetCustomizationRoot(string generationRoot, CancellationToken ct) => null; // No customizations found
        public override Task<bool> ApplyPatchesAsync(string commitSha, string customizationRoot, string packagePath, CancellationToken ct) => Task.FromResult(false);
        public override Task<ValidationResult> ValidateAsync(string packagePath, CancellationToken ct) => Task.FromResult(ValidationResult.CreateSuccess());
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
    private class MockChangeLanguageService : LanguageService
    {
        public override SdkLanguage Language { get; } = SdkLanguage.Python;
        public override bool IsCustomizedCodeUpdateSupported => true;
        public SdkLanguage SupportedLanguage => SdkLanguage.Python;
        public override Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath)
            => Task.FromResult(new List<ApiChange> {
                new ApiChange { Kind = "MethodAdded", Symbol = "S1", Detail = "Added method S1" }
            });
        public override string? GetCustomizationRoot(string generationRoot, CancellationToken ct) =>
            Path.Combine(generationRoot, "customization"); // Mock customization root exists
        public override Task<bool> ApplyPatchesAsync(string commitSha, string customizationRoot, string packagePath, CancellationToken ct)
            => Task.FromResult(true); // Simulate successful patch application
        public override Task<ValidationResult> ValidateAsync(string packagePath, CancellationToken ct) => Task.FromResult(ValidationResult.CreateSuccess());
        public override Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo)> BuildAsync(string packagePath, int timeoutMinutes = 30, CancellationToken ct = default)
            => Task.FromResult<(bool, string?, PackageInfo?)>((true, null, null)); // Mock successful build
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
    public async Task Auto_NoCustomizations_CompletesSuccessfully()
    {
        var gitHelper = new Mock<IGitHelper>();
        var processHelper = new Mock<IProcessHelper>();
        var logger = new TestLogger<LanguageService>();
        var commonValidationHelper = new Mock<ICommonValidationHelpers>();
        var svc = new MockNoChangeLanguageService();
        var tsp = new MockTspHelper();
        var specGenSdkConfigHelper = new Mock<ISpecGenSdkConfigHelper>();
        gitHelper.Setup(g => g.GetRepoName(It.IsAny<string>())).Returns("azure-sdk-for-java");
        gitHelper.Setup(g => g.DiscoverRepoRoot(It.IsAny<string>())).Returns("/mock/repo/root");
        specGenSdkConfigHelper.Setup(s => s.GetConfigurationAsync(It.IsAny<string>(), It.IsAny<SpecGenSdkConfigType>()))
            .ReturnsAsync((SpecGenSdkConfigContentType.Unknown, string.Empty));
        var tool = new CustomizedCodeUpdateTool(new NullLogger<CustomizedCodeUpdateTool>(), [svc], gitHelper.Object, tsp);
        var pkg = CreateTempPackageDir();
        var run = await tool.UpdateAsync("0123456789abcdef0123456789abcdef01234567", packagePath: pkg, ct: CancellationToken.None);
        Assert.That(run.ErrorCode, Is.Null, "Should complete successfully without errors");
        Assert.That(run.NextSteps, Is.Not.Null.And.Not.Empty, "Should provide next steps guidance");
        Assert.That(string.Join(" ", run.NextSteps), Does.Contain("No customizations found"), "Should indicate no customizations found");
    }

    [Test]
    public async Task Auto_WithCustomizations_AppliedSuccessfully()
    {
        var gitHelper = new Mock<IGitHelper>();
        var svc = new MockChangeLanguageService();
        var tsp = new MockTspHelper();
        var specGenSdkConfigHelper = new Mock<ISpecGenSdkConfigHelper>();
        gitHelper.Setup(g => g.GetRepoName(It.IsAny<string>())).Returns("azure-sdk-for-python");
        gitHelper.Setup(g => g.DiscoverRepoRoot(It.IsAny<string>())).Returns("/mock/repo/root");
        specGenSdkConfigHelper.Setup(s => s.GetConfigurationAsync(It.IsAny<string>(), It.IsAny<SpecGenSdkConfigType>()))
            .ReturnsAsync((SpecGenSdkConfigContentType.Unknown, string.Empty));
        var tool = new CustomizedCodeUpdateTool(new NullLogger<CustomizedCodeUpdateTool>(), [svc], gitHelper.Object, tsp);
        var pkg = CreateTempPackageDir();
        // Create a mock customization directory
        Directory.CreateDirectory(Path.Combine(pkg, "customization"));
        var first = await tool.UpdateAsync("89abcdef0123456789abcdef0123456789abcdef", packagePath: pkg, ct: CancellationToken.None);
        Assert.That(first.ErrorCode, Is.Null, "Should complete successfully without errors");
        Assert.That(first.NextSteps, Is.Not.Null.And.Not.Empty, "Should provide guidance for applied patches");
        Assert.That(string.Join(" ", first.NextSteps), Does.Contain("Patches applied automatically"), "Should indicate patches were applied successfully");
    }

    [Test]
    public async Task Validation_Failure_Provides_Guidance()
    {
        var tsp = new MockTspHelper();
        var gitHelper = new Mock<IGitHelper>();
        var specGenSdkConfigHelper = new Mock<ISpecGenSdkConfigHelper>();
        int calls = 0; var svc = new TestLanguageServiceFailThenFix(() => calls++);
        gitHelper.Setup(g => g.GetRepoName(It.IsAny<string>())).Returns("azure-sdk-for-java");
        gitHelper.Setup(g => g.DiscoverRepoRoot(It.IsAny<string>())).Returns("/mock/repo/root");
        specGenSdkConfigHelper.Setup(s => s.GetConfigurationAsync(It.IsAny<string>(), It.IsAny<SpecGenSdkConfigType>()))
            .ReturnsAsync((SpecGenSdkConfigContentType.Unknown, string.Empty));
        var tool = new CustomizedCodeUpdateTool(new NullLogger<CustomizedCodeUpdateTool>(), [svc], gitHelper.Object, tsp);
        var pkg = CreateTempPackageDir();
        // Create a mock customization directory to trigger patch application
        Directory.CreateDirectory(Path.Combine(pkg, "customization"));
        var resp = await tool.UpdateAsync("fedcba9876543210fedcba9876543210fedcba98", packagePath: pkg, ct: CancellationToken.None);
        Assert.That(resp.ErrorCode, Is.Null, "Should complete without throwing errors");
        Assert.That(resp.NextSteps, Is.Not.Null.And.Not.Empty, "Should provide guidance for validation failure");
        Assert.That(string.Join(" ", resp.NextSteps), Does.Contain("Build failed").Or.Contain("validation failed"), "Should indicate validation/build failure");
    }

    private class TestLanguageServiceFailThenFix: LanguageService
    {
        public override SdkLanguage Language { get; } = SdkLanguage.Java;
        public override bool IsCustomizedCodeUpdateSupported => true;
        public SdkLanguage SupportedLanguage => SdkLanguage.Java;
        private Func<int> _next;
        public TestLanguageServiceFailThenFix(Func<int> next) { _next = next; }
        public override Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath) => Task.FromResult(new List<ApiChange>());
        public override string? GetCustomizationRoot(string generationRoot, CancellationToken ct) =>
            Path.Combine(generationRoot, "customization"); // Mock customization root exists
        public override Task<bool> ApplyPatchesAsync(string commitSha, string customizationRoot, string packagePath, CancellationToken ct) => Task.FromResult(true); // Simulate patches applied
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
    public Task<TspToolResponse> ConvertSwaggerAsync(string swaggerReadmePath, string outputDirectory, bool isArm, bool fullyCompatible, bool isCli, CancellationToken ct)
        => Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProject = outputDirectory });
    public Task<TspToolResponse> UpdateGenerationAsync(string tspLocationPath, string outputDirectory, string? commitSha = null, bool isCli = false, CancellationToken ct = default)
        => Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProject = outputDirectory });
    public Task<TspToolResponse> InitializeGenerationAsync(string workingDirectory, string tspConfigPath, string[]? additionalArgs = null, CancellationToken ct = default)
        => Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProject = workingDirectory });
}
