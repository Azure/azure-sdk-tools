// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Tools.TypeSpec;
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.TypeSpec
{
    public class TspConvertToolTests
    {
        [Test]
        public async Task ConvertSwagger_WithInvalidFileExtension_ShouldReturnError()
        {
            // Arrange
            var logger = new Mock<ILogger<TypeSpecConvertTool>>().Object;
            var tspHelper = new Mock<ITspClientHelper>().Object;
            var fileHelper = new Mock<IFileHelper>().Object;
            var tool = new TypeSpecConvertTool(logger, tspHelper, fileHelper);

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
            var logger = new Mock<ILogger<TypeSpecConvertTool>>().Object;
            var tspHelper = new Mock<ITspClientHelper>().Object;
            var fileHelper = new Mock<IFileHelper>().Object;
            var tool = new TypeSpecConvertTool(logger, tspHelper, fileHelper);

            // Act
            var result = await tool.ConvertSwaggerAsync(@"C:\nonexistent\readme.md", @"C:\temp", false, false, false, CancellationToken.None);
            Assert.That(result.IsSuccessful, Is.False);
        }
    }
}
