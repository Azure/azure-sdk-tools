using System.Collections.Generic;
using System;

using NUnit.Framework;

using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers

{
    [TestFixture]
    internal class CodeownersEntryTests
    {

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
            Assert.That(result, Is.EqualTo("""
            # ServiceLabel: %Service Bus
            # PRLabel: %Service Bus
            sdk/servicebus/    @source1 @source2
            """));
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
            Assert.That(result, Is.EqualTo("""
            sdk/servicebus/    @source1 @source2
            """));
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
            Assert.That(result, Is.EqualTo("""
            # AzureSdkOwners: @sdkowner1 @sdkowner2
            # ServiceLabel: %Service Label
            """));
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
            Assert.That(result, Is.EqualTo("""
            # ServiceLabel: %Service Label
            # ServiceOwners: @serviceowner1 @serviceowner2
            """));
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
            Assert.That(result, Is.EqualTo("""
            # AzureSdkOwners: @sdkowner1 @sdkowner2
            # ServiceLabel: %Service Label
            # ServiceOwners: @serviceowner1 @serviceowner2
            """));
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
            Assert.That(result, Is.EqualTo("""
            # AzureSdkOwners: @sdkowner1 @sdkowner2
            sdk/servicebus/    @source1 @source2
            """));
        }

        #endregion
    }
}
