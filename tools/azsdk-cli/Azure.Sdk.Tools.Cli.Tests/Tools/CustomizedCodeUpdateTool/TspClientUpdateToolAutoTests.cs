using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.Update;
using Azure.Sdk.Tools.Cli.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.CustomizedCodeUpdateTool;

[TestFixture]
public class TspClientUpdateToolAutoTests
{
    // DummyRepoFactory removed — not used in these tests

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
    private class MockNoChangeLanguageService : IUpdateLanguageService
    {
        public Task<Dictionary<string, SymbolInfo>> ExtractSymbolsAsync(string rootPath, CancellationToken ct) => Task.FromResult(new Dictionary<string, SymbolInfo>());
        public Task<List<ApiChange>> DiffAsync(Dictionary<string, SymbolInfo> oldSymbols, Dictionary<string, SymbolInfo> newSymbols) => Task.FromResult(new List<ApiChange>());
        public Task<string?> GetCustomizationRootAsync(UpdateSessionState session, string generationRoot, CancellationToken ct) => Task.FromResult<string?>(null); // none
        public Task<List<CustomizationImpact>> AnalyzeCustomizationImpactAsync(UpdateSessionState session, string? customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct) => Task.FromResult(new List<CustomizationImpact>());
        public Task<List<PatchProposal>> ProposePatchesAsync(UpdateSessionState session, IEnumerable<CustomizationImpact> impacts, CancellationToken ct) => Task.FromResult(new List<PatchProposal>());
        public Task<ValidationResult> ValidateAsync(UpdateSessionState session, CancellationToken ct) => Task.FromResult(ValidationResult.CreateSuccess());
        public Task<List<PatchProposal>> ProposeFixesAsync(UpdateSessionState session, List<string> validationErrors, CancellationToken ct) => Task.FromResult(new List<PatchProposal>());
    }

    // Language service that produces a single API change
    private class MockChangeLanguageService : IUpdateLanguageService
    {
        public Task<Dictionary<string, SymbolInfo>> ExtractSymbolsAsync(string rootPath, CancellationToken ct) => Task.FromResult(new Dictionary<string, SymbolInfo>());
        public Task<List<ApiChange>> DiffAsync(Dictionary<string, SymbolInfo> oldSymbols, Dictionary<string, SymbolInfo> newSymbols)
            => Task.FromResult(new List<ApiChange> {
                new ApiChange { Kind = "MethodAdded", Symbol = "S1", Detail = "Added method S1" }
            });
        public Task<string?> GetCustomizationRootAsync(UpdateSessionState session, string generationRoot, CancellationToken ct) => Task.FromResult<string?>(null); // none
        public Task<List<CustomizationImpact>> AnalyzeCustomizationImpactAsync(UpdateSessionState session, string? customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct) => Task.FromResult(new List<CustomizationImpact>());
        public Task<List<PatchProposal>> ProposePatchesAsync(UpdateSessionState session, IEnumerable<CustomizationImpact> impacts, CancellationToken ct) => Task.FromResult(new List<PatchProposal>());
        public Task<ValidationResult> ValidateAsync(UpdateSessionState session, CancellationToken ct) => Task.FromResult(ValidationResult.CreateSuccess());
        public Task<List<PatchProposal>> ProposeFixesAsync(UpdateSessionState session, List<string> validationErrors, CancellationToken ct) => Task.FromResult(new List<PatchProposal>());
    }


    [Test]
    public async Task Auto_NoChanges_TerminatesAtDiff()
    {
        Func<string, IUpdateLanguageService> func = (p) => new MockNoChangeLanguageService();
        var tool = new TspClientUpdateTool(new NullLogger<TspClientUpdateTool>(), new NullOutputService(), func);
        var run = await tool.UnifiedUpdateAsync("placeholder.tsp", new MockNoChangeLanguageService(), ct: CancellationToken.None);
        Assert.That(run.Session, Is.Not.Null, "Session should be created");
        Assert.That(run.Session!.LastStage, Is.EqualTo(UpdateStage.Validated), "No changes now proceed through validation");
        Assert.That(run.Session.ApiChangeCount, Is.EqualTo(0));
        Assert.That(run.Terminal, Is.True, "Should be terminal with no changes");
    }

