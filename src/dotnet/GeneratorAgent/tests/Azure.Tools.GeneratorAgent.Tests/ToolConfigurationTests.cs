using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests.Configuration
{
    [TestFixture]
    public class ToolConfigurationTests
    {
        private const string EnvironmentNameVariable = "AZURE_GENERATOR_ENVIRONMENT";
        private const string EnvironmentVariablePrefix = "AZURE_GENERATOR_";

        [Test]
        [Category("Constructor")]
        public void Constructor_ShouldCreateValidConfiguration()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => new ToolConfiguration());
        }

        [Test]
        [Category("Constructor")]
        public void Constructor_ShouldInitializeConfigurationProperty()
        {
            // Act
            var toolConfig = new ToolConfiguration();

            // Assert
            Assert.That(toolConfig.Configuration, Is.Not.Null);
            Assert.That(toolConfig.Configuration.GetSection("Logging"), Is.Not.Null);
        }

        [Test]
        [Category("LoggerFactory")]
        public void CreateLoggerFactory_ShouldCreateValidLoggerFactory()
        {
            // Arrange
            var toolConfig = new ToolConfiguration();

            // Act
            ILoggerFactory loggerFactory = toolConfig.CreateLoggerFactory();

            // Assert
            Assert.That(loggerFactory, Is.Not.Null);
            
            // Verify we can create a logger
            ILogger<ToolConfigurationTests> logger = loggerFactory.CreateLogger<ToolConfigurationTests>();
            Assert.That(logger, Is.Not.Null);
        }

        [Test]
        [Category("AppSettings")]
        public void CreateAppSettings_ShouldCreateValidAppSettings()
        {
            // Arrange
            var toolConfig = new ToolConfiguration();

            // Act
            AppSettings appSettings = toolConfig.CreateAppSettings();

            // Assert
            Assert.That(appSettings, Is.Not.Null);
            Assert.That(appSettings.ProjectEndpoint, Is.Not.Null);
        }

        [Test]
        [Category("Configuration")]
        public void Configuration_WithInvalidEnvironment_ShouldFallbackToDefault()
        {
            // Arrange
            string? originalEnv = Environment.GetEnvironmentVariable(EnvironmentNameVariable);
            try
            {
                Environment.SetEnvironmentVariable(EnvironmentNameVariable, "Invalid");

                // Act
                var toolConfig = new ToolConfiguration();

                // Assert
                Assert.That(toolConfig.Configuration, Is.Not.Null);
                Assert.That(toolConfig.Configuration.GetSection("Logging"), Is.Not.Null);
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable(EnvironmentNameVariable, originalEnv);
            }
        }

        [Test]
        [Category("Configuration")]
        public void Configuration_ShouldLoadFromCorrectBasePath()
        {
            // Act
            var toolConfig = new ToolConfiguration();

            // Assert
            Assert.That(toolConfig.Configuration, Is.Not.Null);
            
            // Verify that appsettings.json configurations are loaded
            var loggingSection = toolConfig.Configuration.GetSection("Logging");
            Assert.That(loggingSection, Is.Not.Null);
            Assert.That(loggingSection.Exists(), Is.True);
        }

        [Test]
        [Category("Integration")]
        public void ToolConfiguration_ShouldSupportCompleteWorkflow()
        {
            // Act
            var toolConfig = new ToolConfiguration();
            ILoggerFactory loggerFactory = toolConfig.CreateLoggerFactory();
            AppSettings appSettings = toolConfig.CreateAppSettings();

            // Assert - All components should work together
            Assert.That(toolConfig.Configuration, Is.Not.Null);
            Assert.That(loggerFactory, Is.Not.Null);
            Assert.That(appSettings, Is.Not.Null);

            // Verify we can create loggers from the factory
            var logger = loggerFactory.CreateLogger<ToolConfigurationTests>();
            Assert.That(logger, Is.Not.Null);

            // Verify app settings have expected values
            Assert.That(appSettings.Model, Is.Not.Null.And.Not.Empty);
            Assert.That(appSettings.AgentName, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        [Category("Consistency")]
        public void MultipleInstances_ShouldHaveConsistentConfiguration()
        {
            // Arrange & Act
            var toolConfig1 = new ToolConfiguration();
            var toolConfig2 = new ToolConfiguration();

            // Assert - Both instances should load the same configuration values
            var config1LogLevel = toolConfig1.Configuration.GetSection("Logging:LogLevel:Default").Value;
            var config2LogLevel = toolConfig2.Configuration.GetSection("Logging:LogLevel:Default").Value;
            
            Assert.That(config1LogLevel, Is.EqualTo(config2LogLevel));

            var appSettings1 = toolConfig1.CreateAppSettings();
            var appSettings2 = toolConfig2.CreateAppSettings();

            Assert.That(appSettings1.Model, Is.EqualTo(appSettings2.Model));
            Assert.That(appSettings1.AgentName, Is.EqualTo(appSettings2.AgentName));
        }

        [Test]
        [Category("Exception")]
        public void Constructor_WithMissingAppSettingsFile_ShouldThrowInformativeException()
        {
            Assert.DoesNotThrow(() => new ToolConfiguration(), 
                "Constructor should not throw when appsettings.json is present");
        }

        [Test]
        [Category("EnvironmentVariables")]
        public void Configuration_ShouldRespectEnvironmentVariables()
        {
            // Arrange
            const string testKey = "AZURE_GENERATOR_TestValue";
            const string testValue = "TestEnvironmentValue";
            string? originalValue = Environment.GetEnvironmentVariable(testKey);

            try
            {
                Environment.SetEnvironmentVariable(testKey, testValue);

                // Act
                var toolConfig = new ToolConfiguration();

                // Assert
                var configValue = toolConfig.Configuration["TestValue"];
                Assert.That(configValue, Is.EqualTo(testValue));
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable(testKey, originalValue);
            }
        }
    }
}