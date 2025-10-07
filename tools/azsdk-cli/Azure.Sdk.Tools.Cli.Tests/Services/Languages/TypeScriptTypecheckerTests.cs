// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages
{
    [TestFixture]
    public class TypeScriptTypecheckerTests
    {
        private Mock<IDockerService> mockDockerService;
        private TypeScriptTypechecker typechecker;

        [SetUp]
        public void SetUp()
        {
            mockDockerService = new Mock<IDockerService>();
            typechecker = new TypeScriptTypechecker(mockDockerService.Object, NullLogger<TypeScriptTypechecker>.Instance);
        }

        [TearDown]
        public async Task TearDown()
        {
            if (typechecker != null)
            {
                // Setup mock for container removal during disposal
                mockDockerService.Setup(x => x.RemoveContainerAsync(
                    It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ProcessResult { ExitCode = 0 });

                await typechecker.DisposeAsync();
            }
        }

        [Test]
        public void Constructor_WithNullDockerService_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TypeScriptTypechecker(null!, NullLogger<TypeScriptTypechecker>.Instance));
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TypeScriptTypechecker(mockDockerService.Object, null!));
        }

        [Test]
        public async Task TypecheckAsync_WithValidCode_ReturnsSuccess()
        {
            // Arrange
            var code = "const message: string = 'Hello, World!';";
            var parameters = new TypeCheckRequest(
                code,
                "sample.ts",
                "/path/to/azure-sdk-for-js/sdk/keyvault/keyvault-keys",
                "/path/to/azure-sdk-for-js");

            SetupSuccessfulDockerInteractions();

            // Act
            var result = await typechecker.TypecheckAsync(parameters, CancellationToken.None);

            // Assert
            Assert.That(result.Succeeded, Is.True);
            VerifyDockerInteractionSequence();
        }

        [Test]
        public async Task TypecheckAsync_WithFailedTypeCheck_ReturnsFailure()
        {
            // Arrange
            var code = "const message: string = 123; // Type error";
            var parameters = new TypeCheckRequest(
                code,
                "sample.ts",
                "/path/to/azure-sdk-for-js/sdk/keyvault/keyvault-keys",
                "/path/to/azure-sdk-for-js");

            SetupFailedTypecheckInteraction();

            // Act
            var result = await typechecker.TypecheckAsync(parameters, CancellationToken.None);

            // Assert
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Output, Contains.Substring("Type error"));
        }

        private void SetupSuccessfulDockerInteractions()
        {
            // Container creation and startup
            var createResult = new ProcessResult { ExitCode = 0 };
            createResult.AppendStdout("container-id");
            mockDockerService.Setup(x => x.CreateContainerAsync(
                "node:20",
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), // environmentVars
                null, // workingDirectory
                It.IsAny<Dictionary<string, string>>(), // volumeMounts
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(createResult);

            mockDockerService.Setup(x => x.StartContainerAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0 });

            mockDockerService.Setup(x => x.IsContainerRunningAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false); // Force container creation

            // pnpm install globally
            mockDockerService.Setup(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args => args.SequenceEqual(new[] { "npm", "install", "-g", "pnpm" })),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0 });

            // turbo install globally
            mockDockerService.Setup(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args => args.SequenceEqual(new[] { "npm", "install", "-g", "turbo" })),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0 });

            // File operations
            mockDockerService.Setup(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args => args.Contains("mkdir")),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0 });

            mockDockerService.Setup(x => x.CopyToContainerAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0 });

            // pnpm install in monorepo
            var pnpmInstallResult = new ProcessResult { ExitCode = 0 };
            pnpmInstallResult.AppendStdout("pnpm install completed");
            mockDockerService.Setup(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args => args.SequenceEqual(new[] { "pnpm", "install" })),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(pnpmInstallResult);

            // turbo run build for package
            var turboBuildResult = new ProcessResult { ExitCode = 0 };
            turboBuildResult.AppendStdout("turbo build completed");
            mockDockerService.Setup(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args => args.SequenceEqual(new[] { "turbo", "run", "build" })),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(turboBuildResult);

            // build:samples
            var buildResult = new ProcessResult { ExitCode = 0 };
            buildResult.AppendStdout("Compilation successful");
            mockDockerService.Setup(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args => args.SequenceEqual(new[] { "pnpm", "run", "build:samples" })),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(buildResult);
        }

        private void SetupFailedTypecheckInteraction()
        {
            SetupSuccessfulDockerInteractions();

            // Override build:samples to fail
            var failedBuildResult = new ProcessResult { ExitCode = 1 };
            failedBuildResult.AppendStderr("Type error: string is not assignable to number");
            mockDockerService.Setup(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args => args.SequenceEqual(new[] { "pnpm", "run", "build:samples" })),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(failedBuildResult);
        }

        private void VerifyDockerInteractionSequence()
        {
            // Verify container creation with volume mounts and environment vars
            mockDockerService.Verify(x => x.CreateContainerAsync(
                "node:20",
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), // environmentVars
                null, // workingDirectory
                It.IsAny<Dictionary<string, string>>(), // volumeMounts
                It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify pnpm install globally
            mockDockerService.Verify(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args => args.SequenceEqual(new[] { "npm", "install", "-g", "pnpm" })),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify turbo install globally
            mockDockerService.Verify(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args => args.SequenceEqual(new[] { "npm", "install", "-g", "turbo" })),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify pnpm install in monorepo
            mockDockerService.Verify(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args => args.SequenceEqual(new[] { "pnpm", "install" })),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify turbo run build
            mockDockerService.Verify(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args => args.SequenceEqual(new[] { "turbo", "run", "build" })),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify build:samples
            mockDockerService.Verify(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args => args.SequenceEqual(new[] { "pnpm", "run", "build:samples" })),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}