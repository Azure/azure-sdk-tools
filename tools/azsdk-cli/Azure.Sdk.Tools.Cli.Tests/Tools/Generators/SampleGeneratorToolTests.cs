using System.CommandLine;
using System.CommandLine.Parsing;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Moq;
using Azure.Sdk.Tools.Cli.Telemetry;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Generators;

[TestFixture]
public class SampleGeneratorToolTests
{
    private TestLogger<SampleGeneratorTool> logger;
    private OutputHelper _outputHelper;
    private Mock<IMicroagentHostService> microagentHostServiceMock;
    private Mock<ILanguageSpecificCheckResolver> languageResolverMock;
    private SampleGeneratorTool tool;
    private Mock<ITelemetryService> telemetryServiceMock;

    private class TestLanguageChecks(string language) : ILanguageSpecificChecks
    {
        public string SupportedLanguage { get; } = language;
    }

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
        // Arrange fake repo structure for each language
        var repoRoot = Directory.CreateTempSubdirectory($"sample-gen-{language}").FullName;
        var packagePath = Path.Combine(repoRoot, "sdk", "service", "pkg");
        Directory.CreateDirectory(packagePath);

        // Add minimal source files & required directory layout per language so context loads
        switch (language)
        {
            case "dotnet":
                Directory.CreateDirectory(Path.Combine(packagePath, "src"));
                File.WriteAllText(Path.Combine(packagePath, "src", "Foo.cs"), "namespace Foo; class Bar {}");
                break;
            case "java":
                Directory.CreateDirectory(Path.Combine(packagePath, "src"));
                File.WriteAllText(Path.Combine(packagePath, "src", "Foo.java"), "class Foo {}");
                break;
            case "typescript":
                Directory.CreateDirectory(Path.Combine(packagePath, "src"));
                File.WriteAllText(Path.Combine(packagePath, "src", "index.ts"), "export const x=1;");
                break;
            case "python":
                Directory.CreateDirectory(Path.Combine(packagePath, "azure"));
                File.WriteAllText(Path.Combine(packagePath, "azure", "__init__.py"), "# init");
                break;
            case "go":
                File.WriteAllText(Path.Combine(packagePath, "client.go"), "package pkg\nfunc noop(){}");
                break;
        }

        languageResolverMock
            .Setup(r => r.GetLanguageCheckAsync(It.IsAny<string>()))
            .ReturnsAsync(new TestLanguageChecks(language));

        var generatedSamples = new List<GeneratedSample>
        {
            new("scenario_one", $"// sample {language}")
        };
        microagentHostServiceMock
            .Setup(m => m.RunAgentToCompletion(It.IsAny<Microagent<List<GeneratedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generatedSamples);

        var command = tool.GetCommandInstances().First();
        int exitCode = command.Invoke($"--prompt \"Do thing\" --package-path {packagePath} --language {language}");
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
    public void GenerateSamples_UnsupportedLanguageViaOption_ReturnsError()
    {
        var (_, packagePath) = CreateFakeGoPackage();

        // Force language resolver to still report go (unused because we supply --language)
        languageResolverMock
            .Setup(r => r.GetLanguageCheckAsync(It.IsAny<string>()))
            .ReturnsAsync(new TestLanguageChecks("go"));

        var command = tool.GetCommandInstances().First();
        int exitCode = command.Invoke($"--prompt \"One\" --package-path {packagePath} --language invalidlang");

        Assert.That(exitCode, Is.EqualTo(1));
        var combined = string.Join("\n", _outputHelper.Outputs.Select(o => o.Output));
        Assert.That(combined, Does.Contain("Unsupported language"));
    }

    [SetUp]
    public void Setup()
    {
        logger = new TestLogger<SampleGeneratorTool>();
        _outputHelper = new OutputHelper();
        microagentHostServiceMock = new Mock<IMicroagentHostService>();
        languageResolverMock = new Mock<ILanguageSpecificCheckResolver>();
    telemetryServiceMock = new Mock<ITelemetryService>();

        // Default language: go (simplest output directory logic)
        languageResolverMock
            .Setup(r => r.GetLanguageCheckAsync(It.IsAny<string>()))
            .ReturnsAsync(new TestLanguageChecks("go"));

        tool = new SampleGeneratorTool(
            microagentHostServiceMock.Object,
            logger,
            _outputHelper,
            languageResolverMock.Object
        );

        tool.Initialize(_outputHelper, telemetryServiceMock.Object);
    }

    private static (string repoRoot, string packagePath) CreateFakeGoPackage()
    {
        var repoRoot = Directory.CreateTempSubdirectory("sample-gen-repo").FullName;
        var packagePath = Path.Combine(repoRoot, "sdk", "security", "azkeys");
        Directory.CreateDirectory(packagePath);
        File.WriteAllText(Path.Combine(packagePath, "client.go"), "package azkeys\n// minimal source for tests\nfunc noop() {}\n");
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
