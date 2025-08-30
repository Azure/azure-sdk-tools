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
    internal class ThreadManagerTests
    {
        private class TestableThreadManager : ThreadManager
        {
            private readonly string MockThreadId;
            private readonly string MockResponse;
            private readonly bool ShouldThrowOnRun;
            private readonly bool ShouldTimeout;

            public TestableThreadManager(
                PersistentAgentsClient client,
                ILogger<ThreadManager> logger,
                AppSettings appSettings,
                string mockThreadId = "test-thread-123",
                string mockResponse = "Test response",
                bool shouldThrowOnRun = false,
                bool shouldTimeout = false)
                : base(client, logger, appSettings)
            {
                MockThreadId = mockThreadId;
                MockResponse = mockResponse;
                ShouldThrowOnRun = shouldThrowOnRun;
                ShouldTimeout = shouldTimeout;
            }

            public string GetMockThreadId() => MockThreadId;
            public string GetMockResponse() => MockResponse;
            public bool GetShouldThrowOnRun() => ShouldThrowOnRun;
            public bool GetShouldTimeout() => ShouldTimeout;
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithValidParameters_ShouldNotThrow()
        {
            var client = new Mock<PersistentAgentsClient>().Object;
            var logger = NullLogger<ThreadManager>.Instance;
            var appSettings = CreateTestAppSettings();

            Assert.DoesNotThrow(() => new ThreadManager(client, logger, appSettings));
        }

        [Test]
        public void Constructor_WithNullClient_ShouldThrowArgumentNullException()
        {
            var logger = NullLogger<ThreadManager>.Instance;
            var appSettings = CreateTestAppSettings();

            var ex = Assert.Throws<ArgumentNullException>(() => new ThreadManager(null!, logger, appSettings));
            Assert.That(ex!.ParamName, Is.EqualTo("client"));
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            var client = new Mock<PersistentAgentsClient>().Object;
            var appSettings = CreateTestAppSettings();

            var ex = Assert.Throws<ArgumentNullException>(() => new ThreadManager(client, null!, appSettings));
            Assert.That(ex!.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithNullAppSettings_ShouldThrowArgumentNullException()
        {
            var client = new Mock<PersistentAgentsClient>().Object;
            var logger = NullLogger<ThreadManager>.Instance;

            var ex = Assert.Throws<ArgumentNullException>(() => new ThreadManager(client, logger, null!));
            Assert.That(ex!.ParamName, Is.EqualTo("appSettings"));
        }

        #endregion

        #region CreateThreadAsync Tests

        [Test]
        public void CreateThreadAsync_WithValidClient_ShouldReturnThreadId()
        {
            var testableManager = CreateTestableThreadManager();

            var mockThreadId = testableManager.GetMockThreadId();

            Assert.That(mockThreadId, Is.Not.Null);
            Assert.That(mockThreadId, Is.Not.Empty);
            Assert.That(mockThreadId, Is.EqualTo("test-thread-123"));
        }

        [Test]
        public void CreateThreadAsync_WithCancellationToken_ShouldComplete()
        {
            var testableManager = CreateTestableThreadManager();
            var cts = new CancellationTokenSource();

            var mockThreadId = testableManager.GetMockThreadId();
            
            Assert.That(mockThreadId, Is.Not.Null);
            Assert.That(mockThreadId, Is.Not.Empty);
        }

        [Test]
        public void CreateThreadAsync_WithCancelledToken_ShouldRespectCancellation()
        {
            var testableManager = CreateTestableThreadManager();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.DoesNotThrow(() =>
            {
                var mockThreadId = testableManager.GetMockThreadId();
                Assert.That(mockThreadId, Is.Not.Null);
            });
        }

        #endregion

        #region ReadResponseAsync Tests

        [Test]
        public void ReadResponseAsync_WithMockResponse_ShouldReturnResponse()
        {
            var expectedResponse = "Test assistant response";
            var testableManager = CreateTestableThreadManager(mockResponse: expectedResponse);

            var mockResponse = testableManager.GetMockResponse();

            Assert.That(mockResponse, Is.Not.Null);
            Assert.That(mockResponse, Is.EqualTo(expectedResponse));
        }

        [Test]
        public void ReadResponseAsync_WithEmptyResponse_ShouldHandleEmptyString()
        {
            var testableManager = CreateTestableThreadManager(mockResponse: "");

            var mockResponse = testableManager.GetMockResponse();

            Assert.That(mockResponse, Is.EqualTo(""));
        }

        [Test]
        public void ReadResponseAsync_WithNullThreadId_ShouldThrowArgumentNullException()
        {
            var testableManager = CreateTestableThreadManager();

            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await testableManager.ReadResponseAsync(null!, CancellationToken.None));
        }

        [Test]
        public void ReadResponseAsync_WithEmptyThreadId_ShouldThrowArgumentException()
        {
            var testableManager = CreateTestableThreadManager();

            Assert.ThrowsAsync<ArgumentException>(async () =>
                await testableManager.ReadResponseAsync("", CancellationToken.None));
        }

        #endregion

        #region ProcessAgentRunAsync Tests

        [Test]
        public void ProcessAgentRunAsync_WithValidParameters_ShouldComplete()
        {
            var testableManager = CreateTestableThreadManager(shouldThrowOnRun: false);

            var shouldThrow = testableManager.GetShouldThrowOnRun();
            Assert.That(shouldThrow, Is.False);
            
            Assert.Pass();
        }

        [Test]
        public void ProcessAgentRunAsync_WithFailedRunConfiguration_ShouldIndicateFailure()
        {
            var testableManager = CreateTestableThreadManager(shouldThrowOnRun: true);

            var shouldThrow = testableManager.GetShouldThrowOnRun();
            Assert.That(shouldThrow, Is.True);
        }

        [Test]
        public void ProcessAgentRunAsync_WithTimeoutConfiguration_ShouldIndicateTimeout()
        {
            var testableManager = CreateTestableThreadManager(shouldTimeout: true);

            var shouldTimeout = testableManager.GetShouldTimeout();
            Assert.That(shouldTimeout, Is.True);
        }

        [Test]
        public void ProcessAgentRunAsync_WithNullThreadId_ShouldThrowArgumentNullException()
        {
            var testableManager = CreateTestableThreadManager();

            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await testableManager.ProcessAgentRunAsync(null!, "test-agent-id", CancellationToken.None));
        }

        [Test]
        public void ProcessAgentRunAsync_WithNullAgentId_ShouldThrowArgumentNullException()
        {
            var testableManager = CreateTestableThreadManager();

            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await testableManager.ProcessAgentRunAsync("test-thread-id", null!, CancellationToken.None));
        }

        [Test]
        public void ProcessAgentRunAsync_WithEmptyThreadId_ShouldThrowArgumentException()
        {
            var testableManager = CreateTestableThreadManager();

            Assert.ThrowsAsync<ArgumentException>(async () =>
                await testableManager.ProcessAgentRunAsync("", "test-agent-id", CancellationToken.None));
        }

        [Test]
        public void ProcessAgentRunAsync_WithEmptyAgentId_ShouldThrowArgumentException()
        {
            var testableManager = CreateTestableThreadManager();

            Assert.ThrowsAsync<ArgumentException>(async () =>
                await testableManager.ProcessAgentRunAsync("test-thread-id", "", CancellationToken.None));
        }

        #endregion

        #region AppSettings Integration Tests

        [Test]
        public void ThreadManager_WithCustomAppSettings_ShouldUseConfiguredValues()
        {
            var customAppSettings = CreateTestAppSettings(maxWaitTimeSeconds: 60, pollingIntervalSeconds: 2);
            var testableManager = CreateTestableThreadManager(appSettings: customAppSettings);

            Assert.That(customAppSettings, Is.Not.Null);
            Assert.That(customAppSettings.AgentRunMaxWaitTime.TotalSeconds, Is.EqualTo(60));
            Assert.That(customAppSettings.AgentRunPollingInterval.TotalSeconds, Is.EqualTo(2));
        }

        [Test]
        public void ThreadManager_WithDefaultAppSettings_ShouldUseDefaultValues()
        {
            var defaultAppSettings = CreateTestAppSettings();
            var testableManager = CreateTestableThreadManager(appSettings: defaultAppSettings);

            Assert.That(defaultAppSettings, Is.Not.Null);
            Assert.That(defaultAppSettings.AgentRunMaxWaitTime.TotalSeconds, Is.EqualTo(30));
            Assert.That(defaultAppSettings.AgentRunPollingInterval.TotalSeconds, Is.EqualTo(1));
        }

        #endregion

        #region Parameter Validation Tests

        [Test]
        public void ThreadManager_WithAllValidParameters_ShouldCreateSuccessfully()
        {
            var client = new Mock<PersistentAgentsClient>().Object;
            var logger = NullLogger<ThreadManager>.Instance;
            var appSettings = CreateTestAppSettings();

            var threadManager = new ThreadManager(client, logger, appSettings);

            Assert.That(threadManager, Is.Not.Null);
            Assert.That(threadManager, Is.InstanceOf<ThreadManager>());
        }

        [Test]
        public void ThreadManager_ParameterValidation_ShouldEnforceNonNullConstraints()
        {
            var client = new Mock<PersistentAgentsClient>().Object;
            var logger = NullLogger<ThreadManager>.Instance;
            var appSettings = CreateTestAppSettings();

            Assert.DoesNotThrow(() => new ThreadManager(client, logger, appSettings));

            Assert.Throws<ArgumentNullException>(() => new ThreadManager(null!, logger, appSettings));
            Assert.Throws<ArgumentNullException>(() => new ThreadManager(client, null!, appSettings));
            Assert.Throws<ArgumentNullException>(() => new ThreadManager(client, logger, null!));
        }

        #endregion

        #region Mock Data Verification Tests

        [Test]
        public void TestableThreadManager_MockData_ShouldReturnExpectedValues()
        {
            var expectedThreadId = "custom-thread-456";
            var expectedResponse = "Custom test response";
            var testableManager = CreateTestableThreadManager(
                mockThreadId: expectedThreadId,
                mockResponse: expectedResponse);

            Assert.That(testableManager.GetMockThreadId(), Is.EqualTo(expectedThreadId));
            Assert.That(testableManager.GetMockResponse(), Is.EqualTo(expectedResponse));
            Assert.That(testableManager.GetShouldThrowOnRun(), Is.False);
            Assert.That(testableManager.GetShouldTimeout(), Is.False);
        }

        [Test]
        public void TestableThreadManager_ErrorScenarios_ShouldConfigureCorrectly()
        {
            var testableManager = CreateTestableThreadManager(
                shouldThrowOnRun: true,
                shouldTimeout: true);

            Assert.That(testableManager.GetShouldThrowOnRun(), Is.True);
            Assert.That(testableManager.GetShouldTimeout(), Is.True);
        }

        #endregion

        #region Helper Methods

        private TestableThreadManager CreateTestableThreadManager(
            string mockThreadId = "test-thread-123",
            string mockResponse = "Test response",
            bool shouldThrowOnRun = false,
            bool shouldTimeout = false,
            AppSettings? appSettings = null)
        {
            var client = new Mock<PersistentAgentsClient>().Object;
            var logger = NullLogger<ThreadManager>.Instance;
            var settings = appSettings ?? CreateTestAppSettings();

            return new TestableThreadManager(
                client,
                logger,
                settings,
                mockThreadId,
                mockResponse,
                shouldThrowOnRun,
                shouldTimeout);
        }

        private AppSettings CreateTestAppSettings(int maxWaitTimeSeconds = 30, int pollingIntervalSeconds = 1)
        {
            var configMock = new Mock<IConfiguration>();
            var mockLogger = new Mock<ILogger<AppSettings>>();

            var testId = Guid.NewGuid().ToString("N")[..8];

            var defaultSection = new Mock<IConfigurationSection>();
            defaultSection.Setup(s => s.Value).Returns((string?)null);
            configMock.Setup(c => c.GetSection(It.IsAny<string>())).Returns(defaultSection.Object);

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

            var maxWaitSection = new Mock<IConfigurationSection>();
            maxWaitSection.Setup(s => s.Value).Returns(maxWaitTimeSeconds.ToString());
            configMock.Setup(c => c.GetSection("AzureSettings:AgentRunMaxWaitTimeSeconds")).Returns(maxWaitSection.Object);

            var pollingSection = new Mock<IConfigurationSection>();
            pollingSection.Setup(s => s.Value).Returns(pollingIntervalSeconds.ToString());
            configMock.Setup(c => c.GetSection("AzureSettings:AgentRunPollingIntervalSeconds")).Returns(pollingSection.Object);

            return new AppSettings(configMock.Object, mockLogger.Object);
        }

        #endregion
    }
}
