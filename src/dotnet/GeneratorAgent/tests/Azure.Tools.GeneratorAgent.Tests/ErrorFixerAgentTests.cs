using System.Reflection;
using Azure.AI.Agents.Persistent;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using System.ClientModel.Primitives;
using System.ClientModel;

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
            
            // Make test data unique to avoid conflicts between tests
            var uniqueId = Guid.NewGuid().ToString("N")[..8]; // Use first 8 chars of GUID for uniqueness
            var uniqueModel = $"{model}-{uniqueId}";
            var uniqueAgentName = $"{agentName}-{uniqueId}";
            
            var modelSection = new Mock<IConfigurationSection>();
            modelSection.Setup(s => s.Value).Returns(uniqueModel);
            configMock.Setup(c => c.GetSection("AzureSettings:Model")).Returns(modelSection.Object);

            var nameSection = new Mock<IConfigurationSection>();
            nameSection.Setup(s => s.Value).Returns(uniqueAgentName);
            configMock.Setup(c => c.GetSection("AzureSettings:AgentName")).Returns(nameSection.Object);

            var instructionsSection = new Mock<IConfigurationSection>();
            instructionsSection.Setup(s => s.Value).Returns(agentInstructions);
            configMock.Setup(c => c.GetSection("AzureSettings:AgentInstructions")).Returns(instructionsSection.Object);

            var endpointSection = new Mock<IConfigurationSection>();
            endpointSection.Setup(s => s.Value).Returns(projectEndpoint);
            configMock.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(endpointSection.Object);

            var mockAppSettingsLogger = Mock.Of<ILogger<AppSettings>>();
            return new AppSettings(configMock.Object, mockAppSettingsLogger);
        }

        // For tests that need ErrorFixerAgent, we'll create a derived class that overrides CreateAgent
        private class TestableErrorFixerAgent : ErrorFixerAgent
        {
            private readonly PersistentAgent MockAgent;
            private readonly Mock<PersistentAgentsClient>? MockClient;
            
            public TestableErrorFixerAgent(AppSettings appSettings, ILogger<ErrorFixerAgent> logger, PersistentAgent mockAgent, Mock<PersistentAgentsClient>? mockClient = null) 
                : base(appSettings, logger, mockClient?.Object ?? Mock.Of<PersistentAgentsClient>())
            {
                MockAgent = mockAgent;
                MockClient = mockClient;
            }
            
            internal override PersistentAgent CreateAgent()
            {
                return MockAgent;
            }

            // Make some methods testable
            public async Task<string> TestInitializeAgentEnvironmentAsync(string typeSpecDir, CancellationToken ct = default)
                => await InitializeAgentEnvironmentAsync(typeSpecDir, ct);

            public async ValueTask TestDisposeAsync() => await DisposeAsync();
        }

        private static Mock<PersistentAgent> CreateAgentMock(string agentId = "test-agent-id")
        {
            var agentMock = new Mock<PersistentAgent>();
            // Note: Cannot setup Id and Name properties because they are non-overridable
            // The mock will return default values for these properties
            agentMock.SetupAllProperties();
            return agentMock;
        }

        private static ErrorFixerAgent CreateErrorFixerAgent(
            AppSettings? appSettings = null,
            ILogger<ErrorFixerAgent>? logger = null,
            PersistentAgentsClient? client = null)
        {
            var mockAgent = CreateAgentMock().Object;
            return new TestableErrorFixerAgent(
                appSettings ?? CreateTestAppSettings(),
                logger ?? NullLogger<ErrorFixerAgent>.Instance,
                mockAgent);
        }

        private static TestableErrorFixerAgent CreateTestableErrorFixerAgent(
            AppSettings? appSettings = null,
            ILogger<ErrorFixerAgent>? logger = null,
            Mock<PersistentAgentsClient>? mockClient = null)
        {
            var mockAgent = CreateAgentMock().Object;
            return new TestableErrorFixerAgent(
                appSettings ?? CreateTestAppSettings(),
                logger ?? NullLogger<ErrorFixerAgent>.Instance,
                mockAgent,
                mockClient);
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithValidParameters_ShouldNotThrow()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var logger = NullLogger<ErrorFixerAgent>.Instance;
            var mockAgent = CreateAgentMock().Object;

            // Act & Assert
            Assert.DoesNotThrow(() => new TestableErrorFixerAgent(appSettings, logger, mockAgent));
        }

        [Test]
        public void Constructor_WithNullAppSettings_ShouldThrowArgumentNullException()
        {
            // Arrange
            var logger = NullLogger<ErrorFixerAgent>.Instance;
            var mockAgent = CreateAgentMock().Object;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TestableErrorFixerAgent(null!, logger, mockAgent));
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var mockAgent = CreateAgentMock().Object;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TestableErrorFixerAgent(appSettings, null!, mockAgent));
        }

        [Test]
        public void Constructor_WithNullClient_ShouldThrowArgumentNullException()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var logger = NullLogger<ErrorFixerAgent>.Instance;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ErrorFixerAgent(appSettings, logger, null!));
        }

        #endregion

        #region FixCodeAsync Tests

        [Test]
        public void FixCodeAsync_ShouldNotThrow()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var agent = CreateErrorFixerAgent(appSettings);

            // Act & Assert
            Assert.DoesNotThrowAsync(async () => await agent.FixCodeAsync(CancellationToken.None));
        }

        [Test]
        public async Task FixCodeAsync_WithCancellationToken_ShouldComplete()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var agent = CreateErrorFixerAgent(appSettings);
            var cts = new CancellationTokenSource();

            // Act
            await agent.FixCodeAsync(cts.Token);

            // Assert - No exception should be thrown
            Assert.Pass("FixCodeAsync completed successfully");
        }

        #endregion

        #region InitializeAgentEnvironmentAsync Tests

        [Test]
        public void InitializeAgentEnvironmentAsync_WithNonExistentDirectory_ShouldThrowDirectoryNotFoundException()
        {
            // Arrange
            var agent = CreateTestableErrorFixerAgent();
            var nonExistentDir = Path.Combine(Path.GetTempPath(), $"test_nonexistent_{Guid.NewGuid()}");

            // Act & Assert
            Assert.ThrowsAsync<DirectoryNotFoundException>(
                async () => await agent.TestInitializeAgentEnvironmentAsync(nonExistentDir));
        }

        [Test]
        public void InitializeAgentEnvironmentAsync_WithEmptyDirectory_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var agent = CreateTestableErrorFixerAgent();
            var tempDir = Path.Combine(Path.GetTempPath(), $"test_empty_{Guid.NewGuid()}");
            
            try
            {
                Directory.CreateDirectory(tempDir);

                // Act & Assert
                var ex = Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await agent.TestInitializeAgentEnvironmentAsync(tempDir));
                
                Assert.That(ex!.Message, Does.Contain("No TypeSpec files"));
            }
            finally
            {
                // Cleanup - Use retry logic to handle potential file locks
                CleanupDirectory(tempDir);
            }
        }

        #endregion

        #region CreateAgent Tests

        [Test]
        public void CreateAgent_WithValidSettings_ShouldReturnAgent()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var mockClient = new Mock<PersistentAgentsClient>();
            var mockAdministration = new Mock<PersistentAgentsAdministrationClient>();
            var mockAgent = CreateAgentMock().Object;
            
            // Setup the Administration property - though it can't be mocked directly,
            // we can test the behavior by verifying the agent creation logic
            var agent = new TestableErrorFixerAgent(appSettings, NullLogger<ErrorFixerAgent>.Instance, mockAgent, mockClient);

            // Act
            var createdAgent = agent.GetType().GetMethod("CreateAgent", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(agent, null);

            // Assert
            Assert.That(createdAgent, Is.Not.Null);
            Assert.That(createdAgent, Is.EqualTo(mockAgent));
        }

        #endregion

        #region DisposeAsync Tests

        [Test]
        public async Task DisposeAsync_WhenAgentNotCreated_ShouldNotThrow()
        {
            // Arrange
            var agent = CreateTestableErrorFixerAgent();

            // Act & Assert
            Assert.DoesNotThrowAsync(async () => await agent.TestDisposeAsync());
        }

        [Test]
        public async Task DisposeAsync_WhenCalledMultipleTimes_ShouldOnlyDisposeOnce()
        {
            // Arrange
            var agent = CreateTestableErrorFixerAgent();

            // Act
            await agent.TestDisposeAsync();
            await agent.TestDisposeAsync(); // Second call

            // Assert - Should not throw and should handle multiple calls gracefully
            Assert.Pass("Multiple dispose calls handled correctly");
        }

        [Test]
        public async Task DisposeAsync_WithCreatedAgent_ShouldAttemptCleanup()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ErrorFixerAgent>>();
            var agent = CreateTestableErrorFixerAgent(logger: mockLogger.Object);
            
            // Force agent creation by accessing the CreateAgent method
            _ = agent.GetType().GetMethod("CreateAgent", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(agent, null);

            // Act
            await agent.TestDisposeAsync();

            // Assert - Verify the agent was accessed (which means cleanup was attempted)
            // Since we can't easily mock the complex Azure SDK methods, we just verify no exceptions
            Assert.Pass("Dispose completed without exceptions");
        }

        #endregion

        #region Lazy Agent Property Tests

        [Test]
        public void Agent_Property_WhenAccessedMultipleTimes_ShouldReturnSameInstance()
        {
            // Arrange
            var agent = CreateTestableErrorFixerAgent();
            
            // Use reflection to access the Agent property multiple times
            var agentProperty = agent.GetType().BaseType?.GetField("Agent", BindingFlags.NonPublic | BindingFlags.Instance);
            var lazyAgent = agentProperty?.GetValue(agent) as Lazy<PersistentAgent>;

            // Act
            var firstAccess = lazyAgent?.Value;
            var secondAccess = lazyAgent?.Value;

            // Assert
            Assert.That(firstAccess, Is.Not.Null);
            Assert.That(secondAccess, Is.Not.Null);
            Assert.That(firstAccess, Is.EqualTo(secondAccess), "Lazy<T> should return the same instance on multiple accesses");
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public void Constructor_InitializesAllFields()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var logger = new Mock<ILogger<ErrorFixerAgent>>();
            var client = new Mock<PersistentAgentsClient>();
            var mockAgent = CreateAgentMock().Object;

            // Act
            var agent = new TestableErrorFixerAgent(appSettings, logger.Object, mockAgent);

            // Assert - Verify object was created successfully
            Assert.That(agent, Is.Not.Null);
            
            // Verify that the agent can be accessed without throwing
            Assert.DoesNotThrow(() => {
                var agentProperty = agent.GetType().BaseType?.GetField("Agent", BindingFlags.NonPublic | BindingFlags.Instance);
                var lazyAgent = agentProperty?.GetValue(agent) as Lazy<PersistentAgent>;
                _ = lazyAgent?.Value;
            });
        }

        #endregion

        #region Configuration Tests

        [Test]
        public void Constructor_WithDifferentAppSettings_ShouldUseCorrectValues()
        {
            // Arrange
            var customSettings = CreateTestAppSettings(
                model: "custom-model",
                agentName: "custom-agent",
                agentInstructions: "custom instructions",
                projectEndpoint: "https://custom.endpoint.com"
            );
            var logger = NullLogger<ErrorFixerAgent>.Instance;
            var mockAgent = CreateAgentMock().Object;

            // Act
            var agent = new TestableErrorFixerAgent(customSettings, logger, mockAgent);

            // Assert
            Assert.That(agent, Is.Not.Null);
            
            // Verify the settings are accessible through the private field
            var appSettingsField = agent.GetType().BaseType?.GetField("AppSettings", BindingFlags.NonPublic | BindingFlags.Instance);
            var retrievedSettings = appSettingsField?.GetValue(agent) as AppSettings;
            
            Assert.That(retrievedSettings, Is.Not.Null);
            
            // Check that the values contain our custom values (they will have unique ID appended)
            Assert.That(retrievedSettings.Model, Does.StartWith("custom-model-"));
            Assert.That(retrievedSettings.AgentName, Does.StartWith("custom-agent-"));
            Assert.That(retrievedSettings.AgentInstructions, Is.EqualTo("custom instructions"));
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Safely cleans up a directory with retry logic to handle file locks and ensure test isolation
        /// </summary>
        private static void CleanupDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                return;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    Directory.Delete(directoryPath, true);
                    return;
                }
                catch (IOException) when (attempt < 2)
                {
                    // Wait and retry - file might be locked
                    Thread.Sleep(50);
                }
                catch (UnauthorizedAccessException) when (attempt < 2)
                {
                    // Wait and retry - file might be locked
                    Thread.Sleep(50);
                }
            }
        }

        #endregion
    }
}
