// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Services.Repair;
using Azure.Sdk.Tools.Cli.Services.TypeSpec;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.TypeSpec;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class CustomizedCodeRepairTests
{
    private TempDirectory _directory = null!;
    private string _repo = "";
    private string _package = "";
    private string _custom = "";
    private string _generated = "";
    private string _artifactPath = "";
    private RepairLanguage _language = null!;
    private MemorySourceState _source = null!;
    private MemoryArtifacts _artifacts = null!;
    private Mock<IFeedbackClassifierService> _classifier = null!;
    private Mock<ITspClientHelper> _tsp = null!;
    private Mock<IGitHelper> _git = null!;
    private Mock<ITypeSpecHelper> _typeSpec = null!;
    private string _classification = "CODE_CUSTOMIZATION";
    private readonly List<string> _events = [];
    private TimeProvider _clock = TimeProvider.System;

    [SetUp]
    public void SetUp()
    {
        _directory = TempDirectory.Create("customized-repair");
        _repo = Path.Combine(_directory.DirectoryPath, "repo");
        _package = Path.Combine(_repo, "sdk", "service", "package");
        _custom = Path.Combine(_package, "src", "Widget.cs");
        _generated = Path.Combine(_package, "src", "Generated", "Widget.cs");
        _artifactPath = Path.Combine(_directory.DirectoryPath, "evidence");
        Directory.CreateDirectory(Path.GetDirectoryName(_generated)!);
        File.WriteAllText(_custom, "public partial class Widget { public string Name; }");
        File.WriteAllText(_generated, "public partial class Widget { }");
        File.WriteAllText(Path.Combine(_package, "tsp-location.yaml"), "repo: specs\ncommit: pinned\ndirectory: service");
        _events.Clear();
        _clock = TimeProvider.System;
        _classification = "SUCCESS";
        _language = new RepairLanguage(_events, Path.Combine(_package, "src"));
        _source = new MemorySourceState();
        _artifacts = new MemoryArtifacts(_artifactPath);
        _classifier = new();
        _classifier.Setup(c => c.ClassifyItemsAsync(It.IsAny<List<FeedbackItem>>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<EditScope>(), It.IsAny<CancellationToken>()))
            .Returns((List<FeedbackItem> items, string context, string path, string? apiView, string? text,
                string? language, string? service, int? batch, EditScope scope, CancellationToken ct) =>
            {
                _events.Add("classify");
                var item = new FeedbackItem { Text = text! };
                items.Add(item);
                return Task.FromResult(new FeedbackClassificationResponse
                {
                    Classifications = [new()
                    {
                        ItemId = item.Id, Text = item.Text, Classification = _classification, Reason = "test diagnosis"
                    }]
                });
            });
        _tsp = new();
        _tsp.Setup(t => t.UpdateGenerationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                _events.Add("generate");
                return Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProject = _package });
            });
        _git = new();
        _git.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(_repo);
        _typeSpec = new();
        _typeSpec.Setup(t => t.IsValidTypeSpecProjectPath(It.IsAny<string>())).Returns(true);
    }

    [TearDown]
    public void TearDown() => _directory.Dispose();

    private Task<CustomizedCodeUpdateResponse> RunAsync(int maxAttempts = 1, int timeoutMinutes = 30,
        EditScope scope = EditScope.CustomCode, string? localSpec = null, CancellationToken ct = default)
    {
        var service = new CustomizedCodeRepairService(_git.Object, _typeSpec.Object, _tsp.Object,
            _classifier.Object, _source, _artifacts, _clock, NullLogger<CustomizedCodeRepairService>.Instance);
        return service.RunAsync(new(_package, localSpec, "Fix the current build", scope,
            maxAttempts, timeoutMinutes, _artifactPath), (_, _) => Task.FromResult<LanguageService>(_language), ct);
    }

    private void ConfigureLanguage(SdkLanguage language)
    {
        var relativeCustom = language switch
        {
            SdkLanguage.Java => Path.Combine("customization", "src", "main", "java", "Widget.java"),
            SdkLanguage.JavaScript => Path.Combine("src", "widget.ts"),
            SdkLanguage.Python => Path.Combine("package", "_patch.py"),
            _ => Path.Combine("src", "Widget.cs")
        };
        _custom = Path.Combine(_package, relativeCustom);
        Directory.CreateDirectory(Path.GetDirectoryName(_custom)!);
        File.WriteAllText(_custom, language == SdkLanguage.Python
            ? "__all__ = [\"Widget\"]\nclass Widget: pass"
            : "public class Widget { }");
        _language = new RepairLanguage(_events, Path.GetDirectoryName(_custom)!) { CurrentLanguage = language };
    }

    [Test]
    public async Task BaselineGreen_ValidatesInOrderWithoutAnAgent()
    {
        var result = await RunAsync();
        Assert.Multiple(() =>
        {
            Assert.That(_events, Is.EqualTo(new[] { "prepare", "generate", "build", "classify" }));
            Assert.That(result.Success, Is.True);
            Assert.That(result.Repair!.TerminalReason, Is.EqualTo("already_green"));
            Assert.That(result.Repair.AttemptsUsed, Is.Zero);
            Assert.That(result.Repair.Input!.InitialTree, Is.EqualTo(result.Repair.FinalState!.Tree));
            Assert.That(_language.Sessions, Is.Zero);
        });
        AssertBoundSuccess(result);
    }

    [Test]
    public async Task BaselineGenerationChange_IsRepairWithZeroPatchAttempts()
    {
        _tsp.Setup(t => t.UpdateGenerationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(() =>
        {
            File.AppendAllText(_generated, "\n// updated");
            return Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProject = _package });
        });
        var result = await RunAsync();
        Assert.Multiple(() =>
        {
            Assert.That(result.Repair!.TerminalReason, Is.EqualTo("repaired"));
            Assert.That(result.Repair.RepairKind, Is.EqualTo("generation_only"));
            Assert.That(result.Repair.AttemptsUsed, Is.Zero);
            Assert.That(result.Repair.Input!.InitialTree, Is.Not.EqualTo(result.Repair.FinalState!.Tree));
            Assert.That(result.Repair.FinalState.ChangedFiles.Single().Path, Does.EndWith("src/Generated/Widget.cs"));
        });
        AssertBoundSuccess(result);
    }

    [Test]
    public async Task GreenBuild_DoesNotSkipRequestedCustomization()
    {
        _classification = "CODE_CUSTOMIZATION";
        _language.Patch = _ => File.WriteAllText(_custom, File.ReadAllText(_custom).Replace("Name;", "CurrentName;"));
        var result = await RunAsync();
        AssertBoundSuccess(result);
        Assert.That(result.Repair!.TerminalReason, Is.EqualTo("repaired"));
        Assert.That(result.Repair.AttemptsUsed, Is.EqualTo(1));
        Assert.That(File.ReadAllText(_custom), Does.Contain("CurrentName;"));
    }

    [Test]
    public async Task Retry_UsesOneSessionAndRetainsDiffsAndDiagnostics()
    {
        var builds = 0;
        _language.Build = _ => Task.FromResult((++builds >= 3, $"CS100{builds}: failing symbol"));
        _language.Patch = turn => File.AppendAllText(_custom, $"\n// candidate {turn}");
        var result = await RunAsync(maxAttempts: 3);
        Assert.Multiple(() =>
        {
            Assert.That(_language.Sessions, Is.EqualTo(1));
            Assert.That(result.Repair!.AttemptsUsed, Is.EqualTo(2));
            Assert.That(_language.Prompts.Single(), Does.Contain("CS1002"));
            Assert.That(_language.Prompts.Single(), Does.Contain("candidate 1"));
            Assert.That(_language.Prompts.Single(), Does.Contain("hypothesis 1"));
            Assert.That(_events, Is.EqualTo(new[] { "prepare", "generate", "build", "classify", "patch",
                "prepare", "generate", "build", "patch", "prepare", "generate", "build" }));
            Assert.That(_artifacts.Content["attempt-1/patch.diff"], Does.Contain("candidate 1"));
            Assert.That(result.Repair.Attempts.All(a => a.StageIds.Count == 4), Is.True);
        });
        AssertBoundSuccess(result);
    }

    [TestCase(SdkLanguage.Java, 2)]
    [TestCase(SdkLanguage.DotNet, 2)]
    [TestCase(SdkLanguage.JavaScript, 0)]
    [TestCase(SdkLanguage.Python, 0)]
    public async Task LanguageValidation_PreservesRequiredPostPatchRegeneration(SdkLanguage language, int regenerations)
    {
        ConfigureLanguage(language);
        var builds = 0;
        _language.Build = _ => Task.FromResult((++builds > 1, "compiler diagnostic"));
        _language.Patch = _ => File.AppendAllText(_custom, "\n// candidate");
        var result = await RunAsync();
        AssertBoundSuccess(result);
        Assert.That(_events.Count(e => e == "generate"), Is.EqualTo(regenerations));
        Assert.That(_events.Last(), Is.EqualTo("build"));
        Assert.That(result.Repair!.AttemptsUsed, Is.EqualTo(1));
    }

    [Test]
    public async Task ClassifierSuccess_DoesNotReplaceActualFailedBuild()
    {
        _classification = "SUCCESS";
        _language.Build = _ => Task.FromResult((false, "CS1000"));
        var result = await RunAsync();
        AssertFailure(result, "no_progress");
        Assert.That(_events, Does.Contain("build"));
        Assert.That(_language.Sessions, Is.EqualTo(1));
    }

    [Test]
    public async Task AgentClaimsSuccessWithoutChangingFiles_FailsNoProgress()
    {
        _language.Build = _ => Task.FromResult((false, "CS1000"));
        _language.Summary = "Everything is fixed and the build passed.";
        var result = await RunAsync(maxAttempts: 3);
        AssertFailure(result, "no_progress");
        Assert.That(result.Repair!.AttemptsUsed, Is.EqualTo(1));
    }

    [Test]
    public async Task Exhaustion_ConsumesExactlyConfiguredAttempts()
    {
        _language.Build = _ => Task.FromResult((false, "CS1000"));
        _language.Patch = turn => File.AppendAllText(_custom, $"\n// attempt {turn}");
        var result = await RunAsync(maxAttempts: 2);
        AssertFailure(result, "attempt_limit");
        Assert.That(result.Repair!.AttemptsUsed, Is.EqualTo(2));
        Assert.That(_events.Count(e => e == "build"), Is.EqualTo(3));
    }

    [Test]
    public async Task AccessibilityReversalToFailedState_Stops()
    {
        var initial = File.ReadAllText(_custom);
        _language.Build = _ => Task.FromResult((false, "CS0262: conflicting access"));
        _language.Patch = turn => File.WriteAllText(_custom,
            turn == 1 ? initial.Replace("public partial", "internal partial") : initial);
        var result = await RunAsync(maxAttempts: 3);
        AssertFailure(result, "repeated_state");
        Assert.That(result.Repair!.AttemptsUsed, Is.EqualTo(2));
        Assert.That(result.Repair.Attempts[1].ReversedPatchCount, Is.GreaterThan(0));
    }

    [Test]
    public async Task RevertingBadEditInsideNewCandidate_IsPermitted()
    {
        var initial = File.ReadAllText(_custom);
        var builds = 0;
        _language.Build = _ => Task.FromResult((++builds == 3, "CS0262"));
        _language.Patch = turn => File.WriteAllText(_custom, turn == 1
            ? initial.Replace("public partial", "internal partial")
            : initial.Replace("Name;", "CurrentName;"));
        var result = await RunAsync(maxAttempts: 3);
        AssertBoundSuccess(result);
        Assert.That(result.Repair!.AttemptsUsed, Is.EqualTo(2));
    }

    [TestCase("prepare", "preparation_failed")]
    [TestCase("generate", "generation_failed")]
    public async Task RequiredStageFailure_NeverBuildsStaleCode(string stage, string reason)
    {
        if (stage == "prepare") { _language.Prepare = _ => Task.FromResult(new GenerationPreparationResult(true, false, "plugin missing")); }
        else
        {
            _tsp.Setup(t => t.UpdateGenerationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TspToolResponse { IsSuccessful = false, ResponseError = "generation failed", TypeSpecProject = _package });
        }
        var result = await RunAsync();
        AssertFailure(result, reason);
        Assert.That(_events, Does.Not.Contain("build"));
        Assert.That(_language.Sessions, Is.Zero);
    }

    [Test]
    public async Task PostPatchGenerationFailure_CannotClaimPreviousOrStaleBuildSuccess()
    {
        var calls = 0;
        _tsp.Setup(t => t.UpdateGenerationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(() =>
            Task.FromResult(new TspToolResponse { IsSuccessful = ++calls == 1, ResponseError = calls == 1 ? null : "regen failed", TypeSpecProject = _package }));
        _language.Build = _ => Task.FromResult((false, "CS1000"));
        _language.Patch = _ => File.AppendAllText(_custom, "\n// fix");
        var result = await RunAsync();
        AssertFailure(result, "generation_failed");
        Assert.That(_events.Count(e => e == "build"), Is.EqualTo(1));
    }

    [TestCase("generated")]
    [TestCase("metadata")]
    [TestCase("pin")]
    [TestCase("outside")]
    [TestCase("delete")]
    [TestCase("untracked")]
    public async Task ForbiddenPatchDelta_FailsClosedWithActualDiff(string change)
    {
        var outside = Path.Combine(_repo, "shared.cs");
        File.WriteAllText(outside, "shared source");
        _language.Build = _ => Task.FromResult((false, "CS1000"));
        _language.Patch = _ =>
        {
            switch (change)
            {
                case "generated": File.AppendAllText(_generated, "\n// forbidden"); break;
                case "metadata": File.WriteAllText(Path.Combine(_package, "src", "Package.csproj"), "<Project />"); break;
                case "pin": File.AppendAllText(Path.Combine(_package, "tsp-location.yaml"), "\ncommit: moved"); break;
                case "outside": File.AppendAllText(outside, " changed"); break;
                case "delete": File.Delete(outside); break;
                case "untracked": File.WriteAllText(Path.Combine(_repo, "new.cs"), "new"); break;
            }
        };
        var result = await RunAsync();
        AssertFailure(result, "scope_violation");
        Assert.That(_artifacts.Content.Keys, Does.Contain("stages/attempt-1-patch.diff"));
        Assert.That(_events.Count(e => e == "build"), Is.EqualTo(1));
    }

    [Test]
    public async Task GeneratorChangesCustomCode_FailsScope()
    {
        _tsp.Setup(t => t.UpdateGenerationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(() =>
        {
            File.AppendAllText(_custom, "\n// generator changed customization");
            return Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProject = _package });
        });
        AssertFailure(await RunAsync(), "scope_violation");
        Assert.That(_events, Does.Not.Contain("build"));
    }

    [Test]
    public async Task BuildChangesSource_CannotValidateItsFinalTree()
    {
        _language.Build = _ =>
        {
            File.AppendAllText(_custom, "\n// changed during build");
            return Task.FromResult((true, ""));
        };
        AssertFailure(await RunAsync(), "scope_violation");
    }

    [Test]
    public async Task CancellationDuringPatch_PersistsPartialDiffAndAttempt()
    {
        using var cts = new CancellationTokenSource();
        _language.Build = _ => Task.FromResult((false, "CS1000"));
        _language.Patch = _ =>
        {
            File.AppendAllText(_custom, "\n// partial patch");
            cts.Cancel();
        };
        var result = await RunAsync(ct: cts.Token);
        AssertFailure(result, "cancelled");
        Assert.That(result.Repair!.AttemptsUsed, Is.EqualTo(1));
        Assert.That(result.Repair.Stages.Last().Status, Is.EqualTo("cancelled"));
        Assert.That(_artifacts.Content["partial.diff"], Does.Contain("partial patch"));
    }

    [TestCase("prepare")]
    [TestCase("generate")]
    [TestCase("build")]
    public async Task TotalDeadline_AppliesToEveryDeterministicStage(string stage)
    {
        var clock = new ManualTimerProvider();
        _clock = clock;
        if (stage == "prepare")
        {
            _language.Prepare = _ => { clock.Fire(); return Task.FromResult(new GenerationPreparationResult(true, true, null)); };
        }
        else if (stage == "generate")
        {
            _tsp.Setup(t => t.UpdateGenerationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(() =>
                { clock.Fire(); return Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProject = _package }); });
        }
        else
        {
            _language.Build = _ => { clock.Fire(); return Task.FromResult((true, "")); };
        }
        var result = await RunAsync();
        AssertFailure(result, "timed_out");
        Assert.That(result.Repair!.Stages.Last().Status, Is.EqualTo("timed_out"));
    }

    [TestCase(0, 30)]
    [TestCase(11, 30)]
    [TestCase(1, 0)]
    [TestCase(1, 121)]
    public async Task InvalidLimits_FailBeforeAnyWork(int attempts, int minutes)
    {
        AssertFailure(await RunAsync(attempts, minutes), "invalid_input");
        Assert.That(_events, Is.Empty);
    }

    [TestCase(EditScope.All)]
    [TestCase(EditScope.SpecInputs)]
    public async Task UnsupportedScope_IsExplicit(EditScope scope)
    {
        AssertFailure(await RunAsync(scope: scope), "invalid_input");
        Assert.That(_events, Is.Empty);
    }

    [TestCase("TSP_APPLICABLE", "spec_change_required")]
    [TestCase("REQUIRES_MANUAL_INTERVENTION", "manual_intervention_required")]
    public async Task ClassifierOutOfScope_DoesNotPatch(string classification, string terminal)
    {
        _classification = classification;
        _language.Build = _ => Task.FromResult((false, "CS1000"));
        AssertFailure(await RunAsync(), terminal);
        Assert.That(_language.Sessions, Is.Zero);
    }

    [Test]
    public async Task UnknownClassification_FailsEvenOnGreenBuild()
    {
        _classification = "UNKNOWN";
        AssertFailure(await RunAsync(), "agent_failed");
        Assert.That(_language.Sessions, Is.Zero);
    }

    [Test]
    public async Task AgentFailure_PreservesPartialWorkButNeverSuccess()
    {
        _language.Build = _ => Task.FromResult((false, "CS1000"));
        _language.Patch = _ =>
        {
            File.AppendAllText(_custom, "\n// partial agent work");
            throw new InvalidOperationException("agent connection failed");
        };
        var result = await RunAsync();
        AssertFailure(result, "agent_failed");
        Assert.That(result.Repair!.AttemptsUsed, Is.EqualTo(1));
        Assert.That(_artifacts.Content["partial.diff"], Does.Contain("partial agent work"));
    }

    [Test]
    public async Task DeadlineDuringAgent_PersistsTimedOutAttempt()
    {
        var clock = new ManualTimerProvider();
        _clock = clock;
        _language.Build = _ => Task.FromResult((false, "CS1000"));
        _language.Patch = _ => clock.Fire();
        var result = await RunAsync();
        AssertFailure(result, "timed_out");
        Assert.That(result.Repair!.AttemptsUsed, Is.EqualTo(1));
    }

    [Test]
    public async Task CallerCancellationBeforeStart_NeverStartsStages()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await RunAsync(ct: cts.Token);
        AssertFailure(result, "cancelled");
        Assert.That(result.Repair!.Stages, Is.Empty);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task PerStageTimeout_IsDistinctWithoutCancellingTheWholeInvocation(bool operationCancelledException)
    {
        _language.Build = _ => operationCancelledException
            ? throw new OperationCanceledException("The build subprocess timed out.")
            : throw new TimeoutException("The build subprocess timed out.");
        var result = await RunAsync();
        AssertFailure(result, "timed_out");
        Assert.That(result.Repair!.Stages.Last().Status, Is.EqualTo("timed_out"));
    }

    [Test]
    public async Task PinnedInputs_ArePassedUnchanged()
    {
        AssertBoundSuccess(await RunAsync());
        _tsp.Verify(t => t.UpdateGenerationAsync(_package, null, false, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ExplicitLocalSpec_IsBoundAndPassedToRegeneration()
    {
        var specRoot = Path.Combine(_directory.DirectoryPath, "spec");
        var specProject = Path.Combine(specRoot, "service");
        Directory.CreateDirectory(specProject);
        File.WriteAllText(Path.Combine(specProject, "tspconfig.yaml"), "options: {}");
        _git.Setup(g => g.DiscoverRepoRootAsync(specProject, It.IsAny<CancellationToken>())).ReturnsAsync(specRoot);
        var result = await RunAsync(localSpec: specProject);
        AssertBoundSuccess(result);
        Assert.That(result.Repair!.Input!.LocalSpec!.InitialTree, Is.EqualTo(result.Repair.Input.LocalSpec.FinalTree));
        _tsp.Verify(t => t.UpdateGenerationAsync(_package, null, false, specProject, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task FinalPersistenceFailure_ClearsSuccess()
    {
        _artifacts.FailSuccessfulResult = true;
        var result = await RunAsync();
        AssertFailure(result, "infrastructure_failure");
    }

    [Test]
    public async Task Json_ContainsStableSourceBoundContract()
    {
        var result = await RunAsync();
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result));
        var repair = json.RootElement.GetProperty("repair");
        Assert.Multiple(() =>
        {
            Assert.That(json.RootElement.GetProperty("operation_status").GetString(), Is.EqualTo("Succeeded"));
            Assert.That(json.RootElement.GetProperty("buildValidated").GetBoolean(), Is.True);
            Assert.That(repair.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
            Assert.That(repair.GetProperty("attemptsUsed").GetInt32(), Is.Zero);
            Assert.That(repair.GetProperty("validation").GetProperty("validatedTree").GetString(),
                Is.EqualTo(repair.GetProperty("finalState").GetProperty("tree").GetString()));
            Assert.That(repair.GetProperty("input").GetProperty("tspLocationSha256").GetString(),
                Is.EqualTo(repair.GetProperty("finalState").GetProperty("tspLocationSha256").GetString()));
        });
    }

    [TestCase("custom_code", "repaired", 1)]
    [TestCase("generation_only", "repaired", 0)]
    [TestCase("already_green", "already_green", 0)]
    [TestCase("spec_change_required", "spec_change_required", 0)]
    [TestCase("manual_intervention_required", "manual_intervention_required", 0)]
    public async Task EngineResults_HaveEquivalentActualCliAndMcpContracts(string scenario, string terminal, int attempts)
    {
        if (scenario == "custom_code")
        {
            _classification = "CODE_CUSTOMIZATION";
            _language.Build = _ => Task.FromResult(File.ReadAllText(_custom).Contains("CurrentName")
                ? (true, "") : (false, "CS1000: simulated build diagnostic"));
            _language.Patch = _ => File.WriteAllText(_custom, File.ReadAllText(_custom).Replace("Name;", "CurrentName;"));
            _language.PatchReport = new AppliedPatch(_custom, "Rename Name to CurrentName", 1);
        }
        else if (scenario == "generation_only")
        {
            _tsp.Setup(t => t.UpdateGenerationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(() =>
            {
                File.AppendAllText(_generated, "\n// regenerated");
                return Task.FromResult(new TspToolResponse { IsSuccessful = true, TypeSpecProject = _package });
            });
        }
        else if (scenario.EndsWith("_required", StringComparison.Ordinal))
        {
            _classification = scenario == "spec_change_required" ? "TSP_APPLICABLE" : "REQUIRES_MANUAL_INTERVENTION";
            _language.Build = _ => Task.FromResult((false, "CS1000: simulated build diagnostic"));
        }

        var result = await RunAsync(maxAttempts: 3);
        var cliJson = new OutputHelper(OutputHelper.OutputModes.Json).Format(result);
        var mcpJson = await SerializeActualMcpResponseAsync(result);
        var outputDirectory = TestContext.Parameters["RepairContractOutputDirectory"];
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory, $"{scenario}.cli.json"), cliJson);
            File.WriteAllText(Path.Combine(outputDirectory, $"{scenario}.mcp.json"), mcpJson);
        }
        var cliNode = (JsonObject)RemoveNullProperties(JsonNode.Parse(cliJson))!;
        var mcpNode = JsonNode.Parse(mcpJson)!.AsObject();
        cliNode.Remove("appliedPatches");
        mcpNode.Remove("appliedPatches");
        Assert.That(JsonNode.DeepEquals(cliNode, mcpNode), Is.True,
            "Except for legacy patch records and null omission, CLI/MCP names, types, and values must agree.");
        using var json = JsonDocument.Parse(cliJson);
        using var mcp = JsonDocument.Parse(mcpJson);
        Assert.That(json.RootElement.GetProperty("repair").GetProperty("input").GetProperty("localSpec").ValueKind,
            Is.EqualTo(JsonValueKind.Null));
        Assert.That(mcp.RootElement.GetProperty("repair").GetProperty("input").TryGetProperty("localSpec", out _), Is.False);
        var cliPatches = json.RootElement.GetProperty("appliedPatches");
        var mcpPatches = mcp.RootElement.GetProperty("appliedPatches");
        Assert.That(cliPatches.GetArrayLength(), Is.EqualTo(mcpPatches.GetArrayLength()));
        if (scenario == "custom_code")
        {
            Assert.That(cliPatches.GetArrayLength(), Is.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(cliPatches[0].GetProperty("FilePath").GetString(), Is.EqualTo(mcpPatches[0].GetProperty("filePath").GetString()));
                Assert.That(cliPatches[0].GetProperty("Description").GetString(), Is.EqualTo(mcpPatches[0].GetProperty("description").GetString()));
                Assert.That(cliPatches[0].GetProperty("ReplacementCount").GetInt32(), Is.EqualTo(mcpPatches[0].GetProperty("replacementCount").GetInt32()));
            });
        }
        Assert.That(json.RootElement.GetProperty("repair").GetProperty("terminalReason").GetString(), Is.EqualTo(terminal));
        Assert.That(json.RootElement.GetProperty("repair").GetProperty("attemptsUsed").GetInt32(), Is.EqualTo(attempts));
        if (scenario.EndsWith("_required", StringComparison.Ordinal))
        {
            AssertFailure(result, terminal);
            Assert.That(json.RootElement.GetProperty("response_error").ValueKind, Is.EqualTo(JsonValueKind.String));
            var guidance = json.RootElement.GetProperty(scenario == "spec_change_required" ? "specChangeRequired" : "next_steps");
            Assert.That(guidance.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(guidance.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String), Is.True);
        }
        else
        {
            AssertBoundSuccess(result);
            Assert.That(result.Repair!.RepairKind, Is.EqualTo(scenario == "already_green" ? "none" : scenario));
        }
    }

    private static JsonNode? RemoveNullProperties(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var name in obj.Where(p => p.Value == null).Select(p => p.Key).ToList())
            {
                obj.Remove(name);
            }
            foreach (var value in obj.Select(p => p.Value))
            {
                RemoveNullProperties(value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var value in array)
            {
                RemoveNullProperties(value);
            }
        }
        return node;
    }

    private async Task<string> SerializeActualMcpResponseAsync(CustomizedCodeUpdateResponse response)
    {
        var repair = new Mock<ICustomizedCodeRepairService>();
        repair.Setup(r => r.RunAsync(It.IsAny<CustomizedCodeRepairRequest>(),
            It.IsAny<Func<string, CancellationToken, Task<LanguageService>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        var tool = new CustomizedCodeUpdateTool(NullLogger<CustomizedCodeUpdateTool>.Instance, [], _git.Object,
            _tsp.Object, Mock.Of<IAPIViewFeedbackService>(), _classifier.Object, Mock.Of<ITypeSpecCustomizationService>(),
            _typeSpec.Object, Mock.Of<INpxHelper>(), repair.Object);
        var method = typeof(CustomizedCodeUpdateTool).GetMethod(nameof(CustomizedCodeUpdateTool.UpdateAsync))!;
        var mcp = McpServerTool.Create(method, _ => tool);
        using var services = new ServiceCollection().BuildServiceProvider();
        var server = new Mock<McpServer>();
        server.SetupGet(s => s.Services).Returns(services);
        var context = new RequestContext<CallToolRequestParams>(server.Object, new JsonRpcRequest { Method = "tools/call" })
        {
            Params = new()
            {
                Name = "azsdk_customized_code_update",
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["customizationRequest"] = JsonSerializer.SerializeToElement("Fix the current build"),
                    ["packagePath"] = JsonSerializer.SerializeToElement(_package),
                    ["editScope"] = JsonSerializer.SerializeToElement(EditScope.CustomCode, McpJsonUtilities.DefaultOptions),
                    ["maxAttempts"] = JsonSerializer.SerializeToElement(3)
                }
            }
        };
        var result = await mcp.InvokeAsync(context);
        Assert.That(result.IsError, Is.Not.True);
        return result.Content.OfType<TextContentBlock>().Single().Text;
    }

    private static void AssertBoundSuccess(CustomizedCodeUpdateResponse result)
    {
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, result.ResponseError);
            Assert.That(result.BuildValidated, Is.True);
            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.Repair!.Validation.Succeeded, Is.True);
            Assert.That(result.Repair.Validation.ValidatedTree, Is.EqualTo(result.Repair.FinalState!.Tree));
            Assert.That(result.Repair.Input!.BaseHead, Is.EqualTo(result.Repair.FinalState.Head));
        });
        var build = result.Repair!.Stages.Single(s => s.Id == result.Repair.Validation.BuildStageId);
        Assert.That(build.SourceTreeBefore, Is.EqualTo(build.SourceTreeAfter));
        Assert.That(build.SourceTreeAfter, Is.EqualTo(result.Repair.FinalState!.Tree));
        Assert.That(result.Repair.Validation.RequiredStageIds.All(id =>
            result.Repair.Stages.Single(s => s.Id == id).Status == "succeeded"), Is.True);
    }

    private static void AssertFailure(CustomizedCodeUpdateResponse result, string terminal)
    {
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.BuildValidated, Is.False);
            Assert.That(result.ResponseError, Is.Not.Null.And.Not.Empty);
            Assert.That(result.ExitCode, Is.Not.Zero);
            Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
            Assert.That(result.Repair!.TerminalReason, Is.EqualTo(terminal), result.ResponseError);
            Assert.That(result.Repair.Validation.Succeeded, Is.False);
            Assert.That(result.Repair.Validation.ValidatedTree, Is.Null);
        });
    }

    private sealed class RepairLanguage(List<string> events, string root) : LanguageService(
        Mock.Of<IProcessHelper>(), Mock.Of<IGitHelper>(), NullLogger<LanguageService>.Instance,
        Mock.Of<ICommonValidationHelpers>(), Mock.Of<IPackageInfoHelper>(), Mock.Of<IFileHelper>(),
        Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>())
    {
        public SdkLanguage CurrentLanguage { get; set; } = SdkLanguage.DotNet;
        public override SdkLanguage Language => CurrentLanguage;
        public override bool IsCustomizedCodeUpdateSupported => true;
        public int Sessions { get; private set; }
        public List<string> Prompts { get; } = [];
        public string? Summary { get; set; }
        public AppliedPatch? PatchReport { get; set; }
        public Action<int> Patch { get; set; } = _ => { };
        public Func<CancellationToken, Task<(bool, string)>> Build { get; set; } = _ => Task.FromResult((true, ""));
        public Func<CancellationToken, Task<GenerationPreparationResult>> Prepare { get; set; } =
            _ => Task.FromResult(new GenerationPreparationResult(true, true, null));
        public override string? HasCustomizations(string path, CancellationToken ct = default) => root;
        public override async Task<GenerationPreparationResult> PrepareForGenerationAsync(string path, CancellationToken ct)
        {
            events.Add("prepare");
            return await Prepare(ct);
        }
        public override async Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo)> BuildAsync(
            string path, int timeoutMinutes = 30, CancellationToken ct = default)
        {
            events.Add("build");
            var result = await Build(ct);
            return (result.Item1, result.Item2, null);
        }
        public override async Task RunRepairSessionAsync(string customizationRoot, string packagePath, string buildContext,
            int maxAttempts, Func<CancellationToken, Task> onTurnStarting,
            Func<string?, CancellationToken, Task<CopilotAgentTurnResult<string>>> onTurnCompleted,
            Action<AppliedPatch> onPatchApplied, CancellationToken ct)
        {
            Sessions++;
            for (var number = 1; number <= maxAttempts; number++)
            {
                await onTurnStarting(ct);
                events.Add("patch");
                Patch(number);
                if (PatchReport != null)
                {
                    onPatchApplied(PatchReport);
                }
                ct.ThrowIfCancellationRequested();
                var next = await onTurnCompleted(Summary ?? $"hypothesis {number}", ct);
                if (!next.Continue) { return; }
                Prompts.Add(next.Prompt!);
            }
        }
    }

    private sealed class MemoryArtifacts(string path) : IRepairArtifacts
    {
        public Dictionary<string, string> Content { get; } = [];
        public bool FailSuccessfulResult { get; set; }
        public string CreateDirectory(string? requestedPath, string sessionId, string sdkRepositoryRoot, string? localSpecRepositoryRoot)
        {
            Directory.CreateDirectory(path);
            return path;
        }
        public Task WriteTextAsync(string artifactsPath, string relativePath, string content, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Content[relativePath] = content;
            return Task.CompletedTask;
        }
        public Task WriteJsonAsync<T>(string artifactsPath, string relativePath, T value, CancellationToken ct)
        {
            if (FailSuccessfulResult && value is CustomizedCodeUpdateResponse { Success: true })
            {
                throw new IOException("disk full");
            }
            return WriteTextAsync(artifactsPath, relativePath, JsonSerializer.Serialize(value), ct);
        }
    }

    private sealed class MemorySourceState : IRepairSourceState
    {
        private readonly Dictionary<string, Dictionary<string, string>> _trees = [];
        public Task<RepairSourceSnapshot> CaptureAsync(string repositoryRoot, string artifactsPath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var files = Directory.GetFiles(repositoryRoot, "*", SearchOption.AllDirectories)
                .ToDictionary(p => Path.GetRelativePath(repositoryRoot, p).Replace('\\', '/'), File.ReadAllText);
            var identity = string.Join("\0", files.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => $"{f.Key}\0{f.Value}"));
            var tree = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
            _trees[tree] = files;
            return Task.FromResult(new RepairSourceSnapshot("head", tree));
        }
        public Task<IReadOnlyList<RepairSourceChange>> GetChangesAsync(string repositoryRoot, string beforeTree, string afterTree, CancellationToken ct)
        {
            var before = _trees[beforeTree];
            var after = _trees[afterTree];
            IReadOnlyList<RepairSourceChange> changes = before.Keys.Union(after.Keys).Where(p =>
                before.GetValueOrDefault(p) != after.GetValueOrDefault(p)).Select(p =>
                new RepairSourceChange(p, !before.ContainsKey(p) ? "added" : !after.ContainsKey(p) ? "deleted" : "modified",
                    before.ContainsKey(p) ? "100644" : "000000", after.ContainsKey(p) ? "100644" : "000000")).ToList();
            return Task.FromResult(changes);
        }
        public async Task<string> GetDiffAsync(string repositoryRoot, string beforeTree, string afterTree, CancellationToken ct)
        {
            var diff = new StringBuilder();
            foreach (var change in await GetChangesAsync(repositoryRoot, beforeTree, afterTree, ct))
            {
                diff.AppendLine($"diff --git a/{change.Path} b/{change.Path}").AppendLine("@@ -1 +1 @@");
                if (_trees[beforeTree].TryGetValue(change.Path, out var oldText))
                {
                    foreach (var line in oldText.Split('\n')) { diff.Append('-').AppendLine(line); }
                }
                if (_trees[afterTree].TryGetValue(change.Path, out var newText))
                {
                    foreach (var line in newText.Split('\n')) { diff.Append('+').AppendLine(line); }
                }
            }
            return diff.ToString();
        }
        public Task<string> GetSubtreeAsync(string repositoryRoot, string tree, string relativePath, CancellationToken ct) =>
            Task.FromResult(tree);
    }

    private sealed class ManualTimerProvider : TimeProvider
    {
        private ManualTimer? _timer;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _timer = new(callback, state);
            return _timer;
        }
        public void Fire() => _timer!.Fire();
        private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Fire() => callback(state);
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
