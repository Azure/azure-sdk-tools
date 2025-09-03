// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Tools.TypeSpec;
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    public class TspInitToolTests
    {
        [Test]
        public void GetCommand_ShouldReturnCommand()
        {
            // Arrange
            var npxHelper = new Mock<INpxHelper>().Object;
            var logger = new Mock<ILogger<TypeSpecInitTool>>().Object;
            var outputService = new Mock<IOutputHelper>().Object;
            var tool = new TypeSpecInitTool(npxHelper, CreateGitHelper(), logger, outputService);

            // Act
            var command = tool.GetCommand();

            Assert.Multiple(() =>
            {
                Assert.That(command.Name, Is.EqualTo("init"));
                Assert.That(command.Description, Does.Contain("Initialize a new TypeSpec project"));
            });
        }

        [Test]
        public async Task Init_WithInvalidTemplate_ShouldReturnError()
        {
            var npxHelper = new Mock<INpxHelper>().Object;
            var logger = new Mock<ILogger<TypeSpecInitTool>>().Object;
            var outputService = new Mock<IOutputHelper>().Object;
            var tool = new TypeSpecInitTool(npxHelper, CreateGitHelper(), logger, outputService);

            var result = await tool.InitTypeSpecProjectAsync(outputDirectory: "never-used", template: "invalid-template", serviceNamespace: "MyService", isCli: false);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccessful, Is.False);
                Assert.That(result.ResponseError, Does.Contain("Invalid --template"));
            });

        }

        [Test]
        public async Task Init_WithInvalidServiceNamespace_ShouldReturnError()
        {
            var npxHelper = new Mock<INpxHelper>().Object;
            var logger = new Mock<ILogger<TypeSpecInitTool>>().Object;
            var outputService = new Mock<IOutputHelper>().Object;
            var tool = new TypeSpecInitTool(npxHelper, CreateGitHelper(), logger, outputService);

            var result = await tool.InitTypeSpecProjectAsync(outputDirectory: "never-used", template: "azure-core", serviceNamespace: "", isCli: false);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccessful, Is.False);
                Assert.That(result.ResponseError, Does.Contain("Invalid --service-namespace"));
            });
        }

        [Test]
        public async Task Init_WithNonEmptyDirectory_ShouldReturnError()
        {
            var npxHelper = new Mock<INpxHelper>().Object;
            var logger = new Mock<ILogger<TypeSpecInitTool>>().Object;
            var outputService = new Mock<IOutputHelper>().Object;
            var tool = new TypeSpecInitTool(npxHelper, CreateGitHelper(), logger, outputService);
            var tempDir = Path.Combine(Path.GetTempPath(), $"test-nonexistent-{Guid.NewGuid()}");

            Directory.CreateDirectory(tempDir);

            try
            {
                await File.WriteAllTextAsync(Path.Join(tempDir, "somefile.txt"), "some file's contents");

                var result = await tool.InitTypeSpecProjectAsync(outputDirectory: tempDir, template: "azure-core", serviceNamespace: "MyService", isCli: false);

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccessful, Is.False);
                    Assert.That(result.ResponseError, Does.Contain("Invalid --output-directory"));
                });
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Test]
        public async Task Init_IncorrectGitRepo()
        {
            var npxHelper = new Mock<INpxHelper>().Object;
            var logger = new Mock<ILogger<TypeSpecInitTool>>().Object;
            var outputService = new Mock<IOutputHelper>().Object;
            var tool = new TypeSpecInitTool(npxHelper, CreateGitHelper("azure-sdk-for-php"), logger, outputService);
            var tempDir = Path.Combine(Path.GetTempPath(), $"test-nonexistent-{Guid.NewGuid()}");

            try
            {
                var result = await tool.InitTypeSpecProjectAsync(outputDirectory: tempDir, template: "azure-core", serviceNamespace: "MyService", isCli: false);

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccessful, Is.False);
                    Assert.That(result.ResponseError, Does.Contain("Invalid --output-directory"));
                    Assert.That(result.ResponseError, Does.Contain("must be within the azure-rest-api-specs repo"));
                });
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Test]
        public async Task Init_NotUnderSpecifications()
        {
            var npxHelper = new Mock<INpxHelper>().Object;
            var logger = new Mock<ILogger<TypeSpecInitTool>>().Object;
            var outputService = new Mock<IOutputHelper>().Object;
            var tool = new TypeSpecInitTool(npxHelper, CreateGitHelper("azure-rest-api-specs"), logger, outputService);
            var tempDir = Path.Combine(Path.GetTempPath(), $"test-nonexistent-{Guid.NewGuid()}");

            try
            {
                var result = await tool.InitTypeSpecProjectAsync(outputDirectory: tempDir, template: "azure-core", serviceNamespace: "MyService", isCli: false);

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccessful, Is.False);
                    Assert.That(result.ResponseError, Does.Contain("Invalid --output-directory"));
                    Assert.That(result.ResponseError, Does.Contain($"must be under <azure-rest-api-specs>{Path.DirectorySeparatorChar}specification"));
                });
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        private static IGitHelper CreateGitHelper(string repoName = "azure-rest-api-specs")
        {
            var mock = new Mock<IGitHelper>();
            mock.Setup(m => m.GetRepoName(It.IsAny<string>())).Returns(() => repoName);
            return mock.Object;
        }
    }
}
