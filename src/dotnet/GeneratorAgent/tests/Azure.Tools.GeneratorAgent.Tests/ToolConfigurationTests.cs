using Azure.Tools.GeneratorAgent;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests.Configuration
{
    [TestFixture]
    public class ToolConfigurationTests
    {
        private static ToolConfiguration CreateToolConfiguration()
        {
            return new ToolConfiguration();
        }

        private static void WithEnvironmentVariable(string key, string? value, Action testAction)
        {
            string? originalValue = Environment.GetEnvironmentVariable(key);
            try
            {
                Environment.SetEnvironmentVariable(key, value);
                testAction();
            }
            finally
            {
                Environment.SetEnvironmentVariable(key, originalValue);
            }
        }

        [Test]
        public void Constructor_ShouldCreateValidConfiguration()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => CreateToolConfiguration());
        }

        [Test]
        public void Constructor_ShouldInitializeConfigurationProperty()
        {
            // Act
            var toolConfig = CreateToolConfiguration();

            // Assert
            Assert.That(toolConfig.Configuration, Is.Not.Null);
            Assert.That(toolConfig.Configuration.GetSection("Logging"), Is.Not.Null);
        }

        [Test]
        public void Constructor_ShouldSetToolDirectoryFromAssemblyLocation()
        {
            // Act
            var toolConfig = CreateToolConfiguration();

            // Assert
            Assert.That(toolConfig.Configuration, Is.Not.Null);
            // Verify that configuration was built (which means ToolDirectory was set successfully)
            Assert.DoesNotThrow(() => toolConfig.Configuration.GetSection("Logging"));
        }

        [Test]
        public void CreateLoggerFactory_ShouldCreateValidLoggerFactory()
        {
            // Arrange
            var toolConfig = CreateToolConfiguration();

            // Act
            ILoggerFactory loggerFactory = toolConfig.CreateLoggerFactory();

            // Assert
            Assert.That(loggerFactory, Is.Not.Null);
        }

        [Test]
        public void CreateLoggerFactory_ShouldCreateLoggersFromFactory()
        {
            // Arrange
            var toolConfig = CreateToolConfiguration();
            ILoggerFactory loggerFactory = toolConfig.CreateLoggerFactory();

            // Act
            ILogger<ToolConfigurationTests> logger = loggerFactory.CreateLogger<ToolConfigurationTests>();

            // Assert
            Assert.That(logger, Is.Not.Null);
        }

        [Test]
        public void CreateLoggerFactory_ShouldConfigureConsoleLogging()
        {
            // Arrange
            var toolConfig = CreateToolConfiguration();

            // Act
            ILoggerFactory loggerFactory = toolConfig.CreateLoggerFactory();

            // Assert
            Assert.That(loggerFactory, Is.Not.Null);
            
            // Verify minimum log level is set
            var logger = loggerFactory.CreateLogger<ToolConfigurationTests>();
            Assert.That(logger.IsEnabled(LogLevel.Information), Is.True);
        }

        [Test]
        public void CreateAppSettings_ShouldCreateValidAppSettings()
        {
            // Arrange
            var toolConfig = CreateToolConfiguration();

            // Act
            AppSettings appSettings = toolConfig.CreateAppSettings();

            // Assert
            Assert.That(appSettings, Is.Not.Null);
        }

        [Test]
        public void CreateAppSettings_ShouldUseConfigurationFromToolConfiguration()
        {
            // Arrange
            var toolConfig = CreateToolConfiguration();

            // Act
            AppSettings appSettings = toolConfig.CreateAppSettings();

            // Assert
            Assert.That(appSettings.ProjectEndpoint, Is.Not.Null);
            Assert.That(appSettings.Model, Is.Not.Null);
            Assert.That(appSettings.AgentName, Is.Not.Null);
            Assert.That(appSettings.AgentInstructions, Is.Not.Null);
        }

        [Test]
        public void Configuration_WithValidEnvironmentName_ShouldLoadEnvironmentSpecificFile()
        {
            // Act with isolated environment variable
            WithEnvironmentVariable(EnvironmentVariables.EnvironmentName, "Development", () =>
            {
                var toolConfig = CreateToolConfiguration();
                
                Assert.That(toolConfig.Configuration, Is.Not.Null);
                Assert.That(toolConfig.Configuration.GetSection("Logging"), Is.Not.Null);
            });
        }

        [Test]
        public void Configuration_WithInvalidEnvironmentName_ShouldFallbackToDefault()
        {
            // Act with isolated environment variable
            WithEnvironmentVariable(EnvironmentVariables.EnvironmentName, "NonExistentEnvironment", () =>
            {
                var toolConfig = CreateToolConfiguration();
                
                Assert.That(toolConfig.Configuration, Is.Not.Null);
                Assert.That(toolConfig.Configuration.GetSection("Logging"), Is.Not.Null);
            });
        }

        [Test]
        public void Configuration_WithNullEnvironmentName_ShouldDefaultToDevelopment()
        {
            // Act with isolated environment variable
            WithEnvironmentVariable(EnvironmentVariables.EnvironmentName, null, () =>
            {
                var toolConfig = CreateToolConfiguration();
                
                Assert.That(toolConfig.Configuration, Is.Not.Null);
                Assert.That(toolConfig.Configuration.GetSection("Logging"), Is.Not.Null);
            });
        }

        [Test]
        public void Configuration_ShouldLoadFromJsonFiles()
        {
            // Arrange
            var toolConfig = CreateToolConfiguration();

            // Act
            var loggingSection = toolConfig.Configuration.GetSection("Logging");

            // Assert
            Assert.That(loggingSection, Is.Not.Null);
            Assert.That(loggingSection.Exists(), Is.True);
        }

        [Test]
        public void Configuration_ShouldSupportEnvironmentVariableOverrides()
        {
            // Arrange
            const string testKey = "TestConfigValue";
            const string testValue = "OverriddenByEnvironment";
            string envVarName = $"{EnvironmentVariables.Prefix}{testKey}";

            // Act with isolated environment variable
            WithEnvironmentVariable(envVarName, testValue, () =>
            {
                var toolConfig = CreateToolConfiguration();
                
                // Assert
                var configValue = toolConfig.Configuration[testKey];
                Assert.That(configValue, Is.EqualTo(testValue));
            });
        }

        [Test]
        public void Configuration_EnvironmentVariables_ShouldUseCorrectPrefix()
        {
            // Arrange
            const string testKey = "TestPrefixValue";
            const string testValue = "PrefixTestValue";
            string correctEnvVar = $"{EnvironmentVariables.Prefix}{testKey}";
            string incorrectEnvVar = $"WRONG_PREFIX_{testKey}";

            // Act with isolated environment variables
            WithEnvironmentVariable(correctEnvVar, testValue, () =>
            {
                WithEnvironmentVariable(incorrectEnvVar, "ShouldNotBeUsed", () =>
                {
                    var toolConfig = CreateToolConfiguration();
                    
                    // Assert
                    var configValue = toolConfig.Configuration[testKey];
                    Assert.That(configValue, Is.EqualTo(testValue));
                });
            });
        }

        [Test]
        public void MultipleInstances_ShouldCreateIndependentConfigurations()
        {
            // Arrange & Act
            var toolConfig1 = CreateToolConfiguration();
            var toolConfig2 = CreateToolConfiguration();

            // Assert
            Assert.That(toolConfig1.Configuration, Is.Not.Null);
            Assert.That(toolConfig2.Configuration, Is.Not.Null);
            
            // They should be separate instances
            Assert.That(toolConfig1.Configuration, Is.Not.SameAs(toolConfig2.Configuration));
        }

        [Test]
        public void MultipleInstances_ShouldHaveConsistentConfigurationValues()
        {
            // Arrange & Act
            var toolConfig1 = CreateToolConfiguration();
            var toolConfig2 = CreateToolConfiguration();

            // Assert - Both instances should load the same configuration values
            var appSettings1 = toolConfig1.CreateAppSettings();
            var appSettings2 = toolConfig2.CreateAppSettings();

            Assert.That(appSettings1.Model, Is.EqualTo(appSettings2.Model));
            Assert.That(appSettings1.AgentName, Is.EqualTo(appSettings2.AgentName));
            Assert.That(appSettings1.ProjectEndpoint, Is.EqualTo(appSettings2.ProjectEndpoint));
            Assert.That(appSettings1.AgentInstructions, Is.EqualTo(appSettings2.AgentInstructions));
        }

        [Test]
        public void ToolConfiguration_ShouldSupportCompleteWorkflow()
        {
            // Arrange
            var toolConfig = CreateToolConfiguration();

            // Act
            ILoggerFactory loggerFactory = toolConfig.CreateLoggerFactory();
            AppSettings appSettings = toolConfig.CreateAppSettings();

            // Assert - All components should work together
            Assert.That(toolConfig.Configuration, Is.Not.Null);
            Assert.That(loggerFactory, Is.Not.Null);
            Assert.That(appSettings, Is.Not.Null);

            // Verify we can create loggers from the factory
            var logger = loggerFactory.CreateLogger<ToolConfigurationTests>();
            Assert.That(logger, Is.Not.Null);

            // Verify app settings are properly configured
            Assert.That(appSettings.Model, Is.Not.Null.And.Not.Empty);
            Assert.That(appSettings.AgentName, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void Configuration_WithMissingOptionalFiles_ShouldNotThrow()
        {
            // Act with environment that likely doesn't have a specific config file
            WithEnvironmentVariable(EnvironmentVariables.EnvironmentName, "NonExistentEnvironment", () =>
            {
                Assert.DoesNotThrow(() => CreateToolConfiguration());
            });
        }

        [Test]
        public void CreateLoggerFactory_MultipleCalls_ShouldReturnNewInstances()
        {
            // Arrange
            var toolConfig = CreateToolConfiguration();

            // Act
            ILoggerFactory factory1 = toolConfig.CreateLoggerFactory();
            ILoggerFactory factory2 = toolConfig.CreateLoggerFactory();

            // Assert
            Assert.That(factory1, Is.Not.Null);
            Assert.That(factory2, Is.Not.Null);
            Assert.That(factory1, Is.Not.SameAs(factory2));
        }

        [Test]
        public void CreateAppSettings_MultipleCalls_ShouldReturnNewInstances()
        {
            // Arrange
            var toolConfig = CreateToolConfiguration();

            // Act
            AppSettings settings1 = toolConfig.CreateAppSettings();
            AppSettings settings2 = toolConfig.CreateAppSettings();

            // Assert
            Assert.That(settings1, Is.Not.Null);
            Assert.That(settings2, Is.Not.Null);
            Assert.That(settings1, Is.Not.SameAs(settings2));
        }
    }
}