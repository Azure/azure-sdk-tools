using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests.Configuration
{
    [TestFixture]
    public class AppSettingsTests
    {
        private static Mock<IConfigurationSection> CreateConfigurationSectionMock(string? value = null)
        {
            var sectionMock = new Mock<IConfigurationSection>();
            sectionMock.Setup(s => s.Value).Returns(value);
            return sectionMock;
        }

        private static Mock<IConfiguration> CreateConfigurationMock()
        {
            var mock = new Mock<IConfiguration>();
            
            // Setup GetSection to return empty sections for any key
            mock.Setup(x => x.GetSection(It.IsAny<string>()))
                .Returns(() => {
                    var sectionMock = new Mock<IConfigurationSection>();
                    sectionMock.Setup(s => s.Value).Returns((string?)null);
                    return sectionMock.Object;
                });
                
            return mock;
        }

        private static Mock<ILogger<AppSettings>> CreateLoggerMock()
        {
            return new Mock<ILogger<AppSettings>>();
        }

        private static AppSettings CreateAppSettings(Mock<IConfiguration>? configurationMock = null, Mock<ILogger<AppSettings>>? loggerMock = null)
        {
            return new AppSettings(
                (configurationMock ?? CreateConfigurationMock()).Object, 
                (loggerMock ?? CreateLoggerMock()).Object);
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
        {
            var loggerMock = CreateLoggerMock();
            var ex = Assert.Throws<ArgumentNullException>(() => new AppSettings(null!, loggerMock.Object));
            Assert.That(ex?.ParamName, Is.EqualTo("configuration"));
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            var configurationMock = CreateConfigurationMock();
            var ex = Assert.Throws<ArgumentNullException>(() => new AppSettings(configurationMock.Object, null!));
            Assert.That(ex?.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithValidParameters_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => CreateAppSettings());
        }

        #endregion

        #region ProjectEndpoint Tests

        [Test]
        public void ProjectEndpoint_WithValidValue_ReturnsValue()
        {
            const string expectedValue = "https://test-endpoint.example.com";
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock(expectedValue);
            configurationMock.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            string result = appSettings.ProjectEndpoint;

            Assert.That(result, Is.EqualTo(expectedValue));
        }

        [Test]
        public void ProjectEndpoint_WithNullValue_ThrowsInvalidOperationException()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock(null);
            configurationMock.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            var ex = Assert.Throws<InvalidOperationException>(() => _ = appSettings.ProjectEndpoint);
            Assert.That(ex?.Message, Does.Contain("Required configuration setting 'AzureSettings:ProjectEndpoint' is missing or empty"));
        }

        [Test]
        public void ProjectEndpoint_WithEmptyValue_ThrowsInvalidOperationException()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock("");
            configurationMock.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            var ex = Assert.Throws<InvalidOperationException>(() => _ = appSettings.ProjectEndpoint);
            Assert.That(ex?.Message, Does.Contain("Required configuration setting 'AzureSettings:ProjectEndpoint' is missing or empty"));
        }

        [Test]
        public void ProjectEndpoint_WithWhitespaceValue_ThrowsInvalidOperationException()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock("   ");
            configurationMock.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            var ex = Assert.Throws<InvalidOperationException>(() => _ = appSettings.ProjectEndpoint);
            Assert.That(ex?.Message, Does.Contain("Required configuration setting 'AzureSettings:ProjectEndpoint' is missing or empty"));
        }

        #endregion

        #region Model Tests

        [Test]
        public void Model_WithValidValue_ReturnsValue()
        {
            const string expectedValue = "gpt-4";
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock(expectedValue);
            configurationMock.Setup(c => c.GetSection("AzureSettings:Model")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            string result = appSettings.Model;

            Assert.That(result, Is.EqualTo(expectedValue));
        }

        [Test]
        public void Model_WithNullValue_ReturnsDefaultValue()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock(null);
            configurationMock.Setup(c => c.GetSection("AzureSettings:Model")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            string result = appSettings.Model;

            Assert.That(result, Is.EqualTo("gpt-4o"));
        }

        #endregion

        #region AgentName Tests

        [Test]
        public void AgentName_WithValidValue_ReturnsValue()
        {
            const string expectedValue = "TestAgent";
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock(expectedValue);
            configurationMock.Setup(c => c.GetSection("AzureSettings:AgentName")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            string result = appSettings.AgentName;

            Assert.That(result, Is.EqualTo(expectedValue));
        }

        [Test]
        public void AgentName_WithNullValue_ReturnsDefaultValue()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock(null);
            configurationMock.Setup(c => c.GetSection("AzureSettings:AgentName")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            string result = appSettings.AgentName;

            Assert.That(result, Is.EqualTo("AZC Fixer"));
        }

        #endregion

        #region AgentInstructions Tests

        [Test]
        public void AgentInstructions_WithValidValue_ReturnsValue()
        {
            const string expectedValue = "Test instructions";
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock(expectedValue);
            configurationMock.Setup(c => c.GetSection("AzureSettings:AgentInstructions")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            string result = appSettings.AgentInstructions;

            Assert.That(result, Is.EqualTo(expectedValue));
        }

        [Test]
        public void AgentInstructions_WithNullValue_ReturnsEmptyString()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock(null);
            configurationMock.Setup(c => c.GetSection("AzureSettings:AgentInstructions")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            string result = appSettings.AgentInstructions;

            Assert.That(result, Is.EqualTo(string.Empty));
        }

        #endregion

        #region Timeout Configuration Tests

        [Test]
        public void IndexingMaxWaitTime_WithValidValue_ReturnsCorrectTimeSpan()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock("300");
            configurationMock.Setup(c => c.GetSection("AzureSettings:IndexingMaxWaitTimeSeconds")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            TimeSpan result = appSettings.IndexingMaxWaitTime;

            Assert.That(result, Is.EqualTo(TimeSpan.FromSeconds(300)));
        }

        [Test]
        public void IndexingMaxWaitTime_WithNullValue_ReturnsDefaultTimeSpan()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock(null);
            configurationMock.Setup(c => c.GetSection("AzureSettings:IndexingMaxWaitTimeSeconds")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            TimeSpan result = appSettings.IndexingMaxWaitTime;

            Assert.That(result, Is.EqualTo(TimeSpan.FromSeconds(180)));
        }

        [Test]
        public void IndexingPollingInterval_WithValidValue_ReturnsCorrectTimeSpan()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock("10");
            configurationMock.Setup(c => c.GetSection("AzureSettings:IndexingPollingIntervalSeconds")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            TimeSpan result = appSettings.IndexingPollingInterval;

            Assert.That(result, Is.EqualTo(TimeSpan.FromSeconds(10)));
        }

        [Test]
        public void IndexingPollingInterval_WithNullValue_ReturnsDefaultTimeSpan()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock(null);
            configurationMock.Setup(c => c.GetSection("AzureSettings:IndexingPollingIntervalSeconds")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            TimeSpan result = appSettings.IndexingPollingInterval;

            Assert.That(result, Is.EqualTo(TimeSpan.FromSeconds(5)));
        }

        [Test]
        public void VectorStoreReadyWaitTime_WithValidValue_ReturnsCorrectTimeSpan()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock("10000");
            configurationMock.Setup(c => c.GetSection("AzureSettings:VectorStoreReadyWaitTimeMs")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            TimeSpan result = appSettings.VectorStoreReadyWaitTime;

            Assert.That(result, Is.EqualTo(TimeSpan.FromMilliseconds(10000)));
        }

        [Test]
        public void VectorStoreReadyWaitTime_WithNullValue_ReturnsDefaultTimeSpan()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock(null);
            configurationMock.Setup(c => c.GetSection("AzureSettings:VectorStoreReadyWaitTimeMs")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            TimeSpan result = appSettings.VectorStoreReadyWaitTime;

            Assert.That(result, Is.EqualTo(TimeSpan.FromMilliseconds(5000)));
        }

        #endregion

        #region Concurrency Settings Tests

        [Test]
        public void MaxConcurrentUploads_WithValidValue_ReturnsCorrectValue()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock("20");
            configurationMock.Setup(c => c.GetSection("AzureSettings:MaxConcurrentUploads")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            int result = appSettings.MaxConcurrentUploads;

            Assert.That(result, Is.EqualTo(20));
        }

        [Test]
        public void MaxConcurrentUploads_WithNullValue_ReturnsDefaultValue()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock(null);
            configurationMock.Setup(c => c.GetSection("AzureSettings:MaxConcurrentUploads")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            int result = appSettings.MaxConcurrentUploads;

            Assert.That(result, Is.EqualTo(10));
        }

        #endregion

        #region Indexing Batch Processing Tests

        [Test]
        public void IndexingStatusBatchSize_WithValidValue_ReturnsCorrectValue()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock("25");
            configurationMock.Setup(c => c.GetSection("AzureSettings:IndexingStatusBatchSize")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            int result = appSettings.IndexingStatusBatchSize;

            Assert.That(result, Is.EqualTo(25));
        }

        [Test]
        public void IndexingStatusBatchSize_WithNullValue_ReturnsDefaultValue()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock(null);
            configurationMock.Setup(c => c.GetSection("AzureSettings:IndexingStatusBatchSize")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            int result = appSettings.IndexingStatusBatchSize;

            Assert.That(result, Is.EqualTo(10));
        }

        [Test]
        public void MaxPendingFilesToShowInDebug_WithValidValue_ReturnsCorrectValue()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock("5");
            configurationMock.Setup(c => c.GetSection("AzureSettings:MaxPendingFilesToShowInDebug")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            int result = appSettings.MaxPendingFilesToShowInDebug;

            Assert.That(result, Is.EqualTo(5));
        }

        [Test]
        public void MaxPendingFilesToShowInDebug_WithNullValue_ReturnsDefaultValue()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock(null);
            configurationMock.Setup(c => c.GetSection("AzureSettings:MaxPendingFilesToShowInDebug")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            int result = appSettings.MaxPendingFilesToShowInDebug;

            Assert.That(result, Is.EqualTo(3));
        }

        #endregion

        #region Constant Properties Tests

        [Test]
        public void TypespecEmitterPackage_ReturnsExpectedValue()
        {
            var appSettings = CreateAppSettings();

            string result = appSettings.TypespecEmitterPackage;

            Assert.That(result, Is.EqualTo("@typespec/http-client-csharp"));
        }

        [Test]
        public void TypeSpecDirectoryName_ReturnsExpectedValue()
        {
            var appSettings = CreateAppSettings();

            string result = appSettings.TypeSpecDirectoryName;

            Assert.That(result, Is.EqualTo("@typespec"));
        }

        [Test]
        public void HttpClientCSharpDirectoryName_ReturnsExpectedValue()
        {
            var appSettings = CreateAppSettings();

            string result = appSettings.HttpClientCSharpDirectoryName;

            Assert.That(result, Is.EqualTo("http-client-csharp"));
        }

        [Test]
        public void AzureSpecRepository_ReturnsExpectedValue()
        {
            var appSettings = CreateAppSettings();

            string result = appSettings.AzureSpecRepository;

            Assert.That(result, Is.EqualTo("Azure/azure-rest-api-specs"));
        }

        [Test]
        public void AzureSdkDirectoryName_ReturnsExpectedValue()
        {
            var appSettings = CreateAppSettings();

            string result = appSettings.AzureSdkDirectoryName;

            Assert.That(result, Is.EqualTo("azure-sdk-for-net"));
        }

        [Test]
        public void PowerShellScriptPath_ReturnsExpectedValue()
        {
            var appSettings = CreateAppSettings();

            string result = appSettings.PowerShellScriptPath;

            Assert.That(result, Is.EqualTo("eng/scripts/automation/Invoke-TypeSpecDataPlaneGenerateSDKPackage.ps1"));
        }

        #endregion

        #region Error Handling for Invalid Numeric Values

        [Test]
        public void IndexingMaxWaitTime_WithInvalidValue_ThrowsFormatException()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock("invalid");
            configurationMock.Setup(c => c.GetSection("AzureSettings:IndexingMaxWaitTimeSeconds")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            Assert.Throws<FormatException>(() => _ = appSettings.IndexingMaxWaitTime);
        }

        [Test]
        public void MaxConcurrentUploads_WithInvalidValue_ThrowsFormatException()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock("invalid");
            configurationMock.Setup(c => c.GetSection("AzureSettings:MaxConcurrentUploads")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            Assert.Throws<FormatException>(() => _ = appSettings.MaxConcurrentUploads);
        }

        [Test]
        public void VectorStoreReadyWaitTime_WithInvalidValue_ThrowsFormatException()
        {
            var configurationMock = CreateConfigurationMock();
            var sectionMock = CreateConfigurationSectionMock("invalid");
            configurationMock.Setup(c => c.GetSection("AzureSettings:VectorStoreReadyWaitTimeMs")).Returns(sectionMock.Object);
            var appSettings = CreateAppSettings(configurationMock);

            Assert.Throws<FormatException>(() => _ = appSettings.VectorStoreReadyWaitTime);
        }

        #endregion
    }
}
