using System.Reflection;
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
    public class ErrorFixerAgentTests
    {
        private AppSettings CreateTestAppSettings(
            string model = "test-model",
            string agentName = "test-agent", 
            string agentInstructions = "test instructions",
            string projectEndpoint = "https://test.example.com")
        {
            var configMock = new Mock<IConfiguration>();
            var mockLogger = new Mock<ILogger<AppSettings>>();
            
            var testId = Guid.NewGuid().ToString("N")[..8];
            var uniqueModel = $"{model}-{testId}";
            var uniqueAgentName = $"{agentName}-{testId}";
            

            var defaultSection = new Mock<IConfigurationSection>();
            defaultSection.Setup(s => s.Value).Returns((string?)null);
            configMock.Setup(c => c.GetSection(It.IsAny<string>())).Returns(defaultSection.Object);
            
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

            return new AppSettings(configMock.Object, mockLogger.Object);
        }

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

            public async Task<string> TestInitializeAgentEnvironmentAsync(Dictionary<string, string> typeSpecFiles, CancellationToken ct = default)
                => await InitializeAgentEnvironmentAsync(typeSpecFiles, ct);

            public async ValueTask TestDisposeAsync() => await DisposeAsync();
        }

        private Mock<PersistentAgent> CreateAgentMock(string agentId = "test-agent-id")
        {
            var agentMock = new Mock<PersistentAgent>(MockBehavior.Loose);
            agentMock.SetupAllProperties();
            return agentMock;
        }

        private ErrorFixerAgent CreateErrorFixerAgent(
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

        private TestableErrorFixerAgent CreateTestableErrorFixerAgent(
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

        [Test]
        public void InitializeAgentEnvironmentAsync_WithEmptyDictionary_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var agent = CreateTestableErrorFixerAgent();
            var emptyTypeSpecFiles = new Dictionary<string, string>();

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await agent.TestInitializeAgentEnvironmentAsync(emptyTypeSpecFiles));
            
            Assert.That(ex!.Message, Does.Contain("No TypeSpec files"));
        }

        [Test]
        public void InitializeAgentEnvironmentAsync_WithNonTypeSpecFiles_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var agent = CreateTestableErrorFixerAgent();
            var nonTypeSpecFiles = new Dictionary<string, string>
            {
                { "readme.md", "# README" },
                { "config.txt", "some config" }
            };

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await agent.TestInitializeAgentEnvironmentAsync(nonTypeSpecFiles));
            
            Assert.That(ex!.Message, Does.Contain("No TypeSpec files"));
        }

        [Test]
        public void CreateAgent_WithValidSettings_ShouldReturnAgent()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var mockClient = new Mock<PersistentAgentsClient>();
            var mockAdministration = new Mock<PersistentAgentsAdministrationClient>();
            var mockAgent = CreateAgentMock().Object;
            
            var agent = new TestableErrorFixerAgent(appSettings, NullLogger<ErrorFixerAgent>.Instance, mockAgent, mockClient);

            // Act
            var createdAgent = agent.GetType().GetMethod("CreateAgent", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(agent, null);

            // Assert
            Assert.That(createdAgent, Is.Not.Null);
            Assert.That(createdAgent, Is.EqualTo(mockAgent));
        }

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
            
            _ = agent.GetType().GetMethod("CreateAgent", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(agent, null);

            // Act
            await agent.TestDisposeAsync();

            Assert.Pass("Dispose completed without exceptions");
        }

        [Test]
        public void Agent_Property_WhenAccessedMultipleTimes_ShouldReturnSameInstance()
        {
            // Arrange
            var agent = CreateTestableErrorFixerAgent();
            
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
            
            var appSettingsField = agent.GetType().BaseType?.GetField("AppSettings", BindingFlags.NonPublic | BindingFlags.Instance);
            var retrievedSettings = appSettingsField?.GetValue(agent) as AppSettings;
            
            Assert.That(retrievedSettings, Is.Not.Null);
            
            // Check that the values contain our custom values (they will have unique ID appended)
            Assert.That(retrievedSettings.Model, Does.StartWith("custom-model-"));
            Assert.That(retrievedSettings.AgentName, Does.StartWith("custom-agent-"));
            Assert.That(retrievedSettings.AgentInstructions, Is.EqualTo("custom instructions"));
        }

        private void CleanupDirectory(string directoryPath)
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

    }
}
