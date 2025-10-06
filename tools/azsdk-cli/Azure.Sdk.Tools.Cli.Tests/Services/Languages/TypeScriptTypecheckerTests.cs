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
        public void ParseImportedPackages_WithBasicImports_ReturnsExpectedPackages()
        {
            var code = @"
import { Client } from '@azure/storage-blob';
import * as fs from 'fs';
import './relative-file';
import { DefaultAzureCredential } from '@azure/identity';
const path = require('path');
";

            var excludedPackages = new HashSet<string>();
            var packages = TypeScriptTypechecker.ParseImportedPackages(code, excludedPackages);

            Assert.That(packages, Contains.Item("@azure/storage-blob"));
            Assert.That(packages, Contains.Item("@azure/identity"));
            Assert.That(packages, Contains.Item("fs"));
            Assert.That(packages, Contains.Item("path"));
            Assert.That(packages, Contains.Item("typescript"));
            Assert.That(packages, Contains.Item("@types/node"));

            // Should not contain relative imports
            Assert.That(packages, Does.Not.Contain("./relative-file"));
        }

        [Test]
        public void ParseImportedPackages_WithScopedPackages_ExtractsCorrectPackageRoot()
        {
            var code = @"
import { Client } from '@azure/storage-blob/types';
import { helper } from '@azure/core-auth/helpers/utils';
import { normalPkg } from 'normal-package/sub/path';
";

            var excludedPackages = new HashSet<string>();
            var packages = TypeScriptTypechecker.ParseImportedPackages(code, excludedPackages);

            Assert.That(packages, Contains.Item("@azure/storage-blob"));
            Assert.That(packages, Contains.Item("@azure/core-auth"));
            Assert.That(packages, Contains.Item("normal-package"));
        }

        [Test]
        public void ParseImportedPackages_WithExcludedPackages_FiltersCorrectly()
        {
            var code = @"
import { Client } from '@azure/storage-blob';
import { DefaultAzureCredential } from '@azure/identity';
";

            var excludedPackages = new HashSet<string> { "@azure/identity" };
            var packages = TypeScriptTypechecker.ParseImportedPackages(code, excludedPackages);

            Assert.That(packages, Contains.Item("@azure/storage-blob"));
            Assert.That(packages, Does.Not.Contain("@azure/identity"));
        }

        [Test]
        public void ParseImportedPackages_WithDynamicImports_ExtractsPackages()
        {
            var code = @"
const module = await import('@azure/storage-blob');
const fs = await import('fs/promises');
";

            var excludedPackages = new HashSet<string>();
            var packages = TypeScriptTypechecker.ParseImportedPackages(code, excludedPackages);

            Assert.That(packages, Contains.Item("@azure/storage-blob"));
            Assert.That(packages, Contains.Item("fs"));
        }

        [Test]
        public void ParseImportedPackages_WithBarePackageNames_ExtractsCorrectly()
        {
            var code = @"
import fs from 'fs';
import path from 'path';
import crypto from 'crypto';
import { Client } from '@azure/storage-blob';
";

            var excludedPackages = new HashSet<string>();
            var packages = TypeScriptTypechecker.ParseImportedPackages(code, excludedPackages);

            // Verify bare package names (Node.js built-ins) are extracted correctly
            Assert.That(packages, Contains.Item("fs"));
            Assert.That(packages, Contains.Item("path"));
            Assert.That(packages, Contains.Item("crypto"));
            Assert.That(packages, Contains.Item("@azure/storage-blob"));
        }

        [Test]
        public async Task TypecheckAsync_WithValidCode_ReturnsSuccess()
        {
            // Arrange
            var code = "const message: string = 'Hello, World!';";
            var parameters = new TypeCheckRequest(code, null, null);

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
            var parameters = new TypeCheckRequest(code, null, null);

            SetupFailedTypecheckInteraction();

            // Act
            var result = await typechecker.TypecheckAsync(parameters, CancellationToken.None);

            // Assert
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Output, Contains.Substring("tsc output:"));
        }

        [Test]
        public async Task TypecheckAsync_WithClientDist_InstallsClientPackage()
        {
            // Arrange
            var code = "const message: string = 'Hello, World!';";
            var clientDist = "/path/to/client.tgz";
            var parameters = new TypeCheckRequest(code, clientDist, null);

            SetupSuccessfulDockerInteractions();
            SetupClientDistInstallation(clientDist);

            // Create a mock temporary file to simulate the client dist file existing
            var tempFile = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tempFile, "mock client dist content");

                // Update the test to use the actual temp file path
                var parametersWithRealFile = new TypeCheckRequest(code, tempFile, null);

                // Act
                var result = await typechecker.TypecheckAsync(parametersWithRealFile, CancellationToken.None);

                // Assert
                Assert.That(result.Succeeded, Is.True);
                VerifyClientDistInstallation(tempFile);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        private void SetupSuccessfulDockerInteractions()
        {
            // Container creation and startup
            var createResult = new ProcessResult { ExitCode = 0 };
            createResult.AppendStdout("container-id");
            mockDockerService.Setup(x => x.CreateContainerAsync(
                "node:alpine", It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(createResult);

            mockDockerService.Setup(x => x.StartContainerAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0 });

            mockDockerService.Setup(x => x.IsContainerRunningAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false); // Force container creation

            // File operations
            mockDockerService.Setup(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args => args.Contains("mkdir")),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0 });

            mockDockerService.Setup(x => x.CopyToContainerAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0 });

            // npm install
            var npmInstallResult = new ProcessResult { ExitCode = 0 };
            npmInstallResult.AppendStdout("npm install completed");
            mockDockerService.Setup(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args => args.SequenceEqual(new[] { "npm", "install" })),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(npmInstallResult);

            // TypeScript compilation
            var tscResult = new ProcessResult { ExitCode = 0 };
            tscResult.AppendStdout("Compilation successful");
            mockDockerService.Setup(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args => args.SequenceEqual(new[] { "npm", "run", "typecheck" })),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tscResult);

            // Cleanup
            mockDockerService.Setup(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args => args.Contains("rm")),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0 });
        }

        private void SetupFailedTypecheckInteraction()
        {
            SetupSuccessfulDockerInteractions();

            // Override TypeScript compilation to fail
            var failedTscResult = new ProcessResult { ExitCode = 1 };
            failedTscResult.AppendStderr("Type error: string is not assignable to number");
            mockDockerService.Setup(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args => args.SequenceEqual(new[] { "npm", "run", "typecheck" })),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(failedTscResult);
        }

        private void SetupClientDistInstallation(string clientDist)
        {
            // Client dist installation
            var installResult = new ProcessResult { ExitCode = 0 };
            installResult.AppendStdout("Client package installed");
            mockDockerService.Setup(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args =>
                    args.Length >= 4 && args[0] == "npm" && args[1] == "install" && args[2] == "--no-save"),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(installResult);
        }

        private void VerifyDockerInteractionSequence()
        {
            // Verify container creation
            mockDockerService.Verify(x => x.CreateContainerAsync(
                "node:alpine", It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify npm install
            mockDockerService.Verify(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args => args.SequenceEqual(new[] { "npm", "install" })),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify TypeScript compilation
            mockDockerService.Verify(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args => args.SequenceEqual(new[] { "npm", "run", "typecheck" })),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify cleanup
            mockDockerService.Verify(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args => args.Contains("rm")),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        private void VerifyClientDistInstallation(string clientDist)
        {
            var fileName = Path.GetFileName(clientDist);

            // Verify client dist copy
            mockDockerService.Verify(x => x.CopyToContainerAsync(
                It.IsAny<string>(), clientDist, It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify client dist installation
            mockDockerService.Verify(x => x.RunCommandInContainerAsync(
                It.IsAny<string>(), It.Is<string[]>(args =>
                    args.Length >= 4 && args[0] == "npm" && args[1] == "install" && args[2] == "--no-save" && args[3] == fileName),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}