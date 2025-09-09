// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Tools.TypeSpec;
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;

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
            var docsService = new Mock<ITypeSpecDocsService>().Object;
            var tool = new TypeSpecInitTool(npxHelper, logger, outputService, docsService);

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
            var docsService = new Mock<ITypeSpecDocsService>().Object;
            var tool = new TypeSpecInitTool(npxHelper, logger, outputService, docsService);

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
            var docsService = new Mock<ITypeSpecDocsService>().Object;
            var tool = new TypeSpecInitTool(npxHelper, logger, outputService, docsService);

            var result = await tool.InitTypeSpecProjectAsync(outputDirectory: "never-used", template: "azure-core", serviceNamespace: "", isCli: false);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccessful, Is.False);
                Assert.That(result.ResponseError, Does.Contain("Invalid --service-namespace"));
            });
        }

        [Test]
        public async Task Init_WithNonExistentDirectory_ShouldReturnError()
        {
            var npxHelper = new Mock<INpxHelper>().Object;
            var logger = new Mock<ILogger<TypeSpecInitTool>>().Object;
            var outputService = new Mock<IOutputHelper>().Object;
            var docsService = new Mock<ITypeSpecDocsService>().Object;
            var tool = new TypeSpecInitTool(npxHelper, logger, outputService, docsService);

            var result = await tool.InitTypeSpecProjectAsync(outputDirectory: Path.Combine(Path.GetTempPath(), $"test-nonexistent-{Guid.NewGuid()}"), template: "azure-core", serviceNamespace: "MyService", isCli: false);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccessful, Is.False);
                Assert.That(result.ResponseError, Does.Contain("Invalid --output-directory"));
            });
        }
    }
}
