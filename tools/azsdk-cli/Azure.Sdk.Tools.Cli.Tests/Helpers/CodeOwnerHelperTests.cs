using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;

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
            Assert.That(result[0].PathExpression, Is.EqualTo("sdk/servicebus/"));
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
        public void AddCodeownersEntryAtIndex_ValidIndex_InsertsAtCorrectPosition()
        {
            // Arrange
            var content = "line1\nline2\nline3";
            var entry = "new entry";
            var index = 1;

            // Act
            var result = codeOwnerHelper.addCodeownersEntryAtIndex(content, entry, index);

            // Assert
            var lines = result.Split('\n');
            Assert.That(lines.Length, Is.EqualTo(4));
            Assert.That(lines[0], Is.EqualTo("line1"));
            Assert.That(lines[1], Is.EqualTo("new entry"));
            Assert.That(lines[2], Is.EqualTo("line2"));
            Assert.That(lines[3], Is.EqualTo("line3"));
        }

        [Test]
        public void AddCodeownersEntryAtIndex_InvalidIndex_AppendsEntry()
        {
            // Arrange
            var content = "line1\nline2";
            var entry = "new entry";
            var index = -1;

            // Act
            var result = codeOwnerHelper.addCodeownersEntryAtIndex(content, entry, index);

            // Assert
            var lines = result.Split('\n');
            Assert.That(lines.Length, Is.EqualTo(3));
            Assert.That(lines[2], Is.EqualTo("new entry"));
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
            Assert.That(lines[1], Is.EqualTo("sdk/servicebus/           @source1 @source2"));
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
            Assert.That(lines[0], Is.EqualTo("sdk/servicebus/           @source1"));
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
        public void CreateBranchName_ValidInputs_ReturnsFormattedName()
        {
            // Arrange
            var prefix = "add-codeowner";
            var identifier = "Service Bus/path";

            // Act
            var result = codeOwnerHelper.CreateBranchName(prefix, identifier);

            // Assert
            Assert.That(result, Does.StartWith("add-codeowner-service-bus-path"));
            Assert.That(result, Does.Match(@"add-codeowner-service-bus-path-\d{8}-\d{6}"));
        }

        #endregion

        #region NormalizeIdentifier Tests

        [Test]
        public void NormalizeIdentifier_SpecialCharacters_ReplacesWithDashes()
        {
            // Arrange
            var input = "Test Service_Name/Path - Another";

            // Act
            var result = codeOwnerHelper.NormalizeIdentifier(input);

            // Assert
            Assert.That(result, Is.EqualTo("test-service-name-path-another"));
        }

        [Test]
        public void NormalizeIdentifier_EmptyString_ReturnsEmpty()
        {
            // Arrange
            var input = "";

            // Act
            var result = codeOwnerHelper.NormalizeIdentifier(input);

            // Assert
            Assert.That(result, Is.EqualTo(""));
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
