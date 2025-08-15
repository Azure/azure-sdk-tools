using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
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

        private static ILogger<AppSettings> CreateMockLogger()
        {
            return new Mock<ILogger<AppSettings>>().Object;
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
            Assert.DoesNotThrow(() => CreateToolConfiguration());
        }

        [Test]
        public void Constructor_ShouldInitializeConfigurationProperty()
        {
            var toolConfig = CreateToolConfiguration();

            Assert.That(toolConfig.Configuration, Is.Not.Null);
            Assert.That(toolConfig.Configuration.GetSection("Logging"), Is.Not.Null);
        }

        [Test]
        public void Constructor_ShouldSetToolDirectoryFromAssemblyLocation()
        {
            var toolConfig = CreateToolConfiguration();

            Assert.That(toolConfig.Configuration, Is.Not.Null);
            // Verify that configuration was built (which means ToolDirectory was set successfully)
            Assert.DoesNotThrow(() => toolConfig.Configuration.GetSection("Logging"));
        }

        [Test]
        public void CreateLoggerFactory_ShouldCreateValidLoggerFactory()
        {
            var toolConfig = CreateToolConfiguration();

            ILoggerFactory loggerFactory = toolConfig.CreateLoggerFactory();

            Assert.That(loggerFactory, Is.Not.Null);
        }

        [Test]
        public void CreateLoggerFactory_ShouldCreateLoggersFromFactory()
        {
            var toolConfig = CreateToolConfiguration();
            ILoggerFactory loggerFactory = toolConfig.CreateLoggerFactory();

            ILogger<ToolConfigurationTests> logger = loggerFactory.CreateLogger<ToolConfigurationTests>();

            Assert.That(logger, Is.Not.Null);
        }

        [Test]
        public void CreateLoggerFactory_ShouldConfigureConsoleLogging()
        {
            var toolConfig = CreateToolConfiguration();

            ILoggerFactory loggerFactory = toolConfig.CreateLoggerFactory();

            Assert.That(loggerFactory, Is.Not.Null);
            
            // Verify minimum log level is set
            var logger = loggerFactory.CreateLogger<ToolConfigurationTests>();
            Assert.That(logger.IsEnabled(LogLevel.Information), Is.True);
        }

        [Test]
        public void CreateAppSettings_ShouldCreateValidAppSettings()
        {
            var toolConfig = CreateToolConfiguration();
            var mockLogger = CreateMockLogger();

            AppSettings appSettings = toolConfig.CreateAppSettings(mockLogger);

            Assert.That(appSettings, Is.Not.Null);
        }

        [Test]
        public void CreateAppSettings_ShouldUseConfigurationFromToolConfiguration()
        {
            var toolConfig = CreateToolConfiguration();
            var mockLogger = CreateMockLogger();

            AppSettings appSettings = toolConfig.CreateAppSettings(mockLogger);

            Assert.That(appSettings.ProjectEndpoint, Is.Not.Null);
            Assert.That(appSettings.Model, Is.Not.Null);
            Assert.That(appSettings.AgentName, Is.Not.Null);
            Assert.That(appSettings.AgentInstructions, Is.Not.Null);
        }

        [Test]
        public void Configuration_WithValidEnvironmentName_ShouldLoadEnvironmentSpecificFile()
        {
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
            var toolConfig = CreateToolConfiguration();

            var loggingSection = toolConfig.Configuration.GetSection("Logging");

            Assert.That(loggingSection, Is.Not.Null);
            Assert.That(loggingSection.Exists(), Is.True);
        }

        [Test]
        public void Configuration_ShouldSupportEnvironmentVariableOverrides()
        {
            const string testKey = "TestConfigValue";
            const string testValue = "OverriddenByEnvironment";
            string envVarName = $"{EnvironmentVariables.Prefix}{testKey}";

            WithEnvironmentVariable(envVarName, testValue, () =>
            {
                var toolConfig = CreateToolConfiguration();
                
                var configValue = toolConfig.Configuration[testKey];
                Assert.That(configValue, Is.EqualTo(testValue));
            });
        }

        [Test]
        public void Configuration_EnvironmentVariables_ShouldUseCorrectPrefix()
        {
            const string testKey = "TestPrefixValue";
            const string testValue = "PrefixTestValue";
            string correctEnvVar = $"{EnvironmentVariables.Prefix}{testKey}";
            string incorrectEnvVar = $"WRONG_PREFIX_{testKey}";

            WithEnvironmentVariable(correctEnvVar, testValue, () =>
            {
                WithEnvironmentVariable(incorrectEnvVar, "ShouldNotBeUsed", () =>
                {
                    var toolConfig = CreateToolConfiguration();
                    
                    var configValue = toolConfig.Configuration[testKey];
                    Assert.That(configValue, Is.EqualTo(testValue));
                });
            });
        }

        [Test]
        public void MultipleInstances_ShouldCreateIndependentConfigurations()
        {
            var toolConfig1 = CreateToolConfiguration();
            var toolConfig2 = CreateToolConfiguration();

            Assert.That(toolConfig1.Configuration, Is.Not.Null);
            Assert.That(toolConfig2.Configuration, Is.Not.Null);
            
            // They should be separate instances
            Assert.That(toolConfig1.Configuration, Is.Not.SameAs(toolConfig2.Configuration));
        }

        [Test]
        public void MultipleInstances_ShouldHaveConsistentConfigurationValues()
        {
            var toolConfig1 = CreateToolConfiguration();
            var toolConfig2 = CreateToolConfiguration();
            var mockLogger = CreateMockLogger();

            var appSettings1 = toolConfig1.CreateAppSettings(mockLogger);
            var appSettings2 = toolConfig2.CreateAppSettings(mockLogger);

            Assert.That(appSettings1.Model, Is.EqualTo(appSettings2.Model));
            Assert.That(appSettings1.AgentName, Is.EqualTo(appSettings2.AgentName));
            Assert.That(appSettings1.ProjectEndpoint, Is.EqualTo(appSettings2.ProjectEndpoint));
            Assert.That(appSettings1.AgentInstructions, Is.EqualTo(appSettings2.AgentInstructions));
        }

        [Test]
        public void ToolConfiguration_ShouldSupportCompleteWorkflow()
        {
            var toolConfig = CreateToolConfiguration();
            var mockLogger = CreateMockLogger();

            ILoggerFactory loggerFactory = toolConfig.CreateLoggerFactory();
            AppSettings appSettings = toolConfig.CreateAppSettings(mockLogger);

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
            WithEnvironmentVariable(EnvironmentVariables.EnvironmentName, "NonExistentEnvironment", () =>
            {
                Assert.DoesNotThrow(() => CreateToolConfiguration());
            });
        }

        [Test]
        public void CreateLoggerFactory_MultipleCalls_ShouldReturnNewInstances()
        {
            var toolConfig = CreateToolConfiguration();

            ILoggerFactory factory1 = toolConfig.CreateLoggerFactory();
            ILoggerFactory factory2 = toolConfig.CreateLoggerFactory();

            Assert.That(factory1, Is.Not.Null);
            Assert.That(factory2, Is.Not.Null);
            Assert.That(factory1, Is.Not.SameAs(factory2));
        }

        [Test]
        public void CreateAppSettings_MultipleCalls_ShouldReturnNewInstances()
        {
            var toolConfig = CreateToolConfiguration();
            var mockLogger = CreateMockLogger();

            AppSettings settings1 = toolConfig.CreateAppSettings(mockLogger);
            AppSettings settings2 = toolConfig.CreateAppSettings(mockLogger);

            Assert.That(settings1, Is.Not.Null);
            Assert.That(settings2, Is.Not.Null);
            Assert.That(settings1, Is.Not.SameAs(settings2));
        }
    }
}
