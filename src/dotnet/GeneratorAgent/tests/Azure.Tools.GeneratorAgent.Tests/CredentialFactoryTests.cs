using Azure.Core;
using Azure.Identity;
using Azure.Tools.GeneratorAgent.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests.Authentication
{
    [TestFixture]
    public class CredentialFactoryTests
    {
        private static CredentialFactory CreateCredentialFactory(ILogger<CredentialFactory>? logger = null)
        {
            return new CredentialFactory(logger ?? NullLogger<CredentialFactory>.Instance);
        }

        private static Mock<ILogger<CredentialFactory>> CreateLoggerMock()
        {
            return new Mock<ILogger<CredentialFactory>>();
        }

        private static TokenCredentialOptions CreateTokenCredentialOptions(Uri? authorityHost = null)
        {
            return new TokenCredentialOptions { AuthorityHost = authorityHost };
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new CredentialFactory(null!));
            Assert.That(ex.ParamName!, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithValidLogger_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => CreateCredentialFactory());
        }

        [Test]
        public void CreateCredential_LocalDevelopment_WithoutOptions_ReturnsChainedTokenCredential()
        {
            var factory = CreateCredentialFactory();

            TokenCredential credential = factory.CreateCredential(RuntimeEnvironment.LocalDevelopment);

            Assert.That(credential, Is.Not.Null);
            Assert.That(credential, Is.TypeOf<ChainedTokenCredential>());
        }

        [Test]
        public void CreateCredential_LocalDevelopment_WithOptions_ReturnsChainedTokenCredential()
        {
            var factory = CreateCredentialFactory();
            var options = CreateTokenCredentialOptions(new Uri("https://fake-authority.example.com/"));

            TokenCredential credential = factory.CreateCredential(RuntimeEnvironment.LocalDevelopment, options);

            Assert.That(credential, Is.Not.Null);
            Assert.That(credential, Is.TypeOf<ChainedTokenCredential>());
        }

        [Test]
        public void CreateCredential_LocalDevelopment_WithCustomRetryOptions_ReturnsChainedTokenCredential()
        {
            var factory = CreateCredentialFactory();
            var options = new TokenCredentialOptions();
            options.Retry.MaxRetries = 5;
            options.Retry.Delay = TimeSpan.FromSeconds(2);

            TokenCredential credential = factory.CreateCredential(RuntimeEnvironment.LocalDevelopment, options);

            Assert.That(credential, Is.Not.Null);
            Assert.That(credential, Is.TypeOf<ChainedTokenCredential>());
        }

        [Test]
        public void CreateCredential_DevOpsPipeline_WithoutOptions_ReturnsManagedIdentityCredential()
        {
            var factory = CreateCredentialFactory();

            TokenCredential credential = factory.CreateCredential(RuntimeEnvironment.DevOpsPipeline);

            Assert.That(credential, Is.Not.Null);
            Assert.That(credential, Is.TypeOf<ManagedIdentityCredential>());
        }

        [Test]
        public void CreateCredential_DevOpsPipeline_WithOptions_ReturnsManagedIdentityCredential()
        {
            var factory = CreateCredentialFactory();
            var options = CreateTokenCredentialOptions(new Uri("https://fake-authority.example.com/"));

            TokenCredential credential = factory.CreateCredential(RuntimeEnvironment.DevOpsPipeline, options);

            Assert.That(credential, Is.Not.Null);
            Assert.That(credential, Is.TypeOf<ManagedIdentityCredential>());
        }

        [Test]
        public void CreateCredential_DevOpsPipeline_WithDiagnosticsOptions_ReturnsManagedIdentityCredential()
        {
            var factory = CreateCredentialFactory();
            var options = new TokenCredentialOptions();
            options.Diagnostics.IsLoggingEnabled = true;
            options.Diagnostics.IsAccountIdentifierLoggingEnabled = true;

            TokenCredential credential = factory.CreateCredential(RuntimeEnvironment.DevOpsPipeline, options);

            Assert.That(credential, Is.Not.Null);
            Assert.That(credential, Is.TypeOf<ManagedIdentityCredential>());
        }

        [Test]
        public void CreateCredential_InvalidEnvironment_ThrowsArgumentException()
        {
            var factory = CreateCredentialFactory();
            const RuntimeEnvironment invalidEnvironment = (RuntimeEnvironment)999;

            var ex = Assert.Throws<ArgumentException>(() => factory.CreateCredential(invalidEnvironment));
            Assert.That(ex.ParamName!, Is.EqualTo("environment"));
            Assert.That(ex.Message, Does.Contain("Unsupported runtime environment"));
        }

        [Test]
        public void CreateCredential_LogsCredentialCreation()
        {
            var loggerMock = CreateLoggerMock();
            var factory = CreateCredentialFactory(loggerMock.Object);

            factory.CreateCredential(RuntimeEnvironment.LocalDevelopment);

            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Creating credential for environment")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public void CreateCredential_NullOptions_DoesNotThrow()
        {
            var factory = CreateCredentialFactory();

            Assert.DoesNotThrow(() => factory.CreateCredential(RuntimeEnvironment.LocalDevelopment, null));
            Assert.DoesNotThrow(() => factory.CreateCredential(RuntimeEnvironment.DevOpsPipeline, null));
        }

        [Test]
        public void CreateCredential_WithAuthorityHost_AcceptsValidUri()
        {
            var factory = CreateCredentialFactory();
            var options = CreateTokenCredentialOptions(new Uri("https://fake-government-cloud.example.com/")); // Fake government cloud

            Assert.DoesNotThrow(() => factory.CreateCredential(RuntimeEnvironment.LocalDevelopment, options));
            Assert.DoesNotThrow(() => factory.CreateCredential(RuntimeEnvironment.DevOpsPipeline, options));
        }

        [Test]
        public void CreateCredential_LocalDevelopment_RespectsEnvironmentVariables()
        {
            var factory = CreateCredentialFactory();
            string originalTenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? "";
            string uniqueTenantId = $"test-tenant-{Guid.NewGuid():N}";
            
            try
            {
                Environment.SetEnvironmentVariable("AZURE_TENANT_ID", uniqueTenantId);

                TokenCredential credential = factory.CreateCredential(RuntimeEnvironment.LocalDevelopment);

                Assert.That(credential, Is.Not.Null);
                Assert.That(credential, Is.TypeOf<ChainedTokenCredential>());
            }
            finally
            {
                Environment.SetEnvironmentVariable("AZURE_TENANT_ID", originalTenantId);
            }
        }

        [Test]
        public void CreateCredential_LocalDevelopment_WithoutEnvironmentVariables_StillWorks()
        {
            var factory = CreateCredentialFactory();
            string originalTenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? "";
            
            try
            {
                Environment.SetEnvironmentVariable("AZURE_TENANT_ID", null);

                TokenCredential credential = factory.CreateCredential(RuntimeEnvironment.LocalDevelopment);

                Assert.That(credential, Is.Not.Null);
                Assert.That(credential, Is.TypeOf<ChainedTokenCredential>());
            }
            finally
            {
                Environment.SetEnvironmentVariable("AZURE_TENANT_ID", originalTenantId);
            }
        }
    }
}
