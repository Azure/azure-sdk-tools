// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Tools.TypeSpec;
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    public class TspConvertToolTests
    {
        [Test]
        public void GetCommand_ShouldReturnCommand()
        {
            // Arrange
            var npxHelper = new Mock<INpxHelper>().Object;
            var logger = new Mock<ILogger<TypeSpecConvertTool>>().Object;
            var outputService = new Mock<IOutputHelper>().Object;
            var tool = new TypeSpecConvertTool(npxHelper, logger, outputService);

            // Act
            var command = tool.GetCommand();

            Assert.Multiple(() =>
            {
                Assert.That(command.Name, Is.EqualTo("convert-swagger"));
                Assert.That(command.Description, Does.Contain("Convert an existing Azure service swagger definition to a TypeSpec project"));
            });
        }
        [Test]
        public async Task ConvertSwagger_WithInvalidFileExtension_ShouldReturnError()
        {
            // Arrange
            var npxHelper = new Mock<INpxHelper>().Object;
            var logger = new Mock<ILogger<TypeSpecConvertTool>>().Object;
            var outputService = new Mock<IOutputHelper>().Object;
            var tool = new TypeSpecConvertTool(npxHelper, logger, outputService);

            // Act
            var result = await tool.ConvertSwaggerAsync("swagger.json", @"C:\temp", false, false, false, CancellationToken.None);

            // Assert
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.ResponseError, Does.Contain("must be a non-empty path to a swagger README.md file"));
        }

        [Test]
        public async Task ConvertSwagger_WithNonExistentFile_ShouldReturnError()
        {
            // Arrange
            var npxHelper = new Mock<INpxHelper>().Object;
            var logger = new Mock<ILogger<TypeSpecConvertTool>>().Object;
            var outputService = new Mock<IOutputHelper>().Object;
            var tool = new TypeSpecConvertTool(npxHelper, logger, outputService);

            // Act
            var result = await tool.ConvertSwaggerAsync(@"C:\nonexistent\readme.md", @"C:\temp", false, false, false, CancellationToken.None);
            Assert.That(result.IsSuccessful, Is.False);
        }
    }
}
