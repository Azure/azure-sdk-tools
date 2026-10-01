// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.TypeSpec;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.TypeSpec;

public partial class CustomizedCodeUpdateToolAutoTests
{
    [TestCase(SdkLanguage.DotNet, "azure-sdk-for-net", true)]
    [TestCase(SdkLanguage.Java, "azure-sdk-for-java", true)]
    [TestCase(SdkLanguage.JavaScript, "azure-sdk-for-js", false)]
    [TestCase(SdkLanguage.Python, "azure-sdk-for-python", false)]
    public async Task BoundedRetries_RetainSessionAndValidateEachProposal(
        SdkLanguage language, string repoName, bool regenerates)
    {
        using var dir = TempDirectory.Create("retry-order");
        var events = new List<string>();
        var builds = 0;
        var edits = 0;
        var svc = new ConfigurableLanguageService(
            language: language, hasCustomizations: true,
            buildFunc: () =>
            {
                events.Add("build");
                return (++builds >= 3, builds >= 3 ? null : $"compiler diagnostic {builds}", null);
            },
            patchesFunc: () =>
            {
                events.Add("patch");
                return [new("Client.cs", $"edit {++edits}", 1)];
            },
            preGenerateFunc: (_, _) => { events.Add("prepare"); return Task.CompletedTask; });
        var tsp = new Mock<ITspClientHelper>();
        tsp.Setup(t => t.UpdateGenerationAsync(dir.DirectoryPath, null, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                events.Add("generate");
                return new TspToolResponse { IsSuccessful = true, TypeSpecProject = dir.DirectoryPath };
            });
        var (tool, _) = CreateTool(svc, tspHelper: tsp.Object,
            configureGit: g => g.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(repoName),
            configureClassifier: CodeCustomizationClassifier("Fix the custom implementation"));

        var result = await tool.UpdateAsync("Fix the custom implementation", dir.DirectoryPath,
            editScope: EditScope.CustomCode, maxAttempts: 3);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, result.ResponseError);
            Assert.That(result.AttemptsUsed, Is.EqualTo(2));
            Assert.That(result.AppliedPatches, Has.Count.EqualTo(2));
            Assert.That(svc.RepairSessions, Is.EqualTo(1));
            Assert.That(svc.ValidationReasons.Single(), Does.Contain("compiler diagnostic 2").And.Contain("existing edits"));
            Assert.That(events, Is.EqualTo(regenerates
                ? new[] { "build", "patch", "prepare", "generate", "build", "patch", "prepare", "generate", "build" }
                : new[] { "build", "patch", "build", "patch", "build" }));
        });
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(10)]
    public async Task BoundedRetries_ExhaustionPreservesFinalDiagnosticsAndPatches(int attempts)
    {
        using var dir = TempDirectory.Create("retry-limit");
        var builds = 0;
        var svc = new ConfigurableLanguageService(
            hasCustomizations: true,
            buildFunc: () => (false, $"compiler failure {++builds}: incompatible override", null),
            patchesFunc: () => [new("Client.java", "Adjust override", 1)]);
        var (tool, _) = CreateTool(svc, configureClassifier: CodeCustomizationClassifier("Fix overrides"));
        var result = await tool.UpdateAsync("Fix overrides", dir.DirectoryPath, editScope: EditScope.CustomCode, maxAttempts: attempts);
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
            Assert.That(result.AttemptsUsed, Is.EqualTo(attempts));
            Assert.That(builds, Is.EqualTo(attempts + 1));
            Assert.That(result.BuildResult, Is.EqualTo($"compiler failure {attempts + 1}: incompatible override"));
            Assert.That(result.ResponseError, Does.Contain(result.BuildResult!).And.Contain($"limit ({attempts})"));
            Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.BuildAfterPatchesFailed));
            Assert.That(result.AppliedPatches, Has.Count.EqualTo(attempts));
            Assert.That(svc.RepairSessions, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task BoundedRetries_NoNewPatchesStopsAndKeepsLastBuildCause()
    {
        using var dir = TempDirectory.Create("retry-no-progress");
        var edits = 0;
        var builds = 0;
        var svc = new ConfigurableLanguageService(
            hasCustomizations: true,
            buildFunc: () => (false, $"CS0122 failure {++builds}", null),
            patchesFunc: () => ++edits == 1 ? [new("Client.java", "Adjust access", 1)] : []);
        var (tool, _) = CreateTool(svc, configureClassifier: CodeCustomizationClassifier("Fix access"));
        var result = await tool.UpdateAsync("Fix access", dir.DirectoryPath, editScope: EditScope.CustomCode, maxAttempts: 3);
        Assert.That(result.AttemptsUsed, Is.EqualTo(2));
        Assert.That(builds, Is.EqualTo(2));
        Assert.That(result.BuildResult, Is.EqualTo("CS0122 failure 2"));
        Assert.That(result.ResponseError, Does.Contain("No additional patches").And.Contain("CS0122 failure 2"));
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.PatchesFailed));
    }

    [Test]
    public async Task BoundedRetries_MissingExitStillValidatesReturnedPartialPatches()
    {
        using var dir = TempDirectory.Create("retry-missing-exit");
        var builds = 0;
        var svc = new ConfigurableLanguageService(
            hasCustomizations: true, buildFunc: () => (++builds > 1, "initial error", null),
            patchesFunc: () => [new("Client.java", "Fix", 1)])
        { SkipAgentValidation = true };
        var (tool, _) = CreateTool(svc, configureClassifier: CodeCustomizationClassifier("Fix"));
        var result = await tool.UpdateAsync("Fix", dir.DirectoryPath, editScope: EditScope.CustomCode);
        Assert.That(result.Success, Is.True);
        Assert.That(result.AttemptsUsed, Is.EqualTo(1));
        Assert.That(builds, Is.EqualTo(2));
    }

    [Test]
    public async Task BoundedRetries_AgentReturnsNoProposalDoesNotCountAnAttempt()
    {
        using var dir = TempDirectory.Create("retry-no-proposal");
        var builds = 0;
        var svc = new ConfigurableLanguageService(
            hasCustomizations: true,
            buildFunc: () => { builds++; return (false, "initial compiler diagnostic", null); },
            patchesFunc: () => [])
        { SkipAgentValidation = true };
        var (tool, _) = CreateTool(svc, configureClassifier: CodeCustomizationClassifier("Fix"));
        var result = await tool.UpdateAsync("Fix", dir.DirectoryPath, editScope: EditScope.CustomCode, maxAttempts: 3);
        Assert.That(result.Success, Is.False);
        Assert.That(result.AttemptsUsed, Is.Zero);
        Assert.That(builds, Is.EqualTo(1));
        Assert.That(result.ErrorCode, Is.EqualTo(CustomizedCodeUpdateResponse.KnownErrorCodes.PatchesFailed));
        Assert.That(result.ResponseError, Does.Contain("no patch proposal").And.Contain("initial compiler diagnostic"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task BoundedRetries_NoOpClassifierCannotSkipAFailingBuild(bool buildPasses)
    {
        using var dir = TempDirectory.Create("retry-classifier-noop");
        var builds = 0;
        var svc = new ConfigurableLanguageService(buildFunc: () => { builds++; return (buildPasses, "actual compiler failure", null); });
        var (tool, _) = CreateTool(svc, configureClassifier: SingleClassification("SUCCESS"));
        var result = await tool.UpdateAsync("Check the build", dir.DirectoryPath, editScope: EditScope.CustomCode);
        Assert.That(result.Success, Is.EqualTo(buildPasses));
        Assert.That(builds, Is.EqualTo(1));
        Assert.That(result.AttemptsUsed, Is.Zero);
        if (!buildPasses)
        {
            Assert.That(result.BuildResult, Is.EqualTo("actual compiler failure"));
            Assert.That(result.ResponseError, Does.Contain("actual compiler failure"));
            Assert.That(result.ExitCode, Is.Not.Zero);
        }
    }

    [TestCase(null)]
    [TestCase(1)]
    [TestCase(3)]
    public async Task BoundedRetries_GreenBuildStillAppliesSemanticRequest(int? maxAttempts)
    {
        using var dir = TempDirectory.Create("retry-semantic");
        var builds = 0;
        var svc = new ConfigurableLanguageService(hasCustomizations: true,
            buildFunc: () => { builds++; return (true, null, null); },
            patchesFunc: () => [new("Client.java", "Rename the API", 1)]);
        var (tool, _) = CreateTool(svc, configureClassifier: CodeCustomizationClassifier("Rename the API"));
        var result = maxAttempts.HasValue
            ? await tool.UpdateAsync("Rename the API", dir.DirectoryPath, editScope: EditScope.CustomCode, maxAttempts: maxAttempts.Value)
            : await tool.UpdateAsync("Rename the API", dir.DirectoryPath, editScope: EditScope.CustomCode);
        Assert.That(result.Success, Is.True);
        Assert.That(result.AttemptsUsed, Is.EqualTo(1));
        Assert.That(svc.RepairSessions, Is.EqualTo(1));
        Assert.That(builds, Is.EqualTo(2));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task BoundedRetries_PreparationOrGenerationFailurePreservesCause(bool preparationThrows)
    {
        using var dir = TempDirectory.Create("retry-regen-failure");
        var builds = 0;
        var svc = new ConfigurableLanguageService(
            hasCustomizations: true, buildFunc: () => { builds++; return (true, null, null); },
            patchesFunc: () => [new("Client.java", "Rename", 1)],
            preGenerateFunc: (_, _) => preparationThrows ? throw new IOException("plugin preparation diagnostic") : Task.CompletedTask);
        var (tool, _) = CreateTool(svc,
            tspHelper: new MockTspHelper(false, "generator diagnostic"),
            configureClassifier: CodeCustomizationClassifier("Rename"));
        var result = await tool.UpdateAsync("Rename", dir.DirectoryPath, editScope: EditScope.CustomCode, maxAttempts: 3);
        Assert.That(result.Success, Is.False);
        Assert.That(result.AttemptsUsed, Is.EqualTo(3));
        Assert.That(builds, Is.EqualTo(1), "A failed required stage must not validate stale generated output.");
        Assert.That(result.ErrorCode, Is.EqualTo(preparationThrows
            ? CustomizedCodeUpdateResponse.KnownErrorCodes.UnexpectedError
            : CustomizedCodeUpdateResponse.KnownErrorCodes.RegenerateAfterPatchesFailed));
        Assert.That(result.BuildResult, Is.EqualTo(preparationThrows ? "plugin preparation diagnostic" : "generator diagnostic"));
        Assert.That(result.ResponseError, Does.Contain(result.BuildResult!));
    }

    [TestCase("Preparation")]
    [TestCase("Regeneration")]
    [TestCase("Build")]
    public async Task BoundedRetries_StageFailureCanRecoverWithoutRestartingSession(string failedStage)
    {
        using var dir = TempDirectory.Create("retry-stage-recovery");
        var builds = 0;
        var preparations = 0;
        var generations = 0;
        var diagnostic = $"{failedStage} diagnostic";
        var svc = new ConfigurableLanguageService(
            hasCustomizations: true,
            buildFunc: () =>
            {
                builds++;
                if (failedStage == "Build" && builds == 2) { throw new IOException(diagnostic); }
                return (builds > 1, builds == 1 ? "initial compiler diagnostic" : null, null);
            },
            patchesFunc: () => [new("Client.java", "Repair implementation", 1)],
            preGenerateFunc: (_, _) =>
            {
                if (++preparations == 1 && failedStage == "Preparation") { throw new IOException(diagnostic); }
                return Task.CompletedTask;
            });
        var tsp = new Mock<ITspClientHelper>();
        tsp.Setup(t => t.UpdateGenerationAsync(dir.DirectoryPath, null, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new TspToolResponse
            {
                TypeSpecProject = dir.DirectoryPath,
                IsSuccessful = ++generations > 1 || failedStage != "Regeneration",
                ResponseError = diagnostic
            });
        var (tool, _) = CreateTool(svc, tspHelper: tsp.Object, configureClassifier: CodeCustomizationClassifier("Fix"));
        var result = await tool.UpdateAsync("Fix", dir.DirectoryPath, editScope: EditScope.CustomCode, maxAttempts: 2);
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, result.ResponseError);
            Assert.That(result.AttemptsUsed, Is.EqualTo(2));
            Assert.That(result.AppliedPatches, Has.Count.EqualTo(2));
            Assert.That(result.ErrorCode, Is.Null);
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(svc.RepairSessions, Is.EqualTo(1));
            Assert.That(svc.ValidationReasons.Single(), Does.Contain(diagnostic));
        });
    }

    [Test]
    public void BoundedRetries_CancellationDuringValidationPropagatesAndStops()
    {
        using var dir = TempDirectory.Create("retry-cancel");
        using var cts = new CancellationTokenSource();
        var builds = 0;
        var svc = new ConfigurableLanguageService(
            hasCustomizations: true, buildFunc: () => { builds++; return (false, "initial compiler failure", null); },
            patchesFunc: () => [new("Client.java", "Partial fix", 1)],
            preGenerateFunc: (_, _) => { cts.Cancel(); return Task.CompletedTask; });
        var (tool, _) = CreateTool(svc, configureClassifier: CodeCustomizationClassifier("Fix"));
        Assert.ThrowsAsync<OperationCanceledException>(() =>
            tool.UpdateAsync("Fix", dir.DirectoryPath, editScope: EditScope.CustomCode, ct: cts.Token, maxAttempts: 3));
        Assert.That(builds, Is.EqualTo(1));
        Assert.That(svc.RepairSessions, Is.EqualTo(1));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(11)]
    public async Task BoundedRetries_InvalidLimitsAreIdenticalOnCliAndMcp(int attempts)
    {
        var (tool, _) = CreateTool();
        var parsed = tool.GetCommandInstances().Single().Parse(
            ["--customization-request", "Fix", "--edit-scope", "CustomCode", "--max-attempts", attempts.ToString()]);
        var cli = (CustomizedCodeUpdateResponse)await tool.HandleCommand(parsed, CancellationToken.None);
        var mcp = await tool.UpdateAsync("Fix", editScope: EditScope.CustomCode, maxAttempts: attempts);
        Assert.That(cli.ErrorCode, Is.EqualTo("InvalidInput"));
        Assert.That(cli.ResponseError, Is.EqualTo(mcp.ResponseError));
        Assert.That(cli.AttemptsUsed, Is.Zero);
        Assert.That(mcp.ExitCode, Is.Not.Zero);
    }

    [TestCase(EditScope.All)]
    [TestCase(EditScope.SpecInputs)]
    public async Task BoundedRetries_OtherScopesRejectMultipleAttempts(EditScope scope)
    {
        var (tool, _) = CreateTool();
        var result = await tool.UpdateAsync("Fix", editScope: scope, maxAttempts: 2);
        Assert.That(result.ErrorCode, Is.EqualTo("InvalidInput"));
        Assert.That(result.AttemptsUsed, Is.Zero);
    }

    [Test]
    public void BoundedRetries_PublicSchemaAddsOnlyMaxAttemptsWithDefaultOne()
    {
        var (tool, _) = CreateTool();
        var command = tool.GetCommandInstances().Single();
        var option = command.Options.OfType<Option<int>>().Single(o => o.Name == "--max-attempts");
        Assert.That(command.Parse(["--customization-request", "Fix"]).GetValue(option), Is.EqualTo(1));
        var method = typeof(CustomizedCodeUpdateTool).GetMethod(nameof(CustomizedCodeUpdateTool.UpdateAsync))!;
        var schema = McpServerTool.Create(method, _ => tool).ProtocolTool.InputSchema.GetProperty("properties");
        Assert.That(schema.GetProperty("maxAttempts").GetProperty("type").GetString(), Is.EqualTo("integer"));
        Assert.That(method.GetParameters().Single(p => p.Name == "maxAttempts").DefaultValue, Is.EqualTo(1));
        Assert.That(schema.TryGetProperty("repairBuild", out _), Is.False);
        Assert.That(schema.TryGetProperty("timeoutMinutes", out _), Is.False);
        Assert.That(schema.TryGetProperty("repairArtifactsPath", out _), Is.False);
    }

    [TestCase("success", false, 2)]
    [TestCase("success", true, 2)]
    [TestCase("exhausted", false, 3)]
    [TestCase("exhausted", true, 3)]
    [TestCase("already_green", false, 0)]
    [TestCase("already_green", true, 0)]
    [TestCase("spec", false, 0)]
    [TestCase("spec", true, 0)]
    [TestCase("manual", false, 0)]
    [TestCase("manual", true, 0)]
    public async Task BoundedRetries_ActualSerializersExposeReducedContract(string scenario, bool mcp, int expectedAttempts)
    {
        using var dir = TempDirectory.Create("narrow-contract");
        var builds = 0;
        var svc = new ConfigurableLanguageService(
            hasCustomizations: true, language: SdkLanguage.DotNet,
            buildFunc: () =>
            {
                builds++;
                var success = scenario == "already_green" || (scenario == "success" && builds >= 3);
                return (success, success ? null : $"CS0122: Widget member remains inaccessible (build {builds}).", null);
            },
            patchesFunc: () => [new("Client.cs", "Adjust member accessibility", 1)]);
        var category = scenario switch
        {
            "already_green" => "SUCCESS",
            "spec" => "TSP_APPLICABLE",
            "manual" => "REQUIRES_MANUAL_INTERVENTION",
            _ => "CODE_CUSTOMIZATION"
        };
        var (tool, _) = CreateTool(svc, configureClassifier: SingleClassification(category),
            configureGit: g => g.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net"));
        string text;
        if (mcp)
        {
            var method = typeof(CustomizedCodeUpdateTool).GetMethod(nameof(CustomizedCodeUpdateTool.UpdateAsync))!;
            var server = new Mock<McpServer>();
            using var services = new ServiceCollection().BuildServiceProvider();
            server.SetupGet(s => s.Services).Returns(services);
            var context = new RequestContext<CallToolRequestParams>(server.Object, new JsonRpcRequest { Method = "tools/call" })
            {
                Params = new()
                {
                    Name = "azsdk_customized_code_update",
                    Arguments = new Dictionary<string, JsonElement>
                    {
                        ["customizationRequest"] = JsonSerializer.SerializeToElement("Fix member accessibility"),
                        ["packagePath"] = JsonSerializer.SerializeToElement(dir.DirectoryPath),
                        ["editScope"] = JsonSerializer.SerializeToElement(EditScope.CustomCode, McpJsonUtilities.DefaultOptions),
                        ["maxAttempts"] = JsonSerializer.SerializeToElement(3)
                    }
                }
            };
            var call = await McpServerTool.Create(method, _ => tool).InvokeAsync(context);
            Assert.That(call.IsError, Is.Not.True);
            text = call.Content.OfType<TextContentBlock>().Single().Text;
        }
        else
        {
            var parsed = tool.GetCommandInstances().Single().Parse(["--customization-request", "Fix member accessibility",
                "--package-path", dir.DirectoryPath, "--edit-scope", "CustomCode", "--max-attempts", "3"]);
            text = new OutputHelper(OutputHelper.OutputModes.Json).Format(await tool.HandleCommand(parsed, CancellationToken.None));
        }
        using var json = JsonDocument.Parse(text);
        var root = json.RootElement;
        Assert.That(root.GetProperty("attemptsUsed").GetInt32(), Is.EqualTo(expectedAttempts));
        Assert.That(root.TryGetProperty("repair", out _), Is.False);
        Assert.That(root.TryGetProperty("buildValidated", out _), Is.False);
        Assert.That(root.GetProperty("success").GetBoolean(), Is.EqualTo(scenario is "success" or "already_green"));
        Assert.That(root.GetProperty("operation_status").GetString(),
            Is.EqualTo(scenario is "success" or "already_green" ? "Succeeded" : "Failed"));
        if (scenario == "exhausted")
        {
            Assert.That(root.GetProperty("operation_status").GetString(), Is.EqualTo("Failed"));
            Assert.That(root.GetProperty("buildResult").GetString(), Does.Contain("CS0122").And.Contain("build 4"));
            Assert.That(root.GetProperty("response_error").GetString(), Does.Contain("CS0122").And.Contain("limit (3)"));
        }
        if (scenario is "spec" or "manual")
        {
            Assert.That(root.GetProperty(scenario == "spec" ? "specChangeRequired" : "next_steps")[0].GetString(),
                Does.Contain("Use a spec annotation"));
        }
        var output = TestContext.Parameters["NarrowContractOutputDirectory"];
        if (!string.IsNullOrEmpty(output))
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, $"{scenario}.{(mcp ? "mcp" : "cli")}.json"), text);
        }
    }

    private static Action<Mock<IFeedbackClassifierService>> SingleClassification(string category) =>
        c => c.Setup(x => x.ClassifyItemsAsync(It.IsAny<List<FeedbackItem>>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(),
            It.IsAny<EditScope>(), It.IsAny<CancellationToken>()))
        .Returns((List<FeedbackItem> items, string context, string path, string apiView, string request,
            string language, string service, int? batch, EditScope scope, CancellationToken ct) =>
        {
            var item = new FeedbackItem { Text = request };
            items.Add(item);
            return Task.FromResult(new FeedbackClassificationResponse
            {
                Classifications = [new()
                {
                    ItemId = item.Id, Text = item.Text, Classification = category,
                    Reason = "Use a spec annotation or correct the handwritten override."
                }]
            });
        });
}
