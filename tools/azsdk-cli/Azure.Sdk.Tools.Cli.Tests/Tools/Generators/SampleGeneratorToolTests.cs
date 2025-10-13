// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Moq;
using Azure.Sdk.Tools.Cli.Telemetry;
using Azure.Sdk.Tools.Cli.SampleGeneration;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Generators;

[TestFixture]
public class SampleGeneratorToolTests
{
    private TestLogger<SampleGeneratorTool> logger;
    private OutputHelper _outputHelper;
    private Mock<IMicroagentHostService> microagentHostServiceMock;
    private SampleGeneratorTool tool;
    private Mock<ITelemetryService> telemetryServiceMock;


    [Test]
    public void GenerateSamples_MultiScenarioPrompt_ProducesMultipleFiles()
    {
        var (_, packagePath) = CreateFakeGoPackage();
        var generatedSamples = new List<GeneratedSample>
        {
            new("create_key", "package main\nfunc main(){}"),
            new("list_keys", "package main\nfunc main(){}")
        };

        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generatedSamples);

        var command = tool.GetCommandInstances().First();
        int exitCode = command.Invoke($"--prompt \"1) Create key 2) List keys\" --package-path {packagePath}");
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(File.Exists(Path.Combine(packagePath, "create_key.go")), Is.True);
            Assert.That(File.Exists(Path.Combine(packagePath, "list_keys.go")), Is.True);
        });

    }

    [TestCase("dotnet", "tests/samples", ".cs")]
    [TestCase("java", "src/samples/java", ".java")]
    [TestCase("typescript", "samples-dev", ".ts")]
    [TestCase("python", "samples", ".py")]
    [TestCase("go", "", ".go")] // go uses root
    public void GenerateSamples_LanguageSpecificOutputPath(string language, string expectedSubPath, string ext)
    {
        // Arrange fake repo structure for each language (ensure depth sdk/group/service/package)
        var repoRoot = Directory.CreateTempSubdirectory($"sample-gen-{language}").FullName;
        var packagePath = Path.Combine(repoRoot, "sdk", "group", "service", "pkg");
        Directory.CreateDirectory(packagePath);

        switch (language)
        {
            case "dotnet":
                Directory.CreateDirectory(Path.Combine(packagePath, "src"));
                File.WriteAllText(Path.Combine(packagePath, "src", "Foo.cs"), "namespace Foo; class Bar {}");
                File.WriteAllText(Path.Combine(packagePath, "pkg.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
                break;
            case "java":
                var javaSrc = Path.Combine(packagePath, "src", "main", "java", "com", "example");
                Directory.CreateDirectory(javaSrc);
                File.WriteAllText(Path.Combine(javaSrc, "Foo.java"), "package com.example; class Foo {}");
                File.WriteAllText(Path.Combine(packagePath, "pom.xml"), "<project><modelVersion>4.0.0</modelVersion><groupId>com.example</groupId><artifactId>foo</artifactId><version>1.0.0</version></project>");
                break;
            case "typescript":
                Directory.CreateDirectory(Path.Combine(packagePath, "src"));
                File.WriteAllText(Path.Combine(packagePath, "src", "index.ts"), "export const x=1;");
                File.WriteAllText(Path.Combine(packagePath, "package.json"), "{\n  \"name\": \"@azure/testpkg\",\n  \"version\": \"1.0.0\"\n}");
                break;
            case "python":
                Directory.CreateDirectory(Path.Combine(packagePath, "azure"));
                File.WriteAllText(Path.Combine(packagePath, "azure", "__init__.py"), "# init");
                File.WriteAllText(Path.Combine(packagePath, "setup.py"), "from setuptools import setup; setup(name='pkg', version='0.0.1')");
                break;
            case "go":
                File.WriteAllText(Path.Combine(packagePath, "client.go"), "package pkg\nfunc noop(){}");
                File.WriteAllText(Path.Combine(packagePath, "go.mod"), "module example.com/pkg\n\ngo 1.20");
                break;
        }

        var generatedSamples = new List<GeneratedSample> { new("scenario_one", $"// sample {language}") };
        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generatedSamples);

        // Replace resolver to return correct IPackageInfo
        var resolverMock = new Mock<ILanguageSpecificResolver<IPackageInfo>>();
        resolverMock.Setup(r => r.Resolve(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => language switch
        {
            "dotnet" => new DotNetPackageInfo(),
            "java" => new JavaPackageInfo(),
            "python" => new PythonPackageInfo(),
            "typescript" => new TypeScriptPackageInfo(),
            "go" => new GoPackageInfo(),
            _ => null
        });
    var sampleCtxResolverMock = new Mock<ILanguageSpecificResolver<ISampleLanguageContext>>();
    sampleCtxResolverMock.Setup(r => r.Resolve(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => language switch
    {
        "dotnet" => new DotNetSampleLanguageContext(),
        "java" => new JavaSampleLanguageContext(),
        "python" => new PythonSampleLanguageContext(),
        "typescript" => new TypeScriptSampleLanguageContext(),
        "go" => new GoSampleLanguageContext(),
    });
    tool = new SampleGeneratorTool(microagentHostServiceMock.Object, logger, resolverMock.Object, sampleCtxResolverMock.Object);
        tool.Initialize(_outputHelper, telemetryServiceMock.Object);
        var command = tool.GetCommandInstances().First();
        int exitCode = command.Invoke($"--prompt \"Do thing\" --package-path {packagePath}");
        Assert.That(exitCode, Is.EqualTo(0));

        var expectedDir = string.IsNullOrEmpty(expectedSubPath)
            ? packagePath
            : Path.Combine(new[] { packagePath }.Concat(expectedSubPath.Split('/')).ToArray());
        var expectedFile = Path.Combine(expectedDir, $"scenario_one{ext}");
        Assert.That(File.Exists(expectedFile), Is.True, $"Expected file at {expectedFile}");
    }

    [Test]
    public void GenerateSamples_ModelOption_PassedToMicroagent()
    {
        var (_, packagePath) = CreateFakeGoPackage();
        Microagent<List<GeneratedSample>>? captured = null;
        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .Callback<Microagent<List<GeneratedSample>>, CancellationToken>((agent, _) => captured = agent)
            .ReturnsAsync(new List<GeneratedSample> { new("sample_one", "package main\nfunc main(){}") });

        var command = tool.GetCommandInstances().First();
        int exitCode = command.Invoke($"--prompt \"One\" --package-path {packagePath} --model custom-model");
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(captured, Is.Not.Null);
        });

        Assert.That(captured!.Model, Is.EqualTo("custom-model"));
    }

    [Test]
    public void GenerateSamples_PromptFilePath_LoadsFileContent()
    {
        var (_, packagePath) = CreateFakeGoPackage();
        // Create a temporary markdown file that describes scenarios
        var promptFile = Path.Combine(Path.GetTempPath(), $"prompt-{Guid.NewGuid():N}.md");
        File.WriteAllText(promptFile, "# Scenarios\n1) Create key\n2) List keys\n");

        Microagent<List<GeneratedSample>>? captured = null;
        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .Callback<Microagent<List<GeneratedSample>>, CancellationToken>((agent, _) => captured = agent)
            .ReturnsAsync(new List<GeneratedSample> { new("create_key", "package main\nfunc main(){}") });

        var command = tool.GetCommandInstances().First();
        int exitCode = command.Invoke($"--prompt {promptFile} --package-path {packagePath}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(captured, Is.Not.Null, "Microagent should have been invoked");
        });

        // Verify the enhanced prompt includes content from the markdown file
        Assert.That(captured!.Instructions, Does.Contain("Create key"));
        Assert.That(captured.Instructions, Does.Contain("List keys"));

        // Verify generated sample file was created using returned sample
        var sampleFile = Path.Combine(packagePath, "create_key.go");
        Assert.That(File.Exists(sampleFile), Is.True, $"Expected generated sample file '{sampleFile}'");
    }

    [Test]
    public void GenerateSamples_PromptFilePath_Nonexistent_FallsBackToRawText()
    {
        var (_, packagePath) = CreateFakeGoPackage();
        var missingPromptPath = Path.Combine(Path.GetTempPath(), $"noexist-{Guid.NewGuid():N}.md"); // do not create file

        Microagent<List<GeneratedSample>>? captured = null;
        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .Callback<Microagent<List<GeneratedSample>>, CancellationToken>((agent, _) => captured = agent)
            .ReturnsAsync(new List<GeneratedSample> { new("scenario", "package main\nfunc main(){}") });

        var command = tool.GetCommandInstances().First();
        int exitCode = command.Invoke($"--prompt {missingPromptPath} --package-path {packagePath}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(captured, Is.Not.Null);
        });

        // Should fall back to raw prompt text (the path string itself)
        Assert.That(captured!.Instructions, Does.Contain(missingPromptPath));
    }

    [Test]
    public void GenerateSamples_SanitizesFileName()
    {
        var (_, packagePath) = CreateFakeGoPackage();
        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GeneratedSample> { new("nested/folder/sample-name", "package main\nfunc main(){}") });

        var command = tool.GetCommandInstances().First();
        int exitCode = command.Invoke($"--prompt \"Do thing\" --package-path {packagePath}");
        Assert.That(exitCode, Is.EqualTo(0));

        // Expect slashes replaced by underscores and proper .go extension
        var expectedFile = Path.Combine(packagePath, "nested_folder_sample-name.go");
        Assert.That(File.Exists(expectedFile), Is.True, $"Expected sanitized file '{expectedFile}' to be created");
    }

    [Test]
    public async Task GenerateSamples_SkipsInvalidSamples()
    {
        var (_, packagePath) = CreateFakeGoPackage();
        var samples = new List<GeneratedSample>
        {
            new("", "package main\nfunc main(){}"), // empty filename -> skip
            new("valid", ""), // empty content -> skip
            new("ok_sample", "package main\nfunc main(){ println(\"hi\") }") // valid
        };
        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(samples);

        var command = tool.GetCommandInstances().First();
        int exitCode = command.Invoke($"--prompt \"Do thing\" --package-path {packagePath}");
        Assert.That(exitCode, Is.EqualTo(0));

        var skippedEmptyName = Path.Combine(packagePath, ".go"); // would have been name .go if not skipped
        Assert.That(File.Exists(skippedEmptyName), Is.False, "File with empty name should not be created");
        var skippedEmptyContent = Path.Combine(packagePath, "valid.go");
        Assert.That(File.Exists(skippedEmptyContent), Is.False, "File with empty content should not be created");
        var createdValid = Path.Combine(packagePath, "ok_sample.go");
        Assert.That(File.Exists(createdValid), Is.True, "Valid sample should be written");
        var text = await File.ReadAllTextAsync(createdValid);
        Assert.That(text, Does.Contain("println"));
    }

    [Test]
    public void GenerateSamples_PackageInfoResolverReturnsNull_ReturnsError()
    {
        var tempDir = Directory.CreateTempSubdirectory("null-resolver-repo").FullName;
        var pkgPath = Path.Combine(tempDir, "sdk", "group", "service", "pkg");
        Directory.CreateDirectory(pkgPath);

        var nullResolver = new Mock<ILanguageSpecificResolver<IPackageInfo>>();
        nullResolver.Setup(r => r.Resolve(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((IPackageInfo?)null);
        var sampleCtxResolver = new Mock<ILanguageSpecificResolver<ISampleLanguageContext>>();
        sampleCtxResolver.Setup(r => r.Resolve(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new GoSampleLanguageContext());

        var errorTool = new SampleGeneratorTool(microagentHostServiceMock.Object, logger, nullResolver.Object, sampleCtxResolver.Object);
        errorTool.Initialize(_outputHelper, telemetryServiceMock.Object);
        var command = errorTool.GetCommandInstances().First();
        int exitCode = command.Invoke($"--prompt \"Anything\" --package-path {pkgPath}");
        Assert.That(exitCode, Is.EqualTo(1));
    var error = _outputHelper.Outputs.FirstOrDefault(o => o.Stream == OutputHelper.StreamType.Stdout || o.Stream == OutputHelper.StreamType.Stderr).Output;
        Assert.That(error, Does.Contain("validation errors"));
        Assert.That(error, Does.Contain("Unable to determine language"));
    }

    [Test]
    public void GenerateSamples_DefaultModelUsedWhenNotSpecified()
    {
        var (_, packagePath) = CreateFakeGoPackage();
        Microagent<List<GeneratedSample>>? captured = null;
        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .Callback<Microagent<List<GeneratedSample>>, CancellationToken>((agent, _) => captured = agent)
            .ReturnsAsync(new List<GeneratedSample> { new("one", "package main\nfunc main(){}") });
        var command = tool.GetCommandInstances().First();
        int exitCode = command.Invoke($"--prompt \"Scenario\" --package-path {packagePath}");
        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Model, Is.EqualTo("gpt-4.1"));
    }

    [Test]
    public void GenerateSamples_MultilinePrompt_NotTreatedAsFile()
    {
        var (_, packagePath) = CreateFakeGoPackage();
        var multilinePrompt = "First line\nSecond line"; // contains newline -> should not be interpreted as file path
        Microagent<List<GeneratedSample>>? captured = null;
        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .Callback<Microagent<List<GeneratedSample>>, CancellationToken>((agent, _) => captured = agent)
            .ReturnsAsync(new List<GeneratedSample> { new("multi", "package main\nfunc main(){}`") });
        var command = tool.GetCommandInstances().First();
        int exitCode = command.Invoke($"--prompt \"{multilinePrompt}\" --package-path {packagePath}");
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
    telemetryServiceMock = new Mock<ITelemetryService>();
        var packageInfoResolverMock = new Mock<ILanguageSpecificResolver<IPackageInfo>>();
        packageInfoResolverMock
            .Setup(r => r.Resolve(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string p, CancellationToken _) => new GoPackageInfo()); // defer Init to tool
    var sampleCtxResolverMock = new Mock<ILanguageSpecificResolver<ISampleLanguageContext>>();
    sampleCtxResolverMock.Setup(r => r.Resolve(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new GoSampleLanguageContext());
        tool = new SampleGeneratorTool(
            microagentHostServiceMock.Object,
            logger,
            packageInfoResolverMock.Object,
            sampleCtxResolverMock.Object
        );

        tool.Initialize(_outputHelper, telemetryServiceMock.Object);
    }

        private static (string repoRoot, string packagePath) CreateFakeGoPackage()
    {
        var repoRoot = Directory.CreateTempSubdirectory("sample-gen-repo").FullName;
            var packagePath = Path.Combine(repoRoot, "sdk", "security", "keys", "azkeys");
        Directory.CreateDirectory(packagePath);
        File.WriteAllText(Path.Combine(packagePath, "client.go"), "package azkeys\n// minimal source for tests\nfunc noop() {}\n");
        // Add Language-Settings.ps1 for Go detection
        var engScripts = Path.Combine(repoRoot, "eng", "scripts");
        Directory.CreateDirectory(engScripts);
        File.WriteAllText(Path.Combine(engScripts, "Language-Settings.ps1"), "$Language = 'Go'\n");
        return (repoRoot, packagePath);
    }

    [TearDown]
    public void TearDown()
    {
        // Best-effort cleanup for any temp repos created in tests
    }

    [Test]
    public async Task GenerateSamples_CreatesFiles()
    {
        var (_, packagePath) = CreateFakeGoPackage();
        var generatedSamples = new List<GeneratedSample>
        {
            new("retrieve_keys", "package main\nfunc main() { println(\"hi\") }")
        };

        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generatedSamples);

        var command = tool.GetCommandInstances().First();
    int exitCode = command.Invoke($"--prompt \"List keys\" --package-path {packagePath}");

        Assert.That(exitCode, Is.EqualTo(0), "Command should succeed");

        var expectedFile = Path.Combine(packagePath, "retrieve_keys.go"); // go uses package root as samples dir
        Assert.That(File.Exists(expectedFile), Is.True, $"Expected sample file '{expectedFile}' to be created");
        var content = await File.ReadAllTextAsync(expectedFile);
        Assert.That(content, Does.Contain("println"));
    }

    [Test]
    public async Task GenerateSamples_SkipsExistingWithoutOverwrite()
    {
        var (_, packagePath) = CreateFakeGoPackage();
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
    int exitCode = command.Invoke($"--prompt \"List keys\" --package-path {packagePath}");

        Assert.That(exitCode, Is.EqualTo(0));
        var finalContent = await File.ReadAllTextAsync(existingFile);
        Assert.That(finalContent, Is.EqualTo("// original\n"), "File should not have been overwritten without --overwrite");
    }

    [Test]
    public async Task GenerateSamples_OverwritesWithFlag()
    {
        var (_, packagePath) = CreateFakeGoPackage();
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
    int exitCode = command.Invoke($"--prompt \"List keys\" --package-path {packagePath} --overwrite");

        Assert.That(exitCode, Is.EqualTo(0));
        var finalContent = await File.ReadAllTextAsync(existingFile);
        Assert.That(finalContent, Is.EqualTo("// overwritten\n"), "File should have been overwritten with --overwrite");
    }

    [Test]
    public void GenerateSamples_NoSamplesReturned_NoFilesCreated()
    {
        var (_, packagePath) = CreateFakeGoPackage();
        var baseline = Directory.GetFiles(packagePath, "*.go").ToHashSet();

        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var command = tool.GetCommandInstances().First();
    int exitCode = command.Invoke($"--prompt \"List keys\" --package-path {packagePath}");

        Assert.That(exitCode, Is.EqualTo(0));
        var after = Directory.GetFiles(packagePath, "*.go").ToHashSet();
        after.ExceptWith(baseline);
        Assert.That(after, Is.Empty, "No additional sample files should be created when microagent returns empty list");
    }

    [Test]
    public void HandleCommand_InvalidPackagePath_ReturnsError()
    {
        var invalidPath = Directory.CreateTempSubdirectory("invalid-sample-gen").FullName; // missing sdk segment

        var command = tool.GetCommandInstances().First();
    int exitCode = command.Invoke($"--prompt \"List keys\" --package-path {invalidPath}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(_outputHelper.Outputs.Count, Is.GreaterThan(0));
        });
        var error = _outputHelper.Outputs.FirstOrDefault(o => o.Stream == OutputHelper.StreamType.Stdout || o.Stream == OutputHelper.StreamType.Stderr).Output;
    Assert.That(error, Does.Contain("not under an Azure SDK repository"));
    }
}
