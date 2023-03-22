using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb;
using Microsoft.Extensions.Configuration;
using Xunit;

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
    }
}
