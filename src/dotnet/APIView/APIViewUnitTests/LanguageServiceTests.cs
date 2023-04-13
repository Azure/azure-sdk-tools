using APIViewWeb;
using APIViewWeb.Helpers;
using Microsoft.Extensions.Configuration;
using Xunit;
using FluentAssertions;

namespace APIViewUnitTests
{
    public class LanguageServiceTests
    {
        [Fact]
        public void TypeSpectLanguageService_Supports_Multiple_Extensions()
        {
            var languageService = new TypeSpecLanguageService(new ConfigurationBuilder().Build());
            Assert.True(languageService.IsSupportedFile("specification/cognitiveservices/HealthInsights/healthinsights.common/primitives.cadl"));
            Assert.True(languageService.IsSupportedFile("specification/cognitiveservices/HealthInsights/healthinsights.common/primitives.tsp"));
            Assert.False(languageService.IsSupportedFile("specification/cognitiveservices/HealthInsights/healthinsights.common/primitives.json"));
        }

        [Fact]
        public void SupportedLanguages_Is_In_Alphabetical_Order()
        {
            LanguageServiceHelpers.SupportedLanguages.Should().BeInAscendingOrder();
        }
    }
}
