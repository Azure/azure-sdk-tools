using Azure.Tools.GeneratorAgent;
using Azure.Tools.GeneratorAgent.Exceptions;
using Azure.Tools.GeneratorAgent.Security;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    internal class LibraryBuildServiceTests
    {
        private static Mock<ILogger<LibraryBuildService>> CreateMockLogger()
        {
            return new Mock<ILogger<LibraryBuildService>>();
        }

        private static Mock<ProcessExecutionService> CreateMockProcessExecutionService()
        {
            return new Mock<ProcessExecutionService>(Mock.Of<ILogger<ProcessExecutionService>>());
        }

        private static string CreateTempDirectoryWithFiles(params string[] fileNames)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            
            foreach (string fileName in fileNames)
            {
                string filePath = Path.Combine(tempDir, fileName);
                string directory = Path.GetDirectoryName(filePath)!;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(filePath, "test content");
            }
            
            return tempDir;
        }

        private static void CleanupTempDirectory(string tempDir)
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange
            var mockLogger = CreateMockLogger();
            var mockProcessService = CreateMockProcessExecutionService();

            // Act & Assert
            Assert.DoesNotThrow(() => new LibraryBuildService(mockLogger.Object, mockProcessService.Object));
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange
            var mockProcessService = CreateMockProcessExecutionService();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new LibraryBuildService(null!, mockProcessService.Object));
        }

        [Test]
        public void Constructor_WithNullProcessExecutionService_ThrowsArgumentNullException()
        {
            // Arrange
            var mockLogger = CreateMockLogger();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new LibraryBuildService(mockLogger.Object, null!));
        }

        #endregion

        #region BuildSdkAsync Tests

        [Test]
        public void BuildSdkAsync_WithNullSdkOutputDir_ThrowsArgumentNullException()
        {
            // Arrange
            var mockLogger = CreateMockLogger();
            var mockProcessService = CreateMockProcessExecutionService();
            var service = new LibraryBuildService(mockLogger.Object, mockProcessService.Object);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentNullException>(() => service.BuildSdkAsync(null!));
        }

        [Test]
        public void BuildSdkAsync_WithEmptySdkOutputDir_ThrowsArgumentException()
        {
            // Arrange
            var mockLogger = CreateMockLogger();
            var mockProcessService = CreateMockProcessExecutionService();
            var service = new LibraryBuildService(mockLogger.Object, mockProcessService.Object);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(() => service.BuildSdkAsync(""));
        }

        [Test]
        public void BuildSdkAsync_WithWhitespaceSdkOutputDir_ThrowsArgumentException()
        {
            // Arrange
            var mockLogger = CreateMockLogger();
            var mockProcessService = CreateMockProcessExecutionService();
            var service = new LibraryBuildService(mockLogger.Object, mockProcessService.Object);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(() => service.BuildSdkAsync("   "));
        }

        [Test]
        public async Task BuildSdkAsync_WithSolutionFile_CallsProcessExecutionWithSolutionPath()
        {
            // Arrange
            string tempDir = CreateTempDirectoryWithFiles("TestProject.sln");
            string expectedSolutionPath = Path.Combine(tempDir, "TestProject.sln");
            string expectedArguments = $"build \"{expectedSolutionPath}\"";

            var mockLogger = CreateMockLogger();
            var mockProcessService = CreateMockProcessExecutionService();
            var expectedResult = Result<object>.Success(new object());
            
            mockProcessService.Setup(x => x.ExecuteAsync(
                SecureProcessConfiguration.DotNetExecutable,
                expectedArguments,
                tempDir,
                It.IsAny<CancellationToken>(),
                null))
                .ReturnsAsync(expectedResult);

            var service = new LibraryBuildService(mockLogger.Object, mockProcessService.Object);

            try
            {
                // Act
                var result = await service.BuildSdkAsync(tempDir);

                // Assert
                Assert.That(result, Is.EqualTo(expectedResult));
                mockProcessService.Verify(x => x.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    expectedArguments,
                    tempDir,
                    It.IsAny<CancellationToken>(),
                    null), Times.Once);
            }
            finally
            {
                CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public async Task BuildSdkAsync_WithMultipleSolutionFiles_UsesFirstSolutionFile()
        {
            // Arrange
            string tempDir = CreateTempDirectoryWithFiles("First.sln", "Second.sln");
            string expectedSolutionPath = Path.Combine(tempDir, "First.sln");
            string expectedArguments = $"build \"{expectedSolutionPath}\"";

            var mockLogger = CreateMockLogger();
            var mockProcessService = CreateMockProcessExecutionService();
            var expectedResult = Result<object>.Success(new object());
            
            mockProcessService.Setup(x => x.ExecuteAsync(
                SecureProcessConfiguration.DotNetExecutable,
                expectedArguments,
                tempDir,
                It.IsAny<CancellationToken>(),
                null))
                .ReturnsAsync(expectedResult);

            var service = new LibraryBuildService(mockLogger.Object, mockProcessService.Object);

            try
            {
                // Act
                var result = await service.BuildSdkAsync(tempDir);

                // Assert
                Assert.That(result, Is.EqualTo(expectedResult));
                mockProcessService.Verify(x => x.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    expectedArguments,
                    tempDir,
                    It.IsAny<CancellationToken>(),
                    null), Times.Once);
            }
            finally
            {
                CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public async Task BuildSdkAsync_WithProjectInSrcDirectory_CallsProcessExecutionWithProjectPath()
        {
            // Arrange
            string tempDir = CreateTempDirectoryWithFiles("src/TestProject.csproj");
            string expectedProjectPath = Path.Combine(tempDir, "src", "TestProject.csproj");
            string expectedArguments = $"build \"{expectedProjectPath}\"";

            var mockLogger = CreateMockLogger();
            var mockProcessService = CreateMockProcessExecutionService();
            var expectedResult = Result<object>.Success(new object());
            
            mockProcessService.Setup(x => x.ExecuteAsync(
                SecureProcessConfiguration.DotNetExecutable,
                expectedArguments,
                tempDir,
                It.IsAny<CancellationToken>(),
                null))
                .ReturnsAsync(expectedResult);

            var service = new LibraryBuildService(mockLogger.Object, mockProcessService.Object);

            try
            {
                // Act
                var result = await service.BuildSdkAsync(tempDir);

                // Assert
                Assert.That(result, Is.EqualTo(expectedResult));
                mockProcessService.Verify(x => x.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    expectedArguments,
                    tempDir,
                    It.IsAny<CancellationToken>(),
                    null), Times.Once);
            }
            finally
            {
                CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public async Task BuildSdkAsync_WithMultipleProjectsInSrcDirectory_UsesFirstProject()
        {
            // Arrange
            string tempDir = CreateTempDirectoryWithFiles("src/First.csproj", "src/Second.csproj");
            string firstProjectPath = Path.Combine(tempDir, "src", "First.csproj");
            string secondProjectPath = Path.Combine(tempDir, "src", "Second.csproj");
            string expectedArguments = $"build \"{firstProjectPath}\"";

            var mockLogger = CreateMockLogger();
            var mockProcessService = CreateMockProcessExecutionService();
            var expectedResult = Result<object>.Success(new object());
            
            mockProcessService.Setup(x => x.ExecuteAsync(
                SecureProcessConfiguration.DotNetExecutable,
                expectedArguments,
                tempDir,
                It.IsAny<CancellationToken>(),
                null))
                .ReturnsAsync(expectedResult);

            var service = new LibraryBuildService(mockLogger.Object, mockProcessService.Object);

            try
            {
                // Act
                var result = await service.BuildSdkAsync(tempDir);

                // Assert
                Assert.That(result, Is.EqualTo(expectedResult));
                mockProcessService.Verify(x => x.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    expectedArguments,
                    tempDir,
                    It.IsAny<CancellationToken>(),
                    null), Times.Once);
            }
            finally
            {
                CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public async Task BuildSdkAsync_WithNestedProjectsInSrcDirectory_UsesFirstFound()
        {
            // Arrange
            string tempDir = CreateTempDirectoryWithFiles("src/nested/Project.csproj", "src/Project2.csproj");
            
            var mockLogger = CreateMockLogger();
            var mockProcessService = CreateMockProcessExecutionService();
            var expectedResult = Result<object>.Success(new object());
            
            mockProcessService.Setup(x => x.ExecuteAsync(
                SecureProcessConfiguration.DotNetExecutable,
                It.IsAny<string>(),
                tempDir,
                It.IsAny<CancellationToken>(),
                null))
                .ReturnsAsync(expectedResult);

            var service = new LibraryBuildService(mockLogger.Object, mockProcessService.Object);

            try
            {
                // Act
                var result = await service.BuildSdkAsync(tempDir);

                // Assert
                Assert.That(result, Is.EqualTo(expectedResult));
                mockProcessService.Verify(x => x.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    It.Is<string>(args => args.Contains("build") && args.Contains(".csproj")),
                    tempDir,
                    It.IsAny<CancellationToken>(),
                    null), Times.Once);
            }
            finally
            {
                CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public async Task BuildSdkAsync_WithProjectInRootDirectory_CallsProcessExecutionWithProjectPath()
        {
            // Arrange
            string tempDir = CreateTempDirectoryWithFiles("TestProject.csproj");
            string expectedProjectPath = Path.Combine(tempDir, "TestProject.csproj");
            string expectedArguments = $"build \"{expectedProjectPath}\"";

            var mockLogger = CreateMockLogger();
            var mockProcessService = CreateMockProcessExecutionService();
            var expectedResult = Result<object>.Success(new object());
            
            mockProcessService.Setup(x => x.ExecuteAsync(
                SecureProcessConfiguration.DotNetExecutable,
                expectedArguments,
                tempDir,
                It.IsAny<CancellationToken>(),
                null))
                .ReturnsAsync(expectedResult);

            var service = new LibraryBuildService(mockLogger.Object, mockProcessService.Object);

            try
            {
                // Act
                var result = await service.BuildSdkAsync(tempDir);

                // Assert
                Assert.That(result, Is.EqualTo(expectedResult));
                mockProcessService.Verify(x => x.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    expectedArguments,
                    tempDir,
                    It.IsAny<CancellationToken>(),
                    null), Times.Once);
            }
            finally
            {
                CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public async Task BuildSdkAsync_WithSolutionAndProjectFiles_PrefersSolution()
        {
            // Arrange
            string tempDir = CreateTempDirectoryWithFiles("TestSolution.sln", "TestProject.csproj", "src/AnotherProject.csproj");
            string expectedSolutionPath = Path.Combine(tempDir, "TestSolution.sln");
            string expectedArguments = $"build \"{expectedSolutionPath}\"";

            var mockLogger = CreateMockLogger();
            var mockProcessService = CreateMockProcessExecutionService();
            var expectedResult = Result<object>.Success(new object());
            
            mockProcessService.Setup(x => x.ExecuteAsync(
                SecureProcessConfiguration.DotNetExecutable,
                expectedArguments,
                tempDir,
                It.IsAny<CancellationToken>(),
                null))
                .ReturnsAsync(expectedResult);

            var service = new LibraryBuildService(mockLogger.Object, mockProcessService.Object);

            try
            {
                // Act
                var result = await service.BuildSdkAsync(tempDir);

                // Assert
                Assert.That(result, Is.EqualTo(expectedResult));
                mockProcessService.Verify(x => x.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    expectedArguments,
                    tempDir,
                    It.IsAny<CancellationToken>(),
                    null), Times.Once);
            }
            finally
            {
                CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public void BuildSdkAsync_WithNoSolutionOrProjectFiles_ThrowsInvalidOperationException()
        {
            // Arrange
            string tempDir = CreateTempDirectoryWithFiles("readme.txt", "config.json");

            var mockLogger = CreateMockLogger();
            var mockProcessService = CreateMockProcessExecutionService();
            var service = new LibraryBuildService(mockLogger.Object, mockProcessService.Object);

            try
            {
                // Act & Assert
                Assert.ThrowsAsync<InvalidOperationException>(() => service.BuildSdkAsync(tempDir));
            }
            finally
            {
                CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public void BuildSdkAsync_WithNonExistentDirectory_ThrowsInvalidOperationException()
        {
            // Arrange
            string nonExistentDir = Path.Combine(Path.GetTempPath(), "NonExistentDirectory_" + Guid.NewGuid());

            var mockLogger = CreateMockLogger();
            var mockProcessService = CreateMockProcessExecutionService();
            var service = new LibraryBuildService(mockLogger.Object, mockProcessService.Object);

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(() => service.BuildSdkAsync(nonExistentDir));
        }

        [Test]
        public async Task BuildSdkAsync_PassesCancellationToken()
        {
            // Arrange
            string tempDir = CreateTempDirectoryWithFiles("TestProject.sln");
            var cancellationToken = new CancellationToken();

            var mockLogger = CreateMockLogger();
            var mockProcessService = CreateMockProcessExecutionService();
            var expectedResult = Result<object>.Success(new object());
            
            mockProcessService.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                cancellationToken,
                null))
                .ReturnsAsync(expectedResult);

            var service = new LibraryBuildService(mockLogger.Object, mockProcessService.Object);

            try
            {
                // Act
                var result = await service.BuildSdkAsync(tempDir, cancellationToken);

                // Assert
                Assert.That(result, Is.EqualTo(expectedResult));
                mockProcessService.Verify(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    cancellationToken,
                    null), Times.Once);
            }
            finally
            {
                CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public async Task BuildSdkAsync_WithProcessExecutionFailure_ReturnsFailureResult()
        {
            // Arrange
            string tempDir = CreateTempDirectoryWithFiles("TestProject.sln");
            var processException = new ProcessExecutionException("Build failed", "dotnet", "build output", "build error", 1);
            var expectedResult = Result<object>.Failure(processException);

            var mockLogger = CreateMockLogger();
            var mockProcessService = CreateMockProcessExecutionService();
            
            mockProcessService.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                null))
                .ReturnsAsync(expectedResult);

            var service = new LibraryBuildService(mockLogger.Object, mockProcessService.Object);

            try
            {
                // Act
                var result = await service.BuildSdkAsync(tempDir);

                // Assert
                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ProcessException, Is.EqualTo(processException));
            }
            finally
            {
                CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public async Task BuildSdkAsync_ValidatesProcessArguments()
        {
            // Arrange
            string tempDir = CreateTempDirectoryWithFiles("TestProject.sln");
            string solutionPath = Path.Combine(tempDir, "TestProject.sln");
            string expectedArguments = $"build \"{solutionPath}\"";

            var mockLogger = CreateMockLogger();
            var mockProcessService = CreateMockProcessExecutionService();
            var expectedResult = Result<object>.Success(new object());
            
            mockProcessService.Setup(x => x.ExecuteAsync(
                SecureProcessConfiguration.DotNetExecutable,
                expectedArguments,
                tempDir,
                It.IsAny<CancellationToken>(),
                null))
                .ReturnsAsync(expectedResult);

            var service = new LibraryBuildService(mockLogger.Object, mockProcessService.Object);

            try
            {
                // Act
                var result = await service.BuildSdkAsync(tempDir);

                // Assert - Verify that InputValidator.ValidateProcessArguments would not throw
                Assert.DoesNotThrow(() => InputValidator.ValidateProcessArguments(expectedArguments));
                Assert.That(result, Is.EqualTo(expectedResult));
            }
            finally
            {
                CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public async Task BuildSdkAsync_UsesDotNetExecutableFromSecureProcessConfiguration()
        {
            // Arrange
            string tempDir = CreateTempDirectoryWithFiles("TestProject.sln");

            var mockLogger = CreateMockLogger();
            var mockProcessService = CreateMockProcessExecutionService();
            var expectedResult = Result<object>.Success(new object());
            
            mockProcessService.Setup(x => x.ExecuteAsync(
                SecureProcessConfiguration.DotNetExecutable,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                null))
                .ReturnsAsync(expectedResult);

            var service = new LibraryBuildService(mockLogger.Object, mockProcessService.Object);

            try
            {
                // Act
                var result = await service.BuildSdkAsync(tempDir);

                // Assert
                mockProcessService.Verify(x => x.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    null), Times.Once);
                
                Assert.That(SecureProcessConfiguration.DotNetExecutable, Is.EqualTo("dotnet"));
            }
            finally
            {
                CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public async Task BuildSdkAsync_LogsDebugInformation()
        {
            // Arrange
            string tempDir = CreateTempDirectoryWithFiles("TestProject.sln");

            var mockLogger = CreateMockLogger();
            var mockProcessService = CreateMockProcessExecutionService();
            var expectedResult = Result<object>.Success(new object());
            
            mockProcessService.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                null))
                .ReturnsAsync(expectedResult);

            var service = new LibraryBuildService(mockLogger.Object, mockProcessService.Object);

            try
            {
                // Act
                await service.BuildSdkAsync(tempDir);

                // Assert
                mockLogger.Verify(
                    x => x.Log(
                        LogLevel.Debug,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting library build in directory")),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.Once);

                mockLogger.Verify(
                    x => x.Log(
                        LogLevel.Debug,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Found solution file")),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.Once);
            }
            finally
            {
                CleanupTempDirectory(tempDir);
            }
        }

        #endregion
    }
}