using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class EnvironmentVariableTests
    {
        [Test]
        public void OpenAIApiKey_UsesEnvironmentVariable_WhenConfigurationIsEmpty()
        {
            // Arrange
            var testApiKey = "env-var-test-key";
            
            // Set up environment variable
            Environment.SetEnvironmentVariable("AZURE_GENERATOR_OPENAI_API_KEY", testApiKey);
            
            try
            {
                // Create configuration that doesn't have OpenAI:ApiKey set
                var mockConfiguration = new Mock<IConfiguration>();
                var mockOpenAISection = new Mock<IConfigurationSection>();
                mockOpenAISection.Setup(s => s.Value).Returns((string?)null);
                mockConfiguration.Setup(c => c.GetSection("OpenAI:ApiKey")).Returns(mockOpenAISection.Object);
                
                var mockLogger = new Mock<ILogger<AppSettings>>();
                var appSettings = new AppSettings(mockConfiguration.Object, mockLogger.Object);
                
                // Act & Assert
                Assert.That(appSettings.OpenAIApiKey, Is.EqualTo(testApiKey));
            }
            finally
            {
                // Clean up
                Environment.SetEnvironmentVariable("AZURE_GENERATOR_OPENAI_API_KEY", null);
            }
        }
        
        [Test]
        public void OpenAIApiKey_PrefersConfigurationOverEnvironmentVariable()
        {
            // Arrange
            var configApiKey = "config-test-key";
            var envApiKey = "env-test-key";
            
            // Set up environment variable
            Environment.SetEnvironmentVariable("AZURE_GENERATOR_OPENAI_API_KEY", envApiKey);
            
            try
            {
                // Create configuration with OpenAI:ApiKey set
                var mockConfiguration = new Mock<IConfiguration>();
                var mockOpenAISection = new Mock<IConfigurationSection>();
                mockOpenAISection.Setup(s => s.Value).Returns(configApiKey);
                mockConfiguration.Setup(c => c.GetSection("OpenAI:ApiKey")).Returns(mockOpenAISection.Object);
                
                var mockLogger = new Mock<ILogger<AppSettings>>();
                var appSettings = new AppSettings(mockConfiguration.Object, mockLogger.Object);
                
                // Act & Assert
                Assert.That(appSettings.OpenAIApiKey, Is.EqualTo(configApiKey), 
                    "Configuration value should take precedence over environment variable");
            }
            finally
            {
                // Clean up
                Environment.SetEnvironmentVariable("AZURE_GENERATOR_OPENAI_API_KEY", null);
            }
        }
        
        [Test]
        public void OpenAIApiKey_FallsBackToStandardEnvironmentVariable()
        {
            // Arrange
            var testApiKey = "standard-openai-key";
            
            // Set up standard OpenAI environment variable
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", testApiKey);
            
            try
            {
                // Create configuration that doesn't have OpenAI:ApiKey set
                var mockConfiguration = new Mock<IConfiguration>();
                var mockOpenAISection = new Mock<IConfigurationSection>();
                mockOpenAISection.Setup(s => s.Value).Returns((string?)null);
                mockConfiguration.Setup(c => c.GetSection("OpenAI:ApiKey")).Returns(mockOpenAISection.Object);
                
                var mockLogger = new Mock<ILogger<AppSettings>>();
                var appSettings = new AppSettings(mockConfiguration.Object, mockLogger.Object);
                
                // Act & Assert
                Assert.That(appSettings.OpenAIApiKey, Is.EqualTo(testApiKey));
            }
            finally
            {
                // Clean up
                Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            }
        }
        
        [Test]
        public void OpenAIApiKey_PrefersAzureGeneratorOverStandardEnvironmentVariable()
        {
            // Arrange
            var azureGeneratorKey = "azure-generator-key";
            var standardKey = "standard-key";
            
            // Set up both environment variables
            Environment.SetEnvironmentVariable("AZURE_GENERATOR_OPENAI_API_KEY", azureGeneratorKey);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", standardKey);
            
            try
            {
                // Create configuration that doesn't have OpenAI:ApiKey set
                var mockConfiguration = new Mock<IConfiguration>();
                var mockOpenAISection = new Mock<IConfigurationSection>();
                mockOpenAISection.Setup(s => s.Value).Returns((string?)null);
                mockConfiguration.Setup(c => c.GetSection("OpenAI:ApiKey")).Returns(mockOpenAISection.Object);
                
                var mockLogger = new Mock<ILogger<AppSettings>>();
                var appSettings = new AppSettings(mockConfiguration.Object, mockLogger.Object);
                
                // Act & Assert
                Assert.That(appSettings.OpenAIApiKey, Is.EqualTo(azureGeneratorKey), 
                    "AZURE_GENERATOR_OPENAI_API_KEY should take precedence over OPENAI_API_KEY");
            }
            finally
            {
                // Clean up
                Environment.SetEnvironmentVariable("AZURE_GENERATOR_OPENAI_API_KEY", null);
                Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            }
        }
    }
}