// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Services.Languages.Samples;
using Azure.Sdk.Tools.Cli.Telemetry;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tools.Package.Samples;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package.Samples;

[TestFixture]
public class SampleTranslatorToolTests
{
    private TestLogger<SampleTranslatorTool> _logger;
    private Mock<IGitHelper> _mockGitHelper;
    private Mock<ICopilotAgentRunner> _mockCopilotAgentRunner;
    private SampleTranslatorTool _sampleTranslatorTool;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<SampleTranslatorTool>();
        _mockGitHelper = new Mock<IGitHelper>();
        _mockCopilotAgentRunner = new Mock<ICopilotAgentRunner>();

        // Create empty language services list - this will cause the tool to fail language detection
        // which is actually what we want to test since we're focusing on error paths
        var languageServices = new List<LanguageService>();

        _sampleTranslatorTool = new SampleTranslatorTool(
            _mockCopilotAgentRunner.Object,
            _logger,
            _mockGitHelper.Object,
            languageServices);
    }

    [Test]
    public void GetCommand_ReturnsCorrectCommandStructure()
    {
        // Act
        var command = _sampleTranslatorTool.GetCommandInstances().First();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(command.Name, Is.EqualTo("translate"));
            Assert.That(command.Description, Is.EqualTo("Translates sample files from source language to target package language"));
            
            var options = command.Options.ToList();
            Assert.That(options.Count, Is.EqualTo(5)); // from, to, overwrite, model, batch-size

            var fromOption = options.FirstOrDefault(o => o.Name == "--from");
            var toOption = options.FirstOrDefault(o => o.Name == "--to");
            var overwriteOption = options.FirstOrDefault(o => o.Name == "--overwrite");
            var modelOption = options.FirstOrDefault(o => o.Name == "--model");
            var batchSizeOption = options.FirstOrDefault(o => o.Name == "--batch-size");
            
            Assert.That(fromOption, Is.Not.Null);
            Assert.That(fromOption!.Required, Is.True);
            
            Assert.That(toOption, Is.Not.Null);
            Assert.That(toOption!.Required, Is.True);
            
            Assert.That(overwriteOption, Is.Not.Null);
            Assert.That(overwriteOption!.Required, Is.False);
            
            Assert.That(modelOption, Is.Not.Null);
            Assert.That(modelOption!.Required, Is.False);
        });
    }

    [Test]
    public void CommandHierarchy_ReturnsCorrectGroup()
    {
        // Act & Assert
        Assert.That(_sampleTranslatorTool.CommandHierarchy, Contains.Item(SharedCommandGroups.PackageSample));
    }

    [Test]
    public async Task HandleCommand_WithInvalidPaths_ReturnsErrorResponse()
    {
        // Arrange - With no language services, the tool should fail during language detection
        var command = _sampleTranslatorTool.GetCommandInstances().First();
        var parseResult = command.Parse("--from /nonexistent/path --to /another/nonexistent/path");

        // Act
        var result = await _sampleTranslatorTool.HandleCommand(parseResult, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.ResponseError, Is.Not.Null);
            Assert.That(result.ResponseError, Does.Contain("SampleTranslator failed with validation errors"));
            Assert.That(result.ResponseError, Does.Contain("Unable to determine source language"));
        });
    }

    [Test]
    public void ParseCommand_WithAllOptions_ParsesCorrectly()
    {
        // Arrange
        var command = _sampleTranslatorTool.GetCommandInstances().First();
        
        // Act
        var parseResult = command.Parse("--from /source/path --to /target/path --overwrite --model gpt-5");

        // Assert
        Assert.That(parseResult.Errors, Is.Empty);
    }

    [Test] 
    public void ParseCommand_WithRequiredOptionsOnly_ParsesCorrectly()
    {
        // Arrange
        var command = _sampleTranslatorTool.GetCommandInstances().First();
        
        // Act
        var parseResult = command.Parse("--from /source/path --to /target/path");

        // Assert
        Assert.That(parseResult.Errors, Is.Empty);
    }

    [Test]
    public void ParseCommand_WithMissingRequiredOptions_HasErrors()
    {
        // Arrange
        var command = _sampleTranslatorTool.GetCommandInstances().First();
        
        // Act & Assert - Missing both required options
        var parseResult1 = command.Parse("");
        Assert.That(parseResult1.Errors, Is.Not.Empty);

        // Missing --from option
        var parseResult2 = command.Parse("--to /target/path");
        Assert.That(parseResult2.Errors, Is.Not.Empty);

        // Missing --to option  
        var parseResult3 = command.Parse("--from /source/path");
        Assert.That(parseResult3.Errors, Is.Not.Empty);
    }

    [Test]
    public async Task TranslateSamples_PopulatesTelemetryFields()
    {
        // Arrange
        using var sourceTempDir = TempDirectory.Create("azure-sdk-for-go");
        using var targetTempDir = TempDirectory.Create("azure-sdk-for-go");

        var sourceRelativePath = Path.Combine("sdk", "storage", "azstorage");
        var targetRelativePath = Path.Combine("sdk", "security", "keyvault", "azkeys");

        var sourcePkgPath = Path.Combine(sourceTempDir.DirectoryPath, sourceRelativePath);
        var targetPkgPath = Path.Combine(targetTempDir.DirectoryPath, targetRelativePath);

        Directory.CreateDirectory(sourcePkgPath);
        Directory.CreateDirectory(targetPkgPath);

        // Create sample files in the source package
        File.WriteAllText(Path.Combine(sourcePkgPath, "example_upload_test.go"), "package azstorage_test\nfunc Example_upload() {}");
        // Create source files in the target package for context loading
        File.WriteAllText(Path.Combine(targetPkgPath, "client.go"), "package azkeys\nfunc noop() {}");

        var fileHelper = new FileHelper(new TestLogger<FileHelper>());
        var mockGoService = new Mock<LanguageService>();
        mockGoService.Setup(ls => ls.Language).Returns(SdkLanguage.Go);
        mockGoService.Setup(ls => ls.SampleLanguageContext).Returns(new GoSampleLanguageContext(fileHelper));
        mockGoService.Setup(m => m.GetPackageInfo(sourcePkgPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageInfo
            {
                RelativePath = sourceRelativePath,
                RepoRoot = sourceTempDir.DirectoryPath,
                SamplesDirectory = sourcePkgPath,
                Language = SdkLanguage.Go,
                PackageName = "sdk/storage/azstorage",
                PackagePath = sourcePkgPath,
                PackageVersion = "1.0.0",
                SdkType = SdkType.Dataplane,
                ServiceName = "storage"
            });
        mockGoService.Setup(m => m.GetPackageInfo(targetPkgPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageInfo
            {
                RelativePath = targetRelativePath,
                RepoRoot = targetTempDir.DirectoryPath,
                SamplesDirectory = targetPkgPath,
                Language = SdkLanguage.Go,
                PackageName = "sdk/security/keyvault/azkeys",
                PackagePath = targetPkgPath,
                PackageVersion = "1.5.0",
                SdkType = SdkType.Dataplane,
                ServiceName = "keyvault"
            });

        var gitHelperMock = new Mock<IGitHelper>();
        gitHelperMock.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("azure-sdk-for-go");

        var translatedSamples = new List<TranslatedSample>
        {
            new("example_upload_test.go", "example_upload_test.go", "package azkeys_test\nfunc Example_upload() {}")
        };

        _mockCopilotAgentRunner
            .Setup(m => m.RunAsync(It.IsAny<CopilotAgent<List<TranslatedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(translatedSamples);

        var tool = new SampleTranslatorTool(
            _mockCopilotAgentRunner.Object,
            _logger,
            gitHelperMock.Object,
            new List<LanguageService> { mockGoService.Object });

        // Act
        var response = await tool.TranslateSamplesAsync(sourcePkgPath, targetPkgPath, overwrite: false);

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
            Assert.That(json, Does.Contain("\"samples_count\":1"), "Result should contain samples_count with value 1");
        });
    }
}

/// <summary>
/// Tests for directory structure preservation during sample translation.
/// These tests verify that translated samples are written to the same relative
/// subdirectory structure as the source samples.
/// </summary>
[TestFixture]
public class SampleTranslatorDirectoryStructureTests
{
    private TestLogger<SampleTranslatorTool> _logger;
    private OutputHelper _outputHelper;
    private Mock<ICopilotAgentRunner> _copilotAgentRunnerMock;
    private Mock<IGitHelper> _mockGitHelper;
    private Mock<ITelemetryService> _telemetryServiceMock;
    private Mock<LanguageService> _mockSourceLanguageService;
    private Mock<LanguageService> _mockTargetLanguageService;
    private SampleTranslatorTool _tool;
    private List<LanguageService> _languageServices;
    private readonly List<TempDirectory> _tempDirs = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var temp in _tempDirs)
        {
            temp.Dispose();
        }
        _tempDirs.Clear();
    }

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<SampleTranslatorTool>();
        _outputHelper = new OutputHelper();
        _copilotAgentRunnerMock = new Mock<ICopilotAgentRunner>();
        _telemetryServiceMock = new Mock<ITelemetryService>();
        _mockGitHelper = new Mock<IGitHelper>();

        var fileHelper = new FileHelper(new TestLogger<FileHelper>());

        _mockSourceLanguageService = new Mock<LanguageService>();
        _mockSourceLanguageService.Setup(ls => ls.Language).Returns(SdkLanguage.Go);
        _mockSourceLanguageService.Setup(ls => ls.SampleLanguageContext).Returns(new GoSampleLanguageContext(fileHelper));

        _mockTargetLanguageService = new Mock<LanguageService>();
        _mockTargetLanguageService.Setup(ls => ls.Language).Returns(SdkLanguage.Python);
        _mockTargetLanguageService.Setup(ls => ls.SampleLanguageContext).Returns(new PythonSampleLanguageContext(fileHelper));

        _languageServices = [_mockSourceLanguageService.Object, _mockTargetLanguageService.Object];

        _tool = new SampleTranslatorTool(
            _copilotAgentRunnerMock.Object,
            _logger,
            _mockGitHelper.Object,
            _languageServices);
        _tool.Initialize(_outputHelper, _telemetryServiceMock.Object, new MockUpgradeService());
    }

    [Test]
    public async Task TranslateSamples_PreservesSubdirectoryStructure()
    {
        var (sourcePackagePath, targetPackagePath, targetSamplesDir) = await CreateFakePackagesAsync(
            sourceSamples: new Dictionary<string, string>
            {
                ["sync/create_blob.go"] = "package main\nfunc CreateBlob() {}",
                ["async/create_blob.go"] = "package main\nfunc CreateBlobAsync() {}",
            });

        _copilotAgentRunnerMock
            .Setup(m => m.RunAsync(It.IsAny<CopilotAgent<List<TranslatedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TranslatedSample>
            {
                new("sync/create_blob.go", "create_blob.py", "def create_blob(): pass"),
                new("async/create_blob.go", "create_blob_async.py", "async def create_blob(): pass"),
            });

        var result = await _tool.TranslateSamplesAsync(sourcePackagePath, targetPackagePath, overwrite: true);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(File.Exists(Path.Combine(targetSamplesDir, "sync", "create_blob.py")), Is.True,
            "Translated file should be in sync/ subdirectory");
        Assert.That(File.Exists(Path.Combine(targetSamplesDir, "async", "create_blob_async.py")), Is.True,
            "Translated file should be in async/ subdirectory");
    }

    [Test]
    public async Task TranslateSamples_PreservesNestedSubdirectoryStructure()
    {
        var (sourcePackagePath, targetPackagePath, targetSamplesDir) = await CreateFakePackagesAsync(
            sourceSamples: new Dictionary<string, string>
            {
                ["level1/level2/deep_sample.go"] = "package main\nfunc Deep() {}",
            });

        _copilotAgentRunnerMock
            .Setup(m => m.RunAsync(It.IsAny<CopilotAgent<List<TranslatedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TranslatedSample>
            {
                new("level1/level2/deep_sample.go", "deep_sample.py", "def deep(): pass"),
            });

        var result = await _tool.TranslateSamplesAsync(sourcePackagePath, targetPackagePath, overwrite: true);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(File.Exists(Path.Combine(targetSamplesDir, "level1", "level2", "deep_sample.py")), Is.True,
            "Translated file should preserve nested subdirectory structure");
    }

    [Test]
    public async Task TranslateSamples_RootLevelSamples_WrittenToOutputRoot()
    {
        var (sourcePackagePath, targetPackagePath, targetSamplesDir) = await CreateFakePackagesAsync(
            sourceSamples: new Dictionary<string, string>
            {
                ["simple_sample.go"] = "package main\nfunc Simple() {}",
            });

        _copilotAgentRunnerMock
            .Setup(m => m.RunAsync(It.IsAny<CopilotAgent<List<TranslatedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TranslatedSample>
            {
                new("simple_sample.go", "simple_sample.py", "def simple(): pass"),
            });

        var result = await _tool.TranslateSamplesAsync(sourcePackagePath, targetPackagePath, overwrite: true);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(File.Exists(Path.Combine(targetSamplesDir, "simple_sample.py")), Is.True,
            "Root-level sample should be written to target samples directory root");
    }

    [Test]
    public async Task TranslateSamples_SameNameDifferentDirs_BothPreserved()
    {
        var (sourcePackagePath, targetPackagePath, targetSamplesDir) = await CreateFakePackagesAsync(
            sourceSamples: new Dictionary<string, string>
            {
                ["v1/client.go"] = "package v1\nfunc New() {}",
                ["v2/client.go"] = "package v2\nfunc New() {}",
            });

        _copilotAgentRunnerMock
            .Setup(m => m.RunAsync(It.IsAny<CopilotAgent<List<TranslatedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TranslatedSample>
            {
                new("v1/client.go", "client.py", "class ClientV1: pass"),
                new("v2/client.go", "client.py", "class ClientV2: pass"),
            });

        var result = await _tool.TranslateSamplesAsync(sourcePackagePath, targetPackagePath, overwrite: true);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var v1File = Path.Combine(targetSamplesDir, "v1", "client.py");
        var v2File = Path.Combine(targetSamplesDir, "v2", "client.py");
        Assert.That(File.Exists(v1File), Is.True, "v1/client.py should exist");
        Assert.That(File.Exists(v2File), Is.True, "v2/client.py should exist");
        Assert.That(await File.ReadAllTextAsync(v1File), Does.Contain("ClientV1"));
        Assert.That(await File.ReadAllTextAsync(v2File), Does.Contain("ClientV2"));
    }

    [Test]
    public async Task TranslateSamples_FallbackToFilenameLookup_StillWorks()
    {
        var (sourcePackagePath, targetPackagePath, targetSamplesDir) = await CreateFakePackagesAsync(
            sourceSamples: new Dictionary<string, string>
            {
                ["subdir/unique_sample.go"] = "package main\nfunc Unique() {}",
            });

        // AI returns just the filename without the relative path (backward compatibility)
        _copilotAgentRunnerMock
            .Setup(m => m.RunAsync(It.IsAny<CopilotAgent<List<TranslatedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TranslatedSample>
            {
                new("unique_sample.go", "unique_sample.py", "def unique(): pass"),
            });

        var result = await _tool.TranslateSamplesAsync(sourcePackagePath, targetPackagePath, overwrite: true);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(File.Exists(Path.Combine(targetSamplesDir, "subdir", "unique_sample.py")), Is.True,
            "Filename-only fallback should still place file in correct subdirectory");
    }

    [Test]
    public async Task TranslateSamples_AmbiguousFilenameFallback_SkipsSample()
    {
        var (sourcePackagePath, targetPackagePath, targetSamplesDir) = await CreateFakePackagesAsync(
            sourceSamples: new Dictionary<string, string>
            {
                ["v1/client.go"] = "package v1\nfunc New() {}",
                ["v2/client.go"] = "package v2\nfunc New() {}",
            });

        // AI returns just the filename (ambiguous — matches both v1/client.go and v2/client.go)
        _copilotAgentRunnerMock
            .Setup(m => m.RunAsync(It.IsAny<CopilotAgent<List<TranslatedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TranslatedSample>
            {
                new("client.go", "client.py", "class Client: pass"),
            });

        var result = await _tool.TranslateSamplesAsync(sourcePackagePath, targetPackagePath, overwrite: true);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        // The ambiguous sample should be skipped — no file written
        Assert.That(File.Exists(Path.Combine(targetSamplesDir, "v1", "client.py")), Is.False,
            "Ambiguous filename-only match should not write to v1/");
        Assert.That(File.Exists(Path.Combine(targetSamplesDir, "v2", "client.py")), Is.False,
            "Ambiguous filename-only match should not write to v2/");
        Assert.That(File.Exists(Path.Combine(targetSamplesDir, "client.py")), Is.False,
            "Ambiguous filename-only match should not write to root either");
    }

    [Test]
    public async Task TranslateSamples_PromptContainsRelativePaths()
    {
        var (sourcePackagePath, targetPackagePath, _) = await CreateFakePackagesAsync(
            sourceSamples: new Dictionary<string, string>
            {
                ["sync/sample.go"] = "package main\nfunc Sync() {}",
                ["async/sample.go"] = "package main\nfunc Async() {}",
            });

        CopilotAgent<List<TranslatedSample>>? capturedAgent = null;
        _copilotAgentRunnerMock
            .Setup(m => m.RunAsync(It.IsAny<CopilotAgent<List<TranslatedSample>>>(), It.IsAny<CancellationToken>()))
            .Callback<CopilotAgent<List<TranslatedSample>>, CancellationToken>((agent, _) => capturedAgent = agent)
            .ReturnsAsync(new List<TranslatedSample>
            {
                new("sync/sample.go", "sample.py", "def sync(): pass"),
                new("async/sample.go", "sample_async.py", "async def sample(): pass"),
            });

        await _tool.TranslateSamplesAsync(sourcePackagePath, targetPackagePath, overwrite: true);

        Assert.That(capturedAgent, Is.Not.Null, "Copilot agent should have been invoked");
        Assert.That(capturedAgent!.Instructions, Does.Contain($"sync{Path.DirectorySeparatorChar}sample.go").Or.Contains("sync/sample.go"),
            "Prompt should contain relative path including subdirectory for sync sample");
        Assert.That(capturedAgent.Instructions, Does.Contain($"async{Path.DirectorySeparatorChar}sample.go").Or.Contains("async/sample.go"),
            "Prompt should contain relative path including subdirectory for async sample");
    }

    [Test]
    public async Task TranslateSamples_MixedRootAndSubdirSamples_AllPreserved()
    {
        var (sourcePackagePath, targetPackagePath, targetSamplesDir) = await CreateFakePackagesAsync(
            sourceSamples: new Dictionary<string, string>
            {
                ["root_sample.go"] = "package main\nfunc Root() {}",
                ["advanced/nested_sample.go"] = "package main\nfunc Nested() {}",
            });

        _copilotAgentRunnerMock
            .Setup(m => m.RunAsync(It.IsAny<CopilotAgent<List<TranslatedSample>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TranslatedSample>
            {
                new("root_sample.go", "root_sample.py", "def root(): pass"),
                new("advanced/nested_sample.go", "nested_sample.py", "def nested(): pass"),
            });

        var result = await _tool.TranslateSamplesAsync(sourcePackagePath, targetPackagePath, overwrite: true);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(File.Exists(Path.Combine(targetSamplesDir, "root_sample.py")), Is.True,
            "Root-level sample should be in target root");
        Assert.That(File.Exists(Path.Combine(targetSamplesDir, "advanced", "nested_sample.py")), Is.True,
            "Subdirectory sample should preserve its directory");
    }

    /// <summary>
    /// Creates fake source and target package repos for testing translation.
    /// Source package is Go, target package is Python.
    /// </summary>
    private async Task<(string sourcePackagePath, string targetPackagePath, string targetSamplesDir)> CreateFakePackagesAsync(
        Dictionary<string, string> sourceSamples)
    {
        // Source repo (Go)
        var sourceTemp = TempDirectory.Create("azure-sdk-for-go-source");
        _tempDirs.Add(sourceTemp);
        var sourceRepoRoot = sourceTemp.DirectoryPath;
        await GitTestHelper.GitInitAsync(sourceRepoRoot);
        var sourceRelativePath = Path.Combine("sdk", "storage", "azblob");
        var sourcePackagePath = Path.Combine(sourceRepoRoot, sourceRelativePath);
        var sourceSamplesDir = Path.Combine(sourcePackagePath, "samples");
        Directory.CreateDirectory(sourceSamplesDir);

        // Write source sample files in their subdirectory structure
        foreach (var (relativePath, content) in sourceSamples)
        {
            var fullPath = Path.Combine(sourceSamplesDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, content);
        }

        // Write minimal Go source for context loading
        File.WriteAllText(Path.Combine(sourcePackagePath, "client.go"), "package azblob\nfunc noop() {}\n");
        var sourceEngScripts = Path.Combine(sourceRepoRoot, "eng", "scripts");
        Directory.CreateDirectory(sourceEngScripts);
        File.WriteAllText(Path.Combine(sourceEngScripts, "Language-Settings.ps1"), "$Language = 'Go'\n");

        _mockSourceLanguageService
            .Setup(m => m.GetPackageInfo(It.Is<string>(p => p.Contains(sourceRepoRoot)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageInfo
            {
                RelativePath = sourceRelativePath,
                RepoRoot = sourceRepoRoot,
                SamplesDirectory = sourceSamplesDir,
                Language = SdkLanguage.Go,
                PackageName = "sdk/storage/azblob",
                PackagePath = sourcePackagePath,
                PackageVersion = "1.0.0",
                SdkType = SdkType.Dataplane,
                ServiceName = "storage"
            });

        // Target repo (Python)
        var targetTemp = TempDirectory.Create("azure-sdk-for-python-target");
        _tempDirs.Add(targetTemp);
        var targetRepoRoot = targetTemp.DirectoryPath;
        await GitTestHelper.GitInitAsync(targetRepoRoot);
        var targetRelativePath = Path.Combine("sdk", "storage", "azure-storage-blob");
        var targetPackagePath = Path.Combine(targetRepoRoot, targetRelativePath);
        var targetSamplesDir = Path.Combine(targetPackagePath, "samples");
        Directory.CreateDirectory(targetSamplesDir);

        // Write minimal Python source for context loading
        var pythonSrc = Path.Combine(targetPackagePath, "azure", "storage", "blob");
        Directory.CreateDirectory(pythonSrc);
        File.WriteAllText(Path.Combine(pythonSrc, "__init__.py"), "# Azure Storage Blob client");
        File.WriteAllText(Path.Combine(targetPackagePath, "setup.py"), "from setuptools import setup; setup(name='azure-storage-blob', version='1.0.0')");
        var targetEngScripts = Path.Combine(targetRepoRoot, "eng", "scripts");
        Directory.CreateDirectory(targetEngScripts);
        File.WriteAllText(Path.Combine(targetEngScripts, "Language-Settings.ps1"), "$Language = 'Python'\n");

        _mockTargetLanguageService
            .Setup(m => m.GetPackageInfo(It.Is<string>(p => p.Contains(targetRepoRoot)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageInfo
            {
                RelativePath = targetRelativePath,
                RepoRoot = targetRepoRoot,
                SamplesDirectory = targetSamplesDir,
                Language = SdkLanguage.Python,
                PackageName = "azure-storage-blob",
                PackagePath = targetPackagePath,
                PackageVersion = "1.0.0",
                SdkType = SdkType.Dataplane,
                ServiceName = "storage"
            });

        // Configure git helper to resolve language services
        _mockGitHelper.Setup(g => g.DiscoverRepoRootAsync(
                It.Is<string>(p => p.Contains(sourceRepoRoot)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceRepoRoot);
        _mockGitHelper.Setup(g => g.GetRepoNameAsync(
                It.Is<string>(p => p.Contains(sourceRepoRoot)), It.IsAny<CancellationToken>()))
            .ReturnsAsync("azure-sdk-for-go");

        _mockGitHelper.Setup(g => g.DiscoverRepoRootAsync(
                It.Is<string>(p => p.Contains(targetRepoRoot)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetRepoRoot);
        _mockGitHelper.Setup(g => g.GetRepoNameAsync(
                It.Is<string>(p => p.Contains(targetRepoRoot)), It.IsAny<CancellationToken>()))
            .ReturnsAsync("azure-sdk-for-python");

        return (sourcePackagePath, targetPackagePath, targetSamplesDir);
    }
}
