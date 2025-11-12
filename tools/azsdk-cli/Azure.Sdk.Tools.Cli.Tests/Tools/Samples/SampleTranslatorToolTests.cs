// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Samples;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Samples;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Samples;

[TestFixture]
public class SampleTranslatorToolTests
{
    private TestLogger<SampleTranslatorTool> _logger;
    private Mock<IGitHelper> _mockGitHelper;
    private Mock<ILanguageSpecificResolver<SampleLanguageContext>> _mockSampleContextResolver;
    private Mock<IMicroagentHostService> _mockMicroagentHostService;
    private Mock<IFileHelper> _mockFileHelper;
    private SampleTranslatorTool _sampleTranslatorTool;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<SampleTranslatorTool>();
        _mockGitHelper = new Mock<IGitHelper>();
        _mockSampleContextResolver = new Mock<ILanguageSpecificResolver<SampleLanguageContext>>();
        _mockMicroagentHostService = new Mock<IMicroagentHostService>();
        _mockFileHelper = new Mock<IFileHelper>();

        // Create empty language services list - this will cause the tool to fail language detection
        // which is actually what we want to test since we're focusing on error paths
        var languageServices = new List<LanguageService>();

        _sampleTranslatorTool = new SampleTranslatorTool(
            _mockMicroagentHostService.Object,
            _logger,
            _mockGitHelper.Object,
            languageServices,
            _mockSampleContextResolver.Object,
            _mockFileHelper.Object);
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
        Assert.That(_sampleTranslatorTool.CommandHierarchy, Contains.Item(SharedCommandGroups.Samples));
    }

    [Test]
    public async Task HandleCommand_WithInvalidPaths_ReturnsErrorResponse()
    {
        // Arrange
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
}