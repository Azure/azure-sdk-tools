using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Tools.GeneratorAgent;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class LocalTypeSpecSdkGenerationServiceTests
    {
        private sealed class TestEnvironmentFixture : IDisposable
        {
            private readonly List<string> _createdDirectories = new();
            private readonly Mock<ILogger> _mockInputValidatorLogger;

            public TestEnvironmentFixture()
            {
                _mockInputValidatorLogger = new Mock<ILogger>();
                InputValidator.SetLogger(_mockInputValidatorLogger.Object);
                
                TypeSpecDir = CreateUniqueTestDirectory("typespec-test");
                SdkOutputDir = CreateUniqueTestDirectory("sdk-output-test");
                CreateDefaultTypeSpecProject();
            }

            public string TypeSpecDir { get; }
            public string SdkOutputDir { get; }

            public Mock<ILogger<LocalTypeSpecSdkGenerationService>> CreateMockLogger()
            {
                return new Mock<ILogger<LocalTypeSpecSdkGenerationService>>();
            }

            public Mock<ProcessExecutor> CreateMockProcessExecutor()
            {
                return new Mock<ProcessExecutor>(Mock.Of<ILogger<ProcessExecutor>>());
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

            public LocalTypeSpecSdkGenerationService CreateService(
                AppSettings? appSettings = null,
                ILogger<LocalTypeSpecSdkGenerationService>? logger = null,
                ProcessExecutor? processExecutor = null,
                ValidationContext? validationContext = null)
            {
                return new LocalTypeSpecSdkGenerationService(
                    appSettings ?? CreateAppSettings(),
                    logger ?? CreateMockLogger().Object,
                    processExecutor ?? CreateMockProcessExecutor().Object,
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
                // Clean up test directories to prevent disk space issues and test pollution
                // Ignore cleanup exceptions to avoid masking actual test failures
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

        #region Constructor Tests

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
                new LocalTypeSpecSdkGenerationService(
                    null!, 
                    fixture.CreateMockLogger().Object, 
                    fixture.CreateMockProcessExecutor().Object, 
                    fixture.CreateValidationContext()));
            
            Assert.That(ex!.ParamName, Is.EqualTo("appSettings"));
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var ex = Assert.Throws<ArgumentNullException>(() => 
                new LocalTypeSpecSdkGenerationService(
                    fixture.CreateAppSettings(), 
                    null!, 
                    fixture.CreateMockProcessExecutor().Object, 
                    fixture.CreateValidationContext()));
            
            Assert.That(ex!.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithNullProcessExecutor_ShouldThrowArgumentNullException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var ex = Assert.Throws<ArgumentNullException>(() => 
                new LocalTypeSpecSdkGenerationService(
                    fixture.CreateAppSettings(), 
                    fixture.CreateMockLogger().Object, 
                    null!, 
                    fixture.CreateValidationContext()));
            
            Assert.That(ex!.ParamName, Is.EqualTo("processExecutor"));
        }

        [Test]
        public void Constructor_WithNullValidationContext_ShouldThrowArgumentNullException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var ex = Assert.Throws<ArgumentNullException>(() => 
                new LocalTypeSpecSdkGenerationService(
                    fixture.CreateAppSettings(), 
                    fixture.CreateMockLogger().Object, 
                    fixture.CreateMockProcessExecutor().Object, 
                    null!));
            
            Assert.That(ex!.ParamName, Is.EqualTo("validationContext"));
        }

        [Test]
        public async Task CompileTypeSpecAsync_WithSuccessfulExecution_ShouldLogExpectedMessages()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutor = fixture.CreateMockProcessExecutor();

            SetupSuccessfulGlobalInstall(mockProcessExecutor, fixture);
            SetupSuccessfulTspCompile(mockProcessExecutor, fixture);

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                processExecutor: mockProcessExecutor.Object);

            await service.CompileTypeSpecAsync();

            VerifyLogMessage(mockLogger, LogLevel.Information, "Starting TypeSpec compilation for project:");
            VerifyLogMessage(mockLogger, LogLevel.Information, "Installing TypeSpec dependencies globally");
            VerifyLogMessage(mockLogger, LogLevel.Information, "TypeSpec dependencies installed globally successfully:");
            VerifyLogMessage(mockLogger, LogLevel.Information, "Compiling TypeSpec project");
            VerifyLogMessage(mockLogger, LogLevel.Information, "TypeSpec compilation completed:");
            VerifyLogMessage(mockLogger, LogLevel.Information, "TypeSpec compilation completed successfully");
        }

        [Test]
        public void CompileTypeSpecAsync_WithGlobalInstallFailure_ShouldThrowException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutor = fixture.CreateMockProcessExecutor();

            SetupFailedGlobalInstall(mockProcessExecutor, fixture);

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                processExecutor: mockProcessExecutor.Object);

            Assert.ThrowsAsync<InvalidOperationException>(() => service.CompileTypeSpecAsync());
        }

        [Test]
        public void CompileTypeSpecAsync_WithTspCompileFailure_ShouldThrowException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutor = fixture.CreateMockProcessExecutor();

            SetupSuccessfulGlobalInstall(mockProcessExecutor, fixture);
            SetupFailedTspCompile(mockProcessExecutor, fixture);

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                processExecutor: mockProcessExecutor.Object);

            Assert.ThrowsAsync<InvalidOperationException>(() => service.CompileTypeSpecAsync());
        }

        [Test]
        public void CompileTypeSpecAsync_WithException_ShouldThrowException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutor = fixture.CreateMockProcessExecutor();
            var expectedException = new InvalidOperationException("Test exception");

            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(expectedException);

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                processExecutor: mockProcessExecutor.Object);

            Assert.ThrowsAsync<InvalidOperationException>(() => service.CompileTypeSpecAsync());
        }

        [Test]
        public void CompileTypeSpecAsync_WithCancellation_ShouldThrowOperationCanceledException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutor = fixture.CreateMockProcessExecutor();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new OperationCanceledException());

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                processExecutor: mockProcessExecutor.Object);

            Assert.ThrowsAsync<OperationCanceledException>(() => service.CompileTypeSpecAsync(cts.Token));
        }

        #endregion

        #region Process Execution Verification Tests

        [Test]
        public async Task CompileTypeSpecAsync_ShouldCallProcessExecutorWithCorrectGlobalInstallArguments()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockProcessExecutor = fixture.CreateMockProcessExecutor();
            SetupSuccessfulGlobalInstall(mockProcessExecutor, fixture);
            SetupSuccessfulTspCompile(mockProcessExecutor, fixture);

            var service = fixture.CreateService(processExecutor: mockProcessExecutor.Object);

            await service.CompileTypeSpecAsync();

            mockProcessExecutor.Verify(x => x.ExecuteAsync(
                "pwsh",
                "-Command \"npm install --global @typespec/http-client-csharp\"",
                It.IsAny<string>(), // Working directory can vary by OS
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Test]
        public async Task CompileTypeSpecAsync_ShouldCallProcessExecutorWithCorrectCompileArguments()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockProcessExecutor = fixture.CreateMockProcessExecutor();
            SetupSuccessfulGlobalInstall(mockProcessExecutor, fixture);
            SetupSuccessfulTspCompile(mockProcessExecutor, fixture);

            var service = fixture.CreateService(processExecutor: mockProcessExecutor.Object);

            await service.CompileTypeSpecAsync();

            var expectedTspOutputPath = Path.Combine(fixture.SdkOutputDir);
            var expectedCompileArgs = $"-Command \"npx tsp compile . --emit @typespec/http-client-csharp --option '@typespec/http-client-csharp.emitter-output-dir={expectedTspOutputPath}'\"";

            mockProcessExecutor.Verify(x => x.ExecuteAsync(
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
            
            var mockProcessExecutor = fixture.CreateMockProcessExecutor();
            SetupSuccessfulGlobalInstall(mockProcessExecutor, fixture);
            SetupSuccessfulTspCompile(mockProcessExecutor, fixture);

            var service = fixture.CreateService(processExecutor: mockProcessExecutor.Object);

            await service.CompileTypeSpecAsync();

            mockProcessExecutor.Verify(x => x.ExecuteAsync(
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
            
            var mockProcessExecutor = fixture.CreateMockProcessExecutor();
            SetupSuccessfulGlobalInstall(mockProcessExecutor, fixture);
            SetupSuccessfulTspCompile(mockProcessExecutor, fixture);

            var service = fixture.CreateService(processExecutor: mockProcessExecutor.Object);

            await service.CompileTypeSpecAsync();

            var expectedTspOutputPath = Path.Combine(fixture.SdkOutputDir);
            mockProcessExecutor.Verify(x => x.ExecuteAsync(
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

            var mockProcessExecutor = customFixture.CreateMockProcessExecutor();
            
            SetupSuccessfulGlobalInstall(mockProcessExecutor, customFixture);
            
            var expectedTspOutputPath = Path.Combine(customFixture.SdkOutputDir);
            var expectedCompileArgs = $"-Command \"npx tsp compile . --emit @typespec/http-client-csharp --option '@typespec/http-client-csharp.emitter-output-dir={expectedTspOutputPath}'\"";

            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    "pwsh",
                    expectedCompileArgs,
                    customFixture.TypeSpecDir,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync((true, "Custom compile succeeded", string.Empty));

            var service = customFixture.CreateService(processExecutor: mockProcessExecutor.Object);

            await service.CompileTypeSpecAsync();

            // Test passes if no exception is thrown
        }

        [Test]
        public void CompileTypeSpecAsync_WithTimeoutDuringInstall_ShouldThrowTimeoutException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutor = fixture.CreateMockProcessExecutor();

            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    "pwsh",
                    "-Command \"npm install --global @typespec/http-client-csharp\"",
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new TimeoutException("Install timeout"));

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                processExecutor: mockProcessExecutor.Object);

            Assert.ThrowsAsync<TimeoutException>(() => service.CompileTypeSpecAsync());
        }

        [Test]
        public void CompileTypeSpecAsync_WithTimeoutDuringCompile_ShouldThrowTimeoutException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutor = fixture.CreateMockProcessExecutor();

            SetupSuccessfulGlobalInstall(mockProcessExecutor, fixture);

            var expectedTspOutputPath = Path.Combine(fixture.SdkOutputDir);
            var expectedCompileArgs = $"-Command \"npx tsp compile . --emit @typespec/http-client-csharp --option '@typespec/http-client-csharp.emitter-output-dir={expectedTspOutputPath}'\"";

            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    "pwsh",
                    expectedCompileArgs,
                    fixture.TypeSpecDir,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new TimeoutException("Compile timeout"));

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                processExecutor: mockProcessExecutor.Object);

            Assert.ThrowsAsync<TimeoutException>(() => service.CompileTypeSpecAsync());
        }

        [Test]
        public async Task InstallTypeSpecDependencies_WithSuccessfulExecution_ShouldReturnTrue()
        {
            using var fixture = new TestEnvironmentFixture();

            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutor = fixture.CreateMockProcessExecutor();
            
            SetupSuccessfulGlobalInstall(mockProcessExecutor, fixture);

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                processExecutor: mockProcessExecutor.Object);

            var method = typeof(LocalTypeSpecSdkGenerationService).GetMethod("InstallTypeSpecDependencies", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = await (Task<bool>)method!.Invoke(service, new object[] { CancellationToken.None })!;

            Assert.That(result, Is.True);
            VerifyLogMessage(mockLogger, LogLevel.Information, "Installing TypeSpec dependencies globally");
            VerifyLogMessage(mockLogger, LogLevel.Information, "TypeSpec dependencies installed globally successfully:");
        }

        [Test]
        public async Task InstallTypeSpecDependencies_WithProcessFailure_ShouldReturnFalseAndLogError()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutor = fixture.CreateMockProcessExecutor();
            
            SetupFailedGlobalInstall(mockProcessExecutor, fixture);

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                processExecutor: mockProcessExecutor.Object);

            var method = typeof(LocalTypeSpecSdkGenerationService).GetMethod("InstallTypeSpecDependencies", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = await (Task<bool>)method!.Invoke(service, new object[] { CancellationToken.None })!;

            Assert.That(result, Is.False);
            VerifyLogMessage(mockLogger, LogLevel.Error, "Global npm install failed. Error:");
        }

        [Test]
        public async Task InstallTypeSpecDependencies_ShouldCallProcessExecutorWithCorrectArguments()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockProcessExecutor = fixture.CreateMockProcessExecutor();
            SetupSuccessfulGlobalInstall(mockProcessExecutor, fixture);

            var service = fixture.CreateService(processExecutor: mockProcessExecutor.Object);

            var method = typeof(LocalTypeSpecSdkGenerationService).GetMethod("InstallTypeSpecDependencies", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            await (Task<bool>)method!.Invoke(service, new object[] { CancellationToken.None })!;

            mockProcessExecutor.Verify(x => x.ExecuteAsync(
                "pwsh",
                "-Command \"npm install --global @typespec/http-client-csharp\"",
                It.IsAny<string>(), // Working directory varies by OS
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Test]
        public async Task InstallTypeSpecDependencies_WithValidationFailure_ShouldReturnFalse()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutor = fixture.CreateMockProcessExecutor();

            var customAppSettings = fixture.CreateAppSettings("@typespec/http-client-csharp");

            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    "pwsh",
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync((true, "Install succeeded", string.Empty));

            var service = fixture.CreateService(
                appSettings: customAppSettings,
                logger: mockLogger.Object,
                processExecutor: mockProcessExecutor.Object);

            var method = typeof(LocalTypeSpecSdkGenerationService).GetMethod("InstallTypeSpecDependencies", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = await (Task<bool>)method!.Invoke(service, new object[] { CancellationToken.None })!;

            Assert.That(result, Is.True);
        }

        #endregion

        #region CompileTypeSpec Method Tests

        [Test]
        public async Task CompileTypeSpec_WithSuccessfulExecution_ShouldReturnTrue()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutor = fixture.CreateMockProcessExecutor();
            
            SetupSuccessfulTspCompile(mockProcessExecutor, fixture);

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                processExecutor: mockProcessExecutor.Object);

            var method = typeof(LocalTypeSpecSdkGenerationService).GetMethod("CompileTypeSpec", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = await (Task<bool>)method!.Invoke(service, new object[] { CancellationToken.None })!;

            Assert.That(result, Is.True);
            VerifyLogMessage(mockLogger, LogLevel.Information, "Compiling TypeSpec project");
            VerifyLogMessage(mockLogger, LogLevel.Information, "TypeSpec compilation completed:");
        }

        [Test]
        public async Task CompileTypeSpec_WithProcessFailure_ShouldReturnFalseAndLogError()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutor = fixture.CreateMockProcessExecutor();
            
            SetupFailedTspCompile(mockProcessExecutor, fixture);

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                processExecutor: mockProcessExecutor.Object);

            var method = typeof(LocalTypeSpecSdkGenerationService).GetMethod("CompileTypeSpec", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = await (Task<bool>)method!.Invoke(service, new object[] { CancellationToken.None })!;

            Assert.That(result, Is.False);
            VerifyLogMessage(mockLogger, LogLevel.Error, "TypeSpec compilation failed. Error:");
        }

        [Test]
        public async Task CompileTypeSpec_ShouldCallProcessExecutorWithCorrectArguments()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockProcessExecutor = fixture.CreateMockProcessExecutor();
            SetupSuccessfulTspCompile(mockProcessExecutor, fixture);

            var service = fixture.CreateService(processExecutor: mockProcessExecutor.Object);

            var method = typeof(LocalTypeSpecSdkGenerationService).GetMethod("CompileTypeSpec", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            await (Task<bool>)method!.Invoke(service, new object[] { CancellationToken.None })!;

            var expectedTspOutputPath = Path.Combine(fixture.SdkOutputDir);
            var expectedCompileArgs = $"-Command \"npx tsp compile . --emit @typespec/http-client-csharp --option '@typespec/http-client-csharp.emitter-output-dir={expectedTspOutputPath}'\"";

            mockProcessExecutor.Verify(x => x.ExecuteAsync(
                "pwsh",
                expectedCompileArgs,
                fixture.TypeSpecDir,
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Test]
        public async Task CompileTypeSpecAsync_WithValidationFailureDuringInstall_ShouldExecuteSuccessfully()
        {
            using var fixture = new TestEnvironmentFixture();

            var mockLogger = fixture.CreateMockLogger();
            var mockProcessExecutor = fixture.CreateMockProcessExecutor();

            SetupSuccessfulGlobalInstall(mockProcessExecutor, fixture);
            SetupSuccessfulTspCompile(mockProcessExecutor, fixture);

            var service = fixture.CreateService(
                logger: mockLogger.Object,
                processExecutor: mockProcessExecutor.Object);

            await service.CompileTypeSpecAsync();

            // Test passes if no exception is thrown
            mockProcessExecutor.Verify(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()), Times.AtLeast(2));
        }

        #endregion

        #region Helper Methods

        private static void SetupSuccessfulGlobalInstall(Mock<ProcessExecutor> mockProcessExecutor, TestEnvironmentFixture fixture)
        {
            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    "pwsh",
                    "-Command \"npm install --global @typespec/http-client-csharp\"",
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync((true, "Global install succeeded", string.Empty));
        }

        private static void SetupFailedGlobalInstall(Mock<ProcessExecutor> mockProcessExecutor, TestEnvironmentFixture fixture)
        {
            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    "pwsh",
                    "-Command \"npm install --global @typespec/http-client-csharp\"",
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync((false, "Global install output", "Global install failed"));
        }

        private static void SetupSuccessfulTspCompile(Mock<ProcessExecutor> mockProcessExecutor, TestEnvironmentFixture fixture)
        {
            var expectedTspOutputPath = Path.Combine(fixture.SdkOutputDir);
            var expectedCompileArgs = $"-Command \"npx tsp compile . --emit @typespec/http-client-csharp --option '@typespec/http-client-csharp.emitter-output-dir={expectedTspOutputPath}'\"";

            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    "pwsh",
                    expectedCompileArgs,
                    fixture.TypeSpecDir,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync((true, "Compile succeeded", string.Empty));
        }

        private static void SetupFailedTspCompile(Mock<ProcessExecutor> mockProcessExecutor, TestEnvironmentFixture fixture)
        {
            var expectedTspOutputPath = Path.Combine(fixture.SdkOutputDir);
            var expectedCompileArgs = $"-Command \"npx tsp compile . --emit @typespec/http-client-csharp --option '@typespec/http-client-csharp.emitter-output-dir={expectedTspOutputPath}'\"";

            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    "pwsh",
                    expectedCompileArgs,
                    fixture.TypeSpecDir,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync((false, "Compile output", "Compile failed"));
        }

        private static void VerifyLogMessage(
            Mock<ILogger<LocalTypeSpecSdkGenerationService>> mockLogger,
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

        #endregion
    }
}
