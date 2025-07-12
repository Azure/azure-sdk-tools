using Azure.Tools.GeneratorAgent.Composition;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class DependencyInjectionTests
    {
        [Test]
        public void Configure_ShouldReturnConfigurationAndLoggerFactory()
        {
            // Act
            (IConfiguration configuration, ILoggerFactory loggerFactory) = DependencyInjection.Configure();

            // Assert
            Assert.That(configuration, Is.Not.Null);
            Assert.That(loggerFactory, Is.Not.Null);
        }

        [Test]
        public void Configure_ShouldLoadConfiguration()
        {
            // Act
            (IConfiguration configuration, _) = DependencyInjection.Configure();
            AppSettings appSettings = new AppSettings(configuration);

            // Assert
            Assert.That(appSettings.Model, Is.Not.Null);
            Assert.That(appSettings.AgentName, Is.Not.Null);
            Assert.That(appSettings.ProjectEndpoint, Is.Not.Null);
            Assert.That(appSettings.AgentInstructions, Is.Not.Null);
        }

        [Test]
        public void Configure_ShouldCreateLoggers()
        {
            // Arrange
            (_, ILoggerFactory loggerFactory) = DependencyInjection.Configure();

            // Act
            ILogger<Program> programLogger = loggerFactory.CreateLogger<Program>();
            ILogger<ErrorFixerAgent> agentLogger = loggerFactory.CreateLogger<ErrorFixerAgent>();

            // Assert
            Assert.That(programLogger, Is.Not.Null);
            Assert.That(agentLogger, Is.Not.Null);
        }
    }
}