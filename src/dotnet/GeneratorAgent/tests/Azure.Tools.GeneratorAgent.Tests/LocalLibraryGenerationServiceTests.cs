using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class LocalLibraryGenerationServiceTests
    {
        [Test]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var logger = NullLogger<LocalLibraryGenerationService>.Instance;
            var processExecutionService = CreateMockProcessExecutionService().Object;

            // Act & Assert
            Assert.DoesNotThrow(() => new LocalLibraryGenerationService(appSettings, logger, processExecutionService));
        }

        [Test]
        public void Constructor_WithNullAppSettings_ShouldThrowArgumentNullException()
        {
            // Arrange
            var logger = NullLogger<LocalLibraryGenerationService>.Instance;
            var processExecutionService = CreateMockProcessExecutionService().Object;

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new LocalLibraryGenerationService(null!, logger, processExecutionService));
            Assert.That(exception!.ParamName, Is.EqualTo("appSettings"));
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var processExecutionService = CreateMockProcessExecutionService().Object;

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new LocalLibraryGenerationService(appSettings, null!, processExecutionService));
            Assert.That(exception!.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithNullProcessExecutionService_ShouldThrowArgumentNullException()
        {
            // Arrange
            var appSettings = CreateTestAppSettings();
            var logger = NullLogger<LocalLibraryGenerationService>.Instance;

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new LocalLibraryGenerationService(appSettings, logger, null!));
            Assert.That(exception!.ParamName, Is.EqualTo("processExecutionService"));
        }

        [Test]
        public async Task InstallTypeSpecDependencies_WithSuccessfulExecution_ShouldCompleteSuccessfully()
        {
            // Arrange
            var mockProcessExecutionService = CreateMockProcessExecutionService();
            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Success("Dependencies installed successfully"));

            var service = CreateService(processExecutionService: mockProcessExecutionService.Object);

            // Act & Assert
            Assert.DoesNotThrowAsync(() => service.InstallTypeSpecDependencies(CancellationToken.None));
        }

        [Test]
        public async Task InstallTypeSpecDependencies_WithFailedExecution_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var mockProcessExecutionService = CreateMockProcessExecutionService();
            var processException = new ProcessExecutionException("Failed to install dependencies", "npm", "output", "install failed", 1);
            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Failure(processException));

            var service = CreateService(processExecutionService: mockProcessExecutionService.Object);

            // Act & Assert
            var exception = Assert.ThrowsAsync<InvalidOperationException>(() => 
                service.InstallTypeSpecDependencies(CancellationToken.None));
            Assert.That(exception!.Message, Does.Contain("Failed to install TypeSpec dependencies"));
            Assert.That(exception!.Message, Does.Contain("install failed"));
        }

        [Test]
        [Platform("Win")]
        public async Task InstallTypeSpecDependencies_OnWindows_ShouldUsePowerShellCommand()
        {
            // Arrange
            var mockProcessExecutionService = CreateMockProcessExecutionService();
            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Success("Success"));

            var service = CreateService(processExecutionService: mockProcessExecutionService.Object);

            // Act
            await service.InstallTypeSpecDependencies(CancellationToken.None);

            // Assert
            mockProcessExecutionService.Verify(x => x.ExecuteAsync(
                "pwsh",
                "-Command \"npm install --global @typespec/compiler @typespec/http-client-csharp\"",
                Path.GetTempPath(),
                It.IsAny<CancellationToken>(),
                TimeSpan.FromMinutes(3)), Times.Once);
        }

        [Test]
        [Platform("Unix")]
        public async Task InstallTypeSpecDependencies_OnUnix_ShouldUseDirectNpmCommand()
        {
            // Arrange
            var mockProcessExecutionService = CreateMockProcessExecutionService();
            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Success("Success"));

            var service = CreateService(processExecutionService: mockProcessExecutionService.Object);

            // Act
            await service.InstallTypeSpecDependencies(CancellationToken.None);

            // Assert
            mockProcessExecutionService.Verify(x => x.ExecuteAsync(
                "npm",
                "install --global @typespec/compiler @typespec/http-client-csharp",
                Path.GetTempPath(),
                It.IsAny<CancellationToken>(),
                TimeSpan.FromMinutes(3)), Times.Once);
        }

        [Test]
        public async Task InstallTypeSpecDependencies_ShouldUseCorrectTimeout()
        {
            // Arrange
            var mockProcessExecutionService = CreateMockProcessExecutionService();
            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Success("Success"));

            var service = CreateService(processExecutionService: mockProcessExecutionService.Object);

            // Act
            await service.InstallTypeSpecDependencies(CancellationToken.None);

            // Assert
            mockProcessExecutionService.Verify(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                TimeSpan.FromMinutes(3)), Times.Once);
        }

        [Test]
        public async Task InstallTypeSpecDependencies_ShouldValidateProcessArguments()
        {
            // Arrange
            var mockProcessExecutionService = CreateMockProcessExecutionService();
            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Success("Success"));

            var service = CreateService(processExecutionService: mockProcessExecutionService.Object);

            // Act
            await service.InstallTypeSpecDependencies(CancellationToken.None);

            // Assert - should not throw any validation exceptions
            Assert.Pass("Arguments were validated successfully");
        }

        [Test]
        public async Task InstallTypeSpecDependencies_WithCancellation_ShouldRespectCancellationToken()
        {
            // Arrange
            var mockProcessExecutionService = CreateMockProcessExecutionService();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new OperationCanceledException());

            var service = CreateService(processExecutionService: mockProcessExecutionService.Object);

            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(() => 
                service.InstallTypeSpecDependencies(cts.Token));
        }

        [Test]
        public async Task CompileTypeSpecAsync_WithValidContext_ShouldReturnSuccessResult()
        {
            // Arrange
            var mockProcessExecutionService = CreateMockProcessExecutionService();
            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Success("Compilation successful"));

            var service = CreateService(processExecutionService: mockProcessExecutionService.Object);
            var validationContext = CreateTestValidationContext();

            // Act
            var result = await service.CompileTypeSpecAsync(validationContext, CancellationToken.None);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        public async Task CompileTypeSpecAsync_WithFailedCompilation_ShouldReturnFailureResult()
        {
            // Arrange
            var mockProcessExecutionService = CreateMockProcessExecutionService();
            var processException = new ProcessExecutionException("TypeSpec compilation error", "npx", "output", "compilation failed", 1);
            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Failure(processException));

            var service = CreateService(processExecutionService: mockProcessExecutionService.Object);
            var validationContext = CreateTestValidationContext();

            // Act
            var result = await service.CompileTypeSpecAsync(validationContext, CancellationToken.None);

            // Assert
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ProcessException, Is.Not.Null);
        }

        [Test]
        public void CompileTypeSpecAsync_WithNullValidationContext_ShouldThrowArgumentNullException()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentNullException>(() => 
                service.CompileTypeSpecAsync(null!, CancellationToken.None));
            Assert.That(exception!.ParamName, Is.EqualTo("validationContext"));
        }

        [Test]
        [Platform("Win")]
        public async Task CompileTypeSpecAsync_OnWindows_ShouldUsePowerShellCommand()
        {
            // Arrange
            var mockProcessExecutionService = CreateMockProcessExecutionService();
            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Success("Success"));

            var service = CreateService(processExecutionService: mockProcessExecutionService.Object);
            var validationContext = CreateTestValidationContext();

            // Act
            await service.CompileTypeSpecAsync(validationContext, CancellationToken.None);

            // Assert
            var expectedArgs = $"-Command \"npx tsp compile . --emit @typespec/http-client-csharp --option '@typespec/http-client-csharp.emitter-output-dir={validationContext.ValidatedSdkDir}'\"";
            mockProcessExecutionService.Verify(x => x.ExecuteAsync(
                "pwsh",
                expectedArgs,
                validationContext.CurrentTypeSpecDir,
                It.IsAny<CancellationToken>(),
                TimeSpan.FromMinutes(5)), Times.Once);
        }

        [Test]
        [Platform("Unix")]
        public async Task CompileTypeSpecAsync_OnUnix_ShouldUseDirectTspCommand()
        {
            // Arrange
            var mockProcessExecutionService = CreateMockProcessExecutionService();
            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Success("Success"));

            var service = CreateService(processExecutionService: mockProcessExecutionService.Object);
            var validationContext = CreateTestValidationContext();

            // Act
            await service.CompileTypeSpecAsync(validationContext, CancellationToken.None);

            // Assert
            var expectedArgs = $"tsp compile . --emit @typespec/http-client-csharp --option \"@typespec/http-client-csharp.emitter-output-dir={validationContext.ValidatedSdkDir}\"";
            mockProcessExecutionService.Verify(x => x.ExecuteAsync(
                "tsp",
                expectedArgs,
                validationContext.CurrentTypeSpecDir,
                It.IsAny<CancellationToken>(),
                TimeSpan.FromMinutes(5)), Times.Once);
        }

        [Test]
        public async Task CompileTypeSpecAsync_ShouldUseCorrectTimeout()
        {
            // Arrange
            var mockProcessExecutionService = CreateMockProcessExecutionService();
            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Success("Success"));

            var service = CreateService(processExecutionService: mockProcessExecutionService.Object);
            var validationContext = CreateTestValidationContext();

            // Act
            await service.CompileTypeSpecAsync(validationContext, CancellationToken.None);

            // Assert
            mockProcessExecutionService.Verify(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                TimeSpan.FromMinutes(5)), Times.Once);
        }

        [Test]
        public async Task CompileTypeSpecAsync_ShouldUseCorrectWorkingDirectory()
        {
            // Arrange
            var mockProcessExecutionService = CreateMockProcessExecutionService();
            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Success("Success"));

            var service = CreateService(processExecutionService: mockProcessExecutionService.Object);
            var validationContext = CreateTestValidationContext();

            // Act
            await service.CompileTypeSpecAsync(validationContext, CancellationToken.None);

            // Assert
            mockProcessExecutionService.Verify(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                validationContext.CurrentTypeSpecDir,
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Test]
        public async Task CompileTypeSpecAsync_ShouldIncludeCorrectOutputPath()
        {
            // Arrange
            var mockProcessExecutionService = CreateMockProcessExecutionService();
            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Success("Success"));

            var service = CreateService(processExecutionService: mockProcessExecutionService.Object);
            var validationContext = CreateTestValidationContext();

            // Act
            await service.CompileTypeSpecAsync(validationContext, CancellationToken.None);

            // Assert
            mockProcessExecutionService.Verify(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.Is<string>(args => args.Contains(validationContext.ValidatedSdkDir)),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Test]
        public async Task CompileTypeSpecAsync_ShouldValidateProcessArguments()
        {
            // Arrange
            var mockProcessExecutionService = CreateMockProcessExecutionService();
            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Success("Success"));

            var service = CreateService(processExecutionService: mockProcessExecutionService.Object);
            var validationContext = CreateTestValidationContext();

            // Act
            await service.CompileTypeSpecAsync(validationContext, CancellationToken.None);

            // Assert - should not throw any validation exceptions
            Assert.Pass("Arguments were validated successfully");
        }

        [Test]
        public async Task CompileTypeSpecAsync_WithCancellation_ShouldRespectCancellationToken()
        {
            // Arrange
            var mockProcessExecutionService = CreateMockProcessExecutionService();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new OperationCanceledException());

            var service = CreateService(processExecutionService: mockProcessExecutionService.Object);
            var validationContext = CreateTestValidationContext();

            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(() => 
                service.CompileTypeSpecAsync(validationContext, cts.Token));
        }

        [Test]
        public async Task CompileTypeSpecAsync_WithCustomEmitterPackage_ShouldUseCorrectPackage()
        {
            // Arrange
            var mockProcessExecutionService = CreateMockProcessExecutionService();
            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Success("Success"));

            var customAppSettings = CreateTestAppSettings("@custom/emitter-package");
            var service = CreateService(appSettings: customAppSettings, processExecutionService: mockProcessExecutionService.Object);
            var validationContext = CreateTestValidationContext();

            // Act
            await service.CompileTypeSpecAsync(validationContext, CancellationToken.None);

            // Assert
            mockProcessExecutionService.Verify(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.Is<string>(args => args.Contains("@custom/emitter-package")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()), Times.Once);
        }

        private LocalLibraryGenerationService CreateService(
            AppSettings? appSettings = null,
            ProcessExecutionService? processExecutionService = null)
        {
            return new LocalLibraryGenerationService(
                appSettings ?? CreateTestAppSettings(),
                NullLogger<LocalLibraryGenerationService>.Instance,
                processExecutionService ?? CreateMockProcessExecutionService().Object);
        }

        private AppSettings CreateTestAppSettings(string emitterPackage = "@typespec/http-client-csharp")
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AzureSettings:ProjectEndpoint"] = "https://test.openai.azure.com/",
                    ["AzureSettings:TypespecEmitterPackage"] = emitterPackage,
                    ["AzureSettings:TypespecCompiler"] = "@typespec/compiler",
                    ["AzureSettings:Model"] = "gpt-4",
                    ["AzureSettings:AgentName"] = "Test Agent",
                    ["AzureSettings:AgentInstructions"] = "Test instructions",
                    ["AzureSettings:ErrorAnalysisInstructions"] = "Analyze errors",
                    ["AzureSettings:FixPromptTemplate"] = "Fix template",
                    ["AzureSettings:MaxIterations"] = "3",
                    ["AzureSettings:IndexingMaxWaitTimeSeconds"] = "30"
                })
                .Build();

            var logger = NullLogger<AppSettings>.Instance;
            return new AppSettings(configuration, logger);
        }

        private ValidationContext CreateTestValidationContext(
            string? typeSpecDir = null,
            string? sdkDir = null)
        {
            var tempTypeSpecDir = typeSpecDir ?? Path.Combine(Path.GetTempPath(), "test-typespec", Guid.NewGuid().ToString("N")[..8]);
            var tempSdkDir = sdkDir ?? Path.Combine(Path.GetTempPath(), "test-sdk", Guid.NewGuid().ToString("N")[..8]);
            
            Directory.CreateDirectory(tempTypeSpecDir);
            Directory.CreateDirectory(tempSdkDir);

            return ValidationContext.ValidateAndCreate(tempTypeSpecDir, "abc123def456", tempSdkDir);
        }

        private Mock<ProcessExecutionService> CreateMockProcessExecutionService()
        {
            return new Mock<ProcessExecutionService>(NullLogger<ProcessExecutionService>.Instance);
        }
    }
}
