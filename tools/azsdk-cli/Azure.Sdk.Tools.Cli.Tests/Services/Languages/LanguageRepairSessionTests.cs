// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture]
public class LanguageRepairSessionTests
{
    private string _root = null!;
    private Mock<ICopilotAgentRunner> _runner = null!;
    private Mock<IProcessHelper> _process = null!;
    private Mock<IGitHelper> _git = null!;
    private CopilotAgent<string>? _agent;
    private Func<Task>? _duringTurn;
    private readonly List<AppliedPatch> _patches = [];

    [SetUp]
    public void SetUp()
    {
        _root = Path.GetFullPath($"language-repair-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _process = new Mock<IProcessHelper>();
        _git = new Mock<IGitHelper>();
        _runner = new Mock<ICopilotAgentRunner>();
        _runner.Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .Returns(async (CopilotAgent<string> agent, CancellationToken ct) =>
            {
                _agent = agent;
                if (agent.OnTurnStarting != null) { await agent.OnTurnStarting(ct); }
                if (_duringTurn != null) { await _duringTurn(); }
                if (agent.OnTurnCompleted != null) { await agent.OnTurnCompleted("hypothesis", ct); }
                return "agent text is not validation";
            });
        _agent = null;
        _duringTurn = null;
        _patches.Clear();
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [TestCase(SdkLanguage.DotNet, "partial classes", 10)]
    [TestCase(SdkLanguage.Java, "string literal", 10)]
    [TestCase(SdkLanguage.JavaScript, "Merge conflicts", 25)]
    [TestCase(SdkLanguage.Python, "__all__", 25)]
    public async Task RepairRetainsLanguageExpertiseHooksAndOneRunnerCall(SdkLanguage language, string expertise, int legacyIterations)
    {
        var (service, customizationRoot, _) = CreateWithCustomization(language);
        using var cts = new CancellationTokenSource();
        var starts = 0;
        var completions = 0;
        Func<CancellationToken, Task> start = token =>
        {
            Assert.That(token, Is.EqualTo(cts.Token));
            starts++;
            return Task.CompletedTask;
        };
        Func<string?, CancellationToken, Task<CopilotAgentTurnResult<string>>> complete =
            (hypothesis, token) =>
            {
                Assert.That(token, Is.EqualTo(cts.Token));
                Assert.That(hypothesis, Is.EqualTo("hypothesis"));
                completions++;
                return Task.FromResult(new CopilotAgentTurnResult<string>(false, null, string.Empty));
            };
        await service.RunRepairSessionAsync(customizationRoot, _root, "actual build diagnostic", 3,
            start, complete, _patches.Add, cts.Token);

        Assert.Multiple(() =>
        {
            Assert.That(_agent, Is.Not.Null);
            Assert.That(_agent!.Instructions, Does.Contain(expertise).And.Contain("actual build diagnostic"));
            Assert.That(_agent.Instructions, Does.Contain("retained conversation").And.Contain("Only host validation"));
            Assert.That(_agent.MaxIterations, Is.EqualTo(3));
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(_agent.Tools.Select(t => t.Name), Is.EquivalentTo(language == SdkLanguage.DotNet
                ? new[] { "ReadFile", "GrepSearch", "CodePatchTool", "RenameFile" }
                : new[] { "ReadFile", "GrepSearch", "CodePatchTool" }));
        });
        _runner.Verify(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), cts.Token), Times.Once);

        await service.ApplyPatchesAsync(customizationRoot, _root, "legacy diagnostic", CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(_agent!.MaxIterations, Is.EqualTo(legacyIterations));
            Assert.That(_agent.OnTurnStarting, Is.Null);
            Assert.That(_agent.OnTurnCompleted, Is.Null);
        });
    }

    [TestCase(SdkLanguage.DotNet)]
    [TestCase(SdkLanguage.Java)]
    [TestCase(SdkLanguage.JavaScript)]
    [TestCase(SdkLanguage.Python)]
    public void RepairSurfacesAgentErrorsAndCancellation(SdkLanguage language)
    {
        var (service, customizationRoot, _) = CreateWithCustomization(language);
        _runner.Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("agent unavailable"));
        var error = Assert.ThrowsAsync<InvalidOperationException>(() => RunRepair(service, customizationRoot));
        Assert.That(error!.Message, Is.EqualTo("agent unavailable"));
        _runner.Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        Assert.CatchAsync<OperationCanceledException>(() => RunRepair(service, customizationRoot));
    }

    [TestCase(SdkLanguage.DotNet)]
    [TestCase(SdkLanguage.Java)]
    [TestCase(SdkLanguage.JavaScript)]
    [TestCase(SdkLanguage.Python)]
    public void CancelledRepairDoesNotCreateAnAgent(SdkLanguage language)
    {
        var (service, customizationRoot, _) = CreateWithCustomization(language);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.CatchAsync<OperationCanceledException>(() => RunRepair(service, customizationRoot, cts.Token));
        _runner.Verify(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(SdkLanguage.DotNet)]
    [TestCase(SdkLanguage.Java)]
    [TestCase(SdkLanguage.JavaScript)]
    [TestCase(SdkLanguage.Python)]
    public async Task RepairCanOnlyPatchInitialDiscoverySet(SdkLanguage language)
    {
        var (service, customizationRoot, file) = CreateWithCustomization(language);
        _duringTurn = async () =>
        {
            var extension = Path.GetExtension(file);
            var newFile = Path.Combine(Path.GetDirectoryName(file)!, "added", language == SdkLanguage.Python ? "_patch.py" : $"Added{extension}");
            Directory.CreateDirectory(Path.GetDirectoryName(newFile)!);
            File.WriteAllText(newFile, "before");
            await Patch(Path.GetRelativePath(customizationRoot, newFile), "before", "after");
            Assert.That(File.ReadAllText(newFile), Is.EqualTo("before"));
            await Patch(Path.GetRelativePath(customizationRoot, file), "before", "after");
        };
        await RunRepair(service, customizationRoot);
        Assert.That(File.ReadAllText(file), Does.Contain("after"));
        Assert.That(_patches, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DotnetRenameAddsDestinationAndRemovesSourceFromAllowedSet()
    {
        var (service, customizationRoot, source) = CreateWithCustomization(SdkLanguage.DotNet);
        var destination = Path.Combine(customizationRoot, "renamed", "NewClient.cs");
        _duringTurn = async () =>
        {
            var rename = _agent!.Tools.Single(t => t.Name == "RenameFile");
            await rename.InvokeAsync(new AIFunctionArguments
            {
                ["oldFilePath"] = "Client.cs", ["newFilePath"] = "renamed/NewClient.cs"
            });
            await Patch("renamed/NewClient.cs", "before", "after");
            File.WriteAllText(source, "before");
            await Patch("Client.cs", "before", "after");
        };
        await RunRepair(service, customizationRoot);
        Assert.That(File.ReadAllText(destination), Does.Contain("after"));
        Assert.That(File.ReadAllText(source), Is.EqualTo("before"));
        Assert.That(_patches, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task DotnetRenameCannotTargetGeneratedMetadataOrUndiscoveredSources()
    {
        var (service, customizationRoot, _) = CreateWithCustomization(SdkLanguage.DotNet);
        var generated = WriteFile("src/Generated/GeneratedClient.cs", "before");
        _duringTurn = async () =>
        {
            var rename = _agent!.Tools.Single(t => t.Name == "RenameFile");
            foreach (var destination in new[]
            {
                "Generated/NewClient.cs", "NewClient.csproj", "../Client.cs", "eng/Client.cs",
                "Properties/AssemblyInfo.cs", "Version.cs"
            })
            {
                Assert.ThrowsAsync<ArgumentException>(async () => await rename.InvokeAsync(new AIFunctionArguments
                {
                    ["oldFilePath"] = "Client.cs", ["newFilePath"] = destination
                }));
            }
            await Patch("Generated/GeneratedClient.cs", "before", "after");
        };
        await RunRepair(service, customizationRoot);
        Assert.That(File.ReadAllText(generated), Is.EqualTo("before"));
        Assert.That(_patches, Is.Empty);
    }

    [TestCase(SdkLanguage.DotNet, "[assembly: System.Reflection.AssemblyVersion(\"2.0.0\")]")]
    [TestCase(SdkLanguage.JavaScript, "export const SDK_VERSION: string = \"2.0.0\";")]
    [TestCase(SdkLanguage.Java, "String PACKAGE_VERSION = \"2.0.0\";")]
    [TestCase(SdkLanguage.Python, "__version__ = '2.0.0'")]
    public async Task RepairRejectsIntroducingPackageMetadataBeforeWriting(SdkLanguage language, string metadata)
    {
        var (service, customizationRoot, file) = CreateWithCustomization(language);
        var original = File.ReadAllText(file);
        _duringTurn = () => Patch(Path.GetRelativePath(customizationRoot, file), "before", metadata);
        await RunRepair(service, customizationRoot);
        Assert.That(File.ReadAllText(file), Is.EqualTo(original));
        Assert.That(_patches, Is.Empty);
    }

    [Test]
    public async Task PythonDiscoveryDoesNotExposeEmptyTemplateFilesForRepair()
    {
        var (service, customizationRoot, _) = CreateWithCustomization(SdkLanguage.Python);
        var template = WriteFile("azure/service/aio/_patch.py", "__all__ = []");
        _duringTurn = async () =>
        {
            File.WriteAllText(template, "__all__ = ['before']");
            await Patch(Path.GetRelativePath(customizationRoot, template), "before", "after");
        };
        await RunRepair(service, customizationRoot);
        Assert.That(_agent!.Instructions, Does.Not.Contain(Path.GetRelativePath(customizationRoot, template)));
        Assert.That(File.ReadAllText(template), Does.Contain("before"));
    }

    [Test]
    public async Task RepairMutationToolsAreClosedDuringHostCallbacksAndAfterSessionCompletion()
    {
        var (service, customizationRoot, file) = CreateWithCustomization(SdkLanguage.DotNet);
        var starts = 0;
        var validations = 0;
        _duringTurn = () => Patch("Client.cs", "before", "after");
        await service.RunRepairSessionAsync(customizationRoot, _root, "diagnostic", 2,
            _ =>
            {
                starts++;
                AssertMutationToolsUnavailable();
                Assert.That(File.ReadAllText(file), Is.EqualTo("before"));
                return Task.CompletedTask;
            },
            (_, _) =>
            {
                validations++;
                AssertMutationToolsUnavailable();
                Assert.That(File.ReadAllText(file).Trim(), Is.EqualTo("after"));
                return Task.FromResult(new CopilotAgentTurnResult<string>(false, null, string.Empty));
            }, _patches.Add, CancellationToken.None);

        AssertMutationToolsUnavailable();
        Assert.That(File.ReadAllText(file).Trim(), Is.EqualTo("after"));
        Assert.That(File.Exists(Path.Combine(customizationRoot, "LateClient.cs")), Is.False);
        Assert.That(_patches, Has.Count.EqualTo(1));
        Assert.That(starts, Is.EqualTo(1));
        Assert.That(validations, Is.EqualTo(1));
        Assert.CatchAsync<InvalidOperationException>(() => _agent!.OnTurnStarting!(CancellationToken.None),
            "A cached hook must not reopen a disposed session.");
        Assert.That(starts, Is.EqualTo(1));
    }

    [Test]
    public async Task RepairMutationToolsReopenOnlyAfterTheNextHostStartCompletes()
    {
        var (service, customizationRoot, file) = CreateWithCustomization(SdkLanguage.DotNet);
        var completed = 0;
        _runner.Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .Returns(async (CopilotAgent<string> agent, CancellationToken token) =>
            {
                _agent = agent;
                await agent.OnTurnStarting!(token);
                await Patch("Client.cs", "before", "first");
                var next = await agent.OnTurnCompleted!("first candidate", token);
                Assert.That(next.Continue, Is.True);
                AssertMutationToolsUnavailable();
                await agent.OnTurnStarting(token);
                await _agent.Tools.Single(t => t.Name == "RenameFile").InvokeAsync(new AIFunctionArguments
                {
                    ["oldFilePath"] = "Client.cs", ["newFilePath"] = "NewClient.cs"
                }, token);
                await Patch("NewClient.cs", "first", "second");
                next = await agent.OnTurnCompleted("second candidate", token);
                Assert.That(next.Continue, Is.False);
                AssertMutationToolsUnavailable("NewClient.cs");
                return string.Empty;
            });
        await service.RunRepairSessionAsync(customizationRoot, _root, "diagnostic", 2,
            _ =>
            {
                AssertMutationToolsUnavailable();
                return Task.CompletedTask;
            },
            (_, _) =>
            {
                completed++;
                AssertMutationToolsUnavailable(completed == 1 ? "Client.cs" : "NewClient.cs");
                return Task.FromResult(new CopilotAgentTurnResult<string>(completed < 2, "next diagnostics", string.Empty));
            }, _patches.Add, CancellationToken.None);
        AssertMutationToolsUnavailable("NewClient.cs");
        Assert.That(File.Exists(file), Is.False);
        Assert.That(File.ReadAllText(Path.Combine(customizationRoot, "NewClient.cs")).Trim(), Is.EqualTo("second"));
        Assert.That(_patches, Has.Count.EqualTo(3));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AgentFailureOrCancellationClosesPreviouslyOpenedMutationTools(bool cancel)
    {
        var (service, customizationRoot, file) = CreateWithCustomization(SdkLanguage.DotNet);
        using var cts = new CancellationTokenSource();
        _runner.Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .Returns(async (CopilotAgent<string> agent, CancellationToken token) =>
            {
                _agent = agent;
                await agent.OnTurnStarting!(token);
                if (cancel)
                {
                    cts.Cancel();
                    throw new OperationCanceledException(token);
                }
                throw new IOException("agent transport failed");
            });
        if (cancel)
        {
            Assert.CatchAsync<OperationCanceledException>(() => RunRepair(service, customizationRoot, cts.Token));
        }
        else
        {
            Assert.ThrowsAsync<IOException>(() => RunRepair(service, customizationRoot, cts.Token));
        }
        AssertMutationToolsUnavailable(cancelled: cancel);
        Assert.That(File.ReadAllText(file), Is.EqualTo("before"));
        Assert.That(_patches, Is.Empty);
    }

    [Test]
    public async Task DeterministicValidationWaitsForAnInflightMutationToolToFinish()
    {
        var (service, customizationRoot, file) = CreateWithCustomization(SdkLanguage.DotNet);
        var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var finishTool = new ManualResetEventSlim();
        var validations = 0;
        _runner.Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .Returns(async (CopilotAgent<string> agent, CancellationToken token) =>
            {
                _agent = agent;
                await agent.OnTurnStarting!(token);
                var patch = Task.Run(() => Patch("Client.cs", "before", "after"), token);
                try
                {
                    await reported.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
                    var validation = agent.OnTurnCompleted!("hypothesis", token);
                    Assert.That(validation.IsCompleted, Is.False);
                    Assert.That(validations, Is.Zero);
                    AssertMutationToolsUnavailable();
                    finishTool.Set();
                    await patch;
                    await validation.WaitAsync(TimeSpan.FromSeconds(5), token);
                    return string.Empty;
                }
                finally
                {
                    finishTool.Set();
                    await patch;
                }
            });
        await service.RunRepairSessionAsync(customizationRoot, _root, "diagnostic", 1, _ => Task.CompletedTask,
            (_, _) =>
            {
                validations++;
                Assert.That(File.ReadAllText(file).Trim(), Is.EqualTo("after"));
                return Task.FromResult(new CopilotAgentTurnResult<string>(false, null, string.Empty));
            },
            patch =>
            {
                _patches.Add(patch);
                reported.SetResult();
                Assert.That(finishTool.Wait(TimeSpan.FromSeconds(5)), Is.True);
            }, CancellationToken.None);
        Assert.That(validations, Is.EqualTo(1));
        AssertMutationToolsUnavailable();
    }

    private void AssertMutationToolsUnavailable(string filePath = "Client.cs", bool cancelled = false)
    {
        var patchError = Assert.CatchAsync(async () => await Patch(filePath, "after", "late mutation"));
        var renameError = Assert.CatchAsync(async () =>
            await _agent!.Tools.Single(t => t.Name == "RenameFile").InvokeAsync(new AIFunctionArguments
            {
                ["oldFilePath"] = filePath, ["newFilePath"] = "LateClient.cs"
            }));
        Assert.That(patchError, cancelled ? Is.InstanceOf<OperationCanceledException>() : Is.TypeOf<InvalidOperationException>());
        Assert.That(renameError, cancelled ? Is.InstanceOf<OperationCanceledException>() : Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void BuildPropagatesCancellationInsteadOfReportingBuildFailure()
    {
        var service = CreateService(SdkLanguage.DotNet);
        _git.Setup(g => g.DiscoverRepoRootAsync(_root, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        Assert.CatchAsync<OperationCanceledException>(() => service.BuildAsync(_root));
    }

    [TestCase(SdkLanguage.Java)]
    [TestCase(SdkLanguage.JavaScript)]
    [TestCase(SdkLanguage.Python)]
    public async Task OtherLanguagePreparationIsExplicitlyNotRequired(SdkLanguage language)
    {
        var service = CreateService(language);
        Assert.That(await service.PrepareForGenerationAsync(_root, CancellationToken.None),
            Is.EqualTo(new GenerationPreparationResult(false, true, null)));
        _process.Verify(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task MissingPluginFailsStrictPreparationButLegacyContinues()
    {
        var service = CreateService(SdkLanguage.DotNet);
        var result = await service.PrepareForGenerationAsync(_root, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(result.Required, Is.True);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Diagnostics, Does.Contain("Plugin directory not found"));
        });
        await service.PreGenerateAsync(_root, CancellationToken.None);
        _process.Verify(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(0)]
    [TestCase(1)]
    public async Task StrictPluginBuildUsesExistingCommandTimeoutAndReportsResult(int exitCode)
    {
        var plugin = Path.Combine(_root, "eng", "packages", "plugins", "client", "Client.Plugin");
        Directory.CreateDirectory(plugin);
        var processResult = new ProcessResult { ExitCode = exitCode };
        processResult.AppendStdout("plugin diagnostic");
        _process.Setup(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);
        var service = CreateService(SdkLanguage.DotNet);
        var result = await service.PrepareForGenerationAsync(_root, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(result.Required, Is.True);
            Assert.That(result.Success, Is.EqualTo(exitCode == 0));
            Assert.That(result.Diagnostics, Does.Contain("plugin diagnostic"));
            if (exitCode != 0)
            {
                Assert.That(result.Diagnostics, Does.Contain("exit code 1"));
            }
        });
        _process.Verify(p => p.Run(It.Is<ProcessOptions>(o =>
            Path.GetFullPath(o.WorkingDirectory) == plugin && o.Timeout == TimeSpan.FromMinutes(5) &&
            o.Args.Contains("build") && (o.Command == "dotnet" || o.Args.Contains("dotnet"))),
            It.IsAny<CancellationToken>()), Times.Once);
        await service.PreGenerateAsync(_root, CancellationToken.None);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task PluginExceptionsAndLocalTimeoutFailStrictPreparationButLegacyContinues(bool timeout)
    {
        Directory.CreateDirectory(Path.Combine(_root, "eng", "packages", "plugins", "client", "Client.Plugin"));
        _process.Setup(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(timeout ? new OperationCanceledException("plugin timed out") : new IOException("plugin broken"));
        var service = CreateService(SdkLanguage.DotNet);
        var result = await service.PrepareForGenerationAsync(_root, CancellationToken.None);
        Assert.That(result.Required, Is.True);
        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics, Does.Contain(timeout ? "plugin timed out" : "plugin broken"));
        await service.PreGenerateAsync(_root, CancellationToken.None);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void StrictAndLegacyPreparationPropagateCallerCancellation(bool legacy)
    {
        Directory.CreateDirectory(Path.Combine(_root, "eng", "packages", "plugins", "client", "Client.Plugin"));
        using var cts = new CancellationTokenSource();
        _process.Setup(p => p.Run(It.IsAny<ProcessOptions>(), cts.Token))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(new ProcessResult { ExitCode = 1 });
        var service = CreateService(SdkLanguage.DotNet);
        Assert.CatchAsync<OperationCanceledException>(() => legacy
            ? service.PreGenerateAsync(_root, cts.Token)
            : service.PrepareForGenerationAsync(_root, cts.Token));
    }

    private Task RunRepair(LanguageService service, string customizationRoot, CancellationToken ct = default) =>
        service.RunRepairSessionAsync(customizationRoot, _root, "diagnostic", 3,
            _ => Task.CompletedTask,
            (_, _) => Task.FromResult(new CopilotAgentTurnResult<string>(false, null, string.Empty)),
            _patches.Add, ct);

    private async Task Patch(string path, string oldText, string newText)
    {
        await _agent!.Tools.Single(t => t.Name == "CodePatchTool").InvokeAsync(new AIFunctionArguments
        {
            ["filePath"] = path, ["startLine"] = 1, ["endLine"] = 1,
            ["oldText"] = oldText, ["newText"] = newText, ["patchDescription"] = "repair"
        });
    }

    private (LanguageService Service, string CustomizationRoot, string File) CreateWithCustomization(SdkLanguage language)
    {
        var relative = language switch
        {
            SdkLanguage.DotNet => "src/Client.cs",
            SdkLanguage.Java => "customization/src/main/java/Client.java",
            SdkLanguage.JavaScript => "src/client.ts",
            SdkLanguage.Python => "azure/service/_patch.py",
            _ => throw new ArgumentOutOfRangeException(nameof(language))
        };
        var file = WriteFile(relative, language == SdkLanguage.Python ? "__all__ = ['before']" : "before");
        return (CreateService(language), Path.GetDirectoryName(file)!, file);
    }

    private string WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private LanguageService CreateService(SdkLanguage language)
    {
        var logger = NullLogger<LanguageService>.Instance;
        var validation = Mock.Of<ICommonValidationHelpers>();
        var packages = Mock.Of<IPackageInfoHelper>();
        var files = Mock.Of<IFileHelper>();
        var config = Mock.Of<ISpecGenSdkConfigHelper>();
        var changelog = Mock.Of<IChangelogHelper>();
        return language switch
        {
            SdkLanguage.DotNet => new DotnetLanguageService(_process.Object, Mock.Of<IPowershellHelper>(), _runner.Object,
                _git.Object, logger, validation, packages, files, config, changelog),
            SdkLanguage.Java => new JavaLanguageService(_process.Object, _git.Object, Mock.Of<IMavenHelper>(), Mock.Of<IPythonHelper>(),
                _runner.Object, logger, validation, packages, files, config, changelog),
            SdkLanguage.JavaScript => new JavaScriptLanguageService(_process.Object, Mock.Of<INpxHelper>(), _runner.Object,
                _git.Object, logger, validation, packages, files, config, changelog),
            SdkLanguage.Python => new PythonLanguageService(_process.Object, Mock.Of<IPythonHelper>(), Mock.Of<INpxHelper>(),
                _runner.Object, _git.Object, logger, validation, packages, files, config, changelog),
            _ => throw new ArgumentOutOfRangeException(nameof(language))
        };
    }
}
