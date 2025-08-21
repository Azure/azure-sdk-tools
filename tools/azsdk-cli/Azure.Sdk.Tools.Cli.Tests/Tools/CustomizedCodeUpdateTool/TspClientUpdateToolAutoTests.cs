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
        var run = await tool.UnifiedUpdate("placeholder.tsp", new MockNoChangeLanguageService(), TspStageSelection.All, resume: false, finalize: false, ct: CancellationToken.None);
        Assert.That(run.Session, Is.Not.Null, "Session should be created");
        Assert.That(run.Session!.LastStage, Is.EqualTo(UpdateStage.Diffed), "No changes should stop after diff");
        Assert.That(run.Session.ApiChangeCount, Is.EqualTo(0));
        Assert.That(run.Terminal, Is.True, "Should be terminal with no changes");
        Assert.That(run.NextStage, Is.Null, "No next stage expected");
        Assert.That(run.NeedsFinalize, Is.Null.Or.False, "Finalize not required when no apply ran");
    }

    [Test]
    public async Task Auto_WithChanges_DryRunThenFinalize()
    {
        Func<string, IUpdateLanguageService> func = (p) => new MockChangeLanguageService();
        var tool = new TspClientUpdateTool(new NullLogger<TspClientUpdateTool>(), new NullOutputService(), func);
        // First pass (auto run) should reach apply dry-run and request finalize
        var first = await tool.UnifiedUpdate("placeholder-change.tsp", new MockChangeLanguageService(), TspStageSelection.All, resume: false, finalize: false, ct: CancellationToken.None);
        Assert.That(first.Session, Is.Not.Null);
        Assert.That(first.Session!.ApiChangeCount, Is.GreaterThan(0), "Should have at least one API change");
        Assert.That(first.Session.LastStage, Is.EqualTo(UpdateStage.AppliedDryRun), "Should have performed dry-run apply");
        Assert.That(first.NeedsFinalize, Is.True, "Should indicate finalize needed");
        Assert.That(first.NextStage, Is.EqualTo("apply"), "Next stage should be apply to finalize");
        Assert.That(first.Terminal, Is.Null.Or.False, "Not terminal until finalize (validate still pending)");

        // Second pass: finalize only
        var second = await tool.UnifiedUpdate("placeholder-change.tsp", new MockChangeLanguageService(), TspStageSelection.Apply, resume: true, finalize: true, ct: CancellationToken.None);
        Assert.That(second.Session, Is.Not.Null);
        Assert.That(second.Session!.LastStage, Is.EqualTo(UpdateStage.Validated), "Finalize triggers validate automatically in Apply stage");
        Assert.That(second.NeedsFinalize, Is.Null.Or.False, "Finalize flag should clear");
        Assert.That(second.Terminal, Is.True, "Should be terminal after validation");
        Assert.That(second.NextStage, Is.Null, "No next stage after validation");
    }

    [Test]
    public async Task Validation_Failure_Then_AutoFixes_Applied()
    {
        Func<string, IUpdateLanguageService> func = (p) => new MockNoChangeLanguageService();
        var tool = new TspClientUpdateTool(new NullLogger<TspClientUpdateTool>(), new NullOutputService(), func);

        // create a session and attach to tool's private field
        var session = new UpdateSessionState { SpecPath = "spec.tsp", LastStage = UpdateStage.Applied, Status = "Applied" };
        var field = typeof(TspClientUpdateTool).GetField("_currentSession", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(tool, session);

        // language service that fails validation first then reports success after fixes
        int calls = 0;
        var svc = new TestLanguageServiceFailThenFix(() => calls++);

        // invoke ValidateCore via reflection
        var method = typeof(TspClientUpdateTool).GetMethod("ValidateCore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var task = (Task<TspClientUpdateResponse>)method.Invoke(tool, new object?[] { session.SessionId, svc, CancellationToken.None })!;
        var resp = await task;

        Assert.IsNotNull(resp.Session);
        Assert.That(resp.Session!.ValidationAttemptCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(resp.Session.ProposedPatches.Count, Is.GreaterThan(0));
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
