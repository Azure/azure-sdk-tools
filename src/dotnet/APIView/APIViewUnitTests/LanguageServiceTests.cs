using System.Collections.Generic;
using APIViewWeb;
using APIViewWeb.Models;
using APIViewWeb.Helpers;
using Microsoft.Extensions.Configuration;
using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;

namespace APIViewUnitTests
{
    public class LanguageServiceTests
    {
        [Fact(Skip = "Skipping this test because it interface for TelemetryClient")]
        public void TypeSpectLanguageService_Supports_Multiple_Extensions()
        {
            var telemetryClient = new Mock<TelemetryClient>().Object;
            var languageService = new TypeSpecLanguageService(new ConfigurationBuilder().Build(), telemetryClient);
            Assert.True(languageService.IsSupportedFile("specification/cognitiveservices/HealthInsights/healthinsights.common/primitives.cadl"));
            Assert.True(languageService.IsSupportedFile("specification/cognitiveservices/HealthInsights/healthinsights.common/primitives.tsp"));
            Assert.False(languageService.IsSupportedFile("specification/cognitiveservices/HealthInsights/healthinsights.common/primitives.json"));
        }

        [Fact]
        public void SupportedLanguages_Is_In_Alphabetical_Order()
        {
            LanguageServiceHelpers.SupportedLanguages.Should().BeInAscendingOrder();
        }

        [Fact]
        public void SwaggerLanguageService_StripsPathTraversalAndInvalidCharactersFromFileName()
        {
            var telemetryClient = new TelemetryClient(new TelemetryConfiguration());
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["SwaggerReviewGenerationPipelineUrl"] = "https://example.pipeline"
                })
                .Build();

            var languageService = new SwaggerLanguageService(configuration, telemetryClient);

            var traversalParam = new APIRevisionGenerationPipelineParamModel
            {
                FileName = "../../s/eng/scripts/Create-Apiview-Token-Swagger.ps1"
            };

            Assert.True(languageService.GeneratePipelineRunParams(traversalParam));
            Assert.Equal("Create-Apiview-Token-Swagger.ps1", traversalParam.FileName);

            var invalidCharParam = new APIRevisionGenerationPipelineParamModel
            {
                FileName = "swagger:review.ps1"
            };

            Assert.True(languageService.GeneratePipelineRunParams(invalidCharParam));
            Assert.Equal("swagger_review.ps1", invalidCharParam.FileName);

            // Windows reserved device names must be rejected
            var reservedNameParam = new APIRevisionGenerationPipelineParamModel
            {
                FileName = "CON.txt"
            };
            Assert.False(languageService.GeneratePipelineRunParams(reservedNameParam));

            var reservedNameNulParam = new APIRevisionGenerationPipelineParamModel
            {
                FileName = "NUL.swagger"
            };
            Assert.False(languageService.GeneratePipelineRunParams(reservedNameNulParam));

            var reservedNameLptParam = new APIRevisionGenerationPipelineParamModel
            {
                FileName = "LPT1"
            };
            Assert.False(languageService.GeneratePipelineRunParams(reservedNameLptParam));

            // Trailing dots must be stripped (invalid on Windows)
            var trailingDotParam = new APIRevisionGenerationPipelineParamModel
            {
                FileName = "swagger.review."
            };
            Assert.True(languageService.GeneratePipelineRunParams(trailingDotParam));
            Assert.Equal("swagger.review", trailingDotParam.FileName);

            // A filename that becomes empty after trimming trailing dots
            var allDotsParam = new APIRevisionGenerationPipelineParamModel
            {
                FileName = "..."
            };
            Assert.False(languageService.GeneratePipelineRunParams(allDotsParam));
        }
    }
}
