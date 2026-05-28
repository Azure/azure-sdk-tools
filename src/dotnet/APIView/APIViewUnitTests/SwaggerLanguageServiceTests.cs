// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIViewWeb;
using APIViewWeb.Models;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class SwaggerLanguageServiceTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly TelemetryClient _telemetryClient;

    public SwaggerLanguageServiceTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockConfiguration.Setup(c => c["SwaggerReviewGenerationPipelineUrl"]).Returns("https://example.pipeline");

        TelemetryConfiguration telemetryConfiguration = new();
        _telemetryClient = new TelemetryClient(telemetryConfiguration);
    }

    [Fact]
    public void GeneratePipelineRunParams_StripsPathTraversalFromFileName()
    {
        SwaggerLanguageService service = new(_mockConfiguration.Object, _telemetryClient);
        APIRevisionGenerationPipelineParamModel param = new()
        {
            FileName = "../../s/eng/scripts/Create-Apiview-Token-Swagger.ps1"
        };

        bool result = service.GeneratePipelineRunParams(param);

        Assert.True(result);
        Assert.Equal("Create-Apiview-Token-Swagger.ps1", param.FileName);
    }

    [Theory]
    [InlineData("..")] 
    [InlineData(".")]
    [InlineData("")]
    public void GeneratePipelineRunParams_RejectsInvalidFileName(string input)
    {
        SwaggerLanguageService service = new(_mockConfiguration.Object, _telemetryClient);
        APIRevisionGenerationPipelineParamModel param = new()
        {
            FileName = input
        };

        bool result = service.GeneratePipelineRunParams(param);

        Assert.False(result);
    }
}
