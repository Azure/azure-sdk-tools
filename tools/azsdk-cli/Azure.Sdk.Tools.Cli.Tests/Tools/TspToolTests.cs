// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools;
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    public class TypeSpecToolTests
    {
        [Test]
        public void GetCommand_ShouldReturnCommandWithSubcommands()
        {
            // Arrange
            var npxHelper = new Mock<INpxHelper>().Object;
            var logger = new Mock<ILogger<TypeSpecTool>>().Object;
            var outputService = new Mock<IOutputService>().Object;
            var tool = new TypeSpecTool(npxHelper, logger, outputService);

            // Act
            var command = tool.GetCommand();

            Assert.Multiple(() =>
            {
                Assert.That(command.Name, Is.EqualTo("tsp"));
                Assert.That(command.Description, Does.Contain("Tools for initializing TypeSpec projects"));
                Assert.That(command.Subcommands, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task ConvertSwagger_WithInvalidFileExtension_ShouldReturnError()
        {
            // Arrange
            var npxHelper = new Mock<INpxHelper>().Object;
            var logger = new Mock<ILogger<TypeSpecTool>>().Object;
            var outputService = new Mock<IOutputService>().Object;
            var tool = new TypeSpecTool(npxHelper, logger, outputService);

            // Act
            var result = await tool.ConvertSwagger("swagger.json", @"C:\temp", false, false, CancellationToken.None);

            // Assert
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.ResponseError, Does.Contain("must be a non-empty path to a swagger README.md file"));
        }

        [Test]
        public async Task ConvertSwagger_WithNonExistentFile_ShouldReturnError()
        {
            // Arrange
            var npxHelper = new Mock<INpxHelper>().Object;
            var logger = new Mock<ILogger<TypeSpecTool>>().Object;
            var outputService = new Mock<IOutputService>().Object;
            var tool = new TypeSpecTool(npxHelper, logger, outputService);

            // Act
            var result = await tool.ConvertSwagger(@"C:\nonexistent\readme.md", @"C:\temp", false, false, CancellationToken.None);

            // Assert
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.ResponseError, Does.Contain("does not exist"));
        }
    }
}
