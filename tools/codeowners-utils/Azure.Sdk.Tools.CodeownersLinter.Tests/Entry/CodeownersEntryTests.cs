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
            var lines = result.Split('\n');
            Assert.That(lines[0], Is.EqualTo("# PRLabel: %Service Bus"));
            Assert.That(lines[1], Does.StartWith("sdk/servicebus/"));
            Assert.That(lines[1], Does.Contain("@source1 @source2"));
            Assert.That(lines[2], Is.EqualTo(""));
            Assert.That(lines[3], Is.EqualTo("# ServiceLabel: %Service Bus"));
            Assert.That(lines[4], Does.Contain("# ServiceOwners:"));
            Assert.That(lines[4], Does.Contain("@user1 @user2"));
        }

        #endregion

    }
}
