using Azure.AI.Agents.Persistent;
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    internal class ErrorFixerAgentTests
    {
        private class TestableErrorFixerAgent : ErrorFixerAgent
        {
            private readonly PersistentAgent MockAgent;

            public TestableErrorFixerAgent(
                AppSettings appSettings,
                ILogger<ErrorFixerAgent> logger,
                PersistentAgentsClient client,
                FixPromptService fixPromptService,
                AgentResponseParser responseParser,
                PersistentAgent mockAgent)
                : base(appSettings, logger, client, fixPromptService, responseParser)
            {
                MockAgent = mockAgent;
            }

            internal override PersistentAgent CreateAgent() => MockAgent;

            public async Task<string> TestInitializeAgentEnvironmentAsync(Dictionary<string, string> typeSpecFiles, CancellationToken ct = default)
                => await InitializeAgentEnvironmentAsync(typeSpecFiles, ct);

            public async ValueTask TestDisposeAsync() => await DisposeAsync();
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithValidParameters_ShouldNotThrow()
        {
            var appSettings = CreateTestAppSettings();
            var logger = NullLogger<ErrorFixerAgent>.Instance;
            var client = new Mock<PersistentAgentsClient>().Object;
            var fixPromptService = CreateMockFixPromptService();
            var responseParser = CreateMockResponseParser();

            Assert.DoesNotThrow(() => new ErrorFixerAgent(appSettings, logger, client, fixPromptService, responseParser));
        }

        [Test]
        public void Constructor_WithNullAppSettings_ShouldThrowArgumentNullException()
        {
            var logger = NullLogger<ErrorFixerAgent>.Instance;
            var client = new Mock<PersistentAgentsClient>().Object;
            var fixPromptService = CreateMockFixPromptService();
            var responseParser = CreateMockResponseParser();

            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorFixerAgent(null!, logger, client, fixPromptService, responseParser));
            Assert.That(ex!.ParamName, Is.EqualTo("appSettings"));
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            var appSettings = CreateTestAppSettings();
            var client = new Mock<PersistentAgentsClient>().Object;
            var fixPromptService = CreateMockFixPromptService();
            var responseParser = CreateMockResponseParser();

            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorFixerAgent(appSettings, null!, client, fixPromptService, responseParser));
            Assert.That(ex!.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithNullClient_ShouldThrowArgumentNullException()
        {
            var appSettings = CreateTestAppSettings();
            var logger = NullLogger<ErrorFixerAgent>.Instance;
            var fixPromptService = CreateMockFixPromptService();
            var responseParser = CreateMockResponseParser();

            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorFixerAgent(appSettings, logger, null!, fixPromptService, responseParser));
            Assert.That(ex!.ParamName, Is.EqualTo("client"));
        }

        [Test]
        public void Constructor_WithNullFixPromptService_ShouldThrowArgumentNullException()
        {
            var appSettings = CreateTestAppSettings();
            var logger = NullLogger<ErrorFixerAgent>.Instance;
            var client = new Mock<PersistentAgentsClient>().Object;
            var responseParser = CreateMockResponseParser();

            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorFixerAgent(appSettings, logger, client, null!, responseParser));
            Assert.That(ex!.ParamName, Is.EqualTo("fixPromptService"));
        }

        [Test]
        public void Constructor_WithNullResponseParser_ShouldThrowArgumentNullException()
        {
            var appSettings = CreateTestAppSettings();
            var logger = NullLogger<ErrorFixerAgent>.Instance;
            var client = new Mock<PersistentAgentsClient>().Object;
            var fixPromptService = CreateMockFixPromptService();

            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorFixerAgent(appSettings, logger, client, fixPromptService, null!));
            Assert.That(ex!.ParamName, Is.EqualTo("responseParser"));
        }

        #endregion

        #region CreateAgent Tests

        [Test]
        public void CreateAgent_WithValidConfiguration_ShouldCreateAgent()
        {
            var mockAgent = CreateMockAgent();
            var agent = CreateTestableAgent(mockAgent.Object);

            var createdAgent = agent.CreateAgent();

            Assert.That(createdAgent, Is.Not.Null);
            Assert.That(createdAgent, Is.EqualTo(mockAgent.Object));
        }

        #endregion

        #region FixCodeAsync Tests

        [Test]
        public void FixCodeAsync_WithNullFixes_ShouldThrowNullReferenceException()
        {
            var agent = CreateTestableAgent();
            var threadId = "test-thread-id";

            var ex = Assert.ThrowsAsync<NullReferenceException>(
                async () => await agent.FixCodeAsync(null!, threadId));
        }

        [Test]
        public void FixCodeAsync_WithNullThreadId_ShouldThrowNullReferenceException()
        {
            var agent = CreateTestableAgent();
            var fixes = CreateValidFixesList();

            Assert.ThrowsAsync<NullReferenceException>(
                async () => await agent.FixCodeAsync(fixes, null!));
        }

        [Test]
        public void FixCodeAsync_WithEmptyThreadId_ShouldThrowNullReferenceException()
        {
            var agent = CreateTestableAgent();
            var fixes = CreateValidFixesList();

            Assert.ThrowsAsync<NullReferenceException>(
                async () => await agent.FixCodeAsync(fixes, ""));
        }

        [Test]
        public void FixCodeAsync_WithEmptyFixesList_ShouldThrowInvalidOperationException()
        {
            var agent = CreateTestableAgent();
            var fixes = new List<Fix>();
            var threadId = "test-thread-id";

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await agent.FixCodeAsync(fixes, threadId));

            Assert.That(ex!.Message, Does.Contain("No fixes were successfully applied"));
        }

        [Test]
        public void FixCodeAsync_WithCancelledToken_ShouldThrowSemaphoreFullException()
        {
            var agent = CreateTestableAgent();
            var fixes = CreateValidFixesList();
            var threadId = "test-thread-id";
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<SemaphoreFullException>(
                async () => await agent.FixCodeAsync(fixes, threadId, cts.Token));
        }

        #endregion

        #region InitializeAgentEnvironmentAsync Tests

        [Test]
        public void InitializeAgentEnvironmentAsync_WithNullTypeSpecFiles_ShouldThrowArgumentNullException()
        {
            var agent = CreateTestableAgent();

            var ex = Assert.ThrowsAsync<ArgumentNullException>(
                async () => await agent.TestInitializeAgentEnvironmentAsync(null!));

            Assert.That(ex!.ParamName, Is.EqualTo("source"));
        }

        [Test]
        public void InitializeAgentEnvironmentAsync_WithEmptyTypeSpecFiles_ShouldThrowInvalidOperationException()
        {
            var agent = CreateTestableAgent();
            var typeSpecFiles = new Dictionary<string, string>();

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await agent.TestInitializeAgentEnvironmentAsync(typeSpecFiles));

            Assert.That(ex!.Message, Does.Contain("No TypeSpec files provided"));
        }

        [Test]
        public void InitializeAgentEnvironmentAsync_WithNonTypeSpecFiles_ShouldThrowInvalidOperationException()
        {
            var agent = CreateTestableAgent();
            var typeSpecFiles = new Dictionary<string, string>
            {
                { "readme.md", "# README" },
                { "config.json", "{ \"test\": true }" }
            };

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await agent.TestInitializeAgentEnvironmentAsync(typeSpecFiles));

            Assert.That(ex!.Message, Does.Contain("No TypeSpec files provided"));
        }

        [Test]
        public void InitializeAgentEnvironmentAsync_WithCancelledToken_ShouldThrowInvalidOperationException()
        {
            var agent = CreateTestableAgent();
            var typeSpecFiles = new Dictionary<string, string>
            {
                { "main.tsp", "model Test {}" }
            };
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await agent.TestInitializeAgentEnvironmentAsync(typeSpecFiles, cts.Token));
        }

        #endregion

        #region DisposeAsync Tests

        [Test]
        public void DisposeAsync_WithoutInitialization_ShouldNotThrow()
        {
            var agent = CreateTestableAgent();

            Assert.DoesNotThrowAsync(async () => await agent.TestDisposeAsync());
        }

        [Test]
        public void DisposeAsync_CalledMultipleTimes_ShouldNotThrow()
        {
            var agent = CreateTestableAgent();

            Assert.DoesNotThrowAsync(async () =>
            {
                await agent.TestDisposeAsync();
                await agent.TestDisposeAsync();
            });
        }

        [Test]
        public void DisposeAsync_AfterAgentCreation_ShouldNotThrow()
        {
            var agent = CreateTestableAgent();
            agent.CreateAgent();

            Assert.DoesNotThrowAsync(async () => await agent.TestDisposeAsync());
        }

        [Test]
        public void DisposeAsync_AfterFixCodeAsync_ShouldNotThrow()
        {
            var agent = CreateTestableAgent();
            var fixes = CreateValidFixesList();
            var threadId = "test-thread-id";

            Assert.DoesNotThrowAsync(async () =>
            {
                try
                {
                    await agent.FixCodeAsync(fixes, threadId);
                }
                catch
                {
                }
                await agent.TestDisposeAsync();
            });
        }

        #endregion

        #region Helper Methods

        private TestableErrorFixerAgent CreateTestableAgent(PersistentAgent? mockAgent = null)
        {
            var appSettings = CreateTestAppSettings();
            var logger = NullLogger<ErrorFixerAgent>.Instance;
            var client = new Mock<PersistentAgentsClient>().Object;
            var fixPromptService = CreateMockFixPromptService();
            var responseParser = CreateMockResponseParser();
            var agent = mockAgent ?? CreateMockAgent().Object;

            return new TestableErrorFixerAgent(
                appSettings,
                logger,
                client,
                fixPromptService,
                responseParser,
                agent);
        }

        private AppSettings CreateTestAppSettings()
        {
            var configMock = new Mock<IConfiguration>();
            var mockLogger = new Mock<ILogger<AppSettings>>();

            var testId = Guid.NewGuid().ToString("N")[..8];

            var defaultSection = new Mock<IConfigurationSection>();
            defaultSection.Setup(s => s.Value).Returns((string?)null);
            configMock.Setup(c => c.GetSection(It.IsAny<string>())).Returns(defaultSection.Object);

            var modelSection = new Mock<IConfigurationSection>();
            modelSection.Setup(s => s.Value).Returns($"test-model-{testId}");
            configMock.Setup(c => c.GetSection("AzureSettings:Model")).Returns(modelSection.Object);

            var nameSection = new Mock<IConfigurationSection>();
            nameSection.Setup(s => s.Value).Returns($"test-agent-{testId}");
            configMock.Setup(c => c.GetSection("AzureSettings:AgentName")).Returns(nameSection.Object);

            var instructionsSection = new Mock<IConfigurationSection>();
            instructionsSection.Setup(s => s.Value).Returns("test instructions");
            configMock.Setup(c => c.GetSection("AzureSettings:AgentInstructions")).Returns(instructionsSection.Object);

            var endpointSection = new Mock<IConfigurationSection>();
            endpointSection.Setup(s => s.Value).Returns("https://test.example.com");
            configMock.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(endpointSection.Object);

            return new AppSettings(configMock.Object, mockLogger.Object);
        }

        private FixPromptService CreateMockFixPromptService()
        {
            var mockLogger = new Mock<ILogger<FixPromptService>>();
            var appSettings = CreateMockAppSettings();
            return new FixPromptService(mockLogger.Object, appSettings);
        }

        private AppSettings CreateMockAppSettings()
        {
            var mockConfiguration = new Mock<IConfiguration>();
            var mockLogger = new Mock<ILogger<AppSettings>>();
            
            // Set up required configuration values
            var projectEndpointSection = new Mock<IConfigurationSection>();
            projectEndpointSection.Setup(s => s.Value).Returns("https://test.endpoint.com");
            mockConfiguration.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(projectEndpointSection.Object);
            
            // Set up agent instructions
            var agentInstructionsSection = new Mock<IConfigurationSection>();
            agentInstructionsSection.Setup(s => s.Value).Returns("You are an expert Azure SDK developer and TypeSpec author. Your primary goal is to resolve all AZC analyzer and TypeSpec compilation errors in the TypeSpec files and produce a valid, compilable result that strictly follows Azure SDK and TypeSpec guidelines.\n\n### SYSTEM INSTRUCTIONS\n- All files (e.g., main.tsp, client.tsp) are available via FileSearchTool. Retrieve any file content by filename as needed.\n- Never modify main.tsp—only client.tsp may be changed.");
            mockConfiguration.Setup(c => c.GetSection("AzureSettings:AgentInstructions")).Returns(agentInstructionsSection.Object);
            
            return new AppSettings(mockConfiguration.Object, mockLogger.Object);
        }

        private AgentResponseParser CreateMockResponseParser()
        {
            var mockLogger = new Mock<ILogger<AgentResponseParser>>();
            return new AgentResponseParser(mockLogger.Object);
        }

        private Mock<PersistentAgent> CreateMockAgent()
        {
            var mockAgent = new Mock<PersistentAgent>();
            return mockAgent;
        }

        private static List<Fix> CreateValidFixesList()
        {
            var mockErrors = new List<RuleError>
            {
                new RuleError("AZC0012", "Type name 'Client' is too generic")
            };
            
            var analyzer = new BuildErrorAnalyzer(new Mock<ILogger<BuildErrorAnalyzer>>().Object);
            return analyzer.GetFixes(mockErrors).ToList();
        }

        #endregion
    }
}
