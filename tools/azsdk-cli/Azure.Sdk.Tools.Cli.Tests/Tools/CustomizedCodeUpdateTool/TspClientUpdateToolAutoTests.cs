using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.Update;
using Azure.Sdk.Tools.Cli.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.CustomizedCodeUpdateTool;

[TestFixture]
public class TspClientUpdateToolAutoTests
{
    private class NullOutputService : IOutputService
    {
        public string Format(object response) => string.Empty;
        public string ValidateAndFormat<T>(string response) => string.Empty;
        public void Output(object output) { }
        public void Output(string output) { }
        public void OutputError(object output) { }
        public void OutputError(string output) { }
    }

    // Language service that produces no API changes
    private class MockNoChangeLanguageService : IUpdateLanguageService
    {
        public string Language => "java";
        public Task<Dictionary<string, SymbolInfo>> ExtractSymbolsAsync(string rootPath, CancellationToken ct) => Task.FromResult(new Dictionary<string, SymbolInfo>());
        public Task<List<ApiChange>> DiffAsync(Dictionary<string, SymbolInfo> oldSymbols, Dictionary<string, SymbolInfo> newSymbols) => Task.FromResult(new List<ApiChange>());
    public Task<string?> GetCustomizationRootAsync(UpdateSessionState session, string generationRoot, CancellationToken ct) => Task.FromResult<string?>(null); // none
    public Task<List<CustomizationImpact>> AnalyzeCustomizationImpactAsync(UpdateSessionState session, string? customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct) => Task.FromResult(new List<CustomizationImpact>());
    public Task<List<string>> DetectDirectMergeFilesAsync(UpdateSessionState session, string? customizationRoot, CancellationToken ct) => Task.FromResult(new List<string>());
    public Task<List<PatchProposal>> ProposePatchesAsync(UpdateSessionState session, IEnumerable<CustomizationImpact> impacts, IEnumerable<string> directMergeFiles, CancellationToken ct) => Task.FromResult(new List<PatchProposal>());
    public Task<(bool success, List<string> errors)> ValidateAsync(UpdateSessionState session, CancellationToken ct) => Task.FromResult((true, new List<string>()));
    }

    // Language service that produces a single API change
    private class MockChangeLanguageService : IUpdateLanguageService
    {
        public string Language => "java";
        public Task<Dictionary<string, SymbolInfo>> ExtractSymbolsAsync(string rootPath, CancellationToken ct) => Task.FromResult(new Dictionary<string, SymbolInfo>());
        public Task<List<ApiChange>> DiffAsync(Dictionary<string, SymbolInfo> oldSymbols, Dictionary<string, SymbolInfo> newSymbols)
            => Task.FromResult(new List<ApiChange> {
                new ApiChange { Kind = "MethodAdded", Symbol = "S1", Detail = "Added method S1" }
            });
    public Task<string?> GetCustomizationRootAsync(UpdateSessionState session, string generationRoot, CancellationToken ct) => Task.FromResult<string?>(null); // none
    public Task<List<CustomizationImpact>> AnalyzeCustomizationImpactAsync(UpdateSessionState session, string? customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct) => Task.FromResult(new List<CustomizationImpact>());
    public Task<List<string>> DetectDirectMergeFilesAsync(UpdateSessionState session, string? customizationRoot, CancellationToken ct) => Task.FromResult(new List<string>());
    public Task<List<PatchProposal>> ProposePatchesAsync(UpdateSessionState session, IEnumerable<CustomizationImpact> impacts, IEnumerable<string> directMergeFiles, CancellationToken ct) => Task.FromResult(new List<PatchProposal>());
    public Task<(bool success, List<string> errors)> ValidateAsync(UpdateSessionState session, CancellationToken ct) => Task.FromResult((true, new List<string>()));
    }


    [Test]
    public async Task Auto_NoChanges_TerminatesAtDiff()
    {
        var tool = new TspClientUpdateTool(new NullLogger<TspClientUpdateTool>(), new NullOutputService(), new[] { new MockNoChangeLanguageService() });
    var run = await tool.UnifiedUpdate("placeholder.tsp", stage: null, resume: false, finalize: false, ct: CancellationToken.None);
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
        var tool = new TspClientUpdateTool(new NullLogger<TspClientUpdateTool>(), new NullOutputService(), new[] { new MockChangeLanguageService() });
        // First pass (auto run) should reach apply dry-run and request finalize
    var first = await tool.UnifiedUpdate("placeholder-change.tsp", stage: null, resume: false, finalize: false, ct: CancellationToken.None);
        Assert.That(first.Session, Is.Not.Null);
        Assert.That(first.Session!.ApiChangeCount, Is.GreaterThan(0), "Should have at least one API change");
        Assert.That(first.Session.LastStage, Is.EqualTo(UpdateStage.AppliedDryRun), "Should have performed dry-run apply");
        Assert.That(first.NeedsFinalize, Is.True, "Should indicate finalize needed");
        Assert.That(first.NextStage, Is.EqualTo("apply"), "Next stage should be apply to finalize");
    Assert.That(first.Terminal, Is.Null.Or.False, "Not terminal until finalize (validate still pending)");

        // Second pass: finalize only
    var second = await tool.UnifiedUpdate("placeholder-change.tsp", stage: "apply", resume: true, finalize: true, ct: CancellationToken.None);
        Assert.That(second.Session, Is.Not.Null);
        Assert.That(second.Session!.LastStage, Is.EqualTo(UpdateStage.Applied));
        Assert.That(second.NeedsFinalize, Is.Null.Or.False, "Finalize flag should clear");
    Assert.That(second.Terminal, Is.Null.Or.False, "Validate stage now available after apply finalize");
    Assert.That(second.NextStage, Is.EqualTo("validate"), "Should suggest validate after finalize");
    }
}
