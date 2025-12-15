// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using APIViewWeb;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class PythonLanguageServiceTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly TelemetryClient _telemetryClient;

    public PythonLanguageServiceTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockConfiguration.Setup(c => c["PYTHONEXECUTABLEPATH"]).Returns("python");
        _mockConfiguration.Setup(c => c["PythonReviewGenerationPipelineUrl"]).Returns("https://test.pipeline.url");
        _mockConfiguration.Setup(c => c["ReviewGenByPipelineDisabledForPython"]).Returns("false");

        TelemetryConfiguration telemetryConfiguration = new();
        _telemetryClient = new TelemetryClient(telemetryConfiguration);
    }

    [Fact]
    public void Constructor_InitializesProperties()
    {
        PythonLanguageService service = new(_mockConfiguration.Object, _telemetryClient);

        Assert.Equal("Python", service.Name);
        Assert.Contains(".whl", service.Extensions);
        // Assert.Equal("0.3.23", service.VersionString);
        Assert.NotEmpty(service.ReviewGenerationPipelineUrl);
    }


    [Fact]
    public void GetProcessorArgumentsWithMapping_WithoutMappingFile_ReturnsBaseArgs()
    {
        PythonLanguageService service = new(_mockConfiguration.Object, _telemetryClient);
        string originalName = "test-package.whl";
        string tempDirectory = @"C:\Temp\ApiView\abc123";
        string jsonPath = @"C:\Temp\ApiView\abc123\test-package.json";

        string arguments = service.GetProcessorArgumentsWithMapping(originalName, tempDirectory, jsonPath, null);

        Assert.Contains("-m apistub", arguments);
        Assert.Contains($"--pkg-path {originalName}", arguments);
        Assert.DoesNotContain("--mapping-path", arguments);
    }

    [Fact]
    public void GetProcessorArgumentsWithMapping_WithNonExistentMappingFile_ReturnsBaseArgs()
    {
        PythonLanguageService service = new(_mockConfiguration.Object, _telemetryClient);
        string originalName = "test-package.whl";
        string tempDirectory = @"C:\Temp\ApiView\abc123";
        string jsonPath = @"C:\Temp\ApiView\abc123\test-package.json";
        string nonExistentMappingFile = @"C:\Temp\ApiView\abc123\nonexistent.json";

        string arguments =
            service.GetProcessorArgumentsWithMapping(originalName, tempDirectory, jsonPath, nonExistentMappingFile);

        Assert.Contains("-m apistub", arguments);
        Assert.DoesNotContain("--mapping-path", arguments);
    }

    [Fact]
    public void Extensions_ContainsWhlExtension()
    {
        PythonLanguageService service = new(_mockConfiguration.Object, _telemetryClient);

        string[] extensions = service.Extensions;

        Assert.Single(extensions);
        Assert.Equal(".whl", extensions[0]);
    }

    [Fact]
    public void CanUpdate_WithDifferentVersion_ReturnsTrue()
    {
        PythonLanguageService service = new(_mockConfiguration.Object, _telemetryClient);
        string oldVersion = "0.3.22";
        bool canUpdate = service.CanUpdate(oldVersion);
        Assert.True(canUpdate);
    }

    // [Fact]
    // public void CanUpdate_WithSameVersion_ReturnsFalse()
    // {
    //     PythonLanguageService service = new(_mockConfiguration.Object, _telemetryClient);
    //     string currentVersion = "0.3.23";
    //     bool canUpdate = service.CanUpdate(currentVersion);
    //     Assert.False(canUpdate);
    // }
}
