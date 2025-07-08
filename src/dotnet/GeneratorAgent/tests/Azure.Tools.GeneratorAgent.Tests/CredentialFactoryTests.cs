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
    [Category("Unit")]
    public class CredentialFactoryTests
    {
        private ICredentialFactory _factory;
        private Mock<ILogger<CredentialFactory>> _loggerMock;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<CredentialFactory>>();
            _factory = new CredentialFactory(_loggerMock.Object);
        }

        [Test]
        [Category("Constructor")]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new CredentialFactory(null!));
            Assert.That(ex.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        [Category("Constructor")]
        public void Constructor_WithValidLogger_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => new CredentialFactory(NullLogger<CredentialFactory>.Instance));
        }

        [Test]
        [Category("LocalDevelopment")]
        public void CreateCredential_LocalDevelopment_WithoutOptions_ReturnsChainedTokenCredential()
        {
            // Act
            TokenCredential credential = _factory.CreateCredential(RuntimeEnvironment.LocalDevelopment);

            // Assert
            Assert.That(credential, Is.Not.Null);
            Assert.That(credential, Is.TypeOf<ChainedTokenCredential>());
        }

        [Test]
        [Category("LocalDevelopment")]
        public void CreateCredential_LocalDevelopment_WithOptions_ReturnsChainedTokenCredential()
        {
            // Arrange
            var options = new TokenCredentialOptions
            {
                AuthorityHost = new Uri("https://login.microsoftonline.com/")
            };

            // Act
            TokenCredential credential = _factory.CreateCredential(RuntimeEnvironment.LocalDevelopment, options);

            // Assert
            Assert.That(credential, Is.Not.Null);
            Assert.That(credential, Is.TypeOf<ChainedTokenCredential>());
        }

        [Test]
        [Category("LocalDevelopment")]
        public void CreateCredential_LocalDevelopment_WithCustomRetryOptions_ReturnsChainedTokenCredential()
        {
            // Arrange
            var options = new TokenCredentialOptions();
            options.Retry.MaxRetries = 5;
            options.Retry.Delay = TimeSpan.FromSeconds(2);

            // Act
            TokenCredential credential = _factory.CreateCredential(RuntimeEnvironment.LocalDevelopment, options);

            // Assert
            Assert.That(credential, Is.Not.Null);
            Assert.That(credential, Is.TypeOf<ChainedTokenCredential>());
        }

        [Test]
        [Category("DevOpsPipeline")]
        public void CreateCredential_DevOpsPipeline_WithoutOptions_ReturnsManagedIdentityCredential()
        {
            // Act
            TokenCredential credential = _factory.CreateCredential(RuntimeEnvironment.DevOpsPipeline);

            // Assert
            Assert.That(credential, Is.Not.Null);
            Assert.That(credential, Is.TypeOf<ManagedIdentityCredential>());
        }

        [Test]
        [Category("DevOpsPipeline")]
        public void CreateCredential_DevOpsPipeline_WithOptions_ReturnsManagedIdentityCredential()
        {
            // Arrange
            var options = new TokenCredentialOptions
            {
                AuthorityHost = new Uri("https://login.microsoftonline.com/")
            };

            // Act
            TokenCredential credential = _factory.CreateCredential(RuntimeEnvironment.DevOpsPipeline, options);

            // Assert
            Assert.That(credential, Is.Not.Null);
            Assert.That(credential, Is.TypeOf<ManagedIdentityCredential>());
        }

        [Test]
        [Category("DevOpsPipeline")]
        public void CreateCredential_DevOpsPipeline_WithDiagnosticsOptions_ReturnsManagedIdentityCredential()
        {
            // Arrange
            var options = new TokenCredentialOptions();
            options.Diagnostics.IsLoggingEnabled = true;
            options.Diagnostics.IsAccountIdentifierLoggingEnabled = true;

            // Act
            TokenCredential credential = _factory.CreateCredential(RuntimeEnvironment.DevOpsPipeline, options);

            // Assert
            Assert.That(credential, Is.Not.Null);
            Assert.That(credential, Is.TypeOf<ManagedIdentityCredential>());
        }

        [Test]
        [Category("Validation")]
        public void CreateCredential_InvalidEnvironment_ThrowsArgumentException()
        {
            // Arrange
            const RuntimeEnvironment invalidEnvironment = (RuntimeEnvironment)999;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => _factory.CreateCredential(invalidEnvironment));
            Assert.That(ex.ParamName, Is.EqualTo("environment"));
            Assert.That(ex.Message, Does.Contain("Unsupported runtime environment"));
        }

        [Test]
        [Category("Logging")]
        public void CreateCredential_LogsCredentialCreation()
        {
            // Act
            _factory.CreateCredential(RuntimeEnvironment.LocalDevelopment);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Creating credential for environment")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        [Category("Options")]
        public void CreateCredential_NullOptions_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _factory.CreateCredential(RuntimeEnvironment.LocalDevelopment, null));
            Assert.DoesNotThrow(() => _factory.CreateCredential(RuntimeEnvironment.DevOpsPipeline, null));
        }

        [Test]
        [Category("Options")]
        public void CreateCredential_WithAuthorityHost_AcceptsValidUri()
        {
            // Arrange
            var options = new TokenCredentialOptions
            {
                AuthorityHost = new Uri("https://login.microsoftonline.us/") // Government cloud
            };

            // Act & Assert
            Assert.DoesNotThrow(() => _factory.CreateCredential(RuntimeEnvironment.LocalDevelopment, options));
            Assert.DoesNotThrow(() => _factory.CreateCredential(RuntimeEnvironment.DevOpsPipeline, options));
        }
    }
}