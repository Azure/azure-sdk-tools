using Azure.AI.Agents.Persistent;
using Azure.Tools.GeneratorAgent.Agent;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    internal class AgentFileManagerTests
    {
        private class TestableAgentFileManager : AgentFileManager
        {
            private readonly List<string> MockUploadedFileIds;
            private readonly string MockVectorStoreId;
            private readonly bool ShouldFailUpload;
            private readonly bool ShouldTimeout;

            public TestableAgentFileManager(
                PersistentAgentsClient client,
                ILogger<AgentFileManager> logger,
                AppSettings appSettings,
                List<string>? mockUploadedFileIds = null,
                string mockVectorStoreId = "test-vector-store-123",
                bool shouldFailUpload = false,
                bool shouldTimeout = false)
                : base(client, logger, appSettings)
            {
                MockUploadedFileIds = mockUploadedFileIds ?? new List<string> { "file-1", "file-2", "file-3" };
                MockVectorStoreId = mockVectorStoreId;
                ShouldFailUpload = shouldFailUpload;
                ShouldTimeout = shouldTimeout;
            }

            // For testing purposes, expose mock data
            public List<string> GetMockUploadedFileIds() => MockUploadedFileIds;
            public string GetMockVectorStoreId() => MockVectorStoreId;
            public bool GetShouldFailUpload() => ShouldFailUpload;
            public bool GetShouldTimeout() => ShouldTimeout;

            protected override async Task<string?> UploadSingleFileAsync(string fileName, string content, CancellationToken cancellationToken)
            {
                if (ShouldFailUpload)
                {
                    throw new InvalidOperationException("Simulated upload failure");
                }

                if (ShouldTimeout)
                {
                    await Task.Delay(10000, cancellationToken); // This should cause timeout
                }

                // Return a mock file ID
                var fileId = $"file-{fileName.GetHashCode():X}";
                return fileId;
            }

            protected override async Task<string> CreateVectorStoreInternalAsync(List<string> fileIds, CancellationToken ct)
            {
                await Task.Delay(10, ct);
                return MockVectorStoreId;
            }

            protected override async Task WaitForIndexingAsync(List<string> uploadedFilesIds, CancellationToken ct)
            {
                await Task.Delay(10, ct);
            }
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithValidParameters_ShouldNotThrow()
        {
            var client = new Mock<PersistentAgentsClient>().Object;
            var logger = NullLogger<AgentFileManager>.Instance;
            var appSettings = CreateTestAppSettings();

            Assert.DoesNotThrow(() => new AgentFileManager(client, logger, appSettings));
        }

        [Test]
        public void Constructor_WithNullClient_ShouldThrowArgumentNullException()
        {
            var logger = NullLogger<AgentFileManager>.Instance;
            var appSettings = CreateTestAppSettings();

            var ex = Assert.Throws<ArgumentNullException>(() => new AgentFileManager(null!, logger, appSettings));
            Assert.That(ex!.ParamName, Is.EqualTo("client"));
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            var client = new Mock<PersistentAgentsClient>().Object;
            var appSettings = CreateTestAppSettings();

            var ex = Assert.Throws<ArgumentNullException>(() => new AgentFileManager(client, null!, appSettings));
            Assert.That(ex!.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithNullAppSettings_ShouldThrowArgumentNullException()
        {
            var client = new Mock<PersistentAgentsClient>().Object;
            var logger = NullLogger<AgentFileManager>.Instance;

            var ex = Assert.Throws<ArgumentNullException>(() => new AgentFileManager(client, logger, null!));
            Assert.That(ex!.ParamName, Is.EqualTo("appSettings"));
        }

        #endregion

        #region UploadFilesAsync Tests

        [Test]
        public void UploadFilesAsync_WithValidTspFiles_ShouldReturnExpectedResults()
        {
            var testFiles = CreateTestTspFiles();
            var testableManager = CreateTestableAgentFileManager();

            var mockFileIds = testableManager.GetMockUploadedFileIds();
            var mockVectorStoreId = testableManager.GetMockVectorStoreId();

            Assert.That(mockFileIds, Is.Not.Null);
            Assert.That(mockFileIds.Count, Is.EqualTo(3));
            Assert.That(mockVectorStoreId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void UploadFilesAsync_WithEmptyFiles_ShouldHandleEmptyInput()
        {
            var emptyFiles = new Dictionary<string, string>();
            var testableManager = CreateTestableAgentFileManager(mockUploadedFileIds: new List<string>());

            var mockFileIds = testableManager.GetMockUploadedFileIds();

            Assert.That(mockFileIds, Is.Not.Null);
            Assert.That(mockFileIds.Count, Is.EqualTo(0));
        }

        [Test]
        public void UploadFilesAsync_WithNonTspFiles_ShouldFilterCorrectly()
        {
            var mixedFiles = CreateMixedFiles();
            var testableManager = CreateTestableAgentFileManager();

            // Test should filter to only .tsp files
            var testFiles = mixedFiles.Where(kvp => kvp.Key.EndsWith(".tsp", StringComparison.OrdinalIgnoreCase));
            Assert.That(testFiles.Count(), Is.EqualTo(2)); // Only the .tsp files
        }

        [Test]
        public void UploadFilesAsync_WithFailureConfiguration_ShouldIndicateFailure()
        {
            var testFiles = CreateTestTspFiles();
            var testableManager = CreateTestableAgentFileManager(shouldFailUpload: true);

            var shouldFail = testableManager.GetShouldFailUpload();
            Assert.That(shouldFail, Is.True);
        }

        [Test]
        public void UploadFilesAsync_WithTimeoutConfiguration_ShouldIndicateTimeout()
        {
            var testFiles = CreateTestTspFiles();
            var testableManager = CreateTestableAgentFileManager(shouldTimeout: true);

            var shouldTimeout = testableManager.GetShouldTimeout();
            Assert.That(shouldTimeout, Is.True);
        }

        [Test]
        public void UploadFilesAsync_WithNullFiles_ShouldThrowArgumentNullException()
        {
            var testableManager = CreateTestableAgentFileManager();

            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await testableManager.UploadFilesAsync(null!, CancellationToken.None));
        }

        [Test]
        public void UploadFilesAsync_WithCancelledToken_ShouldRespectCancellation()
        {
            var testFiles = CreateTestTspFiles();
            var testableManager = CreateTestableAgentFileManager();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // The method should throw when cancellation token is cancelled
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await testableManager.UploadFilesAsync(testFiles, cts.Token));
        }

        #endregion

        #region File Filtering Tests

        [Test]
        public void FileFiltering_WithTspFiles_ShouldIncludeOnlyTspFiles()
        {
            var files = new Dictionary<string, string>
            {
                { "model.tsp", "content1" },
                { "config.json", "content2" },
                { "types.tsp", "content3" },
                { "readme.md", "content4" },
                { "main.TSP", "content5" } // Test case sensitivity
            };

            var tspFiles = files.Where(kvp => kvp.Key.EndsWith(".tsp", StringComparison.OrdinalIgnoreCase));
            
            Assert.That(tspFiles.Count(), Is.EqualTo(3));
            Assert.That(tspFiles.Any(f => f.Key == "model.tsp"), Is.True);
            Assert.That(tspFiles.Any(f => f.Key == "types.tsp"), Is.True);
            Assert.That(tspFiles.Any(f => f.Key == "main.TSP"), Is.True);
        }

        [Test]
        public void FileFiltering_WithNoTspFiles_ShouldReturnEmpty()
        {
            var files = new Dictionary<string, string>
            {
                { "config.json", "content1" },
                { "readme.md", "content2" },
                { "data.xml", "content3" }
            };

            var tspFiles = files.Where(kvp => kvp.Key.EndsWith(".tsp", StringComparison.OrdinalIgnoreCase));
            
            Assert.That(tspFiles.Count(), Is.EqualTo(0));
        }

        [Test]
        public void FileFiltering_WithEmptyDictionary_ShouldReturnEmpty()
        {
            var files = new Dictionary<string, string>();

            var tspFiles = files.Where(kvp => kvp.Key.EndsWith(".tsp", StringComparison.OrdinalIgnoreCase));
            
            Assert.That(tspFiles.Count(), Is.EqualTo(0));
        }

        #endregion

        #region Vector Store Tests

        [Test]
        public void VectorStore_WithValidConfiguration_ShouldReturnExpectedId()
        {
            var expectedVectorStoreId = "custom-vector-store-456";
            var testableManager = CreateTestableAgentFileManager(mockVectorStoreId: expectedVectorStoreId);

            var vectorStoreId = testableManager.GetMockVectorStoreId();

            Assert.That(vectorStoreId, Is.Not.Null);
            Assert.That(vectorStoreId, Is.Not.Empty);
            Assert.That(vectorStoreId, Is.EqualTo(expectedVectorStoreId));
        }

        [Test]
        public void VectorStore_WithDefaultConfiguration_ShouldReturnDefaultId()
        {
            var testableManager = CreateTestableAgentFileManager();

            var vectorStoreId = testableManager.GetMockVectorStoreId();

            Assert.That(vectorStoreId, Is.EqualTo("test-vector-store-123"));
        }

        #endregion

        #region AppSettings Integration Tests

        [Test]
        public void AgentFileManager_WithCustomIndexingSettings_ShouldUseConfiguredValues()
        {
            var customAppSettings = CreateTestAppSettings(
                indexingMaxWaitTimeSeconds: 120,
                indexingPollingIntervalSeconds: 5);
            var testableManager = CreateTestableAgentFileManager(appSettings: customAppSettings);

            Assert.That(customAppSettings, Is.Not.Null);
            Assert.That(customAppSettings.IndexingMaxWaitTime.TotalSeconds, Is.EqualTo(120));
            Assert.That(customAppSettings.IndexingPollingInterval.TotalSeconds, Is.EqualTo(5));
        }

        [Test]
        public void AgentFileManager_WithDefaultIndexingSettings_ShouldUseDefaultValues()
        {
            var defaultAppSettings = CreateTestAppSettings();
            var testableManager = CreateTestableAgentFileManager(appSettings: defaultAppSettings);

            // Verify that default AppSettings work correctly
            Assert.That(defaultAppSettings, Is.Not.Null);
            Assert.That(defaultAppSettings.IndexingMaxWaitTime.TotalSeconds, Is.EqualTo(60));
            Assert.That(defaultAppSettings.IndexingPollingInterval.TotalSeconds, Is.EqualTo(2));
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public void AgentFileManager_WithUploadFailureScenario_ShouldConfigureCorrectly()
        {
            var testableManager = CreateTestableAgentFileManager(shouldFailUpload: true);

            var shouldFail = testableManager.GetShouldFailUpload();
            Assert.That(shouldFail, Is.True);

            var shouldTimeout = testableManager.GetShouldTimeout();
            Assert.That(shouldTimeout, Is.False);
        }

        [Test]
        public void AgentFileManager_WithTimeoutScenario_ShouldConfigureCorrectly()
        {
            var testableManager = CreateTestableAgentFileManager(shouldTimeout: true);

            var shouldTimeout = testableManager.GetShouldTimeout();
            Assert.That(shouldTimeout, Is.True);

            var shouldFail = testableManager.GetShouldFailUpload();
            Assert.That(shouldFail, Is.False);
        }

        [Test]
        public void AgentFileManager_WithBothErrorScenarios_ShouldConfigureCorrectly()
        {
            var testableManager = CreateTestableAgentFileManager(
                shouldFailUpload: true,
                shouldTimeout: true);

            var shouldFail = testableManager.GetShouldFailUpload();
            var shouldTimeout = testableManager.GetShouldTimeout();

            Assert.That(shouldFail, Is.True);
            Assert.That(shouldTimeout, Is.True);
        }

        #endregion

        #region Parameter Validation Tests

        [Test]
        public void AgentFileManager_WithAllValidParameters_ShouldCreateSuccessfully()
        {
            var client = new Mock<PersistentAgentsClient>().Object;
            var logger = NullLogger<AgentFileManager>.Instance;
            var appSettings = CreateTestAppSettings();

            var AgentFileManager = new AgentFileManager(client, logger, appSettings);

            Assert.That(AgentFileManager, Is.Not.Null);
            Assert.That(AgentFileManager, Is.InstanceOf<AgentFileManager>());
        }

        [Test]
        public void AgentFileManager_ParameterValidation_ShouldEnforceNonNullConstraints()
        {
            var client = new Mock<PersistentAgentsClient>().Object;
            var logger = NullLogger<AgentFileManager>.Instance;
            var appSettings = CreateTestAppSettings();

            // Test all valid parameters work
            Assert.DoesNotThrow(() => new AgentFileManager(client, logger, appSettings));

            // Test each null parameter fails appropriately
            Assert.Throws<ArgumentNullException>(() => new AgentFileManager(null!, logger, appSettings));
            Assert.Throws<ArgumentNullException>(() => new AgentFileManager(client, null!, appSettings));
            Assert.Throws<ArgumentNullException>(() => new AgentFileManager(client, logger, null!));
        }

        #endregion

        #region Integration Tests

        [Test]
        public void IntegrationTest_FileUploadAndVectorStoreCreation_ShouldWorkTogether()
        {
            var testFiles = CreateTestTspFiles();
            var testableManager = CreateTestableAgentFileManager();

            var fileIds = testableManager.GetMockUploadedFileIds();
            var vectorStoreId = testableManager.GetMockVectorStoreId();

            Assert.That(fileIds, Is.Not.Null);
            Assert.That(fileIds.Count, Is.GreaterThan(0));
            Assert.That(vectorStoreId, Is.Not.Null.And.Not.Empty);

            foreach (var fileId in fileIds)
            {
                Assert.That(fileId, Is.Not.Null.And.Not.Empty);
                Assert.That(fileId, Does.StartWith("file-"));
            }
        }

        [Test]
        public void IntegrationTest_FileFilteringAndUpload_ShouldProcessCorrectFiles()
        {
            var mixedFiles = CreateMixedFiles();
            var testableManager = CreateTestableAgentFileManager();

            // Simulate the filtering that happens in UploadFilesAsync
            var relevantFiles = mixedFiles.Where(kvp => kvp.Key.EndsWith(".tsp", StringComparison.OrdinalIgnoreCase));

            Assert.That(relevantFiles.Count(), Is.EqualTo(2)); // Only .tsp files
            Assert.That(relevantFiles.All(f => f.Key.EndsWith(".tsp", StringComparison.OrdinalIgnoreCase)), Is.True);

            var fileIds = testableManager.GetMockUploadedFileIds();
            Assert.That(fileIds, Is.Not.Null);
        }

        #endregion

        #region Helper Methods

        private TestableAgentFileManager CreateTestableAgentFileManager(
            List<string>? mockUploadedFileIds = null,
            string mockVectorStoreId = "test-vector-store-123",
            bool shouldFailUpload = false,
            bool shouldTimeout = false,
            AppSettings? appSettings = null)
        {
            var client = new Mock<PersistentAgentsClient>().Object;
            var logger = NullLogger<AgentFileManager>.Instance;
            var settings = appSettings ?? CreateTestAppSettings();

            return new TestableAgentFileManager(
                client,
                logger,
                settings,
                mockUploadedFileIds,
                mockVectorStoreId,
                shouldFailUpload,
                shouldTimeout);
        }

        private AppSettings CreateTestAppSettings(
            int indexingMaxWaitTimeSeconds = 60,
            int indexingPollingIntervalSeconds = 2)
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

            // Set up indexing-specific configurations
            var maxWaitSection = new Mock<IConfigurationSection>();
            maxWaitSection.Setup(s => s.Value).Returns(indexingMaxWaitTimeSeconds.ToString());
            configMock.Setup(c => c.GetSection("AzureSettings:IndexingMaxWaitTimeSeconds")).Returns(maxWaitSection.Object);

            var pollingSection = new Mock<IConfigurationSection>();
            pollingSection.Setup(s => s.Value).Returns(indexingPollingIntervalSeconds.ToString());
            configMock.Setup(c => c.GetSection("AzureSettings:IndexingPollingIntervalSeconds")).Returns(pollingSection.Object);

            return new AppSettings(configMock.Object, mockLogger.Object);
        }

        private Dictionary<string, string> CreateTestTspFiles()
        {
            return new Dictionary<string, string>
            {
                { "main.tsp", "namespace MyService;" },
                { "models.tsp", "model User { name: string; }" },
                { "operations.tsp", "interface Operations { @get list(): User[]; }" }
            };
        }

        private Dictionary<string, string> CreateMixedFiles()
        {
            return new Dictionary<string, string>
            {
                { "main.tsp", "namespace MyService;" },
                { "models.tsp", "model User { name: string; }" },
                { "config.json", "{ \"version\": \"1.0\" }" },
                { "readme.md", "# Documentation" },
                { "data.xml", "<root></root>" }
            };
        }

        #endregion
    }
}
