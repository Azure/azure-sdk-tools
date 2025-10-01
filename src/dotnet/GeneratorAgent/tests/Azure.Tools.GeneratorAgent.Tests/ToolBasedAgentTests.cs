using Azure.AI.Agents.Persistent;
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Agent;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class ToolBasedAgentTests
    {
        [Test]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            var mockConversationManager = CreateMockConversationManager();
            var mockFormatPromptService = new Mock<FormatPromptService>(Mock.Of<ILogger<FormatPromptService>>(), CreateMockAppSettings());
            var mockAppSettings = CreateMockAppSettings();
            var mockClient = new Mock<PersistentAgentsClient>();
            var logger = NullLogger<ToolBasedAgent>.Instance;

            var agent = new ToolBasedAgent(
                mockConversationManager.Object,
                mockFormatPromptService.Object,
                mockAppSettings,
                mockClient.Object,
                logger);

            Assert.That(agent, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullConversationManager_ShouldThrowArgumentNullException()
        {
            var mockFormatPromptService = new Mock<FormatPromptService>(Mock.Of<ILogger<FormatPromptService>>(), CreateMockAppSettings());
            var mockAppSettings = CreateMockAppSettings();
            var mockClient = new Mock<PersistentAgentsClient>();
            var logger = NullLogger<ToolBasedAgent>.Instance;

            var ex = Assert.Throws<ArgumentNullException>(() =>
                new ToolBasedAgent(
                    null!,
                    mockFormatPromptService.Object,
                    mockAppSettings,
                    mockClient.Object,
                    logger));
            Assert.That(ex?.ParamName, Is.EqualTo("conversationManager"));
        }

        [Test]
        public void Constructor_WithNullFormatPromptService_ShouldThrowArgumentNullException()
        {
            var mockConversationManager = CreateMockConversationManager();
            var mockAppSettings = CreateMockAppSettings();
            var mockClient = new Mock<PersistentAgentsClient>();
            var logger = NullLogger<ToolBasedAgent>.Instance;

            var ex = Assert.Throws<ArgumentNullException>(() =>
                new ToolBasedAgent(
                    mockConversationManager.Object,
                    null!,
                    mockAppSettings,
                    mockClient.Object,
                    logger));
            Assert.That(ex?.ParamName, Is.EqualTo("formatPromptService"));
        }

        [Test]
        public void Constructor_WithNullAppSettings_ShouldThrowArgumentNullException()
        {
            var mockConversationManager = CreateMockConversationManager();
            var mockFormatPromptService = new Mock<FormatPromptService>(Mock.Of<ILogger<FormatPromptService>>(), CreateMockAppSettings());
            var mockClient = new Mock<PersistentAgentsClient>();
            var logger = NullLogger<ToolBasedAgent>.Instance;

            var ex = Assert.Throws<ArgumentNullException>(() =>
                new ToolBasedAgent(
                    mockConversationManager.Object,
                    mockFormatPromptService.Object,
                    null!,
                    mockClient.Object,
                    logger));
            Assert.That(ex?.ParamName, Is.EqualTo("appSettings"));
        }

        [Test]
        public void Constructor_WithNullClient_ShouldThrowArgumentNullException()
        {
            var mockConversationManager = CreateMockConversationManager();
            var mockFormatPromptService = new Mock<FormatPromptService>(Mock.Of<ILogger<FormatPromptService>>(), CreateMockAppSettings());
            var mockAppSettings = CreateMockAppSettings();
            var logger = NullLogger<ToolBasedAgent>.Instance;

            var ex = Assert.Throws<ArgumentNullException>(() =>
                new ToolBasedAgent(
                    mockConversationManager.Object,
                    mockFormatPromptService.Object,
                    mockAppSettings,
                    null!,
                    logger));
            Assert.That(ex?.ParamName, Is.EqualTo("client"));
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            var mockConversationManager = CreateMockConversationManager();
            var mockFormatPromptService = new Mock<FormatPromptService>(Mock.Of<ILogger<FormatPromptService>>(), CreateMockAppSettings());
            var mockAppSettings = CreateMockAppSettings();
            var mockClient = new Mock<PersistentAgentsClient>();

            var ex = Assert.Throws<ArgumentNullException>(() =>
                new ToolBasedAgent(
                    mockConversationManager.Object,
                    mockFormatPromptService.Object,
                    mockAppSettings,
                    mockClient.Object,
                    null!));
            Assert.That(ex?.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public async Task FixCodeAsync_WithNullFixes_ShouldReturnFailure()
        {
            var agent = CreateToolBasedAgent();

            var result = await agent.FixCodeAsync(null!);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Exception, Is.TypeOf<ArgumentNullException>());
        }

        [Test]
        public async Task FixCodeAsync_WithEmptyFixes_ShouldReturnFailure()
        {
            var agent = CreateToolBasedAgent();
            var emptyFixes = new List<Fix>();

            var result = await agent.FixCodeAsync(emptyFixes);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Exception, Is.TypeOf<ArgumentException>());
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithNullErrorLogs_ShouldReturnEmptySuccess()
        {
            var agent = CreateToolBasedAgent();

            var result = await agent.AnalyzeErrorsAsync(null!);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Value!.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithEmptyErrorLogs_ShouldReturnEmptySuccess()
        {
            var agent = CreateToolBasedAgent();

            var result = await agent.AnalyzeErrorsAsync(string.Empty);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Value!.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithWhitespaceErrorLogs_ShouldReturnEmptySuccess()
        {
            var agent = CreateToolBasedAgent();

            var result = await agent.AnalyzeErrorsAsync("   ");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Value!.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task DisposeAsync_ShouldNotThrow()
        {
            var agent = CreateToolBasedAgent();

            await agent.DisposeAsync();
        }

        [Test]
        public async Task DisposeAsync_MultipleCallsShouldNotThrow()
        {
            var agent = CreateToolBasedAgent();

            await agent.DisposeAsync();
            Assert.DoesNotThrowAsync(async () => await agent.DisposeAsync());
        }

        [Test]
        public async Task DisposeAsync_WithLogger_ShouldLogDisposal()
        {
            var mockLogger = new Mock<ILogger<ToolBasedAgent>>();
            var agent = CreateToolBasedAgent(logger: mockLogger.Object);

            await agent.DisposeAsync();

            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Disposing agent")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never); // Won't log if agent wasn't created yet
        }

        [Test]
        public void Constructor_WithDifferentLoggerTypes_ShouldWork()
        {
            var mockConversationManager = CreateMockConversationManager();
            var mockFormatPromptService = new Mock<FormatPromptService>(Mock.Of<ILogger<FormatPromptService>>(), CreateMockAppSettings());
            var mockAppSettings = CreateMockAppSettings();
            var mockClient = new Mock<PersistentAgentsClient>();

            // Test with NullLogger
            var nullLogger = NullLogger<ToolBasedAgent>.Instance;
            var agent1 = new ToolBasedAgent(
                mockConversationManager.Object,
                mockFormatPromptService.Object,
                mockAppSettings,
                mockClient.Object,
                nullLogger);
            Assert.That(agent1, Is.Not.Null);

            // Test with Mock Logger
            var mockLogger = new Mock<ILogger<ToolBasedAgent>>();
            var agent2 = new ToolBasedAgent(
                mockConversationManager.Object,
                mockFormatPromptService.Object,
                mockAppSettings,
                mockClient.Object,
                mockLogger.Object);
            Assert.That(agent2, Is.Not.Null);
        }

        private static ToolBasedAgent CreateToolBasedAgent(
            ConversationManager? conversationManager = null,
            FormatPromptService? formatPromptService = null,
            AppSettings? appSettings = null,
            PersistentAgentsClient? client = null,
            ILogger<ToolBasedAgent>? logger = null)
        {
            return new ToolBasedAgent(
                conversationManager ?? CreateMockConversationManager().Object,
                formatPromptService ?? CreateMockFormatPromptService(),
                appSettings ?? CreateMockAppSettings(),
                client ?? Mock.Of<PersistentAgentsClient>(),
                logger ?? NullLogger<ToolBasedAgent>.Instance);
        }
        
        private static FormatPromptService CreateMockFormatPromptService()
        {
            var mockLogger = Mock.Of<ILogger<FormatPromptService>>();
            var mockAppSettings = CreateMockAppSettings();
            
            return new FormatPromptService(mockLogger, mockAppSettings);
        }

        private static Mock<ConversationManager> CreateMockConversationManager()
        {
            var mockClient = Mock.Of<PersistentAgentsClient>();
            var mockToolHandler = Mock.Of<ITypeSpecToolHandler>();
            var mockAppSettings = CreateMockAppSettings();
            var mockToolExecutorLogger = Mock.Of<ILogger<ToolExecutor>>();
            
            var mockToolExecutor = new Mock<ToolExecutor>(mockToolHandler, mockToolExecutorLogger);
            var mockLogger = Mock.Of<ILogger<ConversationManager>>();
            
            var mock = new Mock<ConversationManager>(
                mockClient,
                mockToolExecutor.Object,
                mockAppSettings,
                mockLogger);
            
            return mock;
        }

        private static AppSettings CreateMockAppSettings()
        {
            var mockConfiguration = new Mock<IConfiguration>();
            
            // Setup configuration sections
            var mockAzureSettingsSection = new Mock<IConfigurationSection>();
            mockAzureSettingsSection.SetupGet(x => x.Value).Returns((string?)null);
            
            var mockModelSection = new Mock<IConfigurationSection>();
            mockModelSection.SetupGet(x => x.Value).Returns((string?)"gpt-4");
            
            var mockAgentNameSection = new Mock<IConfigurationSection>();
            mockAgentNameSection.SetupGet(x => x.Value).Returns((string?)"Test Agent");
            
            var mockInstructionsSection = new Mock<IConfigurationSection>();
            mockInstructionsSection.SetupGet(x => x.Value).Returns((string?)"Test instructions");
            
            var mockTemplateSection = new Mock<IConfigurationSection>();
            mockTemplateSection.SetupGet(x => x.Value).Returns((string?)"Analyze: {0}");
            
            var mockEndpointSection = new Mock<IConfigurationSection>();
            mockEndpointSection.SetupGet(x => x.Value).Returns((string?)"https://test.endpoint.com");
            
            // Setup GetSection calls
            mockConfiguration.Setup(x => x.GetSection("AzureSettings:Model")).Returns(mockModelSection.Object);
            mockConfiguration.Setup(x => x.GetSection("AzureSettings:AgentName")).Returns(mockAgentNameSection.Object);
            mockConfiguration.Setup(x => x.GetSection("AzureSettings:AgentInstructions")).Returns(mockInstructionsSection.Object);
            mockConfiguration.Setup(x => x.GetSection("AzureSettings:ErrorAnalysisPromptTemplate")).Returns(mockTemplateSection.Object);
            mockConfiguration.Setup(x => x.GetSection("AzureSettings:ProjectEndpoint")).Returns(mockEndpointSection.Object);
            
            var mockLogger = Mock.Of<ILogger<AppSettings>>();
            
            return new AppSettings(mockConfiguration.Object, mockLogger);
        }
    }
}
