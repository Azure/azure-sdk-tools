using System.Reflection;
using Azure.AI.Agents.Persistent;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using System.ClientModel.Primitives;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    [Category("Unit")]
    public class ErrorFixerAgentTests
    {
        private const string TestModel = "test-model";
        private const string TestAgentName = "unit-test-agent";
        private const string TestAgentInstructions = "just test";
        private const string TestAgentId = "agent-1";

        private Mock<PersistentAgentsAdministrationClient> _adminMock;
        private AppSettings _settings;
        private ErrorFixerAgent _agent;
        private CancellationToken _ct;

        [SetUp]
        public void Setup()
        {
            _adminMock = new Mock<PersistentAgentsAdministrationClient>(MockBehavior.Strict);
            _ct = CancellationToken.None;

            var configMock = new Mock<IConfiguration>();
            var configSection = new Mock<IConfigurationSection>();

            configSection.Setup(s => s.Value).Returns(TestModel);
            configMock.Setup(c => c.GetSection("AzureSettings:Model")).Returns(configSection.Object);

            var nameSection = new Mock<IConfigurationSection>();
            nameSection.Setup(s => s.Value).Returns(TestAgentName);
            configMock.Setup(c => c.GetSection("AzureSettings:AgentName")).Returns(nameSection.Object);

            var instructionsSection = new Mock<IConfigurationSection>();
            instructionsSection.Setup(s => s.Value).Returns(TestAgentInstructions);
            configMock.Setup(c => c.GetSection("AzureSettings:AgentInstructions")).Returns(instructionsSection.Object);

            var endpointSection = new Mock<IConfigurationSection>();
            endpointSection.Setup(s => s.Value).Returns(string.Empty);
            configMock.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(endpointSection.Object);

            _settings = new AppSettings(configMock.Object);

            _agent = new ErrorFixerAgent(
                _settings,
                NullLogger<ErrorFixerAgent>.Instance,
                _adminMock.Object);
        }

        private static Response<PersistentAgent> CreateAgentResponse(string id)
        {
            var json = $@"{{
                ""id"": ""{id}"",
                ""createdAt"": ""{DateTimeOffset.UtcNow:O}"",
                ""name"": ""{TestAgentName}"",
                ""description"": ""{TestAgentInstructions}"",
                ""model"": ""{TestModel}"",
                ""instructions"": ""{TestAgentInstructions}"",
                ""tools"": [],
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

        private void SetupSuccessfulAgentCreation()
        {
            _adminMock
                .Setup(a => a.CreateAgent(
                    _settings.Model,
                    _settings.AgentName,
                    It.IsAny<string>(),
                    _settings.AgentInstructions,
                    It.IsAny<IEnumerable<ToolDefinition>>(),
                    It.IsAny<ToolResources>(),
                    It.IsAny<float?>(),
                    It.IsAny<float?>(),
                    It.IsAny<BinaryData>(),
                    It.IsAny<IReadOnlyDictionary<string,string>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(CreateAgentResponse(TestAgentId));
        }
        
        private void SetupSuccessfulAgentRetrieval()
        {
            var page = Page<PersistentAgent>.FromValues(
                new[] { CreateAgentResponse(TestAgentId).Value },
                null,
                Mock.Of<Response>());
            var all = AsyncPageable<PersistentAgent>.FromPages(new[] { page });

            _adminMock
                .Setup(a => a.GetAgentsAsync(
                    It.IsAny<int?>(),
                    It.IsAny<ListSortOrder?>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    _ct))
                .Returns(all);
        }

        [Test]
        [Category("Operation")]
        public async Task FixCodeAsync_CreatesAgentOnFirstUse()
        {
            // Arrange
            SetupSuccessfulAgentCreation();

            // Act
            await _agent.FixCodeAsync(_ct);

            // Assert
            _adminMock.Verify(a => a.CreateAgent(
                _settings.Model,
                _settings.AgentName,
                It.IsAny<string>(),
                _settings.AgentInstructions,
                It.IsAny<IEnumerable<ToolDefinition>>(),
                It.IsAny<ToolResources>(),
                It.IsAny<float?>(),
                It.IsAny<float?>(),
                It.IsAny<BinaryData>(),
                It.IsAny<IReadOnlyDictionary<string,string>>(),
                It.IsAny<CancellationToken>()), Times.Once, "Agent should be created on first use");
        }

        [Test]
        [Category("Operation")]
        public async Task FixCodeAsync_ReusesSameAgent()
        {
            // Arrange
            SetupSuccessfulAgentCreation();

            // Act
            await _agent.FixCodeAsync(_ct);
            await _agent.FixCodeAsync(_ct);

            // Assert
            _adminMock.Verify(a => a.CreateAgent(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<ToolDefinition>>(),
                It.IsAny<ToolResources>(),
                It.IsAny<float?>(),
                It.IsAny<float?>(),
                It.IsAny<BinaryData>(),
                It.IsAny<IReadOnlyDictionary<string,string>>(),
                _ct), Times.Once, "Agent should only be created once");
        }

        [Test]
        [Category("Operation")]
        public void FixCodeAsync_ThrowsIfCreateFails()
        {
            // Arrange
            _adminMock
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
                    It.IsAny<IReadOnlyDictionary<string,string>>(),
                    It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("create failed"));

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _agent.FixCodeAsync(_ct),
                "Should throw when agent creation fails");
            Assert.That(ex.Message, Is.EqualTo("create failed"));
        }

        [Test]
        [Category("Cleanup")]
        public async Task DisposeAsync_DeletesOnlyCreatedAgent()
        {
            // Arrange
            SetupSuccessfulAgentCreation();
            SetupSuccessfulAgentRetrieval();

            var fakeRaw = Mock.Of<Response>();
            var deleteResponse = Response.FromValue(true, fakeRaw);
            _adminMock
                .Setup(a => a.DeleteAgentAsync(TestAgentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(deleteResponse);

            // Act
            await _agent.FixCodeAsync(_ct); // This will create the agent
            await _agent.DisposeAsync();

            // Assert
            _adminMock.Verify(
                a => a.DeleteAgentAsync(TestAgentId, It.IsAny<CancellationToken>()), 
                Times.Once,
                "Agent should be deleted exactly once");
        }

        [Test]
        [Category("Cleanup")]
        public async Task DisposeAsync_DoesNothingIfAgentNeverCreated()
        {
            // Act
            await _agent.DisposeAsync();

            // Assert
            _adminMock.Verify(
                a => a.DeleteAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), 
                Times.Never,
                "Should not try to delete agent if it was never created");
        }

        [Test]
        [Category("Cleanup")]
        public async Task DisposeAsync_DoesNotThrowOnDeleteError()
        {
            // Arrange
            SetupSuccessfulAgentCreation();
            SetupSuccessfulAgentRetrieval();

            _adminMock
                .Setup(a => a.DeleteAgentAsync(TestAgentId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("delete error"));

            // Act
            await _agent.FixCodeAsync(_ct);
            
            // Assert
            Assert.DoesNotThrowAsync(async () => await _agent.DisposeAsync(),
                "Should not throw when delete operation fails");
        }
    }
}