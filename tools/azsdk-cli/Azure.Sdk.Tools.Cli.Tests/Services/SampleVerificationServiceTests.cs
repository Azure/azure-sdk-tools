// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services
{
    [TestFixture]
    public class SampleVerificationTests
    {
        private Mock<IDockerService> mockDockerService;
        private Mock<IMicroagentHostService> mockMicroagentService;
        private ILogger<SampleVerificationTests> logger;

        [SetUp]
        public void SetUp()
        {
            mockDockerService = new Mock<IDockerService>();
            mockMicroagentService = new Mock<IMicroagentHostService>();
            logger = NullLogger<SampleVerificationTests>.Instance;
        }

        [Test]
        public async Task VerifyAndFixSampleAsync_WithDockerNotAvailable_ReturnsFailureResult()
        {
            // Arrange
            var sample = new GeneratedSample("test.ts", "const x = 1;");
            mockDockerService.Setup(x => x.IsDockerAvailableAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await SampleVerification.VerifyAndFixSampleAsync(
                sample, "typescript", mockDockerService.Object, mockMicroagentService.Object,
                logger, "/path/to/package", "/path/to/repo", CancellationToken.None);

            // Assert
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Content, Is.EqualTo(sample.Content));
            Assert.That(result.AttemptsMade, Is.EqualTo(0));
            Assert.That(result.Attempts.Count, Is.EqualTo(1));
            Assert.That(result.Attempts[0].TypeCheckOutput, Contains.Substring("Docker is not available"));
        }

        [Test]
        public async Task VerifyAndFixSampleAsync_WithUnsupportedLanguage_ReturnsFailureResult()
        {
            // Arrange
            var sample = new GeneratedSample("test.unsupported", "const x = 1;");
            mockDockerService.Setup(x => x.IsDockerAvailableAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await SampleVerification.VerifyAndFixSampleAsync(
                sample, "unsupported", mockDockerService.Object, mockMicroagentService.Object,
                logger, "/path/to/package", "/path/to/repo", CancellationToken.None);

            // Assert
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Content, Is.EqualTo(sample.Content));
            Assert.That(result.AttemptsMade, Is.EqualTo(0));
            Assert.That(result.Attempts.Count, Is.EqualTo(1));
            Assert.That(result.Attempts[0].TypeCheckOutput, Contains.Substring("not supported for verification"));
        }
    }
}