using Azure.AI.Agents.Persistent;
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
    public class ConversationManagerTests
    {
        [Test]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            var mockClient = new Mock<PersistentAgentsClient>();
            var mockToolExecutor = CreateMockToolExecutor();
            var mockAppSettings = CreateMockAppSettings();
            var logger = NullLogger<ConversationManager>.Instance;

            var manager = new ConversationManager(mockClient.Object, mockToolExecutor, mockAppSettings, logger);

            Assert.That(manager, Is.Not.Null);
            Assert.That(manager.AgentId, Is.Null);
            Assert.That(manager.ThreadId, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Constructor_WithNullClient_ShouldThrowArgumentNullException()
        {
            var mockToolExecutor = CreateMockToolExecutor();
            var mockAppSettings = CreateMockAppSettings();
            var logger = NullLogger<ConversationManager>.Instance;

            var ex = Assert.Throws<ArgumentNullException>(() => 
                new ConversationManager(null!, mockToolExecutor, mockAppSettings, logger));
            Assert.That(ex?.ParamName, Is.EqualTo("client"));
        }

        [Test]
        public void Constructor_WithNullToolExecutor_ShouldThrowArgumentNullException()
        {
            var mockClient = new Mock<PersistentAgentsClient>();
            var mockAppSettings = CreateMockAppSettings();
            var logger = NullLogger<ConversationManager>.Instance;

            var ex = Assert.Throws<ArgumentNullException>(() => 
                new ConversationManager(mockClient.Object, null!, mockAppSettings, logger));
            Assert.That(ex?.ParamName, Is.EqualTo("toolExecutor"));
        }

        [Test]
        public void Constructor_WithNullAppSettings_ShouldThrowArgumentNullException()
        {
            var mockClient = new Mock<PersistentAgentsClient>();
            var mockToolExecutor = CreateMockToolExecutor();
            var logger = NullLogger<ConversationManager>.Instance;

            var ex = Assert.Throws<ArgumentNullException>(() => 
                new ConversationManager(mockClient.Object, mockToolExecutor, null!, logger));
            Assert.That(ex?.ParamName, Is.EqualTo("appSettings"));
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            var mockClient = new Mock<PersistentAgentsClient>();
            var mockToolExecutor = CreateMockToolExecutor();
            var mockAppSettings = CreateMockAppSettings();

            var ex = Assert.Throws<ArgumentNullException>(() => 
                new ConversationManager(mockClient.Object, mockToolExecutor, mockAppSettings, null!));
            Assert.That(ex?.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void SetAgentId_WithValidId_ShouldSetProperty()
        {
            var manager = CreateConversationManager();
            var agentId = "test-agent-id";

            manager.AgentId = agentId;

            Assert.That(manager.AgentId, Is.EqualTo(agentId));
        }

        [Test]
        public void SetAgentId_WithNullId_ShouldThrowArgumentNullException()
        {
            var manager = CreateConversationManager();

            var ex = Assert.Throws<ArgumentNullException>(() => manager.AgentId = null!);
            Assert.That(ex?.ParamName, Is.EqualTo("AgentId"));
        }

        [Test]
        public void SetAgentId_MultipleCallsUpdateProperty()
        {
            var manager = CreateConversationManager();

            manager.AgentId = "first-id";
            Assert.That(manager.AgentId, Is.EqualTo("first-id"));

            manager.AgentId = "second-id";
            Assert.That(manager.AgentId, Is.EqualTo("second-id"));
        }

        [Test]
        public void SendMessageAsync_WithoutStartedConversation_ShouldThrowInvalidOperationException()
        {
            var manager = CreateConversationManager();

            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => 
                await manager.SendMessageAsync("test message"));
            Assert.That(ex?.Message, Is.EqualTo("Conversation not started. Call StartConversationAsync first."));
        }

        [Test]
        public async Task DeleteThreadAsync_WithEmptyThreadId_ShouldReturnWithoutError()
        {
            var mockLogger = new Mock<ILogger<ConversationManager>>();
            var manager = CreateConversationManager(logger: mockLogger.Object);

            await manager.DeleteThreadAsync();

            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No thread to delete")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public void PropertiesInitializedCorrectly()
        {
            var manager = CreateConversationManager();

            Assert.That(manager.AgentId, Is.Null);
            Assert.That(manager.ThreadId, Is.EqualTo(string.Empty));
        }

        private static ConversationManager CreateConversationManager(
            PersistentAgentsClient? client = null,
            ToolExecutor? toolExecutor = null,
            AppSettings? appSettings = null,
            ILogger<ConversationManager>? logger = null)
        {
            return new ConversationManager(
                client ?? Mock.Of<PersistentAgentsClient>(),
                toolExecutor ?? CreateMockToolExecutor(),
                appSettings ?? CreateMockAppSettings(),
                logger ?? NullLogger<ConversationManager>.Instance);
        }

        private static ToolExecutor CreateMockToolExecutor()
        {
            var mockToolHandler = Mock.Of<ITypeSpecToolHandler>();
            var mockAppSettings = CreateMockAppSettings();
            var mockLogger = Mock.Of<ILogger<ToolExecutor>>();
            
            Func<ValidationContext, ITypeSpecToolHandler> toolHandlerFactory = _ => mockToolHandler;
            
            return new ToolExecutor(toolHandlerFactory, mockAppSettings, mockLogger);
        }

        private static AppSettings CreateMockAppSettings()
        {
            // Use actual AppSettings with a test configuration instead of mocking non-virtual properties
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "AzureSettings:AgentRunMaxWaitTimeSeconds", "30" },
                    { "AzureSettings:AgentRunPollingIntervalSeconds", "1" }
                })
                .Build();
            var logger = NullLogger<AppSettings>.Instance;
            return new AppSettings(configuration, logger);
        }
    }
}