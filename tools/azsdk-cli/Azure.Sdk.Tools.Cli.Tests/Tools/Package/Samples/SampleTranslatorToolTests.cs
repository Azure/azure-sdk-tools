// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Services.Languages.Samples;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
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
