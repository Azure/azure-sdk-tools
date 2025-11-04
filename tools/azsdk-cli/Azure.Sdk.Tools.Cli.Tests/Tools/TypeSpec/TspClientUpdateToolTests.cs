using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.ClientUpdate;
using Azure.Sdk.Tools.Cli.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.CustomizedCodeUpdateTool;

[TestFixture]
public class TspClientUpdateToolAutoTests
{
    private static string CreateTempPackageDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "azsdk-test-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(path);
        return path;
    }

    // Language service that produces no API changes and no customizations
    private class MockNoChangeLanguageService : IClientUpdateLanguageService
    {
        public SdkLanguage SupportedLanguage => SdkLanguage.Java;
        public Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath) => Task.FromResult(new List<ApiChange>());
        public string? GetCustomizationRoot(string generationRoot, CancellationToken ct) => null; // No customizations found
        public Task<bool> ApplyPatchesAsync(string commitSha, string customizationRoot, string packagePath, CancellationToken ct) => Task.FromResult(false);
        public Task<ValidationResult> ValidateAsync(string packagePath, CancellationToken ct) => Task.FromResult(ValidationResult.CreateSuccess());
    }

    // Language service that has customizations and successful patch application
    private class MockChangeLanguageService : IClientUpdateLanguageService
    {
        public SdkLanguage SupportedLanguage => SdkLanguage.Java;
        public Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath)
            => Task.FromResult(new List<ApiChange> {
                new ApiChange { Kind = "MethodAdded", Symbol = "S1", Detail = "Added method S1" }
            });
        public string? GetCustomizationRoot(string generationRoot, CancellationToken ct) =>
            Path.Combine(generationRoot, "customization"); // Mock customization root exists
        public Task<bool> ApplyPatchesAsync(string commitSha, string customizationRoot, string packagePath, CancellationToken ct)
            => Task.FromResult(true); // Simulate successful patch application
        public Task<ValidationResult> ValidateAsync(string packagePath, CancellationToken ct) => Task.FromResult(ValidationResult.CreateSuccess());
    }


    [Test]
    public async Task Auto_NoCustomizations_CompletesSuccessfully()
    {
        var svc = new MockNoChangeLanguageService();
        var resolver = new SingleResolver(svc);
        var tsp = new MockTspHelper();
        var tool = new TspClientUpdateTool(new NullLogger<TspClientUpdateTool>(), resolver, tsp);
        var pkg = CreateTempPackageDir();
        var run = await tool.UpdateAsync("0123456789abcdef0123456789abcdef01234567", packagePath: pkg, ct: CancellationToken.None);
        Assert.That(run.ErrorCode, Is.Null, "Should complete successfully without errors");
        Assert.That(run.NextSteps, Is.Not.Null.And.Not.Empty, "Should provide next steps guidance");
        Assert.That(string.Join(" ", run.NextSteps), Does.Contain("No customizations found"), "Should indicate no customizations found");
    }

    [Test]
    public async Task Auto_WithCustomizations_AppliedSuccessfully()
    {
        var svc = new MockChangeLanguageService();
        var resolver = new SingleResolver(svc);
        var tsp = new MockTspHelper();
        var tool = new TspClientUpdateTool(new NullLogger<TspClientUpdateTool>(), resolver, tsp);
        var pkg = CreateTempPackageDir();
        // Create a mock customization directory
        Directory.CreateDirectory(Path.Combine(pkg, "customization"));
        var first = await tool.UpdateAsync("89abcdef0123456789abcdef0123456789abcdef", packagePath: pkg, ct: CancellationToken.None);
        Assert.That(first.ErrorCode, Is.Null, "Should complete successfully without errors");
        Assert.That(first.NextSteps, Is.Not.Null.And.Not.Empty, "Should provide guidance for applied patches");
        Assert.That(string.Join(" ", first.NextSteps), Does.Contain("Patches applied automatically"), "Should indicate patches were applied");
    }

    [Test]
    public async Task Validation_Failure_Provides_Guidance()
    {
        var tsp = new MockTspHelper();
        int calls = 0; var svc = new TestLanguageServiceFailThenFix(() => calls++);
        var tool = new TspClientUpdateTool(new NullLogger<TspClientUpdateTool>(), new SingleResolver(svc), tsp);
        var pkg = CreateTempPackageDir();
        // Create a mock customization directory to trigger patch application
        Directory.CreateDirectory(Path.Combine(pkg, "customization"));
        var resp = await tool.UpdateAsync("fedcba9876543210fedcba9876543210fedcba98", packagePath: pkg, ct: CancellationToken.None);
        Assert.That(resp.ErrorCode, Is.Null, "Should complete without throwing errors");
        Assert.That(resp.NextSteps, Is.Not.Null.And.Not.Empty, "Should provide guidance for validation failure");
        Assert.That(string.Join(" ", resp.NextSteps), Does.Contain("validation failed"), "Should indicate validation failure");
    }

    private class TestLanguageServiceFailThenFix : IClientUpdateLanguageService
    {
        private readonly Func<int> _next;
        public TestLanguageServiceFailThenFix(Func<int> next) { _next = next; }
        public SdkLanguage SupportedLanguage => SdkLanguage.Java;
        public Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath) => Task.FromResult(new List<ApiChange>());
        public string? GetCustomizationRoot(string generationRoot, CancellationToken ct) =>
            Path.Combine(generationRoot, "customization"); // Mock customization root exists
        public Task<bool> ApplyPatchesAsync(string commitSha, string customizationRoot, string packagePath, CancellationToken ct) => Task.FromResult(true); // Simulate patches applied
        public Task<ValidationResult> ValidateAsync(string packagePath, CancellationToken ct)
        {
            var attempt = _next();
            if (attempt == 0)
            {
                return Task.FromResult(ValidationResult.CreateFailure("compile error"));
            }
            return Task.FromResult(ValidationResult.CreateSuccess());
        }
    }

    private class SingleResolver : ILanguageSpecificResolver<IClientUpdateLanguageService>
    {
        private readonly IClientUpdateLanguageService _svc;
        public SingleResolver(IClientUpdateLanguageService svc) { _svc = svc; }
        public Task<IClientUpdateLanguageService?> Resolve(string packagePath, CancellationToken ct = default) => Task.FromResult(_svc);
    }
}

internal class MockTspHelper : ITspClientHelper
{
    public Task<TspToolResponse> ConvertSwaggerAsync(string swaggerReadmePath, string outputDirectory, bool isArm, bool fullyCompatible, bool isCli, CancellationToken ct)
        => Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProjectPath = outputDirectory });
    public Task<TspToolResponse> UpdateGenerationAsync(string tspLocationPath, string outputDirectory, string commitSha, bool isCli, CancellationToken ct)
        => Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProjectPath = outputDirectory });
}
