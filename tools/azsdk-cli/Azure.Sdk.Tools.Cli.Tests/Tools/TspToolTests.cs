// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools;
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;

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
                Assert.That(command.Subcommands, Has.Count.EqualTo(2));
            });

            Assert.Multiple(() =>
            {
                Assert.That(command.Subcommands.Any(c => c.Name == "convert-swagger"), Is.True);
                Assert.That(command.Subcommands.Any(c => c.Name == "init"), Is.True);
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
            Assert.That(result.ResponseError, Does.Contain("must be a non-empty path to a swagger README.md file"));
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
            Assert.That(result.ResponseError, Does.Contain("does not exist"));
        }

        [Test]
        public void Init_WithInvalidTemplate_ShouldReturnError()
        {
            var npxHelper = new Mock<INpxHelper>().Object;
            var logger = new Mock<ILogger<TypeSpecTool>>().Object;
            var outputService = new Mock<IOutputService>().Object;
            var tool = new TypeSpecTool(npxHelper, logger, outputService);

            var result = tool.InitTypeSpecProject(outputDirectory: @"C:\temp", template: "invalid-template", serviceNamespace: "MyService");

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccessful, Is.False);
                Assert.That(result.ResponseError, Does.Contain("Invalid --template"));
            });

        }

        [Test]
        public void Init_WithInvalidServiceNamespace_ShouldReturnError()
        {
            var npxHelper = new Mock<INpxHelper>().Object;
            var logger = new Mock<ILogger<TypeSpecTool>>().Object;
            var outputService = new Mock<IOutputService>().Object;
            var tool = new TypeSpecTool(npxHelper, logger, outputService);

            var result = tool.InitTypeSpecProject(outputDirectory: @"C:\temp", template: "azure-core", serviceNamespace: "");

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccessful, Is.False);
                Assert.That(result.ResponseError, Does.Contain("Invalid --service-namespace"));
            });
        }

        [Test]
        public void Init_WithNonExistentDirectory_ShouldReturnError()
        {
            var npxHelper = new Mock<INpxHelper>().Object;
            var logger = new Mock<ILogger<TypeSpecTool>>().Object;
            var outputService = new Mock<IOutputService>().Object;
            var tool = new TypeSpecTool(npxHelper, logger, outputService);

            var result = tool.InitTypeSpecProject(outputDirectory: @"C:\nonexistent", template: "azure-core", serviceNamespace: "MyService");

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccessful, Is.False);
                Assert.That(result.ResponseError, Does.Contain("Invalid --output-directory"));
            });
        }
    }
}
