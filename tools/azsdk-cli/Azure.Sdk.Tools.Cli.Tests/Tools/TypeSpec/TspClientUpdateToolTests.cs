using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.ClientUpdate;
using Azure.Sdk.Tools.Cli.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses;

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
    private class NullOutputService : IOutputHelper
    {
        public string Format(object response) => string.Empty;
        public string ValidateAndFormat<T>(string response) => string.Empty;
        public void Output(object output) { }
        public void Output(string output) { }
        public void OutputError(object output) { }
        public void OutputError(string output) { }
        public void OutputConsole(string output) { }
        public void OutputConsoleError(string output) { }
    }

    // Language service that produces no API changes
    private class MockNoChangeLanguageService : IClientUpdateLanguageService
    {
        public string SupportedLanguage => "java";
        public Task<List<ApiChange>> ComputeApiChanges(string rawUnifiedDiff) => Task.FromResult(new List<ApiChange>());
        public Task<string?> GetCustomizationRootAsync(ClientUpdateSessionState session, string generationRoot, CancellationToken ct) => Task.FromResult<string?>(null); // none
        public Task<List<CustomizationImpact>> AnalyzeCustomizationImpactAsync(ClientUpdateSessionState session, string customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct) => Task.FromResult(new List<CustomizationImpact>());
        public Task<List<PatchProposal>> ProposePatchesAsync(ClientUpdateSessionState session, IEnumerable<CustomizationImpact> impacts, CancellationToken ct) => Task.FromResult(new List<PatchProposal>());
        public Task<ValidationResult> ValidateAsync(ClientUpdateSessionState session, CancellationToken ct) => Task.FromResult(ValidationResult.CreateSuccess());
        public Task<List<PatchProposal>> ProposeFixesAsync(ClientUpdateSessionState session, List<string> validationErrors, CancellationToken ct) => Task.FromResult(new List<PatchProposal>());
    }

    // Language service that produces a single API change
    private class MockChangeLanguageService : IClientUpdateLanguageService
    {
        public string SupportedLanguage => "java";
        public Task<List<ApiChange>> ComputeApiChanges(string rawUnifiedDiff)
            => Task.FromResult(new List<ApiChange> {
                new ApiChange { Kind = "MethodAdded", Symbol = "S1", Detail = "Added method S1" }
            });
        public Task<string?> GetCustomizationRootAsync(ClientUpdateSessionState session, string generationRoot, CancellationToken ct) => Task.FromResult<string?>(null); // none
        public Task<List<CustomizationImpact>> AnalyzeCustomizationImpactAsync(ClientUpdateSessionState session, string? customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct)
            => Task.FromResult(new List<CustomizationImpact> { new CustomizationImpact { File = "Customization.java", Reasons = new List<string>{ "API change S1" } } });
        public Task<List<PatchProposal>> ProposePatchesAsync(ClientUpdateSessionState session, IEnumerable<CustomizationImpact> impacts, CancellationToken ct) => Task.FromResult(new List<PatchProposal>());
        public Task<ValidationResult> ValidateAsync(ClientUpdateSessionState session, CancellationToken ct) => Task.FromResult(ValidationResult.CreateSuccess());
        public Task<List<PatchProposal>> ProposeFixesAsync(ClientUpdateSessionState session, List<string> validationErrors, CancellationToken ct) => Task.FromResult(new List<PatchProposal>());
    }


    [Test]
    public async Task Auto_NoChanges_TerminatesAtValidation()
    {
        var svc = new MockNoChangeLanguageService();
        var resolver = new SingleResolver(svc);
        var tsp = new MockTspHelper();
        var tool = new TspClientUpdateTool(new NullLogger<TspClientUpdateTool>(), new NullOutputService(), resolver, tsp);
        var pkg = CreateTempPackageDir();
        var run = await tool.UpdateAsync("0123456789abcdef0123456789abcdef01234567", packagePath: pkg, ct: CancellationToken.None);
        Assert.That(run.Session, Is.Not.Null, "Session should be created");
        Assert.That(run.Session!.LastStage, Is.EqualTo(UpdateStage.Validated), "No changes now proceed through validation");
    // Slim model: no stored API change count; reaching Validated implies no changes or all handled.
    }

    [Test]
    public async Task Auto_WithChanges_Validated()
    {
        var svc = new MockChangeLanguageService();
        var resolver = new SingleResolver(svc);
        var tsp = new MockTspHelper();
        var tool = new TspClientUpdateTool(new NullLogger<TspClientUpdateTool>(), new NullOutputService(), resolver, tsp);
        var pkg = CreateTempPackageDir();
        var first = await tool.UpdateAsync("89abcdef0123456789abcdef0123456789abcdef", packagePath: pkg, ct: CancellationToken.None);
        Assert.That(first.Session, Is.Not.Null);
        Assert.That(first.Session.LastStage, Is.EqualTo(UpdateStage.Validated), "Single-pass should reach validated");
    }

    [Test]
    public async Task Validation_Failure_Then_AutoFixes_Applied()
    {
        var tsp = new MockTspHelper();
        var tool = new TspClientUpdateTool(new NullLogger<TspClientUpdateTool>(), new NullOutputService(), new SingleResolver(new MockNoChangeLanguageService()), tsp);
        int calls = 0; var svc = new TestLanguageServiceFailThenFix(() => calls++);
        tool = new TspClientUpdateTool(new NullLogger<TspClientUpdateTool>(), new NullOutputService(), new SingleResolver(svc), tsp);
        var pkg = CreateTempPackageDir();
        var resp = await tool.UpdateAsync("fedcba9876543210fedcba9876543210fedcba98", packagePath: pkg, ct: CancellationToken.None);
        Assert.That(resp.Session, Is.Not.Null);
        Assert.That(resp.Session!.LastStage, Is.EqualTo(UpdateStage.Validated));
        Assert.That(resp.Session.RequiresManualIntervention, Is.False);
    }

    private class TestLanguageServiceFailThenFix : IClientUpdateLanguageService
    {
        private readonly Func<int> _next;
        public TestLanguageServiceFailThenFix(Func<int> next) { _next = next; }
        public string SupportedLanguage => "java";
        public Task<List<ApiChange>> ComputeApiChanges(string rawUnifiedDiff) => Task.FromResult(new List<ApiChange>());
        public Task<string?> GetCustomizationRootAsync(ClientUpdateSessionState session, string generationRoot, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<List<CustomizationImpact>> AnalyzeCustomizationImpactAsync(ClientUpdateSessionState session, string? customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct) => Task.FromResult(new List<CustomizationImpact>());
        public Task<List<PatchProposal>> ProposePatchesAsync(ClientUpdateSessionState session, IEnumerable<CustomizationImpact> impacts, CancellationToken ct) => Task.FromResult(new List<PatchProposal>());
        public Task<ValidationResult> ValidateAsync(ClientUpdateSessionState session, CancellationToken ct)
        {
            var attempt = _next();
            if (attempt == 0)
            {
                return Task.FromResult(ValidationResult.CreateFailure("compile error"));
            }
            return Task.FromResult(ValidationResult.CreateSuccess());
        }
        public Task<List<PatchProposal>> ProposeFixesAsync(ClientUpdateSessionState session, List<string> validationErrors, CancellationToken ct)
        {
            // propose one trivial fix
            var p = new PatchProposal { File = "src/Example.java", Diff = "--- a/src/Example.java\n+++ b/src/Example.java\n// fix" };
            return Task.FromResult(new List<PatchProposal> { p });
        }
    }

    private class SingleResolver : IClientUpdateLanguageServiceResolver
    {
        private readonly IClientUpdateLanguageService _svc;
        public SingleResolver(IClientUpdateLanguageService svc) { _svc = svc; }
        public Task<IClientUpdateLanguageService?> ResolveAsync(string? packagePath, CancellationToken ct = default) => Task.FromResult<IClientUpdateLanguageService?>(_svc);
    }
}

internal class MockTspHelper : ITspClientHelper
{
    public Task<TspToolResponse> ConvertSwaggerAsync(string swaggerReadmePath, string outputDirectory, bool isArm, bool fullyCompatible, bool isCli, CancellationToken ct)
        => Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProjectPath = outputDirectory });
    public Task<TspToolResponse> UpdateGenerationAsync(string tspLocationPath, string outputDirectory, bool isCli, CancellationToken ct)
        => Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProjectPath = outputDirectory });
}
