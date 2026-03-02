// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services.Languages.Samples;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Telemetry;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Azure.Sdk.Tools.Cli.Tools.Package.Samples;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Generators;

[TestFixture]
public class SampleGeneratorToolTests
{
    private TestLogger<SampleGeneratorTool> logger;
    private OutputHelper _outputHelper;
    private Mock<IMicroagentHostService> microagentHostServiceMock;
    private Mock<ICopilotAgentRunner> copilotAgentRunnerMock;
    private SampleGeneratorTool tool;
    private Mock<ITelemetryService> telemetryServiceMock;
    private Mock<INpxHelper> _mockNpxHelper;
    private Mock<IProcessHelper> _mockProcessHelper;
    private Mock<IPythonHelper> _mockPythonHelper;
    private Mock<IPowershellHelper> _mockPowerShellHelper;
    private Mock<IGitHelper> _mockGitHelper;
    private Mock<LanguageService> _mockGoLanguageService;
    private TestLogger<SdkBuildTool> _logger;
    private List<LanguageService> _languageServices;
    private IGitHelper realGitHelper;
    private Mock<ICommonValidationHelpers> _commonValidationHelpers;

    [Test]
    public async Task GenerateSamples_MultiScenarioPrompt_ProducesMultipleFiles()
    {
        var (_, packagePath) = await CreateFakeGoPackageAsync();
        var generatedSamples = new List<GeneratedSample>
        {
            new("create_key", "package main\nfunc main(){}"),
            new("list_keys", "package main\nfunc main(){}")
        };

        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generatedSamples);

