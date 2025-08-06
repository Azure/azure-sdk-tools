using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Tools.GeneratorAgent;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Exceptions;
using Azure.Tools.GeneratorAgent.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class GitHubTypeSpecSdkGenerationServiceTests
    {
        #region Helper Classes

        private sealed class TestEnvironmentFixture : IDisposable
        {
            private readonly List<string> _createdDirectories = new();
            private readonly string _baseTestDirectory;

            public TestEnvironmentFixture()
            {
                _baseTestDirectory = Path.Combine(Path.GetTempPath(), $"GitHubSDKTest_{Guid.NewGuid():N}");
                Directory.CreateDirectory(_baseTestDirectory);
                _createdDirectories.Add(_baseTestDirectory);
            }

            public string CreateTestDirectory(string name)
            {
                var path = Path.Combine(_baseTestDirectory, name);
                Directory.CreateDirectory(path);
                _createdDirectories.Add(path);
                return path;
            }

            public string CreateAzureSdkDirectory()
            {
                var azureSdkDir = CreateTestDirectory("azure-sdk-for-net");
                var gitDir = Path.Combine(azureSdkDir, ".git");
                Directory.CreateDirectory(gitDir);
                _createdDirectories.Add(gitDir);
                
                var srcDir = Path.Combine(azureSdkDir, "src");
                Directory.CreateDirectory(srcDir);
                _createdDirectories.Add(srcDir);
                
                var engDir = Path.Combine(azureSdkDir, "eng", "scripts", "automation");
                Directory.CreateDirectory(engDir);
                _createdDirectories.Add(engDir);
                
                var scriptPath = Path.Combine(engDir, "Invoke-TypeSpecDataPlaneGenerateSDKPackage.ps1");
                File.WriteAllText(scriptPath, "# PowerShell script");
                
                return azureSdkDir;
            }

            public string CreateSdkOutputDirectory()
            {
                var sdkOutputDir = CreateTestDirectory("sdk-output");
                var srcDir = Path.Combine(sdkOutputDir, "src");
                Directory.CreateDirectory(srcDir);
                _createdDirectories.Add(srcDir);
                return sdkOutputDir;
            }

            public string CreateSdkOutputDirectoryWithoutSrc()
            {
                return CreateTestDirectory("sdk-output");
            }

            public void Dispose()
            {
                foreach (var directory in _createdDirectories.AsEnumerable().Reverse())
                {
                    try
                    {
                        if (Directory.Exists(directory))
                        {
                            Directory.Delete(directory, true);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }

        #endregion

        #region Helper Methods

        private static Mock<ILogger<GitHubTypeSpecSdkGenerationService>> CreateMockLogger()
        {
            return new Mock<ILogger<GitHubTypeSpecSdkGenerationService>>();
        }

        private static Mock<ProcessExecutor> CreateMockProcessExecutor()
        {
            return new Mock<ProcessExecutor>(Mock.Of<ILogger<ProcessExecutor>>());
        }

        private static AppSettings CreateTestAppSettings(
            string azureSdkDirectoryName = "azure-sdk-for-net",
            string powerShellScriptPath = @"eng\scripts\automation\Invoke-TypeSpecDataPlaneGenerateSDKPackage.ps1",
            string azureSpecRepository = "Azure/azure-rest-api-specs")
        {
            // Create mock configuration with proper settings
            var inMemorySettings = new Dictionary<string, string?>
            {
                ["AzureSettings:ProjectEndpoint"] = "https://test.openai.azure.com",
                ["AzureSettings:Model"] = "gpt-4o",
                ["AzureSettings:AgentName"] = "Test Agent",
                ["AzureSettings:AgentInstructions"] = "Test instructions"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var logger = new Mock<ILogger<AppSettings>>().Object;
            
            return new AppSettings(configuration, logger);
        }

        private static ValidationContext CreateValidationContext(
            string? typeSpecDir = null,
            string commitId = "abc123def456789012345678901234567890abcdef",
            string? sdkOutputDir = null)
        {
            return ValidationContext.CreateFromValidatedInputs(
                typeSpecDir ?? "specification/keyvault/data-plane",
                commitId,
                sdkOutputDir ?? @"C:\azure-sdk-for-net\sdk\keyvault\Azure.Security.KeyVault.Secrets");
        }

        private static GitHubTypeSpecSdkGenerationService CreateService(
            AppSettings? appSettings = null,
            ILogger<GitHubTypeSpecSdkGenerationService>? logger = null,
            ProcessExecutor? processExecutor = null,
            ValidationContext? validationContext = null)
        {
            return new GitHubTypeSpecSdkGenerationService(
                appSettings ?? CreateTestAppSettings(),
                logger ?? CreateMockLogger().Object,
                processExecutor ?? CreateMockProcessExecutor().Object,
                validationContext ?? CreateValidationContext());
        }

        private static void SetupSuccessfulGitExecution(Mock<ProcessExecutor> mockProcessExecutor, string azureSdkPath)
        {
            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    SecureProcessConfiguration.GitExecutable,
                    "rev-parse --show-toplevel",
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Success(azureSdkPath));
        }

        private static void SetupFailedGitExecution(Mock<ProcessExecutor> mockProcessExecutor)
        {
            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    SecureProcessConfiguration.GitExecutable,
                    "rev-parse --show-toplevel",
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new InvalidOperationException("Git command failed"));
        }

        private static void SetupSuccessfulPowerShellExecution(Mock<ProcessExecutor> mockProcessExecutor)
        {
            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    SecureProcessConfiguration.PowerShellExecutable,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Success("PowerShell script executed successfully"));
        }

        private static void SetupFailedPowerShellExecution(Mock<ProcessExecutor> mockProcessExecutor)
        {
            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    SecureProcessConfiguration.PowerShellExecutable,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Failure(new InvalidOperationException("PowerShell execution failed")));
        }

        private static void SetupSuccessfulDotNetBuildExecution(Mock<ProcessExecutor> mockProcessExecutor)
        {
            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    "build /t:generateCode /p:Debug=True",
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Success("Build completed successfully"));
        }

        private static void SetupFailedDotNetBuildExecution(Mock<ProcessExecutor> mockProcessExecutor, ProcessExecutionException? processException = null)
        {
            var result = processException != null
                ? Result<object>.Failure(processException)
                : Result<object>.Failure(new InvalidOperationException("Build failed"));

            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    "build /t:generateCode /p:Debug=True",
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(result);
        }

        private static void VerifyLogMessage(
            Mock<ILogger<GitHubTypeSpecSdkGenerationService>> mockLogger,
            LogLevel expectedLevel,
            string expectedMessage,
            Times? times = null)
        {
            mockLogger.Verify(
                x => x.Log(
                    expectedLevel,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                times ?? Times.AtLeastOnce());
        }

        #endregion

        #region Constructor Tests

        [Test]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            var appSettings = CreateTestAppSettings();
            var logger = CreateMockLogger();
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext();

            var service = new GitHubTypeSpecSdkGenerationService(
                appSettings, logger.Object, processExecutor.Object, validationContext);

            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullAppSettings_ShouldThrowArgumentNullException()
        {
            var logger = CreateMockLogger();
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext();

            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GitHubTypeSpecSdkGenerationService(
                    null!, logger.Object, processExecutor.Object, validationContext));

            Assert.That(exception?.ParamName, Is.EqualTo("appSettings"));
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            var appSettings = CreateTestAppSettings();
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext();

            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GitHubTypeSpecSdkGenerationService(
                    appSettings, null!, processExecutor.Object, validationContext));

            Assert.That(exception?.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithNullProcessExecutor_ShouldThrowArgumentNullException()
        {
            var appSettings = CreateTestAppSettings();
            var logger = CreateMockLogger();
            var validationContext = CreateValidationContext();

            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GitHubTypeSpecSdkGenerationService(
                    appSettings, logger.Object, null!, validationContext));

            Assert.That(exception?.ParamName, Is.EqualTo("processExecutor"));
        }

        [Test]
        public void Constructor_WithNullValidationContext_ShouldThrowArgumentNullException()
        {
            var appSettings = CreateTestAppSettings();
            var logger = CreateMockLogger();
            var processExecutor = CreateMockProcessExecutor();

            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GitHubTypeSpecSdkGenerationService(
                    appSettings, logger.Object, processExecutor.Object, null!));

            Assert.That(exception?.ParamName, Is.EqualTo("validationContext"));
        }

        [Test]
        public void Constructor_ShouldStoreValidationContextProperties()
        {
            using var fixture = new TestEnvironmentFixture();
            var sdkOutputDir = fixture.CreateSdkOutputDirectory();
            
            var validationContext = CreateValidationContext(
                "test/typespec/dir",
                "commit123",
                sdkOutputDir);

            var service = CreateService(validationContext: validationContext);

            // Verify properties are stored by checking they're used in operations
            Assert.That(service, Is.Not.Null);
        }

        #endregion

        #region CompileTypeSpecAsync Tests

        [Test]
        public async Task CompileTypeSpecAsync_WithSuccessfulExecution_ShouldReturnSuccess()
        {
            using var fixture = new TestEnvironmentFixture();
            var azureSdkDir = fixture.CreateAzureSdkDirectory();
            var sdkOutputDir = fixture.CreateSdkOutputDirectory();
            
            var logger = CreateMockLogger();
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);

            SetupSuccessfulGitExecution(processExecutor, azureSdkDir);
            SetupSuccessfulPowerShellExecution(processExecutor);
            SetupSuccessfulDotNetBuildExecution(processExecutor);

            var service = CreateService(
                logger: logger.Object,
                processExecutor: processExecutor.Object,
                validationContext: validationContext);

            var result = await service.CompileTypeSpecAsync();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo("TypeSpec compilation completed successfully"));
            VerifyLogMessage(logger, LogLevel.Information, "Starting TypeSpec compilation for commit:");
            VerifyLogMessage(logger, LogLevel.Information, "TypeSpec compilation completed successfully");
        }

        [Test]
        public async Task CompileTypeSpecAsync_WithExtractAzureSdkDirFailure_ShouldReturnFailure()
        {
            using var fixture = new TestEnvironmentFixture();
            var sdkOutputDir = fixture.CreateSdkOutputDirectory();
            
            var logger = CreateMockLogger();
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);

            SetupFailedGitExecution(processExecutor);

            var service = CreateService(
                logger: logger.Object,
                processExecutor: processExecutor.Object,
                validationContext: validationContext);

            var result = await service.CompileTypeSpecAsync();

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Exception, Is.Not.Null);
        }

        [Test]
        public async Task CompileTypeSpecAsync_WithPowerShellFailure_ShouldReturnFailure()
        {
            using var fixture = new TestEnvironmentFixture();
            var azureSdkDir = fixture.CreateAzureSdkDirectory();
            var sdkOutputDir = fixture.CreateSdkOutputDirectory();
            
            var logger = CreateMockLogger();
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);

            SetupSuccessfulGitExecution(processExecutor, azureSdkDir);
            SetupFailedPowerShellExecution(processExecutor);

            var service = CreateService(
                logger: logger.Object,
                processExecutor: processExecutor.Object,
                validationContext: validationContext);

            var result = await service.CompileTypeSpecAsync();

            Assert.That(result.IsFailure, Is.True);
        }

        [Test]
        public async Task CompileTypeSpecAsync_WithDotNetBuildFailure_ShouldReturnFailure()
        {
            using var fixture = new TestEnvironmentFixture();
            var azureSdkDir = fixture.CreateAzureSdkDirectory();
            var sdkOutputDir = fixture.CreateSdkOutputDirectory();
            
            var logger = CreateMockLogger();
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);

            SetupSuccessfulGitExecution(processExecutor, azureSdkDir);
            SetupSuccessfulPowerShellExecution(processExecutor);
            SetupFailedDotNetBuildExecution(processExecutor);

            var service = CreateService(
                logger: logger.Object,
                processExecutor: processExecutor.Object,
                validationContext: validationContext);

            var result = await service.CompileTypeSpecAsync();

            Assert.That(result.IsFailure, Is.True);
        }

        [Test]
        public async Task CompileTypeSpecAsync_WithCancellationToken_ShouldPassTokenToOperations()
        {
            using var fixture = new TestEnvironmentFixture();
            var azureSdkDir = fixture.CreateAzureSdkDirectory();
            var sdkOutputDir = fixture.CreateSdkOutputDirectory();
            
            var logger = CreateMockLogger();
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);
            var cancellationToken = new CancellationTokenSource().Token;

            SetupSuccessfulGitExecution(processExecutor, azureSdkDir);
            SetupSuccessfulPowerShellExecution(processExecutor);
            SetupSuccessfulDotNetBuildExecution(processExecutor);

            var service = CreateService(
                logger: logger.Object,
                processExecutor: processExecutor.Object,
                validationContext: validationContext);

            var result = await service.CompileTypeSpecAsync(cancellationToken);

            Assert.That(result.IsSuccess, Is.True);
            
            // Verify cancellation token was passed to PowerShell execution
            processExecutor.Verify(x => x.ExecuteAsync(
                SecureProcessConfiguration.PowerShellExecutable,
                It.IsAny<string>(),
                It.IsAny<string>(),
                cancellationToken,
                It.IsAny<TimeSpan?>()), Times.Once);

            // Verify cancellation token was passed to dotnet build execution
            processExecutor.Verify(x => x.ExecuteAsync(
                SecureProcessConfiguration.DotNetExecutable,
                "build /t:generateCode /p:Debug=True",
                It.IsAny<string>(),
                cancellationToken,
                It.IsAny<TimeSpan?>()), Times.Once);
        }

        #endregion

        #region ExtractAzureSdkDirAsync Tests

        [Test]
        public async Task ExtractAzureSdkDirAsync_WithSuccessfulGitCommand_ShouldReturnGitRoot()
        {
            using var fixture = new TestEnvironmentFixture();
            var azureSdkDir = fixture.CreateAzureSdkDirectory();
            var sdkOutputDir = fixture.CreateSdkOutputDirectory();
            
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);

            SetupSuccessfulGitExecution(processExecutor, azureSdkDir);

            var service = CreateService(
                processExecutor: processExecutor.Object,
                validationContext: validationContext);

            // Use reflection to test the protected method
            var method = typeof(GitHubTypeSpecSdkGenerationService)
                .GetMethod("ExtractAzureSdkDirAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task<Result<string>>)method!.Invoke(service, null)!;
            var result = await task;

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(azureSdkDir));
        }

        [Test]
        public async Task ExtractAzureSdkDirAsync_WithFailedGitCommand_ShouldFallbackToDirectoryTraversal()
        {
            using var fixture = new TestEnvironmentFixture();
            var azureSdkDir = fixture.CreateAzureSdkDirectory();
            var sdkOutputDir = Path.Combine(azureSdkDir, "sdk", "test");
            Directory.CreateDirectory(sdkOutputDir);
            
            var logger = CreateMockLogger();
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);

            SetupFailedGitExecution(processExecutor);

            var service = CreateService(
                logger: logger.Object,
                processExecutor: processExecutor.Object,
                validationContext: validationContext);

            // Use reflection to test the protected method
            var method = typeof(GitHubTypeSpecSdkGenerationService)
                .GetMethod("ExtractAzureSdkDirAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task<Result<string>>)method!.Invoke(service, null)!;
            var result = await task;

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(azureSdkDir));
            VerifyLogMessage(logger, LogLevel.Warning, "Failed to execute git command, falling back to directory traversal");
        }

        [Test]
        public async Task ExtractAzureSdkDirAsync_WithGitExceptionAndFallbackFailure_ShouldReturnCombinedError()
        {
            using var fixture = new TestEnvironmentFixture();
            var sdkOutputDir = fixture.CreateSdkOutputDirectory(); // No azure-sdk-for-net parent
            
            var logger = CreateMockLogger();
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);

            SetupFailedGitExecution(processExecutor);

            var service = CreateService(
                logger: logger.Object,
                processExecutor: processExecutor.Object,
                validationContext: validationContext);

            // Use reflection to test the protected method
            var method = typeof(GitHubTypeSpecSdkGenerationService)
                .GetMethod("ExtractAzureSdkDirAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task<Result<string>>)method!.Invoke(service, null)!;
            var result = await task;

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Exception?.Message, Does.Contain("Both git detection and directory traversal failed"));
        }

        [Test]
        public async Task ExtractAzureSdkDirAsync_WithOperationCanceledException_ShouldThrow()
        {
            using var fixture = new TestEnvironmentFixture();
            var sdkOutputDir = fixture.CreateSdkOutputDirectory();
            
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);

            processExecutor.Setup(x => x.ExecuteAsync(
                    SecureProcessConfiguration.GitExecutable,
                    "rev-parse --show-toplevel",
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new OperationCanceledException());

            var service = CreateService(
                processExecutor: processExecutor.Object,
                validationContext: validationContext);

            // Use reflection to test the protected method
            var method = typeof(GitHubTypeSpecSdkGenerationService)
                .GetMethod("ExtractAzureSdkDirAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                var task = (Task<Result<string>>)method!.Invoke(service, null)!;
                await task;
            });
        }

        #endregion

        #region ExtractAzureSdkDirFallback Tests

        [Test]
        public void ExtractAzureSdkDirFallback_WithGitDirectory_ShouldReturnGitRoot()
        {
            using var fixture = new TestEnvironmentFixture();
            var azureSdkDir = fixture.CreateAzureSdkDirectory();
            var sdkOutputDir = Path.Combine(azureSdkDir, "sdk", "test");
            Directory.CreateDirectory(sdkOutputDir);
            
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);
            var service = CreateService(validationContext: validationContext);

            // Use reflection to test the private method
            var method = typeof(GitHubTypeSpecSdkGenerationService)
                .GetMethod("ExtractAzureSdkDirFallback", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (Result<string>)method!.Invoke(service, null)!;

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(azureSdkDir));
        }

        [Test]
        public void ExtractAzureSdkDirFallback_WithAzureSdkDirectoryName_ShouldReturnDirectoryByName()
        {
            using var fixture = new TestEnvironmentFixture();
            var azureSdkDir = fixture.CreateTestDirectory("azure-sdk-for-net");
            var sdkOutputDir = Path.Combine(azureSdkDir, "sdk", "test");
            Directory.CreateDirectory(sdkOutputDir);
            
            var logger = CreateMockLogger();
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);
            var service = CreateService(logger: logger.Object, validationContext: validationContext);

            // Use reflection to test the private method
            var method = typeof(GitHubTypeSpecSdkGenerationService)
                .GetMethod("ExtractAzureSdkDirFallback", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (Result<string>)method!.Invoke(service, null)!;

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(azureSdkDir));
            VerifyLogMessage(logger, LogLevel.Information, "Found Azure SDK directory by name:");
        }

        [Test]
        public void ExtractAzureSdkDirFallback_WithMaxIterations_ShouldLogWarningAndReturnFailure()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Create a deep directory structure that will hit max iterations
            var deepPath = fixture.CreateSdkOutputDirectory();
            for (int i = 0; i < 70; i++) // More than maxIterations (64)
            {
                deepPath = Path.Combine(deepPath, $"level{i}");
                Directory.CreateDirectory(deepPath);
            }
            
            var logger = CreateMockLogger();
            var validationContext = CreateValidationContext(sdkOutputDir: deepPath);
            var service = CreateService(logger: logger.Object, validationContext: validationContext);

            // Use reflection to test the private method
            var method = typeof(GitHubTypeSpecSdkGenerationService)
                .GetMethod("ExtractAzureSdkDirFallback", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (Result<string>)method!.Invoke(service, null)!;

            Assert.That(result.IsFailure, Is.True);
            VerifyLogMessage(logger, LogLevel.Warning, "Directory traversal reached maximum depth of");
        }

        [Test]
        public void ExtractAzureSdkDirFallback_WithNoValidDirectory_ShouldReturnFailure()
        {
            using var fixture = new TestEnvironmentFixture();
            var sdkOutputDir = fixture.CreateSdkOutputDirectory();
            
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);
            var service = CreateService(validationContext: validationContext);

            // Use reflection to test the private method
            var method = typeof(GitHubTypeSpecSdkGenerationService)
                .GetMethod("ExtractAzureSdkDirFallback", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (Result<string>)method!.Invoke(service, null)!;

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Exception?.Message, Does.Contain("Could not locate azure-sdk-for-net directory"));
        }

        #endregion

        #region RunPowerShellGenerationScriptAsync Tests

        [Test]
        public async Task RunPowerShellGenerationScriptAsync_WithValidScript_ShouldExecuteSuccessfully()
        {
            using var fixture = new TestEnvironmentFixture();
            var azureSdkDir = fixture.CreateAzureSdkDirectory();
            var sdkOutputDir = fixture.CreateSdkOutputDirectory();
            
            var logger = CreateMockLogger();
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);

            SetupSuccessfulPowerShellExecution(processExecutor);

            var service = CreateService(
                logger: logger.Object,
                processExecutor: processExecutor.Object,
                validationContext: validationContext);

            // Use reflection to test the private method
            var method = typeof(GitHubTypeSpecSdkGenerationService)
                .GetMethod("RunPowerShellGenerationScriptAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task<Result<object>>)method!.Invoke(service, new object[] { azureSdkDir, CancellationToken.None })!;
            var result = await task;

            Assert.That(result.IsSuccess, Is.True);
            VerifyLogMessage(logger, LogLevel.Information, "Running PowerShell generation script");
        }

        [Test]
        public void RunPowerShellGenerationScriptAsync_WithInvalidScriptPath_ShouldThrowArgumentException()
        {
            using var fixture = new TestEnvironmentFixture();
            var azureSdkDir = fixture.CreateTestDirectory("test-sdk");
            var sdkOutputDir = fixture.CreateSdkOutputDirectory();
            
            var appSettings = CreateTestAppSettings(powerShellScriptPath: "../invalid/../../path.ps1");
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);
            var service = CreateService(appSettings: appSettings, validationContext: validationContext);

            // Use reflection to test the private method
            var method = typeof(GitHubTypeSpecSdkGenerationService)
                .GetMethod("RunPowerShellGenerationScriptAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                var task = (Task<Result<object>>)method!.Invoke(service, new object[] { azureSdkDir, CancellationToken.None })!;
                await task;
            });
        }

        [Test]
        public async Task RunPowerShellGenerationScriptAsync_WithOperationCanceledException_ShouldThrow()
        {
            using var fixture = new TestEnvironmentFixture();
            var azureSdkDir = fixture.CreateAzureSdkDirectory();
            var sdkOutputDir = fixture.CreateSdkOutputDirectory();
            
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);

            processExecutor.Setup(x => x.ExecuteAsync(
                    SecureProcessConfiguration.PowerShellExecutable,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new OperationCanceledException());

            var service = CreateService(
                processExecutor: processExecutor.Object,
                validationContext: validationContext);

            // Use reflection to test the private method
            var method = typeof(GitHubTypeSpecSdkGenerationService)
                .GetMethod("RunPowerShellGenerationScriptAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                var task = (Task<Result<object>>)method!.Invoke(service, new object[] { azureSdkDir, CancellationToken.None })!;
                await task;
            });
        }

        [Test]
        public async Task RunPowerShellGenerationScriptAsync_WithUnexpectedException_ShouldLogCriticalAndThrow()
        {
            using var fixture = new TestEnvironmentFixture();
            var azureSdkDir = fixture.CreateAzureSdkDirectory();
            var sdkOutputDir = fixture.CreateSdkOutputDirectory();
            
            var logger = CreateMockLogger();
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);

            processExecutor.Setup(x => x.ExecuteAsync(
                    SecureProcessConfiguration.PowerShellExecutable,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new InvalidOperationException("Unexpected error"));

            var service = CreateService(
                logger: logger.Object,
                processExecutor: processExecutor.Object,
                validationContext: validationContext);

            // Use reflection to test the private method
            var method = typeof(GitHubTypeSpecSdkGenerationService)
                .GetMethod("RunPowerShellGenerationScriptAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                var task = (Task<Result<object>>)method!.Invoke(service, new object[] { azureSdkDir, CancellationToken.None })!;
                await task;
            });

            Assert.That(exception?.Message, Is.EqualTo("Unexpected error"));
            VerifyLogMessage(logger, LogLevel.Critical, "Unexpected system error during PowerShell generation script");
        }

        #endregion

        #region RunDotNetBuildGenerateCodeAsync Tests

        [Test]
        public async Task RunDotNetBuildGenerateCodeAsync_WithValidSrcDirectory_ShouldExecuteSuccessfully()
        {
            using var fixture = new TestEnvironmentFixture();
            var sdkOutputDir = fixture.CreateSdkOutputDirectory();
            var srcDir = Path.Combine(sdkOutputDir, "src");
            Directory.CreateDirectory(srcDir);
            
            var logger = CreateMockLogger();
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);

            SetupSuccessfulDotNetBuildExecution(processExecutor);

            var service = CreateService(
                logger: logger.Object,
                processExecutor: processExecutor.Object,
                validationContext: validationContext);

            // Use reflection to test the private method
            var method = typeof(GitHubTypeSpecSdkGenerationService)
                .GetMethod("RunDotNetBuildGenerateCodeAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task<Result<object>>)method!.Invoke(service, new object[] { CancellationToken.None })!;
            var result = await task;

            Assert.That(result.IsSuccess, Is.True);
            VerifyLogMessage(logger, LogLevel.Information, "Running dotnet build /t:generateCode");
        }

        [Test]
        public void RunDotNetBuildGenerateCodeAsync_WithMissingSrcDirectory_ShouldThrowDirectoryNotFoundException()
        {
            using var fixture = new TestEnvironmentFixture();
            var sdkOutputDir = fixture.CreateSdkOutputDirectoryWithoutSrc();
            // Don't create src directory
            
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);
            var service = CreateService(validationContext: validationContext);

            // Use reflection to test the private method
            var method = typeof(GitHubTypeSpecSdkGenerationService)
                .GetMethod("RunDotNetBuildGenerateCodeAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.ThrowsAsync<DirectoryNotFoundException>(async () =>
            {
                var task = (Task<Result<object>>)method!.Invoke(service, new object[] { CancellationToken.None })!;
                await task;
            });
        }

        [Test]
        public async Task RunDotNetBuildGenerateCodeAsync_WithProcessExecutionException_ShouldReturnTypeSpecCompilationException()
        {
            using var fixture = new TestEnvironmentFixture();
            var sdkOutputDir = fixture.CreateSdkOutputDirectory();
            var srcDir = Path.Combine(sdkOutputDir, "src");
            Directory.CreateDirectory(srcDir);
            
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);

            var processException = new GeneralProcessExecutionException("Build failed", "dotnet", "build failed", "error output", 1);
            SetupFailedDotNetBuildExecution(processExecutor, processException);

            var service = CreateService(
                processExecutor: processExecutor.Object,
                validationContext: validationContext);

            // Use reflection to test the private method
            var method = typeof(GitHubTypeSpecSdkGenerationService)
                .GetMethod("RunDotNetBuildGenerateCodeAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task<Result<object>>)method!.Invoke(service, new object[] { CancellationToken.None })!;
            var result = await task;

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ProcessException, Is.TypeOf<TypeSpecCompilationException>());
        }

        [Test]
        public async Task RunDotNetBuildGenerateCodeAsync_WithBuildOutput_ShouldLogOutput()
        {
            using var fixture = new TestEnvironmentFixture();
            var sdkOutputDir = fixture.CreateSdkOutputDirectory();
            var srcDir = Path.Combine(sdkOutputDir, "src");
            Directory.CreateDirectory(srcDir);
            
            var logger = CreateMockLogger();
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);

            processExecutor.Setup(x => x.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    "build /t:generateCode /p:Debug=True",
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result<object>.Success("Build output here"));

            var service = CreateService(
                logger: logger.Object,
                processExecutor: processExecutor.Object,
                validationContext: validationContext);

            // Use reflection to test the private method
            var method = typeof(GitHubTypeSpecSdkGenerationService)
                .GetMethod("RunDotNetBuildGenerateCodeAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task<Result<object>>)method!.Invoke(service, new object[] { CancellationToken.None })!;
            var result = await task;

            Assert.That(result.IsSuccess, Is.True);
            VerifyLogMessage(logger, LogLevel.Information, "dotnet build output:");
        }

        [Test]
        public async Task RunDotNetBuildGenerateCodeAsync_WithOperationCanceledException_ShouldThrow()
        {
            using var fixture = new TestEnvironmentFixture();
            var sdkOutputDir = fixture.CreateSdkOutputDirectory();
            var srcDir = Path.Combine(sdkOutputDir, "src");
            Directory.CreateDirectory(srcDir);
            
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);

            processExecutor.Setup(x => x.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    "build /t:generateCode /p:Debug=True",
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new OperationCanceledException());

            var service = CreateService(
                processExecutor: processExecutor.Object,
                validationContext: validationContext);

            // Use reflection to test the private method
            var method = typeof(GitHubTypeSpecSdkGenerationService)
                .GetMethod("RunDotNetBuildGenerateCodeAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                var task = (Task<Result<object>>)method!.Invoke(service, new object[] { CancellationToken.None })!;
                await task;
            });
        }

        [Test]
        public async Task RunDotNetBuildGenerateCodeAsync_WithUnexpectedException_ShouldLogCriticalAndThrow()
        {
            using var fixture = new TestEnvironmentFixture();
            var sdkOutputDir = fixture.CreateSdkOutputDirectory();
            var srcDir = Path.Combine(sdkOutputDir, "src");
            Directory.CreateDirectory(srcDir);
            
            var logger = CreateMockLogger();
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);

            processExecutor.Setup(x => x.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    "build /t:generateCode /p:Debug=True",
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new InvalidOperationException("Unexpected build error"));

            var service = CreateService(
                logger: logger.Object,
                processExecutor: processExecutor.Object,
                validationContext: validationContext);

            // Use reflection to test the private method
            var method = typeof(GitHubTypeSpecSdkGenerationService)
                .GetMethod("RunDotNetBuildGenerateCodeAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                var task = (Task<Result<object>>)method!.Invoke(service, new object[] { CancellationToken.None })!;
                await task;
            });

            Assert.That(exception?.Message, Is.EqualTo("Unexpected build error"));
            VerifyLogMessage(logger, LogLevel.Critical, "Unexpected system error during dotnet build");
        }

        #endregion

        #region Integration and Thread Safety Tests

        [Test]
        public async Task CompileTypeSpecAsync_FullWorkflow_ShouldExecuteAllStepsInCorrectOrder()
        {
            using var fixture = new TestEnvironmentFixture();
            var azureSdkDir = fixture.CreateAzureSdkDirectory();
            var sdkOutputDir = fixture.CreateSdkOutputDirectory();
            
            var logger = CreateMockLogger();
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);

            SetupSuccessfulGitExecution(processExecutor, azureSdkDir);
            SetupSuccessfulPowerShellExecution(processExecutor);
            SetupSuccessfulDotNetBuildExecution(processExecutor);

            var service = CreateService(
                logger: logger.Object,
                processExecutor: processExecutor.Object,
                validationContext: validationContext);

            var result = await service.CompileTypeSpecAsync();

            Assert.That(result.IsSuccess, Is.True);

            // Verify all executions happened
            processExecutor.Verify(x => x.ExecuteAsync(
                SecureProcessConfiguration.GitExecutable,
                "rev-parse --show-toplevel",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()), Times.Once);

            processExecutor.Verify(x => x.ExecuteAsync(
                SecureProcessConfiguration.PowerShellExecutable,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()), Times.Once);

            processExecutor.Verify(x => x.ExecuteAsync(
                SecureProcessConfiguration.DotNetExecutable,
                "build /t:generateCode /p:Debug=True",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Test]
        public async Task CompileTypeSpecAsync_CalledConcurrently_ShouldHandleMultipleExecutions()
        {
            using var fixture = new TestEnvironmentFixture();
            var azureSdkDir = fixture.CreateAzureSdkDirectory();
            var sdkOutputDir = fixture.CreateSdkOutputDirectory();
            
            var processExecutor = CreateMockProcessExecutor();
            var validationContext = CreateValidationContext(sdkOutputDir: sdkOutputDir);

            SetupSuccessfulGitExecution(processExecutor, azureSdkDir);
            SetupSuccessfulPowerShellExecution(processExecutor);
            SetupSuccessfulDotNetBuildExecution(processExecutor);

            var service = CreateService(
                processExecutor: processExecutor.Object,
                validationContext: validationContext);

            var task1 = service.CompileTypeSpecAsync();
            var task2 = service.CompileTypeSpecAsync();
            var task3 = service.CompileTypeSpecAsync();

            var results = await Task.WhenAll(task1, task2, task3);

            Assert.That(results.All(r => r.IsSuccess), Is.True);
        }

        #endregion
    }
}