    [Test]
    public async Task Auto_WithChanges_DryRunThenFinalize()
    {
        Func<string, IUpdateLanguageService> func = (p) => new MockChangeLanguageService();
        var tool = new TspClientUpdateTool(new NullLogger<TspClientUpdateTool>(), new NullOutputService(), func);
        // First pass (auto run) should reach apply dry-run and request finalize
        var first = await tool.UnifiedUpdateAsync("placeholder-change.tsp", new MockChangeLanguageService(), ct: CancellationToken.None);
        Assert.That(first.Session, Is.Not.Null);
        Assert.That(first.Session!.ApiChangeCount, Is.GreaterThan(0), "Should have at least one API change");
        Assert.That(first.Session.LastStage, Is.EqualTo(UpdateStage.Validated), "Single-pass should reach validated");
        Assert.That(first.Terminal, Is.True, "Terminal after single-pass run");
    }

    [Test]
    public async Task Validation_Failure_Then_AutoFixes_Applied()
    {
        Func<string, IUpdateLanguageService> func = (p) => new MockNoChangeLanguageService();
        var tool = new TspClientUpdateTool(new NullLogger<TspClientUpdateTool>(), new NullOutputService(), func);

        // create a session and attach to tool's private field
        // With simplified tool, validation loop is exercised inside UnifiedUpdateAsync; simulate failing then fixing by custom language service
        int calls = 0; var svc = new TestLanguageServiceFailThenFix(() => calls++);
        var resp = await tool.UnifiedUpdateAsync("spec.tsp", svc, ct: CancellationToken.None);
        Assert.That(resp.Session, Is.Not.Null);
        Assert.That(resp.Session!.ValidationAttemptCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(resp.Session.ValidationSuccess, Is.True, "Should eventually validate");
    }

    private class TestLanguageServiceFailThenFix : IUpdateLanguageService
    {
        private readonly Func<int> _next;
        public TestLanguageServiceFailThenFix(Func<int> next) { _next = next; }
        public string Language => "java";
        public Task<Dictionary<string, SymbolInfo>> ExtractSymbolsAsync(string rootPath, CancellationToken ct) => Task.FromResult(new Dictionary<string, SymbolInfo>());
        public Task<List<ApiChange>> DiffAsync(Dictionary<string, SymbolInfo> oldSymbols, Dictionary<string, SymbolInfo> newSymbols) => Task.FromResult(new List<ApiChange>());
        public Task<string?> GetCustomizationRootAsync(UpdateSessionState session, string generationRoot, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<List<CustomizationImpact>> AnalyzeCustomizationImpactAsync(UpdateSessionState session, string? customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct) => Task.FromResult(new List<CustomizationImpact>());
        public Task<List<PatchProposal>> ProposePatchesAsync(UpdateSessionState session, IEnumerable<CustomizationImpact> impacts, CancellationToken ct) => Task.FromResult(new List<PatchProposal>());
        public Task<ValidationResult> ValidateAsync(UpdateSessionState session, CancellationToken ct)
        {
            var attempt = _next();
            if (attempt == 0)
            {
                return Task.FromResult(ValidationResult.CreateFailure("compile error"));
            }
            return Task.FromResult(ValidationResult.CreateSuccess());
        }
        public Task<List<PatchProposal>> ProposeFixesAsync(UpdateSessionState session, List<string> validationErrors, CancellationToken ct)
        {
            // propose one trivial fix
            var p = new PatchProposal { File = "src/Example.java", Diff = "--- a/src/Example.java\n+++ b/src/Example.java\n// fix", Rationale = "Auto-fix" };
            return Task.FromResult(new List<PatchProposal> { p });
        }
    }
}
