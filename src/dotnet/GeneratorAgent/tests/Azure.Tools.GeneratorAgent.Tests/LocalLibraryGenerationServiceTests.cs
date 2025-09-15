using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Tools.GeneratorAgent;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Security;
using Azure.Tools.GeneratorAgent.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class LocalLibraryGenerationServiceTests
    {
        private sealed class TestEnvironmentFixture : IDisposable
        {
            private readonly List<string> _createdDirectories = new();
            private readonly Mock<ILogger> _mockInputValidatorLogger;

            public TestEnvironmentFixture()
            {
                _mockInputValidatorLogger = new Mock<ILogger>();
                
                TypeSpecDir = CreateUniqueTestDirectory("typespec-test");
                SdkOutputDir = CreateUniqueTestDirectory("sdk-output-test");
                CreateDefaultTypeSpecProject();
            }

            public string TypeSpecDir { get; }
            public string SdkOutputDir { get; }

            public Mock<ILogger<LocalLibraryGenerationService>> CreateMockLogger()
            {
                return new Mock<ILogger<LocalLibraryGenerationService>>();
            }

            public Mock<ProcessExecutionService> CreateMockProcessExecutionService()
            {
                return new Mock<ProcessExecutionService>(Mock.Of<ILogger<ProcessExecutionService>>());
            }

            public AppSettings CreateAppSettings(
                string emitterPackage = "@typespec/http-client-csharp")
            {
                var configMock = new Mock<IConfiguration>();
                var mockLogger = new Mock<ILogger<AppSettings>>();
                
                var projectEndpointSection = new Mock<IConfigurationSection>();
                projectEndpointSection.Setup(s => s.Value).Returns("https://test.openai.azure.com/");
                configMock.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(projectEndpointSection.Object);

                var emitterSection = new Mock<IConfigurationSection>();
                emitterSection.Setup(s => s.Value).Returns(emitterPackage);
                configMock.Setup(c => c.GetSection("AzureSettings:TypespecEmitterPackage")).Returns(emitterSection.Object);

                var defaultSection = new Mock<IConfigurationSection>();
                defaultSection.Setup(s => s.Value).Returns((string?)null);
                configMock.Setup(c => c.GetSection(It.IsAny<string>())).Returns(defaultSection.Object);

                return new AppSettings(configMock.Object, mockLogger.Object);
            }

            public ValidationContext CreateValidationContext(
                string? typeSpecDir = null,
                string commitId = "local",
                string? sdkDir = null)
            {
                return ValidationContext.CreateFromValidatedInputs(
                    typeSpecDir ?? TypeSpecDir, 
                    commitId, 
                    sdkDir ?? SdkOutputDir);
            }

            public LocalLibraryGenerationService CreateService(
                AppSettings? appSettings = null,
                ILogger<LocalLibraryGenerationService>? logger = null,
                ProcessExecutionService? ProcessExecutionService = null,
                ValidationContext? validationContext = null)
            {
                return new LocalLibraryGenerationService(
                    appSettings ?? CreateAppSettings(),
                    logger ?? CreateMockLogger().Object,
                    ProcessExecutionService ?? CreateMockProcessExecutionService().Object,
                    validationContext ?? CreateValidationContext());
            }

            private string CreateUniqueTestDirectory(string baseName)
            {
                var uniqueId = Guid.NewGuid().ToString("N")[..8];
                var directory = Path.Combine(Path.GetTempPath(), $"{baseName}-{uniqueId}");
                Directory.CreateDirectory(directory);
                _createdDirectories.Add(directory);
                return directory;
            }

            private void CreateDefaultTypeSpecProject()
            {
                var packageJsonPath = Path.Combine(TypeSpecDir, "package.json");
                File.WriteAllText(packageJsonPath, @"{
                    ""name"": ""test-typespec"",
                    ""version"": ""1.0.0"",
                    ""devDependencies"": {
                        ""@typespec/compiler"": ""latest"",
                        ""@typespec/http-client-csharp"": ""latest""
                    }
                    }");
                
                var mainTspPath = Path.Combine(TypeSpecDir, "main.tsp");
                File.WriteAllText(mainTspPath, @"import ""@typespec/http-client-csharp"";

                    @service({
                    title: ""Test Service"",
                    })
                    namespace TestService;
                    ");

                var tspConfigPath = Path.Combine(TypeSpecDir, "tspconfig.yaml");
                File.WriteAllText(tspConfigPath, @"emit:
  - ""@typespec/http-client-csharp""
");
            }

            public void Dispose()
            {
                foreach (var directory in _createdDirectories)
                {
                    if (Directory.Exists(directory))
                    {
                        try
                        {
                            Directory.Delete(directory, true);
                        }
                        catch
                        {
                            // Intentionally ignored - cleanup failures shouldn't fail tests
                        }
                    }
                }
            }
        }


        [Test]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            using var fixture = new TestEnvironmentFixture();
            
            Assert.DoesNotThrow(() => fixture.CreateService());
        }

        [Test]
        public void Constructor_WithNullAppSettings_ShouldThrowArgumentNullException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var ex = Assert.Throws<ArgumentNullException>(() => 
                new LocalLibraryGenerationService(
                    null!, 
                    fixture.CreateMockLogger().Object, 
                    fixture.CreateMockProcessExecutionService().Object, 
                    fixture.CreateValidationContext()));
            
            Assert.That(ex!.ParamName, Is.EqualTo("appSettings"));
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var ex = Assert.Throws<ArgumentNullException>(() => 
                new LocalLibraryGenerationService(
                    fixture.CreateAppSettings(), 
                    null!, 
                    fixture.CreateMockProcessExecutionService().Object, 
                    fixture.CreateValidationContext()));
            
            Assert.That(ex!.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithNullProcessExecutionService_ShouldThrowArgumentNullException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var ex = Assert.Throws<ArgumentNullException>(() => 
                new LocalLibraryGenerationService(
                    fixture.CreateAppSettings(), 
                    fixture.CreateMockLogger().Object, 
                    null!, 
                    fixture.CreateValidationContext()));
            
            Assert.That(ex!.ParamName, Is.EqualTo("processExecutionService"));
        }

        [Test]
        public void Constructor_WithNullValidationContext_ShouldThrowArgumentNullException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var ex = Assert.Throws<ArgumentNullException>(() => 
                new LocalLibraryGenerationService(
                    fixture.CreateAppSettings(), 
                    fixture.CreateMockLogger().Object, 
                    fixture.CreateMockProcessExecutionService().Object, 
                    null!));
            
            Assert.That(ex!.ParamName, Is.EqualTo("validationContext"));
        }

        [Test]
        public async Task CompileTypeSpecAsync_WithSuccessfulExecution_ShouldLogExpectedMessages()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutionService = fixture.CreateMockProcessExecutionService();

            SetupSuccessfulGlobalInstall(mockProcessExecutionService, fixture);
            SetupSuccessfulTspCompile(mockProcessExecutionService, fixture);

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                ProcessExecutionService: mockProcessExecutionService.Object);

            var result = await service.CompileTypeSpecAsync();

            Assert.That(result.IsSuccess, Is.True);
            VerifyLogMessage(mockLogger, LogLevel.Information, "Starting TypeSpec compilation for project:");
            VerifyLogMessage(mockLogger, LogLevel.Information, "Installing TypeSpec dependencies globally");
            VerifyLogMessage(mockLogger, LogLevel.Information, "Compiling TypeSpec project");
            VerifyLogMessage(mockLogger, LogLevel.Information, "TypeSpec compilation completed successfully");
        }

        [Test]
        public async Task CompileTypeSpecAsync_WithGlobalInstallFailure_ShouldReturnFailure()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutionService = fixture.CreateMockProcessExecutionService();

            SetupFailedGlobalInstall(mockProcessExecutionService, fixture);

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                ProcessExecutionService: mockProcessExecutionService.Object);

            var result = await service.CompileTypeSpecAsync();
            
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Exception, Is.Not.Null);
        }

        [Test]
        public async Task CompileTypeSpecAsync_WithTspCompileFailure_ShouldReturnFailure()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutionService = fixture.CreateMockProcessExecutionService();

            SetupSuccessfulGlobalInstall(mockProcessExecutionService, fixture);
            SetupFailedTspCompile(mockProcessExecutionService, fixture);

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                ProcessExecutionService: mockProcessExecutionService.Object);

            var result = await service.CompileTypeSpecAsync();
            
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Exception, Is.Not.Null);
        }

        [Test]
        public void CompileTypeSpecAsync_WithException_ShouldThrowException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutionService = fixture.CreateMockProcessExecutionService();
            var expectedException = new InvalidOperationException("Test exception");

            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(expectedException);

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                ProcessExecutionService: mockProcessExecutionService.Object);

            var ex = Assert.ThrowsAsync<InvalidOperationException>(() => service.CompileTypeSpecAsync());
            
            Assert.That(ex!.Message, Is.EqualTo("Test exception"));
        }

        [Test]
        public void CompileTypeSpecAsync_WithCancellation_ShouldThrowOperationCanceledException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutionService = fixture.CreateMockProcessExecutionService();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new OperationCanceledException());

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                ProcessExecutionService: mockProcessExecutionService.Object);

            Assert.ThrowsAsync<OperationCanceledException>(() => service.CompileTypeSpecAsync(cts.Token));
        }

        [Test]
        public async Task CompileTypeSpecAsync_ShouldCallProcessExecutionServiceWithCorrectGlobalInstallArguments()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockProcessExecutionService = fixture.CreateMockProcessExecutionService();
            SetupSuccessfulGlobalInstall(mockProcessExecutionService, fixture);
            SetupSuccessfulTspCompile(mockProcessExecutionService, fixture);

            var service = fixture.CreateService(ProcessExecutionService: mockProcessExecutionService.Object);

            await service.CompileTypeSpecAsync();

            mockProcessExecutionService.Verify(x => x.ExecuteAsync(
                "pwsh",
                "-Command \"npm install --global @typespec/http-client-csharp\"",
                It.IsAny<string>(), // Working directory can vary by OS
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Test]
        public async Task CompileTypeSpecAsync_ShouldCallProcessExecutionServiceWithCorrectCompileArguments()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockProcessExecutionService = fixture.CreateMockProcessExecutionService();
            SetupSuccessfulGlobalInstall(mockProcessExecutionService, fixture);
            SetupSuccessfulTspCompile(mockProcessExecutionService, fixture);

            var service = fixture.CreateService(ProcessExecutionService: mockProcessExecutionService.Object);

            await service.CompileTypeSpecAsync();

            var expectedTspOutputPath = Path.Combine(fixture.SdkOutputDir);
            var expectedCompileArgs = $"-Command \"npx tsp compile . --emit @typespec/http-client-csharp --option '@typespec/http-client-csharp.emitter-output-dir={expectedTspOutputPath}'\"";

            mockProcessExecutionService.Verify(x => x.ExecuteAsync(
                "pwsh",
                expectedCompileArgs,
                fixture.TypeSpecDir,
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Test]
        public async Task CompileTypeSpecAsync_OnWindows_ShouldUsePowerShellExecutor()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockProcessExecutionService = fixture.CreateMockProcessExecutionService();
            SetupSuccessfulGlobalInstall(mockProcessExecutionService, fixture);
            SetupSuccessfulTspCompile(mockProcessExecutionService, fixture);

            var service = fixture.CreateService(ProcessExecutionService: mockProcessExecutionService.Object);

            await service.CompileTypeSpecAsync();

            mockProcessExecutionService.Verify(x => x.ExecuteAsync(
                "pwsh",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()), Times.AtLeast(2));
        }

        [Test]
        public async Task CompileTypeSpecAsync_ShouldCreateCorrectTspOutputPath()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockProcessExecutionService = fixture.CreateMockProcessExecutionService();
            SetupSuccessfulGlobalInstall(mockProcessExecutionService, fixture);
            SetupSuccessfulTspCompile(mockProcessExecutionService, fixture);

            var service = fixture.CreateService(ProcessExecutionService: mockProcessExecutionService.Object);

            await service.CompileTypeSpecAsync();

            var expectedTspOutputPath = Path.Combine(fixture.SdkOutputDir);
            mockProcessExecutionService.Verify(x => x.ExecuteAsync(
                "pwsh",
                It.Is<string>(args => args.Contains(expectedTspOutputPath)),
                fixture.TypeSpecDir,
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Test]
        public async Task CompileTypeSpecAsync_WithDifferentWorkingDirectories_ShouldWork()
        {
            using var customFixture = new TestEnvironmentFixture();

            var mockProcessExecutionService = customFixture.CreateMockProcessExecutionService();
            
            SetupSuccessfulGlobalInstall(mockProcessExecutionService, customFixture);
            
            var expectedTspOutputPath = Path.Combine(customFixture.SdkOutputDir);
            var expectedCompileArgs = $"-Command \"npx tsp compile . --emit @typespec/http-client-csharp --option '@typespec/http-client-csharp.emitter-output-dir={expectedTspOutputPath}'\"";

            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    "pwsh",
                    expectedCompileArgs,
                    customFixture.TypeSpecDir,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Success("Custom compile succeeded"));

            var service = customFixture.CreateService(ProcessExecutionService: mockProcessExecutionService.Object);

            await service.CompileTypeSpecAsync();

            // Test passes if no exception is thrown
        }

        [Test]
        public void CompileTypeSpecAsync_WithTimeoutDuringInstall_ShouldThrowTimeoutException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutionService = fixture.CreateMockProcessExecutionService();

            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    "pwsh",
                    "-Command \"npm install --global @typespec/http-client-csharp\"",
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new TimeoutException("Install timeout"));

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                ProcessExecutionService: mockProcessExecutionService.Object);

            TimeoutException caughtException = Assert.ThrowsAsync<TimeoutException>(() => service.CompileTypeSpecAsync())!;
            Assert.That(caughtException.Message, Does.Contain("Install timeout"));
        }

        [Test]
        public void CompileTypeSpecAsync_WithTimeoutDuringCompile_ShouldThrowTimeoutException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutionService = fixture.CreateMockProcessExecutionService();

            SetupSuccessfulGlobalInstall(mockProcessExecutionService, fixture);

            var expectedTspOutputPath = Path.Combine(fixture.SdkOutputDir);
            var expectedCompileArgs = $"-Command \"npx tsp compile . --emit @typespec/http-client-csharp --option '@typespec/http-client-csharp.emitter-output-dir={expectedTspOutputPath}'\"";

            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    "pwsh",
                    expectedCompileArgs,
                    fixture.TypeSpecDir,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new TimeoutException("Compile timeout"));

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                ProcessExecutionService: mockProcessExecutionService.Object);

            TimeoutException caughtException = Assert.ThrowsAsync<TimeoutException>(() => service.CompileTypeSpecAsync())!;
            Assert.That(caughtException.Message, Does.Contain("Compile timeout"));
        }

        [Test]
        public async Task CompileTypeSpecAsync_WithSuccessfulGlobalInstall_ShouldLogSuccess()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutionService = fixture.CreateMockProcessExecutionService();
            
            SetupSuccessfulGlobalInstall(mockProcessExecutionService, fixture);
            SetupSuccessfulTspCompile(mockProcessExecutionService, fixture);

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                ProcessExecutionService: mockProcessExecutionService.Object);

            await service.CompileTypeSpecAsync(CancellationToken.None);

            VerifyLogMessage(mockLogger, LogLevel.Information, "Installing TypeSpec dependencies globally");
            VerifyLogMessage(mockLogger, LogLevel.Information, "Compiling TypeSpec project");
        }

        [Test]
        public async Task CompileTypeSpecAsync_WithGlobalInstallFailure_ShouldReturnFailure2()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutionService = fixture.CreateMockProcessExecutionService();
            
            SetupFailedGlobalInstall(mockProcessExecutionService, fixture);

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                ProcessExecutionService: mockProcessExecutionService.Object);

            var result = await service.CompileTypeSpecAsync(CancellationToken.None);
            
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Exception, Is.Not.Null);
        }

        [Test]
        public async Task CompileTypeSpecAsync_WithValidationFailureDuringInstall_ShouldExecuteSuccessfully()
        {
            using var fixture = new TestEnvironmentFixture();

            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutionService = fixture.CreateMockProcessExecutionService();

            SetupSuccessfulGlobalInstall(mockProcessExecutionService, fixture);
            SetupSuccessfulTspCompile(mockProcessExecutionService, fixture);

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                ProcessExecutionService: mockProcessExecutionService.Object);

            await service.CompileTypeSpecAsync();

            // Test passes if no exception is thrown
            mockProcessExecutionService.Verify(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()), Times.AtLeast(2));
        }

        private static void SetupSuccessfulGlobalInstall(Mock<ProcessExecutionService> mockProcessExecutionService, TestEnvironmentFixture fixture)
        {
            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    "pwsh",
                    "-Command \"npm install --global @typespec/http-client-csharp\"",
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Success("Global install succeeded"));
        }

        private static void SetupFailedGlobalInstall(Mock<ProcessExecutionService> mockProcessExecutionService, TestEnvironmentFixture fixture)
        {
            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    "pwsh",
                    "-Command \"npm install --global @typespec/http-client-csharp\"",
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Failure(new InvalidOperationException("Global install failed")));
        }

        private static void SetupSuccessfulTspCompile(Mock<ProcessExecutionService> mockProcessExecutionService, TestEnvironmentFixture fixture)
        {
            var expectedTspOutputPath = Path.Combine(fixture.SdkOutputDir);
            var expectedCompileArgs = $"-Command \"npx tsp compile . --emit @typespec/http-client-csharp --option '@typespec/http-client-csharp.emitter-output-dir={expectedTspOutputPath}'\"";

            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    "pwsh",
                    expectedCompileArgs,
                    fixture.TypeSpecDir,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Success("Compile succeeded"));
        }

        private static void SetupFailedTspCompile(Mock<ProcessExecutionService> mockProcessExecutionService, TestEnvironmentFixture fixture)
        {
            var expectedTspOutputPath = Path.Combine(fixture.SdkOutputDir);
            var expectedCompileArgs = $"-Command \"npx tsp compile . --emit @typespec/http-client-csharp --option '@typespec/http-client-csharp.emitter-output-dir={expectedTspOutputPath}'\"";

            mockProcessExecutionService.Setup(x => x.ExecuteAsync(
                    "pwsh",
                    expectedCompileArgs,
                    fixture.TypeSpecDir,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Failure(new InvalidOperationException("Compile failed")));
        }

        private static void VerifyLogMessage(
            Mock<ILogger<LocalLibraryGenerationService>> mockLogger,
            LogLevel expectedLevel,
            string expectedMessage)
        {
            mockLogger.Verify(
                x => x.Log(
                    expectedLevel,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce,
                $"Expected {expectedLevel} log containing: {expectedMessage}");
        }
    }
}
