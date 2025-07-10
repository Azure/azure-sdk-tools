using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.Agents.Persistent;
using Azure.Core;                                  
using Microsoft.Extensions.Logging.Abstractions;   
using Azure.Tools.GeneratorAgent;
using Azure.Tools.GeneratorAgent.Interfaces;
using Moq;
using Xunit;
using System.ClientModel.Primitives;             

namespace Azure.Tools.GeneratorAgent.Tests
{
    public class ErrorFixerAgentTests
    {
        private readonly Mock<PersistentAgentsAdministrationClient> _adminMock;
        private readonly IAppSettings _settings;
        private readonly ErrorFixerAgent _agent;
        private readonly CancellationToken _ct = CancellationToken.None;

        public ErrorFixerAgentTests()
        {
            _adminMock = new Mock<PersistentAgentsAdministrationClient>(MockBehavior.Strict);

            var settingsMock = new Mock<IAppSettings>();
            settingsMock.SetupGet(s => s.Model).Returns("test-model");
            settingsMock.SetupGet(s => s.AgentName).Returns("unit-test-agent");
            settingsMock.SetupGet(s => s.AgentInstructions).Returns("just test");
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
                ""name"": ""unit-test-agent"",
                ""description"": ""just test"",
                ""model"": ""test-model"",
                ""instructions"": ""just test"",
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

        [Fact]
        public async Task InitializeAsync_CreatesAgentOnlyOnce()
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
                    _ct
                ))
                .ReturnsAsync(CreateAgentResponse("agent-1"));

            // Act
            await _agent.InitializeAsync(_ct);
            await _agent.InitializeAsync(_ct);  // no second create

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
                _ct), Times.Once);
        }

        [Fact]
        public async Task InitializeAsync_ThrowsIfCreateFails()
        {
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

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _agent.InitializeAsync(_ct));

            Assert.Equal("create failed", ex.Message);
        }

        [Fact]
        public async Task FixCodeAsync_ThrowsIfNotInitialized()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _agent.FixCodeAsync(_ct));
        }

        [Fact]
        public async Task DisposeAsync_DeletesOnlyCreatedAgent()
        {
            _adminMock
                .Setup(a => a.CreateAgentAsync(
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<IEnumerable<ToolDefinition>>(),
                    It.IsAny<ToolResources>(), It.IsAny<float?>(),
                    It.IsAny<float?>(), It.IsAny<BinaryData>(),
                    It.IsAny<IReadOnlyDictionary<string,string>>(),
                    _ct))
                .ReturnsAsync(CreateAgentResponse("agent-1"));

            var page = Page<PersistentAgent>.FromValues(
                new[] { CreateAgentResponse("agent-1").Value },
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

            var fakeRaw = Mock.Of<Response>();
            var deleteResponse = Response.FromValue(true, fakeRaw);
            _adminMock
                .Setup(a => a.DeleteAgentAsync("agent-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(deleteResponse);

            await _agent.InitializeAsync(_ct);
            await _agent.DisposeAsync();

            _adminMock.Verify(a => a.DeleteAgentAsync("agent-1", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DisposeAsync_DoesNotThrowOnDeleteError()
        {
            _adminMock
                .Setup(a => a.CreateAgentAsync(
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<IEnumerable<ToolDefinition>>(),
                    It.IsAny<ToolResources>(), It.IsAny<float?>(),
                    It.IsAny<float?>(), It.IsAny<BinaryData>(),
                    It.IsAny<IReadOnlyDictionary<string,string>>(),
                    _ct))
                .ReturnsAsync(CreateAgentResponse("agent-1"));

            var page = Page<PersistentAgent>.FromValues(
                new[] { CreateAgentResponse("agent-1").Value },
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

            _adminMock
                .Setup(a => a.DeleteAgentAsync("agent-1", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("delete error"));

            await _agent.InitializeAsync(_ct);
            await _agent.DisposeAsync();
        }
    }
}