// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.TspTool;
using Moq;
using Azure.Sdk.Tools.Cli.Helpers.Process;

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
        public void ConvertSwagger_WithInvalidFileExtension_ShouldReturnError()
        {
            // Arrange
            var npxHelper = new Mock<INpxHelper>().Object;
            var logger = new Mock<ILogger<TypeSpecTool>>().Object;
            var outputService = new Mock<IOutputService>().Object;
            var tool = new TypeSpecTool(npxHelper, logger, outputService);

            // Act
            var result = tool.ConvertSwagger("swagger.json", @"C:\temp", false, false);

            // Assert
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("must be a valid path to a swagger README.md file"));
        }

        [Test]
        public void ConvertSwagger_WithNonExistentFile_ShouldReturnError()
        {
            // Arrange
            var npxHelper = new Mock<INpxHelper>().Object;
            var logger = new Mock<ILogger<TypeSpecTool>>().Object;
            var outputService = new Mock<IOutputService>().Object;
            var tool = new TypeSpecTool(npxHelper, logger, outputService);

            // Act
            var result = tool.ConvertSwagger(@"C:\nonexistent\readme.md", @"C:\temp", false, false);

            // Assert
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("does not exist"));
        }
    }
}
