using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.Agents.Persistent;
using Azure.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Azure.Tools.GeneratorAgent.Interfaces;
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
        private IAppSettings _settings;
        private ErrorFixerAgent _agent;
        private CancellationToken _ct;

        [SetUp]
        public void Setup()
        {
            _adminMock = new Mock<PersistentAgentsAdministrationClient>(MockBehavior.Strict);
            _ct = CancellationToken.None;

            var settingsMock = new Mock<IAppSettings>();
            settingsMock.SetupGet(s => s.Model).Returns(TestModel);
            settingsMock.SetupGet(s => s.AgentName).Returns(TestAgentName);
            settingsMock.SetupGet(s => s.AgentInstructions).Returns(TestAgentInstructions);
            _settings = settingsMock.Object;

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
                .Setup(a => a.CreateAgentAsync(
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
                    _ct))
                .ReturnsAsync(CreateAgentResponse(TestAgentId));
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
        [Category("Initialize")]
        public async Task InitializeAsync_CreatesAgentOnlyOnce()
        {
            // Arrange
            SetupSuccessfulAgentCreation();

            // Act
            await _agent.InitializeAsync(_ct);
            await _agent.InitializeAsync(_ct);  // Should not create again

            // Assert
            _adminMock.Verify(a => a.CreateAgentAsync(
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
                _ct), Times.Once, "Agent should be created exactly once");
        }

        [Test]
        [Category("Initialize")]
        public async Task InitializeAsync_ThrowsIfCreateFails()
        {
            // Arrange
            _adminMock
                .Setup(a => a.CreateAgentAsync(
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
                .ThrowsAsync(new InvalidOperationException("create failed"));

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _agent.InitializeAsync(_ct),
                "Should throw when agent creation fails");
            Assert.That(ex.Message, Is.EqualTo("create failed"));
        }

        [Test]
        [Category("Operation")]
        public void FixCodeAsync_ThrowsIfNotInitialized()
        {
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _agent.FixCodeAsync(_ct),
                "Should throw when agent is not initialized");
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
            await _agent.InitializeAsync(_ct);
            await _agent.DisposeAsync();

            // Assert
            _adminMock.Verify(
                a => a.DeleteAgentAsync(TestAgentId, It.IsAny<CancellationToken>()), 
                Times.Once,
                "Agent should be deleted exactly once");
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
            await _agent.InitializeAsync(_ct);
            
            // Assert
            Assert.DoesNotThrowAsync(async () => await _agent.DisposeAsync(),
                "Should not throw when delete operation fails");
        }
    }
}