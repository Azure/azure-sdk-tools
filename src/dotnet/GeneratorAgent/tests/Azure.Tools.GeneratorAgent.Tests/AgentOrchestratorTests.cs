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
    public class AgentOrchestratorTests
    {
        [Test]
        public void Constructor_WithNullAgentProcessor_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new AgentOrchestrator(
                agentProcessor: null!,
                fileManager: CreateMockAgentFileManager(),
                agentThreadManager: CreateMockAgentThreadManager(),
                agentManager: CreateMockAgentManager(),
                fixPromptService: CreateMockFixPromptService(),
                logger: Mock.Of<ILogger<AgentOrchestrator>>()));
            
            Assert.That(ex.ParamName!, Is.EqualTo("agentProcessor"));
        }

        [Test]
        public void Constructor_WithNullAgentFileManager_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new AgentOrchestrator(
                agentProcessor: CreateMockAgentProcessor(),
                fileManager: null!,
                agentThreadManager: CreateMockAgentThreadManager(),
                agentManager: CreateMockAgentManager(),
                fixPromptService: CreateMockFixPromptService(),
                logger: Mock.Of<ILogger<AgentOrchestrator>>()));
            
            Assert.That(ex.ParamName!, Is.EqualTo("fileManager"));
        }

        [Test]
        public void Constructor_WithNullAgentThreadManager_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new AgentOrchestrator(
                agentProcessor: CreateMockAgentProcessor(),
                fileManager: CreateMockAgentFileManager(),
                agentThreadManager: null!,
                agentManager: CreateMockAgentManager(),
                fixPromptService: CreateMockFixPromptService(),
                logger: Mock.Of<ILogger<AgentOrchestrator>>()));
            
            Assert.That(ex.ParamName!, Is.EqualTo("agentThreadManager"));
        }

        [Test]
        public void Constructor_WithNullAgentManager_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new AgentOrchestrator(
                agentProcessor: CreateMockAgentProcessor(),
                fileManager: CreateMockAgentFileManager(),
                agentThreadManager: CreateMockAgentThreadManager(),
                agentManager: null!,
                fixPromptService: CreateMockFixPromptService(),
                logger: Mock.Of<ILogger<AgentOrchestrator>>()));
            
            Assert.That(ex.ParamName!, Is.EqualTo("agentManager"));
        }

        [Test]
        public void Constructor_WithNullFixPromptService_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new AgentOrchestrator(
                agentProcessor: CreateMockAgentProcessor(),
                fileManager: CreateMockAgentFileManager(),
                agentThreadManager: CreateMockAgentThreadManager(),
                agentManager: CreateMockAgentManager(),
                fixPromptService: null!,
                logger: Mock.Of<ILogger<AgentOrchestrator>>()));
            
            Assert.That(ex.ParamName!, Is.EqualTo("fixPromptService"));
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new AgentOrchestrator(
                agentProcessor: CreateMockAgentProcessor(),
                fileManager: CreateMockAgentFileManager(),
                agentThreadManager: CreateMockAgentThreadManager(),
                agentManager: CreateMockAgentManager(),
                fixPromptService: CreateMockFixPromptService(),
                logger: null!));
            
            Assert.That(ex.ParamName!, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithValidParameters_SetsAllDependencies()
        {
            // Arrange
            var agentProcessor = CreateMockAgentProcessor();
            var AgentFileManager = CreateMockAgentFileManager();
            var agentThreadManager = CreateMockAgentThreadManager();
            var agentManager = CreateMockAgentManager();
            var fixPromptService = CreateMockFixPromptService();
            var logger = Mock.Of<ILogger<AgentOrchestrator>>();

            // Act & Assert - should not throw
            var orchestrator = new AgentOrchestrator(
                agentProcessor: agentProcessor,
                fileManager: AgentFileManager,
                agentThreadManager: agentThreadManager,
                agentManager: agentManager,
                fixPromptService: fixPromptService,
                logger: logger);

            Assert.That(orchestrator, Is.Not.Null);
        }

        [Test]
        public void InitializeAgentEnvironmentAsync_WithNullTypeSpecFiles_ThrowsArgumentNullException()
        {
            // Arrange
            var orchestrator = CreateValidOrchestrator();

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentNullException>(async () => 
                await orchestrator.InitializeAgentEnvironmentAsync(null!, CancellationToken.None));
            
            Assert.That(ex.ParamName!, Is.EqualTo("typeSpecFiles"));
        }

        [Test]
        public void FixCodeAsync_WithNullFixes_ThrowsArgumentNullException()
        {
            // Arrange
            var orchestrator = CreateValidOrchestrator();

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentNullException>(async () => 
                await orchestrator.FixCodeAsync(null!, CancellationToken.None));
            
            Assert.That(ex.ParamName!, Is.EqualTo("fixes"));
        }

        [Test]
        public void AnalyzeErrorsAsync_WithNullErrorLogs_ThrowsArgumentNullException()
        {
            // Arrange
            var orchestrator = CreateValidOrchestrator();

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentNullException>(async () => 
                await orchestrator.AnalyzeErrorsAsync(null!, CancellationToken.None));
            
            Assert.That(ex.ParamName!, Is.EqualTo("errorLogs"));
        }

        // Helper methods to create mock dependencies
        private static AgentProcessor CreateMockAgentProcessor()
        {
            return new AgentProcessor(
                Mock.Of<PersistentAgentsClient>(),
                Mock.Of<ILogger<AgentProcessor>>(),
                new AgentResponseParser(Mock.Of<ILogger<AgentResponseParser>>()),
                CreateMockAgentManager(),
                CreateMockAgentThreadManager(),
                CreateTestAppSettings());
        }

        private static AgentFileManager CreateMockAgentFileManager()
        {
            return new AgentFileManager(
                Mock.Of<PersistentAgentsClient>(),
                Mock.Of<ILogger<AgentFileManager>>(),
                CreateTestAppSettings());
        }

        private static AgentThreadManager CreateMockAgentThreadManager()
        {
            return new AgentThreadManager(
                Mock.Of<PersistentAgentsClient>(),
                Mock.Of<ILogger<AgentThreadManager>>(),
                CreateTestAppSettings());
        }

        private static AgentManager CreateMockAgentManager()
        {
            return new AgentManager(
                Mock.Of<PersistentAgentsClient>(),
                Mock.Of<ILogger<AgentManager>>(),
                CreateTestAppSettings());
        }

        private static FixPromptService CreateMockFixPromptService()
        {
            return new FixPromptService(
                Mock.Of<ILogger<FixPromptService>>(),
                CreateTestAppSettings());
        }

        private static AgentOrchestrator CreateValidOrchestrator()
        {
            return new AgentOrchestrator(
                agentProcessor: CreateMockAgentProcessor(),
                fileManager: CreateMockAgentFileManager(),
                agentThreadManager: CreateMockAgentThreadManager(),
                agentManager: CreateMockAgentManager(),
                fixPromptService: CreateMockFixPromptService(),
                logger: Mock.Of<ILogger<AgentOrchestrator>>());
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