        var command = tool.GetCommandInstances().First();
        var parseResult = command.Parse(["generate", "--prompt", "1) Create key 2) List keys", "--package-path", packagePath]);
        int exitCode = await parseResult.InvokeAsync();
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(File.Exists(Path.Combine(packagePath, "create_key.go")), Is.True);
            Assert.That(File.Exists(Path.Combine(packagePath, "list_keys.go")), Is.True);
        });

    }

    [TestCase(SdkLanguage.DotNet, "tests/samples", ".cs")]
    [TestCase(SdkLanguage.Java, "src/samples/java", ".java")]
    [TestCase(SdkLanguage.JavaScript, "samples-dev", ".ts")]
    [TestCase(SdkLanguage.Python, "samples", ".py")]
    [TestCase(SdkLanguage.Go, "", ".go")]
    public async Task GenerateSamples_LanguageSpecificOutputPath(SdkLanguage language, string expectedSubPath, string ext)
    {
        using var tempRepo = TempDirectory.Create($"sample-gen-{language}");
        var repoRoot = tempRepo.DirectoryPath;
        await GitTestHelper.GitInitAsync(repoRoot);
        var relativePath = Path.Combine("sdk", "group", "service", "pkg");
        var packagePath = Path.Combine(repoRoot, relativePath);
        Directory.CreateDirectory(packagePath);

        switch (language)
        {
            case SdkLanguage.DotNet:
                Directory.CreateDirectory(Path.Combine(packagePath, "src"));
                File.WriteAllText(Path.Combine(packagePath, "src", "Foo.cs"), "namespace Foo; class Bar {}");
                File.WriteAllText(Path.Combine(packagePath, "pkg.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
                break;
            case SdkLanguage.Java:
                var javaSrc = Path.Combine(packagePath, "src", "main", "java", "com", "example");
                Directory.CreateDirectory(javaSrc);
                File.WriteAllText(Path.Combine(javaSrc, "Foo.java"), "package com.example; class Foo {}");
                File.WriteAllText(Path.Combine(packagePath, "pom.xml"), "<project><modelVersion>4.0.0</modelVersion><groupId>com.example</groupId><artifactId>foo</artifactId><version>1.0.0</version></project>");
                break;
            case SdkLanguage.JavaScript:
                Directory.CreateDirectory(Path.Combine(packagePath, "src"));
                File.WriteAllText(Path.Combine(packagePath, "src", "index.ts"), "export const x=1;");
                File.WriteAllText(Path.Combine(packagePath, "package.json"), "{\n  \"name\": \"@azure/testpkg\",\n  \"version\": \"1.0.0\"\n}");
                break;
            case SdkLanguage.Python:
                Directory.CreateDirectory(Path.Combine(packagePath, "azure"));
                File.WriteAllText(Path.Combine(packagePath, "azure", "__init__.py"), "# init");
                File.WriteAllText(Path.Combine(packagePath, "setup.py"), "from setuptools import setup; setup(name='pkg', version='0.0.1')");
                break;
            case SdkLanguage.Go:
                File.WriteAllText(Path.Combine(packagePath, "client.go"), "package pkg\nfunc noop(){}");
                File.WriteAllText(Path.Combine(packagePath, "go.mod"), "module example.com/pkg\n\ngo 1.20");
                break;
        }

        var generatedSamples = new List<GeneratedSample> { new("scenario_one", $"// sample {language}") };
        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generatedSamples);

        var gitHubServiceMock = new Mock<IGitHubService>();
        var gitLogger = new TestLogger<GitHelper>();
        var gitCommandHelper = new GitCommandHelper(NullLogger<GitCommandHelper>.Instance, Mock.Of<IRawOutputHelper>());
        var realGitHelper = new GitHelper(gitHubServiceMock.Object, gitCommandHelper, gitLogger);

        if (language == SdkLanguage.Go)
        {
            _mockGoLanguageService
                .Setup(m => m.GetPackageInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((packagePath, ct) => Task.FromResult(new PackageInfo()
                {
                    RelativePath = relativePath,
                    RepoRoot = repoRoot,
                    SamplesDirectory = packagePath,
                    Language = SdkLanguage.Go,
                    PackageName = "sdk/group/service/pkg",
                    PackagePath = packagePath,
                    PackageVersion = "1.5.0",
                    SdkType = SdkType.Dataplane,
                    ServiceName = "service"
                }));
        }

        var mockGitHelper = new Mock<IGitHelper>();
        mockGitHelper.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(repoRoot);
        mockGitHelper.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() =>
        {
            return language switch
            {
                SdkLanguage.DotNet => "azure-sdk-for-net",
                SdkLanguage.Java => "azure-sdk-for-java",
                SdkLanguage.JavaScript => "azure-sdk-for-js",
                SdkLanguage.Python => "azure-sdk-for-python",
                SdkLanguage.Go => "azure-sdk-for-go",
                _ => "unknown-sdk"
            };
        });
        var testServiceLogger = new TestLogger<SampleGeneratorTool>();
        tool = new SampleGeneratorTool(microagentHostServiceMock.Object, testServiceLogger, mockGitHelper.Object, _languageServices);
        tool.Initialize(_outputHelper, telemetryServiceMock.Object, new MockUpgradeService());
        var command = tool.GetCommandInstances().First();
        var parseResult = command.Parse(["generate", "--prompt", "Do thing", "--package-path", packagePath]);
        int exitCode = await parseResult.InvokeAsync();
        Assert.That(exitCode, Is.EqualTo(0));

        var expectedDir = string.IsNullOrEmpty(expectedSubPath)
            ? packagePath
            : Path.Combine(new[] { packagePath }.Concat(expectedSubPath.Split('/')).ToArray());
        var expectedFile = Path.Combine(expectedDir, $"scenario_one{ext}");
        Assert.That(File.Exists(expectedFile), Is.True, $"Expected file at {expectedFile}");
    }

    [Test]
    public async Task GenerateSamples_ModelOption_PassedToMicroagent()
    {
        var (_, packagePath) = await CreateFakeGoPackageAsync();
        Microagent<List<GeneratedSample>>? captured = null;
        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .Callback<Microagent<List<GeneratedSample>>, CancellationToken>((agent, _) => captured = agent)
            .ReturnsAsync(new List<GeneratedSample> { new("sample_one", "package main\nfunc main(){}") });

        var command = tool.GetCommandInstances().First();
        var parseResult = command.Parse(["generate", "--prompt", "One", "--package-path", packagePath, "--model", "custom-model"]);
        int exitCode = await parseResult.InvokeAsync();
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(captured, Is.Not.Null);
        });

        Assert.That(captured!.Model, Is.EqualTo("custom-model"));
    }

    [Test]
    public async Task GenerateSamples_PromptFilePath_LoadsFileContent()
    {
        var (repoRoot, packagePath) = await CreateFakeGoPackageAsync();
        var promptFile = Path.Combine(repoRoot, "prompt.md");
        File.WriteAllText(promptFile, "# Scenarios\n1) Create key\n2) List keys\n");

        Microagent<List<GeneratedSample>>? captured = null;
        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .Callback<Microagent<List<GeneratedSample>>, CancellationToken>((agent, _) => captured = agent)
            .ReturnsAsync(new List<GeneratedSample> { new("create_key", "package main\nfunc main(){}") });

        var command = tool.GetCommandInstances().First();
        var parseResult = command.Parse(["generate", "--prompt", promptFile, "--package-path", packagePath]);
        int exitCode = await parseResult.InvokeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(captured, Is.Not.Null, "Microagent should have been invoked");
        });

        Assert.That(captured!.Instructions, Does.Contain("Create key"));
        Assert.That(captured.Instructions, Does.Contain("List keys"));

        var sampleFile = Path.Combine(packagePath, "create_key.go");
        Assert.That(File.Exists(sampleFile), Is.True, $"Expected generated sample file '{sampleFile}'");
    }

    [Test]
    public async Task GenerateSamples_PromptFilePath_Nonexistent_FallsBackToRawText()
    {
        var (repoRoot, packagePath) = await CreateFakeGoPackageAsync();
        var missingPromptPath = Path.Combine(repoRoot, "nonexistent-prompt.md");

        Microagent<List<GeneratedSample>>? captured = null;
        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .Callback<Microagent<List<GeneratedSample>>, CancellationToken>((agent, _) => captured = agent)
            .ReturnsAsync(new List<GeneratedSample> { new("scenario", "package main\nfunc main(){}") });

        var command = tool.GetCommandInstances().First();
        var parseResult = command.Parse(["generate", "--prompt", missingPromptPath, "--package-path", packagePath]);
        int exitCode = await parseResult.InvokeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(captured, Is.Not.Null);
        });

        Assert.That(captured!.Instructions, Does.Contain(missingPromptPath));
    }

    [Test]
    public async Task GenerateSamples_SanitizesFileName()
    {
        var (_, packagePath) = await CreateFakeGoPackageAsync();
        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GeneratedSample> { new("nested/folder/sample-name", "package main\nfunc main(){}") });

        var command = tool.GetCommandInstances().First();
        var parseResult = command.Parse(["generate", "--prompt", "Do thing", "--package-path", packagePath]);
        int exitCode = await parseResult.InvokeAsync();
        Assert.That(exitCode, Is.EqualTo(0));

        var expectedFile = Path.Combine(packagePath, "nested_folder_sample-name.go");
        Assert.That(File.Exists(expectedFile), Is.True, $"Expected sanitized file '{expectedFile}' to be created");
    }

    [Test]
    public async Task GenerateSamples_SkipsInvalidSamples()
    {
        var (_, packagePath) = await CreateFakeGoPackageAsync();

        var samples = new List<GeneratedSample>
        {
            new("", "package main\nfunc main(){}"),
            new("valid", ""),
            new("ok_sample", "package main\nfunc main(){ println(\"hi\") }")
        };
        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(samples);

        var command = tool.GetCommandInstances().First();
        var parseResult = command.Parse(["generate", "--prompt", "Do thing", "--package-path", packagePath]);
        int exitCode = await parseResult.InvokeAsync();
        Assert.That(exitCode, Is.EqualTo(0));

        var skippedEmptyName = Path.Combine(packagePath, ".go");
        Assert.That(File.Exists(skippedEmptyName), Is.False, "File with empty name should not be created");
        var skippedEmptyContent = Path.Combine(packagePath, "valid.go");
        Assert.That(File.Exists(skippedEmptyContent), Is.False, "File with empty content should not be created");
        var createdValid = Path.Combine(packagePath, "ok_sample.go");
        Assert.That(File.Exists(createdValid), Is.True, "Valid sample should be written");
        var text = await File.ReadAllTextAsync(createdValid);
        Assert.That(text, Does.Contain("println"));
    }

    [Test]
    public async Task GenerateSamples_PackageInfoResolverReturnsNull_ReturnsError()
    {
        using var tempDir = TempDirectory.Create("null-resolver-repo");
        var pkgPath = Path.Combine(tempDir.DirectoryPath, "sdk", "group", "service", "pkg");
        Directory.CreateDirectory(pkgPath);

        var emptyToolLogger = new TestLogger<SampleGeneratorTool>();
        var errorTool = new SampleGeneratorTool(microagentHostServiceMock.Object, emptyToolLogger, _mockGitHelper.Object, []);
        errorTool.Initialize(_outputHelper, telemetryServiceMock.Object, new MockUpgradeService());
        var command = errorTool.GetCommandInstances().First();
        var parseResult = command.Parse(["generate", "--prompt", "Anything", "--package-path", pkgPath]);
        int exitCode = await parseResult.InvokeAsync();
        Assert.That(exitCode, Is.EqualTo(1));
        var error = _outputHelper.Outputs.FirstOrDefault(o => o.Stream == OutputHelper.StreamType.Stdout || o.Stream == OutputHelper.StreamType.Stderr).Output;
        Assert.That(error, Does.Contain("validation errors"));
        Assert.That(error, Does.Contain("Unable to determine language"));
    }

    [Test]
    public async Task GenerateSamples_DefaultModelUsedWhenNotSpecified()
    {
        var (_, packagePath) = await CreateFakeGoPackageAsync();
        Microagent<List<GeneratedSample>>? captured = null;
        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .Callback<Microagent<List<GeneratedSample>>, CancellationToken>((agent, _) => captured = agent)
            .ReturnsAsync(new List<GeneratedSample> { new("one", "package main\nfunc main(){}") });
        var command = tool.GetCommandInstances().First();
        var parseResult = command.Parse(["generate", "--prompt", "Scenario", "--package-path", packagePath]);
        int exitCode = await parseResult.InvokeAsync();
        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Model, Is.EqualTo("gpt-4.1"));
    }

    [Test]
    public async Task GenerateSamples_MultilinePrompt_NotTreatedAsFile()
    {
        var (_, packagePath) = await CreateFakeGoPackageAsync();
        var multilinePrompt = "First line\nSecond line";
        Microagent<List<GeneratedSample>>? captured = null;
        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .Callback<Microagent<List<GeneratedSample>>, CancellationToken>((agent, _) => captured = agent)
            .ReturnsAsync(new List<GeneratedSample> { new("multi", "package main\nfunc main(){}`") });
        var command = tool.GetCommandInstances().First();
        var parseResult = command.Parse(["generate", "--prompt", multilinePrompt, "--package-path", packagePath]);
        int exitCode = await parseResult.InvokeAsync();
        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Instructions, Does.Contain(multilinePrompt));
    }

    [SetUp]
    public void Setup()
    {
        logger = new TestLogger<SampleGeneratorTool>();
        _outputHelper = new OutputHelper();
        microagentHostServiceMock = new Mock<IMicroagentHostService>();
        copilotAgentRunnerMock = new Mock<ICopilotAgentRunner>();
        telemetryServiceMock = new Mock<ITelemetryService>();
        _mockNpxHelper = new Mock<INpxHelper>();
        _mockPowerShellHelper = new Mock<IPowershellHelper>();
        _mockProcessHelper = new Mock<IProcessHelper>();
        _mockPythonHelper = new Mock<IPythonHelper>();
        _mockGitHelper = new Mock<IGitHelper>();
        _logger = new TestLogger<SdkBuildTool>();
        _mockGoLanguageService = new Mock<LanguageService>();
        _commonValidationHelpers = new Mock<ICommonValidationHelpers>();

        var languageLogger = new TestLogger<LanguageService>();
        var gitLogger = new TestLogger<GitHelper>();
        var gitHubServiceMock = new Mock<IGitHubService>();
        var gitCommandHelper = new GitCommandHelper(NullLogger<GitCommandHelper>.Instance, Mock.Of<IRawOutputHelper>());
        realGitHelper = new GitHelper(gitHubServiceMock.Object, gitCommandHelper, gitLogger);
        var packageInfoHelper = new PackageInfoHelper(new TestLogger<PackageInfoHelper>(), realGitHelper);
        // Use a real FileHelper instance instead of mock
        var fileHelper = new FileHelper(new TestLogger<FileHelper>());
        _languageServices = [
            new PythonLanguageService(_mockProcessHelper.Object, _mockPythonHelper.Object, _mockNpxHelper.Object, realGitHelper, languageLogger, _commonValidationHelpers.Object, packageInfoHelper, fileHelper, Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>()),
            new JavaLanguageService(_mockProcessHelper.Object, realGitHelper, new Mock<IMavenHelper>().Object, copilotAgentRunnerMock.Object, languageLogger, _commonValidationHelpers.Object, packageInfoHelper, fileHelper, Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>()),
            new JavaScriptLanguageService(_mockProcessHelper.Object, _mockNpxHelper.Object, realGitHelper, languageLogger, _commonValidationHelpers.Object, packageInfoHelper, fileHelper, Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>()),
            _mockGoLanguageService.Object,
            new DotnetLanguageService(_mockProcessHelper.Object, _mockPowerShellHelper.Object, realGitHelper, languageLogger, _commonValidationHelpers.Object, packageInfoHelper, fileHelper, Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>())
        ];

        _mockGoLanguageService.Setup(ls => ls.Language).Returns(SdkLanguage.Go);
        _mockGoLanguageService.Setup(r => r.SampleLanguageContext).Returns(new GoSampleLanguageContext(fileHelper));

        tool = new SampleGeneratorTool(
            microagentHostServiceMock.Object,
            logger,
            _mockGitHelper.Object,
            _languageServices
        );

        tool.Initialize(_outputHelper, telemetryServiceMock.Object, new MockUpgradeService());
    }

    private async Task<(string repoRoot, string packagePath)> CreateFakeGoPackageAsync()
    {
        var tempDir = TempDirectory.Create("azure-sdk-for-go");
        var repoRoot = tempDir.DirectoryPath;
        var relativePath = Path.Combine("sdk", "security", "keys", "azkeys");
        var packagePath = Path.Combine(repoRoot, relativePath);
        Directory.CreateDirectory(packagePath);
        if (!Directory.Exists(Path.Combine(repoRoot, ".git")))
        {
            await GitTestHelper.GitInitAsync(repoRoot);
        }
        File.WriteAllText(Path.Combine(packagePath, "client.go"), "package azkeys\n// minimal source for tests\nfunc noop() {}\n");
        var engScripts = Path.Combine(repoRoot, "eng", "scripts");
        Directory.CreateDirectory(engScripts);
        File.WriteAllText(Path.Combine(engScripts, "Language-Settings.ps1"), "$Language = 'Go'\n");
        _mockGitHelper.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(repoRoot);
        _mockGitHelper.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-go");

        _mockGoLanguageService
            .Setup(m => m.GetPackageInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((packagePath, ct) => Task.FromResult(new PackageInfo()
            {
                RelativePath = relativePath,
                RepoRoot = repoRoot,
                SamplesDirectory = packagePath,
                Language = SdkLanguage.Go,
                PackageName = "sdk/security/keyvault/azkeys",
                PackagePath = packagePath,
                PackageVersion = "1.5.0",
                SdkType = SdkType.Dataplane,
                ServiceName = "keyvault"
            }));

        return (repoRoot, packagePath);
    }
    [Test]
    public async Task GenerateSamples_CreatesFiles()
    {
        var (_, packagePath) = await CreateFakeGoPackageAsync();
        var generatedSamples = new List<GeneratedSample>
        {
            new("retrieve_keys", "package main\nfunc main() { println(\"hi\") }")
        };

        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generatedSamples);

        var command = tool.GetCommandInstances().First();
        var parseResult = command.Parse(["generate", "--prompt", "List keys", "--package-path", packagePath]);
        int exitCode = await parseResult.InvokeAsync();

        Assert.That(exitCode, Is.EqualTo(0), "Command should succeed");

        var expectedFile = Path.Combine(packagePath, "retrieve_keys.go");
        Assert.That(File.Exists(expectedFile), Is.True, $"Expected sample file '{expectedFile}' to be created");
        var content = await File.ReadAllTextAsync(expectedFile);
        Assert.That(content, Does.Contain("println"));
    }

    [Test]
    public async Task GenerateSamples_SkipsExistingWithoutOverwrite()
    {
        var (_, packagePath) = await CreateFakeGoPackageAsync();
        var existingFile = Path.Combine(packagePath, "retrieve_keys.go");
        await File.WriteAllTextAsync(existingFile, "// original\n");

        var generatedSamples = new List<GeneratedSample>
        {
            new("retrieve_keys", "// new content\n")
        };

        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generatedSamples);

        var command = tool.GetCommandInstances().First();
        var parseResult = command.Parse(["generate", "--prompt", "List keys", "--package-path", packagePath]);
        int exitCode = await parseResult.InvokeAsync();

        Assert.That(exitCode, Is.EqualTo(0));
        var finalContent = await File.ReadAllTextAsync(existingFile);
        Assert.That(finalContent, Is.EqualTo("// original\n"), "File should not have been overwritten without --overwrite");
    }

    [Test]
    public async Task GenerateSamples_OverwritesWithFlag()
    {
        var (_, packagePath) = await CreateFakeGoPackageAsync();
        var existingFile = Path.Combine(packagePath, "retrieve_keys.go");
        await File.WriteAllTextAsync(existingFile, "// original\n");

        var generatedSamples = new List<GeneratedSample>
        {
            new("retrieve_keys", "// overwritten\n")
        };

        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generatedSamples);

        var command = tool.GetCommandInstances().First();
        var parseResult = command.Parse(["generate", "--prompt", "List keys", "--package-path", packagePath, "--overwrite"]);
        int exitCode = await parseResult.InvokeAsync();

        Assert.That(exitCode, Is.EqualTo(0));
        var finalContent = await File.ReadAllTextAsync(existingFile);
        Assert.That(finalContent, Is.EqualTo("// overwritten\n"), "File should have been overwritten with --overwrite");
    }

    [Test]
    public async Task GenerateSamples_NoSamplesReturned_NoFilesCreated()
    {
        var (_, packagePath) = await CreateFakeGoPackageAsync();
        var baseline = Directory.GetFiles(packagePath, "*.go").ToHashSet();

        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var command = tool.GetCommandInstances().First();
        var parseResult = command.Parse(["generate", "--prompt", "List keys", "--package-path", packagePath]);
        int exitCode = await parseResult.InvokeAsync();

        Assert.That(exitCode, Is.EqualTo(0));
        var after = Directory.GetFiles(packagePath, "*.go").ToHashSet();
        after.ExceptWith(baseline);
        Assert.That(after, Is.Empty, "No additional sample files should be created when microagent returns empty list");
    }

    [Test]
    public async Task HandleCommand_InvalidPackagePath_ReturnsError()
    {
        using var invalidTemp = TempDirectory.Create("invalid-sample-gen");
        var invalidPath = invalidTemp.DirectoryPath;

        var command = tool.GetCommandInstances().First();
        var parseResult = command.Parse(["generate", "--prompt", "List keys", "--package-path", invalidPath]);
        int exitCode = await parseResult.InvokeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(_outputHelper.Outputs.Count, Is.GreaterThan(0));
        });
        var error = _outputHelper.Outputs.FirstOrDefault(o => o.Stream == OutputHelper.StreamType.Stdout || o.Stream == OutputHelper.StreamType.Stderr).Output;
        Assert.That(error, Does.Contain("Unable to determine language for package"));
    }

    [Test]
    public async Task GenerateSamples_MissingPromptOption_ShowsError()
    {
        // Arrange: create a valid package path but omit --prompt (required)
        var (_, packagePath) = await CreateFakeGoPackageAsync();
        var command = tool.GetCommandInstances().First();

        // Act: parse without required --prompt
        var parseResult = command.Parse(["generate", "--package-path", packagePath]);

        // Assert: parser/tool should fail with non-zero exit code
        Assert.That(parseResult.Errors, Is.Not.Empty, "Expected parse errors when required --prompt option is missing");
    }

    [Test]
    public async Task GenerateSamples_PopulatesTelemetryFields()
    {
        // Arrange
        var (_, packagePath) = await CreateFakeGoPackageAsync();
        var generatedSamples = new List<GeneratedSample>
        {
            new("sample_one", "package main\nfunc main(){}"),
            new("sample_two", "package main\nfunc main(){}")
        };

        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generatedSamples);

        // Act
        var response = await tool.GenerateSamplesAsync(packagePath, "Generate samples", overwrite: false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.Language, Is.EqualTo(SdkLanguage.Go), "Language should be set");
            Assert.That(response.PackageName, Is.EqualTo("sdk/security/keyvault/azkeys"), "Package name should be set");
            Assert.That(response.PackageType, Is.EqualTo(SdkType.Dataplane), "Package type should be set");
            Assert.That(response.Version, Is.EqualTo("1.5.0"), "Version should be set");

            // Verify samples_count is in Result as an anonymous type
            Assert.That(response.Result, Is.Not.Null, "Result should not be null");
            var json = System.Text.Json.JsonSerializer.Serialize(response.Result);
            Assert.That(json, Does.Contain("\"samples_count\":2"), "Result should contain samples_count with value 2");
        });
    }
}
