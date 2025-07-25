using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests.Configuration
{
    [TestFixture]
    public class AppSettingsTests
    {
        private static Mock<IConfiguration> CreateConfigurationMock()
        {
            return new Mock<IConfiguration>();
        }

        private static Mock<IConfigurationSection> CreateConfigurationSectionMock(string? value = null)
        {
            var sectionMock = new Mock<IConfigurationSection>();
            sectionMock.Setup(s => s.Value).Returns(value);
            return sectionMock;
        }

        private static AppSettings CreateAppSettings(IConfiguration? configuration = null)
        {
            return new AppSettings(configuration ?? CreateConfigurationMock().Object);
        }

        [Test]
        public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new AppSettings(null!));
            Assert.That(ex.ParamName, Is.EqualTo("configuration"));
        }

        [Test]
        public void Constructor_WithValidConfiguration_DoesNotThrow()
        {
            // Arrange
            var configurationMock = CreateConfigurationMock();

            // Act & Assert
            Assert.DoesNotThrow(() => CreateAppSettings(configurationMock.Object));
        }

        [Test]
        public void ProjectEndpoint_WithValidValue_ReturnsValue()
        {
            // Arrange
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock("https://test-endpoint.example.com");
            configurationMock.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock.Object);

            // Act
            string result = appSettings.ProjectEndpoint;

            // Assert
            Assert.That(result, Is.EqualTo("https://test-endpoint.example.com"));
        }

        [Test]
        public void ProjectEndpoint_WithNullValue_ReturnsEmptyString()
        {
            // Arrange
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock(null);
            configurationMock.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock.Object);

            // Act
            string result = appSettings.ProjectEndpoint;

            // Assert
            Assert.That(result, Is.EqualTo(string.Empty));
        }

        [Test]
        public void ProjectEndpoint_WithEmptyValue_ReturnsEmptyString()
        {
            // Arrange
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock("");
            configurationMock.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock.Object);

            // Act
            string result = appSettings.ProjectEndpoint;

            // Assert
            Assert.That(result, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Model_WithValidValue_ReturnsValue()
        {
            // Arrange
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock("gpt-4");
            configurationMock.Setup(c => c.GetSection("AzureSettings:Model")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock.Object);

            // Act
            string result = appSettings.Model;

            // Assert
            Assert.That(result, Is.EqualTo("gpt-4"));
        }

        [Test]
        public void Model_WithNullValue_ReturnsDefaultValue()
        {
            // Arrange
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock(null);
            configurationMock.Setup(c => c.GetSection("AzureSettings:Model")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock.Object);

            // Act
            string result = appSettings.Model;

            // Assert
            Assert.That(result, Is.EqualTo("gpt-4o"));
        }

        [Test]
        public void AgentName_WithValidValue_ReturnsValue()
        {
            // Arrange
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock("TestAgent");
            configurationMock.Setup(c => c.GetSection("AzureSettings:AgentName")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock.Object);

            // Act
            string result = appSettings.AgentName;

            // Assert
            Assert.That(result, Is.EqualTo("TestAgent"));
        }

        [Test]
        public void AgentName_WithNullValue_ReturnsDefaultValue()
        {
            // Arrange
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock(null);
            configurationMock.Setup(c => c.GetSection("AzureSettings:AgentName")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock.Object);

            // Act
            string result = appSettings.AgentName;

            // Assert
            Assert.That(result, Is.EqualTo("AZC Fixer"));
        }

        [Test]
        public void AgentInstructions_WithValidValue_ReturnsValue()
        {
            // Arrange
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock("Test instructions");
            configurationMock.Setup(c => c.GetSection("AzureSettings:AgentInstructions")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock.Object);

            // Act
            string result = appSettings.AgentInstructions;

            // Assert
            Assert.That(result, Is.EqualTo("Test instructions"));
        }

        [Test]
        public void AgentInstructions_WithNullValue_ReturnsEmptyString()
        {
            // Arrange
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock(null);
            configurationMock.Setup(c => c.GetSection("AzureSettings:AgentInstructions")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock.Object);

            // Act
            string result = appSettings.AgentInstructions;

            // Assert
            Assert.That(result, Is.EqualTo(string.Empty));
        }

        [Test]
        public void AllProperties_MultipleAccesses_UseSameConfigurationSection()
        {
            // Arrange
            var configurationMock = CreateConfigurationMock();
            var projectEndpointSection = CreateConfigurationSectionMock("https://endpoint.example.com");
            var modelSection = CreateConfigurationSectionMock("gpt-4");
            var agentNameSection = CreateConfigurationSectionMock("TestAgent");
            var agentInstructionsSection = CreateConfigurationSectionMock("Instructions");

            configurationMock.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(projectEndpointSection.Object);
            configurationMock.Setup(c => c.GetSection("AzureSettings:Model")).Returns(modelSection.Object);
            configurationMock.Setup(c => c.GetSection("AzureSettings:AgentName")).Returns(agentNameSection.Object);
            configurationMock.Setup(c => c.GetSection("AzureSettings:AgentInstructions")).Returns(agentInstructionsSection.Object);

            var appSettings = CreateAppSettings(configurationMock.Object);

            // Act - access each property multiple times
            string endpoint1 = appSettings.ProjectEndpoint;
            string endpoint2 = appSettings.ProjectEndpoint;
            string model1 = appSettings.Model;
            string model2 = appSettings.Model;
            string name1 = appSettings.AgentName;
            string name2 = appSettings.AgentName;
            string instructions1 = appSettings.AgentInstructions;
            string instructions2 = appSettings.AgentInstructions;

            // Assert
            Assert.That(endpoint1, Is.EqualTo(endpoint2));
            Assert.That(model1, Is.EqualTo(model2));
            Assert.That(name1, Is.EqualTo(name2));
            Assert.That(instructions1, Is.EqualTo(instructions2));

            // Verify each section was accessed for each property access
            configurationMock.Verify(c => c.GetSection("AzureSettings:ProjectEndpoint"), Times.Exactly(2));
            configurationMock.Verify(c => c.GetSection("AzureSettings:Model"), Times.Exactly(2));
            configurationMock.Verify(c => c.GetSection("AzureSettings:AgentName"), Times.Exactly(2));
            configurationMock.Verify(c => c.GetSection("AzureSettings:AgentInstructions"), Times.Exactly(2));
        }
    }
}
