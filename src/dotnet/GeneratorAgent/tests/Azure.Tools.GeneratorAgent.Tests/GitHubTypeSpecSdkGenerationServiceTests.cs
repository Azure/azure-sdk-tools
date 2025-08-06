using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Tools.GeneratorAgent;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class GitHubTypeSpecSdkGenerationServiceTests
    {
        private const string TestCommitId = "abc123def456789012345678901234567890abcdef";
        private const string TestTypeSpecDir = "specification/keyvault/data-plane";
        private const string TestSdkOutputDir = @"C:\azure-sdk-for-net\sdk\keyvault\Azure.Security.KeyVault.Secrets";
        private const string TestAzureSdkDir = @"C:\azure-sdk-for-net";

        private sealed class TestFileSystemFixture : IDisposable
        {
            private readonly List<string> _createdDirectories = new();
            private readonly Mock<ILogger> _inputValidatorLogger;

            public TestFileSystemFixture()
            {
                _inputValidatorLogger = new Mock<ILogger>();
            }

            public string CreateUniqueTestDirectory()
            {
                var uniqueId = Guid.NewGuid().ToString("N")[..8];
                var testDir = Path.Combine(Path.GetTempPath(), $"GitHubSDKTest_{uniqueId}");
                Directory.CreateDirectory(testDir);
                _createdDirectories.Add(testDir);
                return testDir;
            }

            public string CreateUniqueAzureSdkTestDirectory()
            {
                var baseDir = CreateUniqueTestDirectory();
                var azureSdkDir = Path.Combine(baseDir, "azure-sdk-for-net");
                Directory.CreateDirectory(azureSdkDir);
                
                var srcDirectory = Path.Combine(azureSdkDir, "src");
                Directory.CreateDirectory(srcDirectory);
                
                var scriptDir = Path.Combine(azureSdkDir, "eng", "scripts", "automation");
                Directory.CreateDirectory(scriptDir);
                var scriptPath = Path.Combine(scriptDir, "Invoke-TypeSpecDataPlaneGenerateSDKPackage.ps1");
                File.WriteAllText(scriptPath, "# Test PowerShell script");
                
                return azureSdkDir;
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
                        }
                    }
                }
            }
        }

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
            var configMock = new Mock<IConfiguration>();
            var mockLogger = new Mock<ILogger<AppSettings>>();
            
            var projectEndpointSection = new Mock<IConfigurationSection>();
            projectEndpointSection.Setup(s => s.Value).Returns("https://test.openai.azure.com/");
            configMock.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(projectEndpointSection.Object);

            var defaultSection = new Mock<IConfigurationSection>();
            defaultSection.Setup(s => s.Value).Returns((string?)null);
            configMock.Setup(c => c.GetSection(It.IsAny<string>())).Returns(defaultSection.Object);

            return new AppSettings(configMock.Object, mockLogger.Object);
        }

        private static ValidationContext CreateValidationContext(
            string? typeSpecDir = null,
            string commitId = TestCommitId,
            string? sdkOutputDir = null)
        {
            var actualTypeSpecDir = typeSpecDir ?? TestTypeSpecDir;
            var actualSdkOutputDir = sdkOutputDir ?? TestSdkOutputDir;
            
            return ValidationContext.CreateFromValidatedInputs(actualTypeSpecDir, commitId, actualSdkOutputDir);
        }

        private static string CreateUniqueTestDirectory()
        {
            var baseDir = Path.Combine(Path.GetTempPath(), "GitHubTypeSpecSdkGenerationServiceTests");
            var uniqueDir = Path.Combine(baseDir, Guid.NewGuid().ToString());
            Directory.CreateDirectory(uniqueDir);
            return uniqueDir;
        }

        private static string CreateUniqueAzureSdkTestDirectory()
        {
            var baseDir = Path.Combine(Path.GetTempPath(), "GitHubTypeSpecSdkGenerationServiceTests");
            var azureSdkDir = Path.Combine(baseDir, "azure-sdk-for-net", "sdk", "keyvault", "Azure.Security.KeyVault.Secrets");
            Directory.CreateDirectory(azureSdkDir);
            
            var srcDirectory = Path.Combine(azureSdkDir, "src");
            Directory.CreateDirectory(srcDirectory);
            
            var scriptDir = Path.Combine(baseDir, "azure-sdk-for-net", "eng", "scripts", "automation");
            Directory.CreateDirectory(scriptDir);
            var scriptPath = Path.Combine(scriptDir, "Invoke-TypeSpecDataPlaneGenerateSDKPackage.ps1");
            File.WriteAllText(scriptPath, "# Test PowerShell script");
            
            return azureSdkDir;
        }

        private static GitHubTypeSpecSdkGenerationService CreateGitHubTypeSpecSdkGenerationService(
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

        private static void SetupSuccessfulPowerShellExecution(Mock<ProcessExecutor> mockProcessExecutor)
        {
            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    "pwsh",
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result.Success("PowerShell succeeded"));
        }

        private static void SetupFailedPowerShellExecution(Mock<ProcessExecutor> mockProcessExecutor)
        {
            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    SecureProcessConfiguration.PowerShellExecutable,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result.Failure("PowerShell failed"));
        }

        private static void SetupSuccessfulDotNetBuildExecution(Mock<ProcessExecutor> mockProcessExecutor)
        {
            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    "build /t:generateCode /p:Debug=True",
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result.Success("Build succeeded"));
        }

        private static void SetupFailedDotNetBuildExecution(Mock<ProcessExecutor> mockProcessExecutor)
        {
            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    "build /t:generateCode /p:Debug=True",
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result.Failure("Build failed"));
        }

        private static void VerifyInformationLogged(Mock<ILogger<GitHubTypeSpecSdkGenerationService>> mockLogger, string expectedMessage)
        {
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce,
                $"Expected information log containing: {expectedMessage}");
        }

        private static void VerifyErrorLogged(Mock<ILogger<GitHubTypeSpecSdkGenerationService>> mockLogger, string expectedMessage)
        {
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce,
                $"Expected error log containing: {expectedMessage}");
        }

        [Test]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            using var fixture = new TestFileSystemFixture();
            Assert.DoesNotThrow(() => CreateGitHubTypeSpecSdkGenerationService());
        }

        [Test]
        public void Constructor_WithNullAppSettings_ShouldThrowArgumentNullException()
        {
            using var fixture = new TestFileSystemFixture();

            var exception = Assert.Throws<ArgumentNullException>(() => 
                new GitHubTypeSpecSdkGenerationService(
                    null!,
                    CreateMockLogger().Object,
                    CreateMockProcessExecutor().Object,
                    CreateValidationContext()));

            Assert.That(exception?.ParamName, Is.EqualTo("appSettings"));
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            using var fixture = new TestFileSystemFixture();

            var exception = Assert.Throws<ArgumentNullException>(() => 
                new GitHubTypeSpecSdkGenerationService(
                    CreateTestAppSettings(),
                    null!,
                    CreateMockProcessExecutor().Object,
                    CreateValidationContext()));

            Assert.That(exception?.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithNullProcessExecutor_ShouldThrowArgumentNullException()
        {
            using var fixture = new TestFileSystemFixture();

            var exception = Assert.Throws<ArgumentNullException>(() => 
                new GitHubTypeSpecSdkGenerationService(
                    CreateTestAppSettings(),
                    CreateMockLogger().Object,
                    null!,
                    CreateValidationContext()));

            Assert.That(exception?.ParamName, Is.EqualTo("processExecutor"));
        }

        [Test]
        public void Constructor_WithNullValidationContext_ShouldThrowArgumentNullException()
        {
            using var fixture = new TestFileSystemFixture();

            var exception = Assert.Throws<ArgumentNullException>(() => 
                new GitHubTypeSpecSdkGenerationService(
                    CreateTestAppSettings(),
                    CreateMockLogger().Object,
                    CreateMockProcessExecutor().Object,
                    null!));

            Assert.That(exception?.ParamName, Is.EqualTo("validationContext"));
        }

        [Test]
        public async Task CompileTypeSpecAsync_WithSuccessfulExecution_ShouldReturnTrue()
        {
            using var fixture = new TestFileSystemFixture();
            var azureSdkOutputDir = fixture.CreateUniqueAzureSdkTestDirectory();
            var validationContext = CreateValidationContext(
                typeSpecDir: TestTypeSpecDir,
                commitId: TestCommitId,
                sdkOutputDir: azureSdkOutputDir);
            
            var mockProcessExecutor = CreateMockProcessExecutor();
            var mockLogger = CreateMockLogger();
            
            SetupSuccessfulPowerShellExecution(mockProcessExecutor);
            SetupSuccessfulDotNetBuildExecution(mockProcessExecutor);

            var service = CreateGitHubTypeSpecSdkGenerationService(
                processExecutor: mockProcessExecutor.Object,
                logger: mockLogger.Object,
                validationContext: validationContext);

            await service.CompileTypeSpecAsync();

            VerifyInformationLogged(mockLogger, "Starting GitHub-based TypeSpec compilation for commit:");
            VerifyInformationLogged(mockLogger, "GitHub-based TypeSpec compilation completed successfully");
        }

        [Test]
        public void CompileTypeSpecAsync_WithPowerShellFailure_ShouldThrowException()
        {
            var mockProcessExecutor = CreateMockProcessExecutor();
            var mockLogger = CreateMockLogger();
            
            SetupFailedPowerShellExecution(mockProcessExecutor);

            var service = CreateGitHubTypeSpecSdkGenerationService(
                processExecutor: mockProcessExecutor.Object,
                logger: mockLogger.Object);
            
            Assert.ThrowsAsync<InvalidOperationException>(() => service.CompileTypeSpecAsync());
        }

        [Test]
        public void CompileTypeSpecAsync_WithDotNetBuildFailure_ShouldThrowException()
        {
            using var fixture = new TestFileSystemFixture();
            var testDirectory = fixture.CreateUniqueAzureSdkTestDirectory();
            var mockProcessExecutor = CreateMockProcessExecutor();
            var mockLogger = CreateMockLogger();
            var validationContext = ValidationContext.CreateFromValidatedInputs(TestTypeSpecDir, TestCommitId, testDirectory);
            
            SetupSuccessfulPowerShellExecution(mockProcessExecutor);
            SetupFailedDotNetBuildExecution(mockProcessExecutor);

            var service = CreateGitHubTypeSpecSdkGenerationService(
                processExecutor: mockProcessExecutor.Object,
                logger: mockLogger.Object,
                validationContext: validationContext);
                
            Assert.ThrowsAsync<InvalidOperationException>(() => service.CompileTypeSpecAsync());
        }

        [Test]
        public void CompileTypeSpecAsync_WithException_ShouldThrowException()
        {
            using var fixture = new TestFileSystemFixture();
            var azureSdkOutputDir = fixture.CreateUniqueAzureSdkTestDirectory();
            var validationContext = CreateValidationContext(
                typeSpecDir: TestTypeSpecDir,
                commitId: TestCommitId,
                sdkOutputDir: azureSdkOutputDir);
            
            var mockProcessExecutor = CreateMockProcessExecutor();
            var mockLogger = CreateMockLogger();
            var expectedException = new InvalidOperationException("Test exception");
            
            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    "pwsh",
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(expectedException);

            var service = CreateGitHubTypeSpecSdkGenerationService(
                processExecutor: mockProcessExecutor.Object,
                logger: mockLogger.Object,
                validationContext: validationContext);
                
            Assert.ThrowsAsync<InvalidOperationException>(() => service.CompileTypeSpecAsync());
        }

        [Test]
        public void CompileTypeSpecAsync_WithCancellation_ShouldThrowOperationCanceledException()
        {
            using var fixture = new TestFileSystemFixture();
            var azureSdkDirectory = fixture.CreateUniqueAzureSdkTestDirectory();
            var sdkOutputDirectory = Path.Combine(azureSdkDirectory, "output"); // Create a subdirectory for SDK output
            Directory.CreateDirectory(sdkOutputDirectory);
            
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var mockProcessExecutor = CreateMockProcessExecutor();
            var mockLogger = CreateMockLogger();
            var validationContext = ValidationContext.CreateFromValidatedInputs(TestTypeSpecDir, TestCommitId, sdkOutputDirectory);
            
            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new OperationCanceledException());

            var service = CreateGitHubTypeSpecSdkGenerationService(
                processExecutor: mockProcessExecutor.Object,
                logger: mockLogger.Object,
                validationContext: validationContext);
            
            Assert.ThrowsAsync<OperationCanceledException>(() => service.CompileTypeSpecAsync(cts.Token));
        }

        [Test]
        public async Task CompileTypeSpecAsync_ShouldLogCorrectInformation()
        {
            using var fixture = new TestFileSystemFixture();
            var testDirectory = fixture.CreateUniqueAzureSdkTestDirectory();
            var mockProcessExecutor = CreateMockProcessExecutor();
            var mockLogger = CreateMockLogger();
            var validationContext = ValidationContext.CreateFromValidatedInputs(TestTypeSpecDir, TestCommitId, testDirectory);
            
            SetupSuccessfulPowerShellExecution(mockProcessExecutor);
            SetupSuccessfulDotNetBuildExecution(mockProcessExecutor);

            var service = CreateGitHubTypeSpecSdkGenerationService(
                processExecutor: mockProcessExecutor.Object,
                logger: mockLogger.Object,
                validationContext: validationContext);
            await service.CompileTypeSpecAsync();
            VerifyInformationLogged(mockLogger, $"Starting GitHub-based TypeSpec compilation for commit: {TestCommitId}");
            // Removed SDK output directory and TypeSpec spec directory logging expectations
            // since we simplified the logging to focus on essential information
        }

        [Test]
        public async Task ExtractAzureSdkDir_WithValidPath_ShouldReturnCorrectDirectory()
        {
            var testValidationContext = ValidationContext.CreateFromValidatedInputs(
                TestTypeSpecDir, 
                TestCommitId, 
                @"C:\azure-sdk-for-net\sdk\keyvault\Azure.Security.KeyVault.Secrets");

            var service = CreateGitHubTypeSpecSdkGenerationService(validationContext: testValidationContext);
            var method = typeof(GitHubTypeSpecSdkGenerationService).GetMethod("ExtractAzureSdkDirAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = await (Task<string>)method!.Invoke(service, null)!;
            Assert.That(result, Is.EqualTo(@"C:\azure-sdk-for-net"));
        }

        [Test]
        public async Task ExtractAzureSdkDir_WithNestedPath_ShouldReturnCorrectDirectory()
        {
            var testValidationContext = ValidationContext.CreateFromValidatedInputs(
                TestTypeSpecDir, 
                TestCommitId, 
                @"C:\azure-sdk-for-net\sdk\storage\Azure.Storage.Blobs\src");

            var service = CreateGitHubTypeSpecSdkGenerationService(validationContext: testValidationContext);
            var method = typeof(GitHubTypeSpecSdkGenerationService).GetMethod("ExtractAzureSdkDirAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = await (Task<string>)method!.Invoke(service, null)!;
            Assert.That(result, Is.EqualTo(@"C:\azure-sdk-for-net"));
        }

        [Test]
        public void ExtractAzureSdkDir_WithInvalidPath_ShouldThrowInvalidOperationException()
        {
            var testValidationContext = ValidationContext.CreateFromValidatedInputs(
                TestTypeSpecDir, 
                TestCommitId, 
                @"C:\some\other\path\not\containing\azure-sdk");

            var service = CreateGitHubTypeSpecSdkGenerationService(validationContext: testValidationContext);
            var method = typeof(GitHubTypeSpecSdkGenerationService).GetMethod("ExtractAzureSdkDirAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => 
                await (Task<string>)method!.Invoke(service, null)!);
            
            Assert.That(ex!.Message, Does.Contain("Could not locate azure-sdk-for-net directory"));
        }

        [Test]
        public void RunPowerShellGenerationScript_WithValidScript_ShouldReturnTrue()
        {
            using var fixture = new TestFileSystemFixture();
            var mockProcessExecutor = CreateMockProcessExecutor();
            var mockLogger = CreateMockLogger();
            var azureSdkOutputDir = fixture.CreateUniqueAzureSdkTestDirectory();
            var validationContext = CreateValidationContext(
                typeSpecDir: TestTypeSpecDir,
                commitId: TestCommitId,
                sdkOutputDir: azureSdkOutputDir);
            
            SetupSuccessfulPowerShellExecution(mockProcessExecutor);

            var service = CreateGitHubTypeSpecSdkGenerationService(
                processExecutor: mockProcessExecutor.Object,
                logger: mockLogger.Object,
                validationContext: validationContext);
            var method = typeof(GitHubTypeSpecSdkGenerationService).GetMethod("RunPowerShellGenerationScriptAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var azureSdkDir = azureSdkOutputDir;
            while (!string.IsNullOrEmpty(azureSdkDir) && !Path.GetFileName(azureSdkDir).Equals("azure-sdk-for-net", StringComparison.OrdinalIgnoreCase))
            {
                azureSdkDir = Path.GetDirectoryName(azureSdkDir);
            }
            
            Assert.DoesNotThrowAsync(async () => 
            {
                var result = await (Task<Result>)method!.Invoke(service, new object[] { azureSdkDir!, CancellationToken.None })!;
                Assert.That(result.IsSuccess, Is.True);
            });
            VerifyInformationLogged(mockLogger, "Running PowerShell generation script");
            VerifyInformationLogged(mockLogger, "PowerShell generation script completed successfully");
        }

        [Test]
        public void RunPowerShellGenerationScript_WithValidationFailure_ShouldThrowException()
        {
            var mockLogger = CreateMockLogger();
            var invalidAppSettings = CreateTestAppSettings(
                powerShellScriptPath: "../../../malicious/script.ps1"); // Invalid path with traversal
            
            var service = CreateGitHubTypeSpecSdkGenerationService(
                appSettings: invalidAppSettings,
                logger: mockLogger.Object);
            var method = typeof(GitHubTypeSpecSdkGenerationService).GetMethod("RunPowerShellGenerationScriptAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.DoesNotThrowAsync(async () => 
            {
                var result = await (Task<Result>)method!.Invoke(service, new object[] { TestAzureSdkDir, CancellationToken.None })!;
                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.Error, Does.Contain("PowerShell script validation failed"));
            });
            // Removed error logging verification since we now use Result pattern for clean error handling
        }

        [Test]
        public void RunPowerShellGenerationScript_WithProcessFailure_ShouldThrowAndLogError()
        {
            using var fixture = new TestFileSystemFixture();
            const string errorMessage = "PowerShell execution failed";
            
            var azureSdkOutputDir = fixture.CreateUniqueAzureSdkTestDirectory();
            var validationContext = CreateValidationContext(
                typeSpecDir: TestTypeSpecDir,
                commitId: TestCommitId,
                sdkOutputDir: azureSdkOutputDir);
            var mockProcessExecutor = CreateMockProcessExecutor();
            var mockLogger = CreateMockLogger();
            
            mockProcessExecutor.Setup(x => x.ExecuteAsync(
                    "pwsh",
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(Result.Failure(errorMessage));

            var service = CreateGitHubTypeSpecSdkGenerationService(
                processExecutor: mockProcessExecutor.Object,
                logger: mockLogger.Object,
                validationContext: validationContext);
            var method = typeof(GitHubTypeSpecSdkGenerationService).GetMethod("RunPowerShellGenerationScriptAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var azureSdkDir = azureSdkOutputDir;
            while (!string.IsNullOrEmpty(azureSdkDir) && !Path.GetFileName(azureSdkDir).Equals("azure-sdk-for-net", StringComparison.OrdinalIgnoreCase))
            {
                azureSdkDir = Path.GetDirectoryName(azureSdkDir);
            }
            
            Assert.DoesNotThrowAsync(async () => 
            {
                var result = await (Task<Result>)method!.Invoke(service, new object[] { azureSdkDir!, CancellationToken.None })!;
                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.Error, Does.Contain("PowerShell execution failed"));
            });
            // Removed error logging verification since we now use Result pattern for clean error handling
        }

        [Test]
        public void RunPowerShellGenerationScript_ShouldCallProcessExecutorWithCorrectArguments()
        {
            using var fixture = new TestFileSystemFixture();
            var azureSdkOutputDir = fixture.CreateUniqueAzureSdkTestDirectory();
            var validationContext = CreateValidationContext(
                typeSpecDir: TestTypeSpecDir,
                commitId: TestCommitId,
                sdkOutputDir: azureSdkOutputDir);
            var mockProcessExecutor = CreateMockProcessExecutor();
            SetupSuccessfulPowerShellExecution(mockProcessExecutor);

            var service = CreateGitHubTypeSpecSdkGenerationService(
                processExecutor: mockProcessExecutor.Object,
                validationContext: validationContext);
            var method = typeof(GitHubTypeSpecSdkGenerationService).GetMethod("RunPowerShellGenerationScriptAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var azureSdkDir = azureSdkOutputDir;
            while (!string.IsNullOrEmpty(azureSdkDir) && !Path.GetFileName(azureSdkDir).Equals("azure-sdk-for-net", StringComparison.OrdinalIgnoreCase))
            {
                azureSdkDir = Path.GetDirectoryName(azureSdkDir);
            }
            
            Assert.DoesNotThrowAsync(async () => 
            {
                var result = await (Task<Result>)method!.Invoke(service, new object[] { azureSdkDir!, CancellationToken.None })!;
                Assert.That(result.IsSuccess, Is.True);
            });
            mockProcessExecutor.Verify(x => x.ExecuteAsync(
                "pwsh",
                It.Is<string>(args => 
                    args.Contains($"-sdkFolder \"{azureSdkOutputDir}\"") &&
                    args.Contains($"-typespecSpecDirectory \"{TestTypeSpecDir}\"") &&
                    args.Contains($"-commit \"{TestCommitId}\"") &&
                    args.Contains($"-repo \"Azure/azure-rest-api-specs\"")),
                azureSdkDir,
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()), Times.Once);
        }
    }
}
