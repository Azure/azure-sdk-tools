using System.Collections.Generic;
using System;

using NUnit.Framework;

using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers

{
    [TestFixture]
    internal class CodeownersEntryTests
    {
        private static string NormalizeLineEndings(string input)
            => input.Replace("\r\n", "\n").Replace("\r", "\n");

        #region formatCodeownersEntry Tests

        [Test]
        public void FormatCodeownersEntry_AllParameters_FormatsCorrectly()
        {
            // Arrange
            var codeownersEntry = new CodeownersEntry
            {
                PathExpression = "sdk/servicebus/",
                ServiceLabels = new List<string> { "Service Bus" },
                PRLabels = new List<string> { "Service Bus" },
                ServiceOwners = new List<string> { "user1", "@user2" },
                SourceOwners = new List<string> { "source1", "@source2" },
                AzureSdkOwners = new List<string>()
            };

            // Act
            var result = codeownersEntry.FormatCodeownersEntry();

            // Assert
            var expected = """
                # ServiceLabel: %Service Bus
                # PRLabel: %Service Bus
                sdk/servicebus/    @source1 @source2
                """;
            Assert.That(NormalizeLineEndings(result), Is.EqualTo(NormalizeLineEndings(expected)));
        }

        [Test]
        public void FormatCodeownersEntry_NoLabels()
        {
            // Arrange
            var codeownersEntry = new CodeownersEntry
            {
                PathExpression = "sdk/servicebus/",
                ServiceLabels = new List<string>(),
                PRLabels = new List<string>(),
                SourceOwners = new List<string> { "source1", "@source2" },
                AzureSdkOwners = new List<string>()
            };

            // Act
            var result = codeownersEntry.FormatCodeownersEntry();

            // Assert
            var expected = """
                sdk/servicebus/    @source1 @source2
                """;
            Assert.That(NormalizeLineEndings(result), Is.EqualTo(NormalizeLineEndings(expected)));
        }

        [Test]
        public void FormatCodeownersEntry_AzureSdkOwners()
        {
            // Arrange
            var codeownersEntry = new CodeownersEntry
            {
                ServiceLabels = ["Service Label"],
                AzureSdkOwners = ["sdkowner1", "sdkowner2"],
            };

            // Act
            var result = codeownersEntry.FormatCodeownersEntry();

            // Assert
            var expected = """
                # AzureSdkOwners: @sdkowner1 @sdkowner2
                # ServiceLabel: %Service Label
                """;
            Assert.That(NormalizeLineEndings(result), Is.EqualTo(NormalizeLineEndings(expected)));
        }

        [Test]
        public void FormatCodeownersEntry_ServiceLabelsAndOwners()
        {
            // Arrange
            var codeownersEntry = new CodeownersEntry
            {
                ServiceLabels = ["Service Label"],
                ServiceOwners = ["serviceowner1", "serviceowner2"],
            };

            // Act
            var result = codeownersEntry.FormatCodeownersEntry();

            // Assert
            var expected = """
                # ServiceLabel: %Service Label
                # ServiceOwners: @serviceowner1 @serviceowner2
                """;
            Assert.That(NormalizeLineEndings(result), Is.EqualTo(NormalizeLineEndings(expected)));
        }

        [Test]
        public void FormatCodeownersEntry_ServiceLabelsServiceOwnersAzureSdkOwners()
        {
            // Arrange
            var codeownersEntry = new CodeownersEntry
            {
                ServiceLabels = ["Service Label"],
                ServiceOwners = ["serviceowner1", "serviceowner2"],
                AzureSdkOwners = ["sdkowner1", "sdkowner2"],
            };

            // Act
            var result = codeownersEntry.FormatCodeownersEntry();

            // Assert
            var expected = """
                # AzureSdkOwners: @sdkowner1 @sdkowner2
                # ServiceLabel: %Service Label
                # ServiceOwners: @serviceowner1 @serviceowner2
                """;
            Assert.That(NormalizeLineEndings(result), Is.EqualTo(NormalizeLineEndings(expected)));
        }

        [Test]
        public void FormatCodeownersEntry_AzureSdkOwnersWithPath()
        {
            // Arrange
            var codeownersEntry = new CodeownersEntry
            {
                PathExpression = "sdk/servicebus/",
                SourceOwners = ["source1", "@source2"],
                AzureSdkOwners = ["sdkowner1", "sdkowner2"],
            };

            // Act
            var result = codeownersEntry.FormatCodeownersEntry();

            // Assert
            var expected = """
                # AzureSdkOwners: @sdkowner1 @sdkowner2
                sdk/servicebus/    @source1 @source2
                """;
            Assert.That(NormalizeLineEndings(result), Is.EqualTo(NormalizeLineEndings(expected)));
        }

        #endregion
    }
}
