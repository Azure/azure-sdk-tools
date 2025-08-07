using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers

{
    [TestFixture]
    internal class CodeOwnerHelperTests
    {
        private CodeOwnerHelper codeOwnerHelper;

        [SetUp]
        public void Setup()
        {
            codeOwnerHelper = new CodeOwnerHelper();
        }

        #region FindMatchingEntries Tests

        [Test]
        public void FindMatchingEntries_ByServiceName_ReturnsMatches()
        {
            // Arrange
            var entries = new List<CodeownersEntry>
            {
                new CodeownersEntry
                {
                    PathExpression = "sdk/servicebus/",
                    ServiceLabels = new List<string> { "Service Bus", "Messaging" },
                    SourceOwners = new List<string> { "@azure/servicebus-team" }
                },
                new CodeownersEntry
                {
                    PathExpression = "sdk/storage/",
                    ServiceLabels = new List<string> { "Storage" },
                    SourceOwners = new List<string> { "@azure/storage-team" }
                }
            };

            // Act
            var result = codeOwnerHelper.FindMatchingEntries(entries, "Service Bus");

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0]?.PathExpression, Is.EqualTo("sdk/servicebus/"));
        }

        [Test]
        public void FindMatchingEntries_NoMatches_ReturnsEmptyList()
        {
            // Arrange
            var entries = new List<CodeownersEntry>
            {
                new CodeownersEntry
                {
                    PathExpression = "sdk/servicebus/",
                    SourceOwners = new List<string> { "@azure/servicebus-team" }
                }
            };

            // Act
            var result = codeOwnerHelper.FindMatchingEntries(entries, "nonexistent");

            // Assert
            Assert.That(result.Count, Is.EqualTo(0));
        }

        #endregion

        #region ExtractUniqueOwners Tests

        [Test]
        public void ExtractUniqueOwners_AllOwnerTypes_ReturnsUniqueList()
        {
            // Arrange
            var entry = new CodeownersEntry
            {
                SourceOwners = new List<string> { "@user1", "@user2" },
                ServiceOwners = new List<string> { "@user2", "@user3" },
                AzureSdkOwners = new List<string> { "@user3", "@user4" }
            };

            // Act
            var result = codeOwnerHelper.ExtractUniqueOwners(entry);

            // Assert
            Assert.That(result.Count, Is.EqualTo(4));
            Assert.That(result, Contains.Item("@user1"));
            Assert.That(result, Contains.Item("@user2"));
            Assert.That(result, Contains.Item("@user3"));
            Assert.That(result, Contains.Item("@user4"));
        }

        [Test]
        public void ExtractUniqueOwners_EmptyLists_ReturnsEmptyList()
        {
            // Arrange
            var entry = new CodeownersEntry();

            // Act
            var result = codeOwnerHelper.ExtractUniqueOwners(entry);

            // Assert
            Assert.That(result.Count, Is.EqualTo(0));
        }

        #endregion

        #region addCodeownersEntryAtIndex Tests

        [Test]
        [TestCase("line1\nline2\nline3", "new entry", 1, new[] { "line1", "new entry", "line2", "line3" })]
        [TestCase("line1\nline2", "new entry", -1, new[] { "line1", "line2", "new entry" })]
        [TestCase("line1\nline2", "new entry", 0, new[] { "new entry", "line1", "line2" })]
        [TestCase("line1\nline2", "new entry", 2, new[] { "line1", "line2", "new entry" })]
        [TestCase("", "new entry", 0, new[] { "new entry", "" })]
        [TestCase("single line", "new entry", 0, new[] { "new entry", "single line" })]
        public void TestAddCodeownersEntryAtIndex(string content, string entry, int index, string[] expectedLines)
        {
            var result = codeOwnerHelper.addCodeownersEntryAtIndex(content, entry, index);
            var lines = result.Split('\n');
            
            Assert.That(lines.Length, Is.EqualTo(expectedLines.Length));
            for (int i = 0; i < expectedLines.Length; i++)
            {
                Assert.That(lines[i], Is.EqualTo(expectedLines[i]));
            }
        }

        #endregion

        #region formatCodeownersEntry Tests

        [Test]
        public void FormatCodeownersEntry_AllParameters_FormatsCorrectly()
        {
            // Arrange
            var path = "sdk/servicebus/";
            var serviceLabel = "Service Bus";
            var serviceOwners = new List<string> { "user1", "@user2" };
            var sourceOwners = new List<string> { "source1", "@source2" };

            // Act
            var result = codeOwnerHelper.formatCodeownersEntry(path, serviceLabel, serviceOwners, sourceOwners);

            // Assert
            var lines = result.Split('\n');
            Assert.That(lines[0], Is.EqualTo("# PRLabel: %Service Bus"));
            Assert.That(lines[1], Is.EqualTo("sdk/servicebus/                                                    @source1 @source2"));
            Assert.That(lines[2], Is.EqualTo(""));
            Assert.That(lines[3], Is.EqualTo("# ServiceLabel: %Service Bus"));
            Assert.That(lines[4], Is.EqualTo("# ServiceOwners: @user1 @user2"));
        }

        [Test]
        public void FormatCodeownersEntry_OnlyPath_FormatsMinimally()
        {
            // Arrange
            var path = "sdk/servicebus/";
            var sourceOwners = new List<string> { "source1" };

            // Act
            var result = codeOwnerHelper.formatCodeownersEntry(path, string.Empty, new List<string>(), sourceOwners);

            // Assert
            var lines = result.Split('\n');
            Assert.That(lines[0], Is.EqualTo("sdk/servicebus/                                                    @source1"));
            Assert.That(lines[1], Is.EqualTo(""));
        }

        #endregion

        #region findBlock Tests

        [Test]
        public void FindBlock_ServiceCategoryFound_ReturnsCorrectRange()
        {
            // Arrange
            var content = @"# Some header
# ######## Services ########
line1
line2
# ######## Other Section ########
line3";

            // Act
            var result = codeOwnerHelper.findBlock(content, "# ######## Services ########");

            // Assert
            Assert.That(result.StartLine, Is.EqualTo(1));
            Assert.That(result.EndLine, Is.EqualTo(4));
        }

        [Test]
        public void FindBlock_ServiceCategoryNotFound_ReturnsFullRange()
        {
            // Arrange
            var content = @"line1
line2
line3";

            // Act
            var result = codeOwnerHelper.findBlock(content, "# ######## Services ########");

            // Assert
            Assert.That(result.StartLine, Is.EqualTo(0));
            Assert.That(result.EndLine, Is.EqualTo(2));
        }

        #endregion

        #region CreateBranchName Tests

        [Test]
        [TestCase("add-codeowner", "Service Bus/path", "add-codeowner-service-bus-path")]
        [TestCase("update-entry", "Storage", "update-entry-storage")]
        [TestCase("fix-codeowners", "Communication - Chat", "fix-codeowners-communication-chat")]
        [TestCase("test-branch", "Azure.Storage.Blobs", "test-branch-azure-storage-blobs")]
        [TestCase("", "Service", "service")]
        [TestCase("prefix", "", "prefix")]
        [TestCase("add", "Special@Chars!", "add-specialchars")]
        public void TestCreateBranchName(string prefix, string identifier, string expected)
        {
            var actual = codeOwnerHelper.CreateBranchName(prefix, identifier);
            Assert.That(actual, Does.Match($"{expected}"));
        }

        #endregion

        #region NormalizeIdentifier Tests

        [Test]
        [TestCase("Test Service_Name/Path - Another", "test-service-name-path-another")]
        [TestCase("", "")]
        [TestCase("   ", "")]
        [TestCase("Storage@Special!Chars", "storagespecialchars")]
        [TestCase("Service Bus", "service-bus")]
        [TestCase("Communication - Chat", "communication-chat")]
        [TestCase("Azure.Storage.Blobs", "azure-storage-blobs")]
        [TestCase("Multiple   Spaces", "multiple---spaces")]
        [TestCase("123Numbers456", "123numbers456")]
        [TestCase("Path/With/Slashes", "path-with-slashes")]
        [TestCase("Under_Score_Text", "under-score-text")]
        public void TestNormalizeIdentifier(string input, string expected)
        {
            var actual = codeOwnerHelper.NormalizeIdentifier(input);
            Assert.That(actual, Is.EqualTo(expected));
        }

        #endregion

        #region findAlphabeticalInsertionPoint Tests

        [Test]
        public void FindAlphabeticalInsertionPoint_EmptyList_ReturnsOne()
        {
            // Arrange
            var entries = new List<CodeownersEntry>();

            // Act
            var result = codeOwnerHelper.findAlphabeticalInsertionPoint(entries, "sdk/test/");

            // Assert
            Assert.That(result, Is.EqualTo(1));
        }

        [Test]
        public void FindAlphabeticalInsertionPoint_ShouldInsertAtBeginning_ReturnsCorrectIndex()
        {
            // Arrange
            var entries = new List<CodeownersEntry>
            {
                new CodeownersEntry
                {
                    PathExpression = "sdk/storage/",
                    startLine = 5,
                    endLine = 7
                }
            };

            // Act
            var result = codeOwnerHelper.findAlphabeticalInsertionPoint(entries, "sdk/identity/");

            // Assert
            Assert.That(result, Is.EqualTo(5)); // Should insert before storage
        }

        [Test]
        public void FindAlphabeticalInsertionPoint_ShouldInsertAtEnd_ReturnsEndIndex()
        {
            // Arrange
            var entries = new List<CodeownersEntry>
            {
                new CodeownersEntry
                {
                    PathExpression = "sdk/identity/",
                    startLine = 5,
                    endLine = 7
                }
            };

            // Act
            var result = codeOwnerHelper.findAlphabeticalInsertionPoint(entries, "sdk/storage/");

            // Assert
            Assert.That(result, Is.EqualTo(8)); // Should insert after identity (endLine + 1)
        }

        [Test]
        public void FindAlphabeticalInsertionPoint_WithServiceLabel_FindsCorrectPosition()
        {
            // Arrange
            var entries = new List<CodeownersEntry>
            {
                new CodeownersEntry
                {
                    ServiceLabels = new List<string> { "Identity" },
                    startLine = 5,
                    endLine = 7
                },
                new CodeownersEntry
                {
                    ServiceLabels = new List<string> { "Storage" },
                    startLine = 10,
                    endLine = 12
                }
            };

            // Act
            var result = codeOwnerHelper.findAlphabeticalInsertionPoint(entries, serviceLabel: "Service Bus");

            // Assert
            Assert.That(result, Is.EqualTo(10));
        }

        [Test]
        public void FindAlphabeticalInsertionPoint_WithMergableServiceLabel_FindsCorrectPosition()
        {
            // Arrange
            var entries = new List<CodeownersEntry>
                {
                    new CodeownersEntry
                    {
                        ServiceLabels = new List<string> { "Identity" },
                        startLine = 5,
                        endLine = 7
                    },
                    new CodeownersEntry
                    {
                        PathExpression = "sdk/identity",
                        startLine = 8,
                        endLine = 10
                    },
                    new CodeownersEntry
                    {
                        ServiceLabels = new List<string> { "Storage" },
                        startLine = 11,
                        endLine = 13
                    }
                };

            // Act
            var result = codeOwnerHelper.findAlphabeticalInsertionPoint(entries, serviceLabel: "Service Bus");

            // Assert
            Assert.That(result, Is.EqualTo(11));
        }

        #endregion
    }
}
