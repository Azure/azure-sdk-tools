// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture(SdkLanguage.DotNet, 10)]
[TestFixture(SdkLanguage.Java, 10)]
[TestFixture(SdkLanguage.JavaScript, 25)]
[TestFixture(SdkLanguage.Python, 25)]
public class LanguagePatchValidationTests(SdkLanguage language, int originalIterations)
{
    private const string BuildContext = "Compiler diagnostic: replace before with after.";
    private TempDirectory _directory = null!;
    private Mock<ICopilotAgentRunner> _runner = null!;
    private LanguageService _service = null!;
    private string _root = null!;
    private string _file = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = TempDirectory.Create("language-patch-validation");
        _runner = new Mock<ICopilotAgentRunner>();
        var relativePath = language switch
        {
            SdkLanguage.DotNet => Path.Combine("src", "Client.cs"),
            SdkLanguage.Java => Path.Combine("customization", "src", "main", "java", "Customization.java"),
            SdkLanguage.JavaScript => Path.Combine("src", "client.ts"),
            SdkLanguage.Python => Path.Combine("azure", "example", "_patch.py"),
            _ => throw new ArgumentOutOfRangeException(nameof(language))
        };
        _file = Path.Combine(_directory.DirectoryPath, relativePath);
        _root = Path.GetDirectoryName(_file)!;
        Directory.CreateDirectory(_root);
        File.WriteAllText(_file, language == SdkLanguage.Python ? "__all__ = [\"Client\"]\nbefore\n" : "before\n");
        _service = CreateService();
    }

    [TearDown]
    public void TearDown() => _directory.Dispose();

    [TestCase(true, 1)]
    [TestCase(false, 1)]
    [TestCase(false, 3)]
    public async Task OverloadsPreservePromptsToolsAndOriginalIterationAllowance(bool legacyOverload, int maxAttempts)
    {
        CopilotAgent<string>? captured = null;
        using var cancellation = new CancellationTokenSource();
        _runner.Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), cancellation.Token))
            .Callback<CopilotAgent<string>, CancellationToken>((agent, _) => captured = agent)
            .ReturnsAsync("The agent claims patches were applied.");

        var patches = legacyOverload
            ? await _service.ApplyPatchesAsync(_root, _directory.DirectoryPath, BuildContext, cancellation.Token)
            : await _service.ApplyPatchesAsync(_root, _directory.DirectoryPath, BuildContext, cancellation.Token, maxAttempts, null);

        Assert.That(patches, Is.Empty, "An agent summary is not a tool-recorded patch.");
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.ValidateResult, Is.Null);
        Assert.That(captured.MaxIterations, Is.EqualTo(originalIterations + maxAttempts - 1));
        Assert.That(captured.Instructions, Is.EqualTo(ExpectedPrompt()));
        string[] expectedTools = language switch
        {
            SdkLanguage.DotNet => ["ReadFile", "GrepSearch", "CodePatchTool", "RenameFile"],
            SdkLanguage.Java => ["ReadFile", "GrepSearch", "CodePatchTool"],
            _ => ["GrepSearch", "ReadFile", "CodePatchTool"]
        };
        Assert.That(captured.Tools.Select(t => t.Name), Is.EqualTo(expectedTools));
        _runner.Verify(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), cancellation.Token), Times.Once);
    }

    [TestCase(1)]
    [TestCase(3)]
    public async Task ValidatorSeesCumulativeSuccessfulPatchesAndControlsValidation(int maxAttempts)
    {
        var snapshots = new List<IReadOnlyList<AppliedPatch>>();
        var results = new List<CopilotAgentValidationResult>();
        var failed = new CopilotAgentValidationResult { Success = false, Reason = "Real build still fails." };
        var passed = new CopilotAgentValidationResult { Success = true };
        var callbackIterations = 0;
        _runner.Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .Returns(async (CopilotAgent<string> agent, CancellationToken token) =>
            {
                callbackIterations = agent.MaxIterations;
                await Patch(agent, "before", "first", token);
                await Patch(agent, "does not exist", "not applied", token);
                if (language == SdkLanguage.DotNet)
                {
                    var originalName = Path.GetFileName(_file);
                    _file = Path.Combine(_root, "RenamedClient.cs");
                    await agent.Tools.Single(t => t.Name == "RenameFile").InvokeAsync(new AIFunctionArguments
                    {
                        ["oldFilePath"] = originalName,
                        ["newFilePath"] = Path.GetFileName(_file)
                    }, token);
                }
                results.Add(await agent.ValidateResult!("Everything is fixed."));
                if (maxAttempts > 1)
                {
                    await Patch(agent, "first", "after", token);
                    results.Add(await agent.ValidateResult!("Everything is fixed."));
                }
                return "The claimed result is not the validation authority.";
            });

        var patches = await _service.ApplyPatchesAsync(_root, _directory.DirectoryPath, BuildContext,
            CancellationToken.None, maxAttempts, async current =>
            {
                await Task.Yield();
                snapshots.Add(current);
                return snapshots.Count == 1 ? failed : passed;
            });

        Assert.That(callbackIterations, Is.EqualTo(originalIterations + maxAttempts - 1));
        Assert.That(snapshots, Has.Count.EqualTo(maxAttempts == 1 ? 1 : 2));
        var firstPatchCount = language == SdkLanguage.DotNet ? 2 : 1;
        Assert.That(snapshots[0], Has.Count.EqualTo(firstPatchCount), "Later patches must not mutate earlier validation snapshots.");
        Assert.That(results[0], Is.SameAs(failed), "The agent's success claim must not override failed host validation.");
        Assert.That(patches, Is.EquivalentTo(snapshots[^1]));
        if (maxAttempts > 1)
        {
            Assert.That(snapshots[1], Has.Count.EqualTo(firstPatchCount + 1));
            Assert.That(results[1], Is.SameAs(passed));
        }
        Assert.That(File.ReadAllText(_file), Does.Contain(maxAttempts == 1 ? "first" : "after"));
        _runner.Verify(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task TerminalValidatorExceptionReturnsTheActualCumulativePatchLog()
    {
        IReadOnlyList<AppliedPatch>? validatedPatches = null;
        _runner.Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .Returns(async (CopilotAgent<string> agent, CancellationToken token) =>
            {
                await Patch(agent, "before", "after", token);
                await agent.ValidateResult!("Success");
                return "unreachable";
            });

        var result = await _service.ApplyPatchesAsync(_root, _directory.DirectoryPath, BuildContext,
            CancellationToken.None, 3, patches =>
            {
                validatedPatches = patches;
                throw new InvalidOperationException("The host stopped at its repair-attempt limit.");
            });

        Assert.That(validatedPatches, Has.Count.EqualTo(1));
        Assert.That(result, Is.EquivalentTo(validatedPatches));
        Assert.That(File.ReadAllText(_file), Does.Contain("after"));
        _runner.Verify(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase(true, false)]
    [TestCase(false, false)]
    [TestCase(false, true)]
    public void CancellationFromTheRunnerOrValidatorIsNotSwallowed(bool legacyOverload, bool cancelInValidator)
    {
        using var cancellation = new CancellationTokenSource();
        var validationCalled = false;
        _runner.Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), cancellation.Token))
            .Returns(async (CopilotAgent<string> agent, CancellationToken token) =>
            {
                if (cancelInValidator)
                {
                    await agent.ValidateResult!("Success");
                }
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return "unreachable";
            });
        Task<CopilotAgentValidationResult> Validate(IReadOnlyList<AppliedPatch> _)
        {
            validationCalled = true;
            cancellation.Cancel();
            cancellation.Token.ThrowIfCancellationRequested();
            return Task.FromResult(new CopilotAgentValidationResult { Success = true });
        }

        var error = Assert.CatchAsync<OperationCanceledException>(() => legacyOverload
            ? _service.ApplyPatchesAsync(_root, _directory.DirectoryPath, BuildContext, cancellation.Token)
            : _service.ApplyPatchesAsync(_root, _directory.DirectoryPath, BuildContext, cancellation.Token, 3, Validate));

        Assert.That(error!.CancellationToken, Is.EqualTo(cancellation.Token));
        Assert.That(validationCalled, Is.EqualTo(cancelInValidator));
        Assert.That(File.ReadAllText(_file), Does.Contain("before"));
        _runner.Verify(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), cancellation.Token), Times.Once);
    }

    private Task<object?> Patch(CopilotAgent<string> agent, string oldText, string newText, CancellationToken ct) =>
        agent.Tools.Single(t => t.Name == "CodePatchTool").InvokeAsync(new AIFunctionArguments
        {
            ["filePath"] = Path.GetFileName(_file),
            ["startLine"] = language == SdkLanguage.Python ? 2 : 1,
            ["endLine"] = language == SdkLanguage.Python ? 2 : 1,
            ["oldText"] = oldText,
            ["newText"] = newText,
            ["patchDescription"] = $"Replace {oldText} with {newText}"
        }, ct).AsTask();

    private string ExpectedPrompt()
    {
        List<string> readPaths = [Path.GetRelativePath(_directory.DirectoryPath, _file)];
        List<string> patchPaths = [Path.GetRelativePath(_root, _file)];
        return language switch
        {
            SdkLanguage.DotNet => new DotNetErrorDrivenPatchTemplate(BuildContext, _directory.DirectoryPath, _root, readPaths, patchPaths).BuildPrompt(),
            SdkLanguage.Java => new JavaErrorDrivenPatchTemplate(BuildContext, _directory.DirectoryPath, _root, readPaths, patchPaths).BuildPrompt(),
            SdkLanguage.JavaScript => new JavaScriptErrorDrivenPatchTemplate(BuildContext, _directory.DirectoryPath, _root, readPaths, patchPaths).BuildPrompt(),
            SdkLanguage.Python => new PythonErrorDrivenPatchTemplate(BuildContext, _directory.DirectoryPath, _root, readPaths, patchPaths).BuildPrompt(),
            _ => throw new ArgumentOutOfRangeException(nameof(language))
        };
    }

    private LanguageService CreateService() => language switch
    {
        SdkLanguage.DotNet => new DotNetLanguageService(Mock.Of<IProcessHelper>(), Mock.Of<IPowershellHelper>(),
            _runner.Object, Mock.Of<IGitHelper>(), NullLogger<LanguageService>.Instance,
            Mock.Of<ICommonValidationHelpers>(), Mock.Of<IPackageInfoHelper>(), Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>()),
        SdkLanguage.Java => new JavaLanguageService(Mock.Of<IProcessHelper>(), Mock.Of<IGitHelper>(),
            Mock.Of<IMavenHelper>(), Mock.Of<IPythonHelper>(), _runner.Object, NullLogger<LanguageService>.Instance,
            Mock.Of<ICommonValidationHelpers>(), Mock.Of<IPackageInfoHelper>(), Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>()),
        SdkLanguage.JavaScript => new JavaScriptLanguageService(Mock.Of<IProcessHelper>(), Mock.Of<INpxHelper>(),
            _runner.Object, Mock.Of<IGitHelper>(), NullLogger<LanguageService>.Instance,
            Mock.Of<ICommonValidationHelpers>(), Mock.Of<IPackageInfoHelper>(), Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>()),
        SdkLanguage.Python => new PythonLanguageService(Mock.Of<IProcessHelper>(), Mock.Of<IPythonHelper>(), Mock.Of<INpxHelper>(),
            _runner.Object, Mock.Of<IGitHelper>(), NullLogger<LanguageService>.Instance,
            Mock.Of<ICommonValidationHelpers>(), Mock.Of<IPackageInfoHelper>(), Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>()),
        _ => throw new ArgumentOutOfRangeException(nameof(language))
    };
}
