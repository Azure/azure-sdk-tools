// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.Reflection;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Services.Repair;
using Azure.Sdk.Tools.Cli.Services.TypeSpec;
using Azure.Sdk.Tools.Cli.Tools.TypeSpec;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.TypeSpec;

[TestFixture]
[NonParallelizable]
public class CustomizedCodeUpdateContractTests
{
    private Mock<ICustomizedCodeRepairService> _repair = null!;
    private CustomizedCodeUpdateTool _tool = null!;
    private List<CustomizedCodeRepairRequest> _requests = [];

    [SetUp]
    public void SetUp()
    {
        _requests = [];
        _repair = new();
        _repair.Setup(r => r.RunAsync(It.IsAny<CustomizedCodeRepairRequest>(),
            It.IsAny<Func<string, CancellationToken, Task<LanguageService>>>(), It.IsAny<CancellationToken>()))
            .Callback((CustomizedCodeRepairRequest request, Func<string, CancellationToken, Task<LanguageService>> resolve, CancellationToken ct) =>
                _requests.Add(request))
            .ReturnsAsync(new CustomizedCodeUpdateResponse { Success = true });
        _tool = new CustomizedCodeUpdateTool(NullLogger<CustomizedCodeUpdateTool>.Instance, [], Mock.Of<IGitHelper>(),
            Mock.Of<ITspClientHelper>(), Mock.Of<IAPIViewFeedbackService>(), Mock.Of<IFeedbackClassifierService>(),
            Mock.Of<ITypeSpecCustomizationService>(), Mock.Of<ITypeSpecHelper>(), Mock.Of<INpxHelper>(), _repair.Object);
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(10)]
    public async Task CliAndMcp_UseTheSameBoundedRequest(int attempts)
    {
        var package = Path.Combine(Path.GetTempPath(), "contract-package");
        var command = _tool.GetCommandInstances().Single();
        var parsed = command.Parse(["--package-path", package, "--edit-scope", "CustomCode",
            "--customization-request", "Rename Widget", "--max-attempts", attempts.ToString()]);
        Assert.That(parsed.Errors, Is.Empty);

        await _tool.HandleCommand(parsed, CancellationToken.None);
        await _tool.UpdateAsync("Rename Widget", package, editScope: EditScope.CustomCode, maxAttempts: attempts);

        Assert.That(_requests, Has.Count.EqualTo(2));
        Assert.That(_requests[0], Is.EqualTo(_requests[1]));
        Assert.That(_requests[0].MaxAttempts, Is.EqualTo(attempts));
        Assert.That(_requests[0].TimeoutMinutes, Is.EqualTo(30));
        Assert.That(_requests[0].ArtifactsPath, Is.Null);
    }

    [Test]
    public async Task OmittedMaxAttempts_DefaultsToOneInBothSurfaces()
    {
        var package = Path.Combine(Path.GetTempPath(), "contract-package");
        var parsed = _tool.GetCommandInstances().Single().Parse(["--package-path", package,
            "--edit-scope", "CustomCode", "--customization-request", "Fix custom code"]);
        await _tool.HandleCommand(parsed, CancellationToken.None);
        await _tool.UpdateAsync("Fix custom code", package, editScope: EditScope.CustomCode);
        Assert.That(_requests.Select(r => r.MaxAttempts), Is.EqualTo(new[] { 1, 1 }));
    }

    [Test]
    public void OnlyMaxAttempts_IsAddedToThePublicSchema()
    {
        var command = _tool.GetCommandInstances().Single();
        var option = command.Options.OfType<Option<int>>().Single(o => o.Name == "--max-attempts");
        Assert.That(command.Parse(["--customization-request", "test"]).GetValue(option), Is.EqualTo(1));
        Assert.That(command.Options.Select(o => o.Name),
            Does.Not.Contain("--repair-build").And.Not.Contain("--timeout-minutes").And.Not.Contain("--repair-artifacts-path"));

        var method = typeof(CustomizedCodeUpdateTool).GetMethod(nameof(CustomizedCodeUpdateTool.UpdateAsync))!;
        var mcp = McpServerTool.Create(method, _ => _tool);
        var properties = mcp.ProtocolTool.InputSchema.GetProperty("properties");
        Assert.Multiple(() =>
        {
            Assert.That(properties.GetProperty("maxAttempts").GetProperty("type").GetString(), Is.EqualTo("integer"));
            Assert.That(method.GetParameters().Single(p => p.Name == "maxAttempts").DefaultValue, Is.EqualTo(1));
            Assert.That(properties.TryGetProperty("ct", out _), Is.False);
            Assert.That(properties.TryGetProperty("repairBuild", out _), Is.False);
            Assert.That(properties.TryGetProperty("timeoutMinutes", out _), Is.False);
            Assert.That(properties.TryGetProperty("repairArtifactsPath", out _), Is.False);
        });
        Assert.That(method.GetCustomAttribute<McpServerToolAttribute>()!.Name, Is.EqualTo("azsdk_customized_code_update"));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(11)]
    public async Task InvalidLimits_FailIdenticallyWithoutStartingARepair(int attempts)
    {
        var parsed = _tool.GetCommandInstances().Single().Parse(["--customization-request", "test",
            "--edit-scope", "CustomCode", "--max-attempts", attempts.ToString()]);
        var cli = (CustomizedCodeUpdateResponse)await _tool.HandleCommand(parsed, CancellationToken.None);
        var mcp = await _tool.UpdateAsync("test", editScope: EditScope.CustomCode, maxAttempts: attempts);
        Assert.Multiple(() =>
        {
            Assert.That(cli.ErrorCode, Is.EqualTo("InvalidInput"));
            Assert.That(mcp.ErrorCode, Is.EqualTo(cli.ErrorCode));
            Assert.That(mcp.ResponseError, Is.EqualTo(cli.ResponseError));
            Assert.That(cli.ExitCode, Is.Not.Zero);
            Assert.That(mcp.ExitCode, Is.Not.Zero);
            Assert.That(_requests, Is.Empty);
        });
    }

    [TestCase(EditScope.All)]
    [TestCase(EditScope.SpecInputs)]
    public async Task UnsupportedMultiAttemptScope_IsNotSilentlyIgnored(EditScope scope)
    {
        var response = await _tool.UpdateAsync("test", editScope: scope, maxAttempts: 2);
        Assert.That(response.ErrorCode, Is.EqualTo("InvalidInput"));
        Assert.That(response.ExitCode, Is.Not.Zero);
        Assert.That(_requests, Is.Empty);
    }

    [Test]
    public async Task ExistingPositionalCancellationArgument_IsPreserved()
    {
        using var cts = new CancellationTokenSource();
        await _tool.UpdateAsync("test", "package", null, EditScope.CustomCode, cts.Token);
        _repair.Verify(r => r.RunAsync(It.Is<CustomizedCodeRepairRequest>(request => request.MaxAttempts == 1),
            It.IsAny<Func<string, CancellationToken, Task<LanguageService>>>(), cts.Token), Times.Once);
    }

    [Test]
    public async Task ApiViewRequests_PreserveFeedbackSource()
    {
        const string apiView = "https://apiview.dev/Assemblies/Review/id";
        await _tool.UpdateAsync(apiView, "package", editScope: EditScope.CustomCode);
        Assert.That(_requests.Single().ApiViewUrl, Is.EqualTo(apiView));
        Assert.That(_requests.Single().CustomizationRequest, Is.EqualTo(apiView));
    }
}
