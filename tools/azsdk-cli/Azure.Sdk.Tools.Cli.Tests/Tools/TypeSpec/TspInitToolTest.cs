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
            var tool = new TypeSpecInitTool(npxHelper, CreateTypeSpecHelper(), logger);

            // Act
            var command = tool.GetCommandInstances().First();

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
            var tool = new TypeSpecInitTool(npxHelper, CreateTypeSpecHelper(), logger);

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
            var tool = new TypeSpecInitTool(npxHelper, CreateTypeSpecHelper(), logger);

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
            var tool = new TypeSpecInitTool(npxHelper, CreateTypeSpecHelper(), logger);
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
            var tool = new TypeSpecInitTool(npxHelper, CreateTypeSpecHelper(false), logger);
            var tempDir = Path.Combine(Path.GetTempPath(), $"test-nonexistent-{Guid.NewGuid()}");

            try
            {
                var result = await tool.InitTypeSpecProjectAsync(outputDirectory: tempDir, template: "azure-core", serviceNamespace: "MyService", isCli: false);

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccessful, Is.False);
                    Assert.That(result.ResponseError, Is.EqualTo($"Failed: Invalid --output-directory, must be under the azure-rest-api-specs or azure-rest-api-specs-pr repo"
));
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
            var tool = new TypeSpecInitTool(npxHelper, CreateTypeSpecHelper(true), logger);
            var tempDir = Path.Combine(Path.GetTempPath(), $"test-nonexistent-{Guid.NewGuid()}");

            try
            {
                var result = await tool.InitTypeSpecProjectAsync(outputDirectory: tempDir, template: "azure-core", serviceNamespace: "MyService", isCli: false);

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccessful, Is.False);
                    Assert.That(result.ResponseError, Does.Contain("Invalid --output-directory"));
                    Assert.That(result.ResponseError, Is.EqualTo($"Failed: Invalid --output-directory, must be under <azure-rest-api-specs or azure-rest-api-specs-pr>{Path.DirectorySeparatorChar}specification"));
                });
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        private static ITypeSpecHelper CreateTypeSpecHelper(bool isSpecRepo = false)
        {
            var mock = new Mock<ITypeSpecHelper>();
            mock.Setup(m => m.IsRepoPathForSpecRepo(It.IsAny<string>())).Returns(isSpecRepo);
            return mock.Object;
        }
    }
}
