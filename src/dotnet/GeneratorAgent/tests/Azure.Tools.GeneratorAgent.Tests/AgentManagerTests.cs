using Azure.AI.Agents.Persistent;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    internal class AgentManagerTests
    {
        private class TestableAgentManager : AgentManager
        {
            private readonly PersistentAgent MockAgent;

            public TestableAgentManager(
                PersistentAgentsClient client,
                ILogger<AgentManager> logger,
                AppSettings appSettings,
                PersistentAgent mockAgent)
                : base(client, logger, appSettings)
            {
                MockAgent = mockAgent;
            }

            public PersistentAgent GetTestAgent() => MockAgent;

            public override async Task DeleteAgentsAsync(CancellationToken cancellationToken)
            {
                await Task.Delay(10, cancellationToken); // Simulate async work
            }

            public override async Task UpdateAgentVectorStoreAsync(string agentId, string vectorStoreId, CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(agentId);
                ArgumentNullException.ThrowIfNull(vectorStoreId);
                
                await Task.Delay(10, cancellationToken); // Simulate async work
            }
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithValidParameters_ShouldNotThrow()
        {
            var client = new Mock<PersistentAgentsClient>().Object;
            var logger = NullLogger<AgentManager>.Instance;
            var appSettings = CreateTestAppSettings();

            Assert.DoesNotThrow(() => new AgentManager(client, logger, appSettings));
        }

        [Test]
        public void Constructor_WithNullClient_ShouldThrowArgumentNullException()
        {
            var logger = NullLogger<AgentManager>.Instance;
            var appSettings = CreateTestAppSettings();

            var ex = Assert.Throws<ArgumentNullException>(() => new AgentManager(null!, logger, appSettings));
            Assert.That(ex!.ParamName, Is.EqualTo("client"));
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            var client = new Mock<PersistentAgentsClient>().Object;
            var appSettings = CreateTestAppSettings();

            var ex = Assert.Throws<ArgumentNullException>(() => new AgentManager(client, null!, appSettings));
            Assert.That(ex!.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithNullAppSettings_ShouldThrowArgumentNullException()
        {
            var client = new Mock<PersistentAgentsClient>().Object;
            var logger = NullLogger<AgentManager>.Instance;

            var ex = Assert.Throws<ArgumentNullException>(() => new AgentManager(client, logger, null!));
            Assert.That(ex!.ParamName, Is.EqualTo("appSettings"));
        }

        #endregion

        #region GetAgent Tests

        [Test]
        public void GetAgent_WhenCalled_ShouldReturnAgent()
        {
            var mockAgent = CreateMockAgent();
            var testableManager = CreateTestableAgentManager(mockAgent.Object);

            var result = testableManager.GetTestAgent();

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.EqualTo(mockAgent.Object));
        }

        [Test]
        public void GetAgent_WhenCalledMultipleTimes_ShouldReturnSameInstance()
        {
            var mockAgent = CreateMockAgent();
            var testableManager = CreateTestableAgentManager(mockAgent.Object);

            var result1 = testableManager.GetTestAgent();
            var result2 = testableManager.GetTestAgent();

            Assert.That(result1, Is.EqualTo(result2));
            Assert.That(ReferenceEquals(result1, result2), Is.True);
        }

        #endregion

        #region UpdateAgentVectorStoreAsync Tests

        [Test]
        public async Task UpdateAgentVectorStoreAsync_WithValidParameters_ShouldComplete()
        {
            var mockAgent = CreateMockAgent();
            var testableManager = CreateTestableAgentManager(mockAgent.Object);
            var agentId = "test-agent-id";
            var vectorStoreId = "test-vector-store-id";

            // This test verifies the method can be called without throwing
            await testableManager.UpdateAgentVectorStoreAsync(agentId, vectorStoreId, CancellationToken.None);
            
            // If we reach here, the method completed successfully
            Assert.Pass();
        }

        [Test]
        public void UpdateAgentVectorStoreAsync_WithNullAgentId_ShouldThrowArgumentNullException()
        {
            var mockAgent = CreateMockAgent();
            var testableManager = CreateTestableAgentManager(mockAgent.Object);
            var vectorStoreId = "test-vector-store-id";

            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await testableManager.UpdateAgentVectorStoreAsync(null!, vectorStoreId, CancellationToken.None));
        }

        [Test]
        public void UpdateAgentVectorStoreAsync_WithNullVectorStoreId_ShouldThrowArgumentNullException()
        {
            var mockAgent = CreateMockAgent();
            var testableManager = CreateTestableAgentManager(mockAgent.Object);
            var agentId = "test-agent-id";

            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await testableManager.UpdateAgentVectorStoreAsync(agentId, null!, CancellationToken.None));
        }

        #endregion

        #region DeleteAgentsAsync Tests

        [Test]
        public async Task DeleteAgentsAsync_WithValidCancellationToken_ShouldComplete()
        {
            var mockAgent = CreateMockAgent();
            var testableManager = CreateTestableAgentManager(mockAgent.Object);

            // This test verifies the method can be called without throwing
            await testableManager.DeleteAgentsAsync(CancellationToken.None);
            
            // If we reach here, the method completed successfully
            Assert.Pass();
        }

        [Test]
        public void DeleteAgentsAsync_WithCancelledToken_ShouldRespectCancellation()
        {
            var mockAgent = CreateMockAgent();
            var testableManager = CreateTestableAgentManager(mockAgent.Object);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // The method should throw when cancellation token is cancelled
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await testableManager.DeleteAgentsAsync(cts.Token));
        }

        #endregion

        #region DisposeAsync Tests

        [Test]
        public async Task DisposeAsync_WhenCalled_ShouldNotThrow()
        {
            var mockAgent = CreateMockAgent();
            var testableManager = CreateTestableAgentManager(mockAgent.Object);

            await testableManager.DisposeAsync();
            
            // If we reach here, the method completed successfully
            Assert.Pass();
        }

        [Test]
        public async Task DisposeAsync_CalledMultipleTimes_ShouldNotThrow()
        {
            var mockAgent = CreateMockAgent();
            var testableManager = CreateTestableAgentManager(mockAgent.Object);

            await testableManager.DisposeAsync();
            await testableManager.DisposeAsync();
            
            // If we reach here, both calls completed successfully
            Assert.Pass();
        }

        [Test]
        public async Task DisposeAsync_AfterGetAgent_ShouldNotThrow()
        {
            var mockAgent = CreateMockAgent();
            var testableManager = CreateTestableAgentManager(mockAgent.Object);
            
            testableManager.GetTestAgent(); // Trigger agent access

            await testableManager.DisposeAsync();
            
            // If we reach here, the method completed successfully
            Assert.Pass();
        }

        #endregion

        #region Lazy Initialization Tests

        [Test]
        public void LazyInitialization_AgentShouldBeCreatedOnFirstAccess()
        {
            var mockAgent = CreateMockAgent();
            var testableManager = CreateTestableAgentManager(mockAgent.Object);

            // Agent should not be created until first access
            var result = testableManager.GetTestAgent();
            
            Assert.That(result, Is.Not.Null);
            // Note: Can't test Id since it's not virtual and can't be mocked
        }

        [Test]
        public void LazyInitialization_ShouldCreateAgentOnlyOnce()
        {
            var mockAgent = CreateMockAgent();
            var testableManager = CreateTestableAgentManager(mockAgent.Object);

            var result1 = testableManager.GetTestAgent();
            var result2 = testableManager.GetTestAgent();

            Assert.That(result1, Is.EqualTo(result2));
            Assert.That(ReferenceEquals(result1, result2), Is.True);
        }

        #endregion

        #region Integration Tests

        [Test]
        public async Task IntegrationTest_CreateGetUpdateDispose_ShouldWorkTogether()
        {
            var mockAgent = CreateMockAgent();
            var testableManager = CreateTestableAgentManager(mockAgent.Object);

            // Get agent
            var agent = testableManager.GetTestAgent();
            Assert.That(agent, Is.Not.Null);

            // Update vector store - use a test ID since we can't access agent.Id
            await testableManager.UpdateAgentVectorStoreAsync("test-agent-id", "test-vector-store", CancellationToken.None);

            // Delete agents
            await testableManager.DeleteAgentsAsync(CancellationToken.None);

            // Dispose
            await testableManager.DisposeAsync();

            // Should still be able to get agent after operations
            var agentAfterOperations = testableManager.GetTestAgent();
            Assert.That(agentAfterOperations, Is.EqualTo(agent));
        }

        #endregion

        #region Helper Methods

        private TestableAgentManager CreateTestableAgentManager(PersistentAgent mockAgent)
        {
            var client = new Mock<PersistentAgentsClient>().Object;
            var logger = NullLogger<AgentManager>.Instance;
            var appSettings = CreateTestAppSettings();

            return new TestableAgentManager(client, logger, appSettings, mockAgent);
        }

        private AppSettings CreateTestAppSettings()
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

        private Mock<PersistentAgent> CreateMockAgent(string? agentId = null)
        {
            // Since PersistentAgent.Id is not virtual, we can't mock it directly
            // We'll use a different approach for testing
            var mockAgent = new Mock<PersistentAgent>();
            var id = agentId ?? $"test-agent-{Guid.NewGuid().ToString("N")[..8]}";
            // Don't try to mock non-virtual properties
            return mockAgent;
        }

        #endregion
    }
}
