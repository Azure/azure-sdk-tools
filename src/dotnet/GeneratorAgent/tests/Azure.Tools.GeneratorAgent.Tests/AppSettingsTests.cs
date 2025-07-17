using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests.Configuration
{
    [TestFixture]
    [Category("Unit")]
    public class AppSettingsTests
    {
        private Mock<IConfiguration> _configurationMock;
        private AppSettings _appSettings;

        [SetUp]
        public void Setup()
        {
            _configurationMock = new Mock<IConfiguration>();
            _appSettings = new AppSettings(_configurationMock.Object);
        }

        [Test]
        [Category("Constructor")]
        public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AppSettings(null!));
        }

        [Test]
        [Category("ProjectEndpoint")]
        public void ProjectEndpoint_WithValidValue_ReturnsValue()
        {
            // Arrange
            var sectionMock = new Mock<IConfigurationSection>();
            sectionMock.Setup(s => s.Value).Returns("https://test-endpoint.com");
            _configurationMock.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(sectionMock.Object);

            // Act
            string result = _appSettings.ProjectEndpoint;

            // Assert
            Assert.That(result, Is.EqualTo("https://test-endpoint.com"));
        }

        [Test]
        [Category("ProjectEndpoint")]
        public void ProjectEndpoint_WithNullValue_ReturnsEmptyString()
        {
            // Arrange
            var sectionMock = new Mock<IConfigurationSection>();
            sectionMock.Setup(s => s.Value).Returns((string)null!);
            _configurationMock.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(sectionMock.Object);

            // Act
            string result = _appSettings.ProjectEndpoint;

            // Assert
            Assert.That(result, Is.EqualTo(""));
        }

        [Test]
        [Category("Model")]
        public void Model_WithValidValue_ReturnsValue()
        {
            // Arrange
            var sectionMock = new Mock<IConfigurationSection>();
            sectionMock.Setup(s => s.Value).Returns("gpt-4-turbo");
            _configurationMock.Setup(c => c.GetSection("AzureSettings:Model")).Returns(sectionMock.Object);

            // Act
            string result = _appSettings.Model;

            // Assert
            Assert.That(result, Is.EqualTo("gpt-4-turbo"));
        }

        [Test]
        [Category("Model")]
        public void Model_WithNullValue_ReturnsDefault()
        {
            // Arrange
            var sectionMock = new Mock<IConfigurationSection>();
            sectionMock.Setup(s => s.Value).Returns((string)null!);
            _configurationMock.Setup(c => c.GetSection("AzureSettings:Model")).Returns(sectionMock.Object);

            // Act
            string result = _appSettings.Model;

            // Assert
            Assert.That(result, Is.EqualTo("gpt-4o"));
        }

        [Test]
        [Category("AgentName")]
        public void AgentName_WithValidValue_ReturnsValue()
        {
            // Arrange
            var sectionMock = new Mock<IConfigurationSection>();
            sectionMock.Setup(s => s.Value).Returns("Custom Agent");
            _configurationMock.Setup(c => c.GetSection("AzureSettings:AgentName")).Returns(sectionMock.Object);

            // Act
            string result = _appSettings.AgentName;

            // Assert
            Assert.That(result, Is.EqualTo("Custom Agent"));
        }

        [Test]
        [Category("AgentName")]
        public void AgentName_WithNullValue_ReturnsDefault()
        {
            // Arrange
            var sectionMock = new Mock<IConfigurationSection>();
            sectionMock.Setup(s => s.Value).Returns((string)null!);
            _configurationMock.Setup(c => c.GetSection("AzureSettings:AgentName")).Returns(sectionMock.Object);

            // Act
            string result = _appSettings.AgentName;

            // Assert
            Assert.That(result, Is.EqualTo("AZC Fixer"));
        }

        [Test]
        [Category("AgentInstructions")]
        public void AgentInstructions_WithValidValue_ReturnsValue()
        {
            // Arrange
            const string instructions = "Custom instructions for the agent";
            var sectionMock = new Mock<IConfigurationSection>();
            sectionMock.Setup(s => s.Value).Returns(instructions);
            _configurationMock.Setup(c => c.GetSection("AzureSettings:AgentInstructions")).Returns(sectionMock.Object);

            // Act
            string result = _appSettings.AgentInstructions;

            // Assert
            Assert.That(result, Is.EqualTo(instructions));
        }

        [Test]
        [Category("AgentInstructions")]
        public void AgentInstructions_WithNullValue_ReturnsEmptyString()
        {
            // Arrange
            var sectionMock = new Mock<IConfigurationSection>();
            sectionMock.Setup(s => s.Value).Returns((string)null!);
            _configurationMock.Setup(c => c.GetSection("AzureSettings:AgentInstructions")).Returns(sectionMock.Object);

            // Act
            string result = _appSettings.AgentInstructions;

            // Assert
            Assert.That(result, Is.EqualTo(""));
        }
    }
}