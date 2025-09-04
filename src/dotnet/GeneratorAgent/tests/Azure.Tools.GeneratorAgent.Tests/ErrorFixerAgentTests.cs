using Azure.Tools.GeneratorAgent.Agent;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Azure.AI.Agents.Persistent;
using Moq;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class ErrorFixerAgentTests
    {
        [Test]
        public void Constructor_WithNullConversationProcessor_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorFixerAgent(
                conversationProcessor: null!,
                fileManager: CreateMockAgentFileManager(),
                client: Mock.Of<PersistentAgentsClient>(),
                appSettings: CreateTestAppSettings(),
                logger: Mock.Of<ILogger<ErrorFixerAgent>>(),
                fixPromptService: CreateMockFixPromptService()));
            
            Assert.That(ex.ParamName!, Is.EqualTo("conversationProcessor"));
        }

        [Test]
        public void Constructor_WithNullAgentFileManager_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorFixerAgent(
                conversationProcessor: CreateMockConversationProcessor(),
                fileManager: null!,
                client: Mock.Of<PersistentAgentsClient>(),
                appSettings: CreateTestAppSettings(),
                logger: Mock.Of<ILogger<ErrorFixerAgent>>(),
                fixPromptService: CreateMockFixPromptService()));
            
            Assert.That(ex.ParamName!, Is.EqualTo("fileManager"));
        }

        [Test]
        public void Constructor_WithNullClient_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorFixerAgent(
                conversationProcessor: CreateMockConversationProcessor(),
                fileManager: CreateMockAgentFileManager(),
                client: null!,
                appSettings: CreateTestAppSettings(),
                logger: Mock.Of<ILogger<ErrorFixerAgent>>(),
                fixPromptService: CreateMockFixPromptService()));
            
            Assert.That(ex.ParamName!, Is.EqualTo("client"));
        }

        [Test]
        public void Constructor_WithNullAppSettings_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorFixerAgent(
                conversationProcessor: CreateMockConversationProcessor(),
                fileManager: CreateMockAgentFileManager(),
                client: Mock.Of<PersistentAgentsClient>(),
                appSettings: null!,
                logger: Mock.Of<ILogger<ErrorFixerAgent>>(),
                fixPromptService: CreateMockFixPromptService()));
            
            Assert.That(ex.ParamName!, Is.EqualTo("appSettings"));
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorFixerAgent(
                conversationProcessor: CreateMockConversationProcessor(),
                fileManager: CreateMockAgentFileManager(),
                client: Mock.Of<PersistentAgentsClient>(),
                appSettings: CreateTestAppSettings(),
                logger: null!,
                fixPromptService: CreateMockFixPromptService()));
            
            Assert.That(ex.ParamName!, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithNullFixPromptService_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorFixerAgent(
                conversationProcessor: CreateMockConversationProcessor(),
                fileManager: CreateMockAgentFileManager(),
                client: Mock.Of<PersistentAgentsClient>(),
                appSettings: CreateTestAppSettings(),
                logger: Mock.Of<ILogger<ErrorFixerAgent>>(),
                fixPromptService: null!));
            
            Assert.That(ex.ParamName!, Is.EqualTo("fixPromptService"));
        }

        [Test]
        public void Constructor_WithValidParameters_SetsAllDependencies()
        {
            // Arrange
            var conversationProcessor = CreateMockConversationProcessor();
            var AgentFileManager = CreateMockAgentFileManager();
            var client = Mock.Of<PersistentAgentsClient>();
            var appSettings = CreateTestAppSettings();
            var fixPromptService = CreateMockFixPromptService();
            var logger = Mock.Of<ILogger<ErrorFixerAgent>>();

            // Act & Assert - should not throw
            var errorFixerAgent = new ErrorFixerAgent(
                conversationProcessor: conversationProcessor,
                fileManager: AgentFileManager,
                client: client,
                appSettings: appSettings,
                logger: logger,
                fixPromptService: fixPromptService);

            Assert.That(errorFixerAgent, Is.Not.Null);
        }

        [Test]
        public void InitializeAgentEnvironmentAsync_WithNullTypeSpecFiles_ThrowsArgumentNullException()
        {
            // Arrange
            var errorFixerAgent = CreateValidErrorFixerAgent();

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentNullException>(async () => 
                await errorFixerAgent.InitializeAgentEnvironmentAsync(null!, CancellationToken.None));
            
            Assert.That(ex.ParamName!, Is.EqualTo("typeSpecFiles"));
        }

        [Test]
        public void FixCodeAsync_WithNullFixes_ThrowsArgumentNullException()
        {
            // Arrange
            var errorFixerAgent = CreateValidErrorFixerAgent();

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentNullException>(async () => 
                await errorFixerAgent.FixCodeAsync(null!, CancellationToken.None));
            
            Assert.That(ex.ParamName!, Is.EqualTo("fixes"));
        }

        [Test]
        public void AnalyzeErrorsAsync_WithNullErrorLogs_ThrowsArgumentNullException()
        {
            // Arrange
            var errorFixerAgent = CreateValidErrorFixerAgent();

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentNullException>(async () => 
                await errorFixerAgent.AnalyzeErrorsAsync(null!, CancellationToken.None));
            
            Assert.That(ex.ParamName!, Is.EqualTo("errorLogs"));
        }
        private static AgentConversationProcessor CreateMockConversationProcessor()
        {
            return new AgentConversationProcessor(
                Mock.Of<PersistentAgentsClient>(),
                Mock.Of<ILogger<AgentConversationProcessor>>(),
                CreateTestAppSettings());
        }

        private static AgentFileManager CreateMockAgentFileManager()
        {
            return new AgentFileManager(
                Mock.Of<PersistentAgentsClient>(),
                Mock.Of<ILogger<AgentFileManager>>(),
                CreateTestAppSettings());
        }

        private static FixPromptService CreateMockFixPromptService()
        {
            return new FixPromptService(
                Mock.Of<ILogger<FixPromptService>>(),
                CreateTestAppSettings());
        }

        private static ErrorFixerAgent CreateValidErrorFixerAgent()
        {
            return new ErrorFixerAgent(
                conversationProcessor: CreateMockConversationProcessor(),
                fileManager: CreateMockAgentFileManager(),
                client: Mock.Of<PersistentAgentsClient>(),
                appSettings: CreateTestAppSettings(),
                logger: Mock.Of<ILogger<ErrorFixerAgent>>(),
                fixPromptService: CreateMockFixPromptService());
        }

        private static AppSettings CreateTestAppSettings()
        {
            var configMock = new Mock<IConfiguration>();
            var mockLogger = new Mock<ILogger<AppSettings>>();

            var testId = Guid.NewGuid().ToString("N")[..8];

            // Set up default section
            var defaultSection = new Mock<IConfigurationSection>();
            defaultSection.Setup(s => s.Value).Returns((string?)null);
            configMock.Setup(c => c.GetSection(It.IsAny<string>())).Returns(defaultSection.Object);

            // Set up required configuration values
            var projectEndpointSection = new Mock<IConfigurationSection>();
            projectEndpointSection.Setup(s => s.Value).Returns("https://test.example.com");
            configMock.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(projectEndpointSection.Object);

            var modelSection = new Mock<IConfigurationSection>();
            modelSection.Setup(s => s.Value).Returns($"test-model-{testId}");
            configMock.Setup(c => c.GetSection("AzureSettings:Model")).Returns(modelSection.Object);

            var nameSection = new Mock<IConfigurationSection>();
            nameSection.Setup(s => s.Value).Returns($"test-agent-{testId}");
            configMock.Setup(c => c.GetSection("AzureSettings:AgentName")).Returns(nameSection.Object);

            var instructionsSection = new Mock<IConfigurationSection>();
            instructionsSection.Setup(s => s.Value).Returns("test instructions");
            configMock.Setup(c => c.GetSection("AzureSettings:AgentInstructions")).Returns(instructionsSection.Object);

            return new AppSettings(configMock.Object, mockLogger.Object);
        }
    }
}
