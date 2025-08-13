using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.Update;
using Azure.Sdk.Tools.Cli.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Azure.Sdk.Tools.Cli.Services; // for IOutputService

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

    private class MockLanguageService : IUpdateLanguageService
    {
        public string Language => "java";
        public Task RegenerateAsync(UpdateSessionState session, string specPath, string? newGeneratedPath, CancellationToken ct)
        {
            session.NewGeneratedPath = newGeneratedPath ?? Path.Combine(Path.GetTempPath(), $"mock-gen-{Guid.NewGuid():N}");
            Directory.CreateDirectory(session.NewGeneratedPath);
            return Task.CompletedTask;
        }
        public Task<Dictionary<string, SymbolInfo>> ExtractSymbolsAsync(string rootPath, CancellationToken ct) => Task.FromResult(new Dictionary<string, SymbolInfo>());
        public Task<List<ApiChange>> DiffAsync(Dictionary<string, SymbolInfo> oldSymbols, Dictionary<string, SymbolInfo> newSymbols) => Task.FromResult(new List<ApiChange>());
        public Task<List<CustomizationImpact>> AnalyzeCustomizationImpactAsync(UpdateSessionState session, string customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct) => Task.FromResult(new List<CustomizationImpact>());
    }

    [Test]
    public async Task AutoChaining_NoApiChanges_StopsAfterDiff()
    {
        var tool = new TspClientUpdateTool(new NullLogger<TspClientUpdateTool>(), new NullOutputService(), new[] { new MockLanguageService() });
        var regenerate = await tool.Regenerate(specPath: "placeholder.tsp", sessionId: null, newGeneratedPath: null, simulateChange: false, ct: CancellationToken.None);
        Assert.That(regenerate.NextTool, Is.EqualTo("azsdk_tsp_update_diff"));
        var sid = regenerate.Session!.SessionId;
        var diff = await tool.Diff(sid, null, null, CancellationToken.None);
        // With no old generation baseline, apiChangeCount likely 0 => no next tool
        Assert.That(diff.Session!.ApiChangeCount, Is.EqualTo(0));
        Assert.That(diff.NextTool, Is.Null);
    }

    [Test]
    public async Task AutoMode_LoopTerminatesGracefully()
    {
        var tool = new TspClientUpdateTool(new NullLogger<TspClientUpdateTool>(), new NullOutputService(), new[] { new MockLanguageService() });
        var regen = await tool.Regenerate("placeholder2.tsp", null, null, false, CancellationToken.None);
        var current = regen;
        var sessionId = regen.Session!.SessionId;
        int safety = 10; // prevent infinite loop
        while (!string.IsNullOrEmpty(current.NextTool) && safety-- > 0)
        {
            TspClientUpdateResponse resp;
            switch (current.NextTool)
            {
                case "azsdk_tsp_update_diff":
                    resp = await tool.Diff(sessionId);
                    break;
                case "azsdk_tsp_update_map":
                    resp = await tool.Map(sessionId);
                    break;
                case "azsdk_tsp_update_merge":
                    resp = await tool.Merge(sessionId);
                    break;
                case "azsdk_tsp_update_propose":
                    resp = await tool.Propose(sessionId);
                    break;
                case "azsdk_tsp_update_apply":
                    resp = await tool.Apply(sessionId, dryRun: current.Session!.LastStage == UpdateStage.PatchesProposed);
                    break;
                default:
                    Assert.Fail($"Unexpected nextTool {current.NextTool}");
                    return;
            }
            current = resp;
        }
        Assert.That(safety, Is.GreaterThan(0), "Loop did not terminate as expected");
        Assert.That(current.NextTool, Is.Null, "Should end with no further nextTool since no API changes");
    }
}
