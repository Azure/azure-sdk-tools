using System.Reflection;
using Azure.AI.Agents.Persistent;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using System.ClientModel.Primitives;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class ErrorFixerAgentTests
    {
        private static AppSettings CreateTestAppSettings(
            string model = "test-model",
            string agentName = "test-agent",
            string agentInstructions = "test instructions",
            string projectEndpoint = "https://test.example.com")
        {
            var configMock = new Mock<IConfiguration>();
            
            var modelSection = new Mock<IConfigurationSection>();
            modelSection.Setup(s => s.Value).Returns(model);
            configMock.Setup(c => c.GetSection("AzureSettings:Model")).Returns(modelSection.Object);

            var nameSection = new Mock<IConfigurationSection>();
            nameSection.Setup(s => s.Value).Returns(agentName);
            configMock.Setup(c => c.GetSection("AzureSettings:AgentName")).Returns(nameSection.Object);

            var instructionsSection = new Mock<IConfigurationSection>();
            instructionsSection.Setup(s => s.Value).Returns(agentInstructions);
            configMock.Setup(c => c.GetSection("AzureSettings:AgentInstructions")).Returns(instructionsSection.Object);

            var endpointSection = new Mock<IConfigurationSection>();
            endpointSection.Setup(s => s.Value).Returns(projectEndpoint);
            configMock.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(endpointSection.Object);

            return new AppSettings(configMock.Object);
        }

        private static Mock<PersistentAgentsAdministrationClient> CreateAdminClientMock()
        {
            return new Mock<PersistentAgentsAdministrationClient>(MockBehavior.Strict);
        }

        private static Mock<ILogger<ErrorFixerAgent>> CreateLoggerMock()
        {
            return new Mock<ILogger<ErrorFixerAgent>>();
        }

        private static ErrorFixerAgent CreateErrorFixerAgent(
            AppSettings? appSettings = null,
            ILogger<ErrorFixerAgent>? logger = null,
            PersistentAgentsAdministrationClient? adminClient = null)
        {
            return new ErrorFixerAgent(
                appSettings ?? CreateTestAppSettings(),
                logger ?? NullLogger<ErrorFixerAgent>.Instance,
                adminClient ?? CreateAdminClientMock().Object);
        }

        private static Response<PersistentAgent> CreateAgentResponse(
            string id = "test-agent-id",
            string name = "test-agent",
            string model = "test-model",
            string instructions = "test instructions")
        {
            var json = $@"{{
                ""id"": ""{id}"",
                ""createdAt"": ""{DateTimeOffset.UtcNow:O}"",
                ""name"": ""{name}"",
                ""description"": ""{instructions}"",
                ""model"": ""{model}"",
                ""instructions"": ""{instructions}"",
                ""tools"": [{{
                    ""type"": ""file_search""
                }}],
                ""toolResources"": {{}},
                ""metadata"": {{}}
            }}";

            var payload = BinaryData.FromString(json);
            var ctor = typeof(PersistentAgent)
                .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null)!;
            var rawAgent = (PersistentAgent)ctor.Invoke(null)!;

            var agent = ((IPersistableModel<PersistentAgent>)rawAgent)
                .Create(payload, ModelReaderWriterOptions.Json);

            return Response.FromValue(agent, Mock.Of<Response>());
        }

        private static AsyncPageable<PersistentAgent> CreateAgentPageable(params PersistentAgent[] agents)
        {
            var page = Page<PersistentAgent>.FromValues(agents, null, Mock.Of<Response>());
            return AsyncPageable<PersistentAgent>.FromPages(new[] { page });
        }

        [Test]
        public void Constructor_WithNullAppSettings_ThrowsArgumentNullException()
        {
            // Arrange
            var adminClient = CreateAdminClientMock().Object;
            var logger = NullLogger<ErrorFixerAgent>.Instance;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorFixerAgent(null!, logger, adminClient));
            Assert.That(ex.ParamName, Is.EqualTo("appSettings"));
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var adminClient = CreateAdminClientMock().Object;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorFixerAgent(appSettings, null!, adminClient));
            Assert.That(ex.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithNullAdminClient_ThrowsArgumentNullException()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var logger = NullLogger<ErrorFixerAgent>.Instance;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorFixerAgent(appSettings, logger, null!));
            Assert.That(ex.ParamName, Is.EqualTo("adminClient"));
        }

        [Test]
        public void Constructor_WithValidParameters_DoesNotThrow()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var logger = NullLogger<ErrorFixerAgent>.Instance;
            var adminClient = CreateAdminClientMock().Object;

            // Act & Assert
            Assert.DoesNotThrow(() => new ErrorFixerAgent(appSettings, logger, adminClient));
        }

        [Test]
        public async Task FixCodeAsync_FirstCall_CreatesAgent()
        {
            // Arrange
            var appSettings = CreateTestAppSettings("gpt-4", "TestAgent", "Fix code");
            var adminClientMock = CreateAdminClientMock();
            var agentResponse = CreateAgentResponse("agent-123", "TestAgent", "gpt-4", "Fix code");

            adminClientMock
                .Setup(a => a.CreateAgent(
                    "gpt-4",
                    "TestAgent",
                    It.IsAny<string>(),
                    "Fix code",
                    It.Is<IEnumerable<ToolDefinition>>(tools => tools.OfType<FileSearchToolDefinition>().Any()),
                    It.IsAny<ToolResources>(),
                    It.IsAny<float?>(),
                    It.IsAny<float?>(),
                    It.IsAny<BinaryData>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(agentResponse);

            var agent = CreateErrorFixerAgent(appSettings, adminClient: adminClientMock.Object);

            // Act
            await agent.FixCodeAsync(CancellationToken.None);

            // Assert
            adminClientMock.Verify(
                a => a.CreateAgent(
                    "gpt-4",
                    "TestAgent",
                    It.IsAny<string>(),
                    "Fix code",
                    It.Is<IEnumerable<ToolDefinition>>(tools => tools.OfType<FileSearchToolDefinition>().Any()),
                    It.IsAny<ToolResources>(),
                    It.IsAny<float?>(),
                    It.IsAny<float?>(),
                    It.IsAny<BinaryData>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task FixCodeAsync_CreatesAgentWithExactlyOneFileSearchTool()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var adminClientMock = CreateAdminClientMock();
            var agentResponse = CreateAgentResponse();

            adminClientMock
                .Setup(a => a.CreateAgent(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.Is<IEnumerable<ToolDefinition>>(tools => 
                        tools.Count() == 1 && 
                        tools.Single() is FileSearchToolDefinition),
                    It.IsAny<ToolResources>(),
                    It.IsAny<float?>(),
                    It.IsAny<float?>(),
                    It.IsAny<BinaryData>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(agentResponse);

            var agent = CreateErrorFixerAgent(appSettings, adminClient: adminClientMock.Object);

            // Act
            await agent.FixCodeAsync(CancellationToken.None);

            // Assert
            adminClientMock.Verify(
                a => a.CreateAgent(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.Is<IEnumerable<ToolDefinition>>(tools => 
                        tools.Count() == 1 && 
                        tools.Single() is FileSearchToolDefinition),
                    It.IsAny<ToolResources>(),
                    It.IsAny<float?>(),
                    It.IsAny<float?>(),
                    It.IsAny<BinaryData>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task FixCodeAsync_MultipleCallsOnSameInstance_CreatesAgentOnce()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var adminClientMock = CreateAdminClientMock();
            var agentResponse = CreateAgentResponse();

            adminClientMock
                .Setup(a => a.CreateAgent(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<ToolDefinition>>(),
                    It.IsAny<ToolResources>(),
                    It.IsAny<float?>(),
                    It.IsAny<float?>(),
                    It.IsAny<BinaryData>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(agentResponse);

            var agent = CreateErrorFixerAgent(appSettings, adminClient: adminClientMock.Object);

            // Act
            await agent.FixCodeAsync(CancellationToken.None);
            await agent.FixCodeAsync(CancellationToken.None);
            await agent.FixCodeAsync(CancellationToken.None);

            // Assert
            adminClientMock.Verify(
                a => a.CreateAgent(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<ToolDefinition>>(),
                    It.IsAny<ToolResources>(),
                    It.IsAny<float?>(),
                    It.IsAny<float?>(),
                    It.IsAny<BinaryData>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public void FixCodeAsync_AgentCreationFails_ThrowsInvalidOperationException()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var adminClientMock = CreateAdminClientMock();

            adminClientMock
                .Setup(a => a.CreateAgent(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<ToolDefinition>>(),
                    It.IsAny<ToolResources>(),
                    It.IsAny<float?>(),
                    It.IsAny<float?>(),
                    It.IsAny<BinaryData>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("Agent creation failed"));

            var agent = CreateErrorFixerAgent(appSettings, adminClient: adminClientMock.Object);

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await agent.FixCodeAsync(CancellationToken.None));
            Assert.That(ex.Message, Is.EqualTo("Agent creation failed"));
        }

        [Test]
        public void FixCodeAsync_AgentCreationReturnsNullId_ThrowsInvalidOperationException()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var adminClientMock = CreateAdminClientMock();
            var agentResponse = CreateAgentResponse(""); // Empty ID

            adminClientMock
                .Setup(a => a.CreateAgent(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<ToolDefinition>>(),
                    It.IsAny<ToolResources>(),
                    It.IsAny<float?>(),
                    It.IsAny<float?>(),
                    It.IsAny<BinaryData>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(agentResponse);

            var agent = CreateErrorFixerAgent(appSettings, adminClient: adminClientMock.Object);

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await agent.FixCodeAsync(CancellationToken.None));
            Assert.That(ex.Message, Is.EqualTo("Failed to create AZC Fixer agent"));
        }

        [Test]
        public void FixCodeAsync_AgentCreationReturnsNullAgent_ThrowsInvalidOperationException()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var adminClientMock = CreateAdminClientMock();
            
            // Create response with null agent
            var response = Response.FromValue<PersistentAgent>(null!, Mock.Of<Response>());

            adminClientMock
                .Setup(a => a.CreateAgent(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<ToolDefinition>>(),
                    It.IsAny<ToolResources>(),
                    It.IsAny<float?>(),
                    It.IsAny<float?>(),
                    It.IsAny<BinaryData>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(response);

            var agent = CreateErrorFixerAgent(appSettings, adminClient: adminClientMock.Object);

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await agent.FixCodeAsync(CancellationToken.None));
            Assert.That(ex.Message, Is.EqualTo("Failed to create AZC Fixer agent"));
        }

        [Test]
        public async Task FixCodeAsync_LogsAgentCreation()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var adminClientMock = CreateAdminClientMock();
            var loggerMock = CreateLoggerMock();
            var agentResponse = CreateAgentResponse("agent-123", "TestAgent");

            adminClientMock
                .Setup(a => a.CreateAgent(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<ToolDefinition>>(),
                    It.IsAny<ToolResources>(),
                    It.IsAny<float?>(),
                    It.IsAny<float?>(),
                    It.IsAny<BinaryData>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(agentResponse);

            var agent = CreateErrorFixerAgent(appSettings, loggerMock.Object, adminClientMock.Object);

            // Act
            await agent.FixCodeAsync(CancellationToken.None);

            // Assert
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Creating AZC Fixer agent")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Agent created successfully")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task DisposeAsync_WithoutAgentCreation_DoesNotDeleteAnything()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var adminClientMock = CreateAdminClientMock();
            var agent = CreateErrorFixerAgent(appSettings, adminClient: adminClientMock.Object);

            // Act
            await agent.DisposeAsync();

            // Assert
            adminClientMock.Verify(
                a => a.GetAgentsAsync(
                    It.IsAny<int?>(),
                    It.IsAny<ListSortOrder?>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            adminClientMock.Verify(
                a => a.DeleteAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public async Task DisposeAsync_WithCreatedAgent_DeletesAllAgents()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var adminClientMock = CreateAdminClientMock();
            var agentResponse = CreateAgentResponse("agent-123");
            var agents = new[] { agentResponse.Value };

            adminClientMock
                .Setup(a => a.CreateAgent(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<ToolDefinition>>(),
                    It.IsAny<ToolResources>(),
                    It.IsAny<float?>(),
                    It.IsAny<float?>(),
                    It.IsAny<BinaryData>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(agentResponse);

            adminClientMock
                .Setup(a => a.GetAgentsAsync(
                    It.IsAny<int?>(),
                    It.IsAny<ListSortOrder?>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(CreateAgentPageable(agents));

            adminClientMock
                .Setup(a => a.DeleteAgentAsync("agent-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

            var agent = CreateErrorFixerAgent(appSettings, adminClient: adminClientMock.Object);

            // Act
            await agent.FixCodeAsync(CancellationToken.None); // Create agent
            await agent.DisposeAsync();

            // Assert
            adminClientMock.Verify(
                a => a.DeleteAgentAsync("agent-123", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task DisposeAsync_WithMultipleAgents_DeletesAllAgents()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var adminClientMock = CreateAdminClientMock();
            var loggerMock = CreateLoggerMock();
            var agentResponse = CreateAgentResponse("agent-123");
            
            var agent1 = CreateAgentResponse("agent-1", "Agent1").Value;
            var agent2 = CreateAgentResponse("agent-2", "Agent2").Value;
            var agent3 = CreateAgentResponse("agent-3", "Agent3").Value;
            var agents = new[] { agent1, agent2, agent3 };

            adminClientMock
                .Setup(a => a.CreateAgent(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<ToolDefinition>>(),
                    It.IsAny<ToolResources>(),
                    It.IsAny<float?>(),
                    It.IsAny<float?>(),
                    It.IsAny<BinaryData>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(agentResponse);

            adminClientMock
                .Setup(a => a.GetAgentsAsync(
                    It.IsAny<int?>(),
                    It.IsAny<ListSortOrder?>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(CreateAgentPageable(agents));

            // Setup successful deletions
            adminClientMock
                .Setup(a => a.DeleteAgentAsync("agent-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
            adminClientMock
                .Setup(a => a.DeleteAgentAsync("agent-2", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
            adminClientMock
                .Setup(a => a.DeleteAgentAsync("agent-3", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

            var agent = CreateErrorFixerAgent(appSettings, loggerMock.Object, adminClientMock.Object);

            // Act
            await agent.FixCodeAsync(CancellationToken.None); // Create agent
            await agent.DisposeAsync();

            // Assert
            adminClientMock.Verify(a => a.DeleteAgentAsync("agent-1", It.IsAny<CancellationToken>()), Times.Once);
            adminClientMock.Verify(a => a.DeleteAgentAsync("agent-2", It.IsAny<CancellationToken>()), Times.Once);
            adminClientMock.Verify(a => a.DeleteAgentAsync("agent-3", It.IsAny<CancellationToken>()), Times.Once);

            // Verify success logging for each agent
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Deleted agent: Agent1 (agent-1)")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task DisposeAsync_WithMixedDeleteResults_LogsErrorsAndSuccesses()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var adminClientMock = CreateAdminClientMock();
            var loggerMock = CreateLoggerMock();
            var agentResponse = CreateAgentResponse("agent-123");
            
            var agent1 = CreateAgentResponse("agent-1", "Agent1").Value;
            var agent2 = CreateAgentResponse("agent-2", "Agent2").Value;
            var agent3 = CreateAgentResponse("agent-3", "Agent3").Value;
            var agents = new[] { agent1, agent2, agent3 };

            adminClientMock
                .Setup(a => a.CreateAgent(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<ToolDefinition>>(),
                    It.IsAny<ToolResources>(),
                    It.IsAny<float?>(),
                    It.IsAny<float?>(),
                    It.IsAny<BinaryData>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(agentResponse);

            adminClientMock
                .Setup(a => a.GetAgentsAsync(
                    It.IsAny<int?>(),
                    It.IsAny<ListSortOrder?>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(CreateAgentPageable(agents));

            // Setup: agent-1 succeeds, agent-2 fails, agent-3 succeeds
            adminClientMock
                .Setup(a => a.DeleteAgentAsync("agent-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
            adminClientMock
                .Setup(a => a.DeleteAgentAsync("agent-2", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Delete failed for agent-2"));
            adminClientMock
                .Setup(a => a.DeleteAgentAsync("agent-3", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

            var agent = CreateErrorFixerAgent(appSettings, loggerMock.Object, adminClientMock.Object);

            // Act
            await agent.FixCodeAsync(CancellationToken.None); // Create agent
            await agent.DisposeAsync();

            // Assert
            // Verify success logging for agent-1 and agent-3
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Deleted agent: Agent1 (agent-1)")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Deleted agent: Agent3 (agent-3)")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // Verify error logging for agent-2
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to delete agent Agent2 (agent-2)")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task DisposeAsync_DeleteAgentFails_DoesNotThrow()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var adminClientMock = CreateAdminClientMock();
            var loggerMock = CreateLoggerMock();
            var agentResponse = CreateAgentResponse("agent-123");
            var agents = new[] { agentResponse.Value };

            adminClientMock
                .Setup(a => a.CreateAgent(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<ToolDefinition>>(),
                    It.IsAny<ToolResources>(),
                    It.IsAny<float?>(),
                    It.IsAny<float?>(),
                    It.IsAny<BinaryData>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(agentResponse);

            adminClientMock
                .Setup(a => a.GetAgentsAsync(
                    It.IsAny<int?>(),
                    It.IsAny<ListSortOrder?>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(CreateAgentPageable(agents));

            adminClientMock
                .Setup(a => a.DeleteAgentAsync("agent-123", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Delete failed"));

            var agent = CreateErrorFixerAgent(appSettings, loggerMock.Object, adminClientMock.Object);

            // Act
            await agent.FixCodeAsync(CancellationToken.None); // Create agent
            
            // Assert
            Assert.DoesNotThrowAsync(async () => await agent.DisposeAsync());

            // Verify error was logged
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to delete agent")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task DisposeAsync_CalledMultipleTimes_OnlyDeletesOnce()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var adminClientMock = CreateAdminClientMock();
            var agentResponse = CreateAgentResponse("agent-123");
            var agents = new[] { agentResponse.Value };

            adminClientMock
                .Setup(a => a.CreateAgent(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<ToolDefinition>>(),
                    It.IsAny<ToolResources>(),
                    It.IsAny<float?>(),
                    It.IsAny<float?>(),
                    It.IsAny<BinaryData>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(agentResponse);

            adminClientMock
                .Setup(a => a.GetAgentsAsync(
                    It.IsAny<int?>(),
                    It.IsAny<ListSortOrder?>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(CreateAgentPageable(agents));

            adminClientMock
                .Setup(a => a.DeleteAgentAsync("agent-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

            var agent = CreateErrorFixerAgent(appSettings, adminClient: adminClientMock.Object);

            // Act
            await agent.FixCodeAsync(CancellationToken.None); // Create agent
            await agent.DisposeAsync();
            await agent.DisposeAsync(); // Second disposal
            await agent.DisposeAsync(); // Third disposal

            // Assert
            adminClientMock.Verify(
                a => a.GetAgentsAsync(
                    It.IsAny<int?>(),
                    It.IsAny<ListSortOrder?>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task DisposeAsync_UsesCorrectCancellationToken()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var adminClientMock = CreateAdminClientMock();
            var agentResponse = CreateAgentResponse("agent-123");
            var agents = new[] { agentResponse.Value };

            adminClientMock
                .Setup(a => a.CreateAgent(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<ToolDefinition>>(),
                    It.IsAny<ToolResources>(),
                    It.IsAny<float?>(),
                    It.IsAny<float?>(),
                    It.IsAny<BinaryData>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(agentResponse);

            adminClientMock
                .Setup(a => a.GetAgentsAsync(
                    It.IsAny<int?>(),
                    It.IsAny<ListSortOrder?>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    CancellationToken.None)) // Verify CancellationToken.None is used
                .Returns(CreateAgentPageable(agents));

            adminClientMock
                .Setup(a => a.DeleteAgentAsync("agent-123", CancellationToken.None)) // Verify CancellationToken.None is used
                .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

            var agent = CreateErrorFixerAgent(appSettings, adminClient: adminClientMock.Object);

            // Act
            await agent.FixCodeAsync(CancellationToken.None);
            await agent.DisposeAsync();

            // Assert
            adminClientMock.Verify(
                a => a.GetAgentsAsync(
                    It.IsAny<int?>(),
                    It.IsAny<ListSortOrder?>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    CancellationToken.None),
                Times.Once);

            adminClientMock.Verify(
                a => a.DeleteAgentAsync("agent-123", CancellationToken.None),
                Times.Once);
        }
    }
}