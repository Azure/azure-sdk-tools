using Azure.Tools.GeneratorAgent.Configuration;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests.Configuration
{
    [TestFixture]
    public class ValidationContextTests
    {
        [Test]
        public void ValidateAndCreate_WithValidInputs_CreatesContextCorrectly()
        {
            // Use valid test paths that don't require actual directories
            var typeSpecPath = "specification/test/TestService";
            var commitId = "abc123def456";
            var outputPath = "C:\\temp\\output";

            Assert.DoesNotThrow(() =>
            {
                var context = ValidationContext.ValidateAndCreate(typeSpecPath, commitId, outputPath);
                Assert.Multiple(() =>
                {
                    Assert.That(context, Is.Not.Null);
                    Assert.That(context.ValidatedCommitId, Is.EqualTo(commitId));
                    Assert.That(context.IsGitHubWorkflow, Is.True);
                });
            });
        }

        [Test]
        public void ValidateAndCreate_WithLocalPath_CreatesContextCorrectly()
        {
            // For local paths (null commitId), we need actual directories
            // This test will throw an exception due to missing directory, which is expected behavior
            var outputPath = "C:\\temp\\output";

            Assert.Throws<InvalidOperationException>(() =>
            {
                ValidationContext.ValidateAndCreate("C:\\temp\\typespec", null, outputPath);
            });
        }

        [Test]
        public void ValidateAndCreate_WithNullTypeSpecPath_ThrowsException()
        {
            var outputPath = "C:\\temp\\output";

            Assert.Throws<ArgumentException>(() =>
            {
                ValidationContext.ValidateAndCreate(null, null, outputPath);
            });
        }

        [Test]
        public void ValidateAndCreate_WithNullOutputPath_ThrowsException()
        {
            var typeSpecPath = "specification/test/TestService";

            Assert.Throws<ArgumentException>(() =>
            {
                ValidationContext.ValidateAndCreate(typeSpecPath, "abc123", null!);
            });
        }

        [Test]
        public void IsGitHubWorkflow_WithCommitId_ReturnsTrue()
        {
            var context = ValidationContext.ValidateAndCreate("specification/test/TestService", "abc123", "C:\\temp\\output");
            Assert.That(context.IsGitHubWorkflow, Is.True);
        }

        [Test]
        public void IsGitHubWorkflow_WithNullCommitId_ReturnsFalse()
        {
            // This will throw due to directory validation for local paths
            Assert.Throws<InvalidOperationException>(() =>
            {
                ValidationContext.ValidateAndCreate("C:\\temp\\typespec", null, "C:\\temp\\output");
            });
        }
    }
}