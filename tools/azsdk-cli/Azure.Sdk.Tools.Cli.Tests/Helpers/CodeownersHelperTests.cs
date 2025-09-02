using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers

{
    [TestFixture]
    internal class CodeownersHelperTests
    {
        private CodeownersHelper codeownersHelper;

        [SetUp]
        public void Setup()
        {
            codeownersHelper = new CodeownersHelper();
        }

        #region FindMatchingEntries Tests

        [Test]
        [TestCase("Service Bus", 1, "sdk/servicebus/")]
        [TestCase("Storage", 1, "sdk/storage/")]
        [TestCase("Messaging", 1, "sdk/servicebus/")]
        [TestCase("NonExistentService", 0, "")]
        [TestCase("", 0, "")]
        [TestCase("   ", 0, "")]
        [TestCase("SERVICE BUS", 1, "sdk/servicebus/")] // Case insensitive exact match
        [TestCase("service bus", 1, "sdk/servicebus/")] // Case insensitive exact match
        [TestCase("ServiceBus", 1, "sdk/servicebus/")] // Space insensitive exact match
        [TestCase("SERVICEBUS", 1, "sdk/servicebus/")] // Case and space insensitive exact match
        [TestCase("Service", 0, "")] // Partial match should not work
        [TestCase("Bus", 0, "")] // Partial match should not work
        public void FindMatchingEntries_ByServiceName_TestCases(string serviceName, int expectedCount, string expectedPath)
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
                },
                new CodeownersEntry
                {
                    PathExpression = "sdk/communication/",
                    ServiceLabels = new List<string> { },
                    SourceOwners = new List<string> { "@azure/communication-team" }
                }
            };

            // Act
            var result = CodeownersHelper.FindMatchingEntries(entries, serviceLabel: serviceName);

            // Assert
            if (expectedCount == 0)
            {
                Assert.That(result, Is.Null, $"Service name '{serviceName}' should return no entries");
            }
            else
            {
                Assert.That(result, Is.Not.Null, $"Service name '{serviceName}' should return an entry");
                Assert.That(result?.PathExpression, Is.EqualTo(expectedPath), $"Service name '{serviceName}' should match path '{expectedPath}'");
            }
        }

        [Test]
        public void FindMatchingEntries_ByServiceName_NullInput_ReturnsEmpty()
        {
            // Arrange
            var entries = new List<CodeownersEntry>
            {
                new CodeownersEntry
                {
                    PathExpression = "sdk/servicebus/",
                    ServiceLabels = new List<string> { "Service Bus" },
                    SourceOwners = new List<string> { "@azure/servicebus-team" }
                }
            };

            // Act
            var result = CodeownersHelper.FindMatchingEntries(entries, serviceLabel: null!);

            // Assert
            Assert.That(result, Is.Null, "Null service name should return no entries");
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
            var result = CodeownersHelper.FindMatchingEntries(entries, serviceLabel: "nonexistent");

            // Assert
            Assert.That(result, Is.Null);
        }

        #endregion

        #region addCodeownersEntryAtIndex Tests

        [Test]
        public void TestAddCodeownersEntryAtIndex_NewEntry()
        {
            // Arrange
            var content = "line1\nline2\nline3";
            var codeownersEntry = new CodeownersEntry
            {
                PathExpression = "sdk/test/",
                ServiceLabels = new List<string> { "Test Service" },
                SourceOwners = new List<string> { "testowner" },
                ServiceOwners = new List<string>(),
                AzureSdkOwners = new List<string>()
            };
            var codeownersEntryExists = false;

            // Act
            var result = codeownersHelper.AddCodeownersEntryToFile(new List<CodeownersEntry>(), content, codeownersEntry, codeownersEntryExists);

            // Assert
            Assert.That(result, Does.Contain("line1"));
            Assert.That(result, Does.Contain("line2"));
            Assert.That(result, Does.Contain("line3"));
            Assert.That(result, Does.Contain("Test Service"));
            Assert.That(result, Does.Contain("sdk/test/"));
        }

        [Test]
        public void TestAddCodeownersEntryAtIndex_ExistingEntry()
        {
            // Arrange
            var content = "line1\nline2\nline3";
            var codeownersEntry = new CodeownersEntry
            {
                PathExpression = "sdk/test/",
                ServiceLabels = new List<string> { "Test Service" },
                SourceOwners = new List<string> { "testowner" },
                ServiceOwners = new List<string>(),
                AzureSdkOwners = new List<string>(),
                startLine = 1,
                endLine = 1
            };
            var codeownersEntryExists = true;

            // Act
            var result = codeownersHelper.AddCodeownersEntryToFile(new List<CodeownersEntry>(), content, codeownersEntry, codeownersEntryExists);

            // Assert
            Assert.That(result, Does.Contain("Test Service"));
            Assert.That(result, Does.Contain("sdk/test/"));
        }

        #endregion

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
            var result = codeownersHelper.FormatCodeownersEntry(codeownersEntry);

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
            var result = codeownersHelper.FindBlock(content, "# ######## Services ########");

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
            var result = codeownersHelper.FindBlock(content, "# ######## Services ########");

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
            var actual = CodeownersHelper.CreateBranchName(prefix, identifier);
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
            var actual = CodeownersHelper.NormalizeIdentifier(input);
            Assert.That(actual, Is.EqualTo(expected));
        }

        #endregion

        #region findAlphabeticalInsertionPoint Tests

        [Test]
        public void FindAlphabeticalInsertionPoint_EmptyList_ReturnsOne()
        {
            // Arrange
            var entries = new List<CodeownersEntry>();
            var newEntry = new CodeownersEntry { PathExpression = "sdk/test/" };

            // Act
            var result = codeownersHelper.FindAlphabeticalInsertionPoint(entries, newEntry);

            // Assert
            Assert.That(result.startLine, Is.EqualTo(1));
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
            var newEntry = new CodeownersEntry { PathExpression = "sdk/identity/" };

            // Act
            var result = codeownersHelper.FindAlphabeticalInsertionPoint(entries, newEntry);

            // Assert
            Assert.That(result.startLine, Is.EqualTo(5)); // Should insert before storage
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
            var newEntry = new CodeownersEntry { PathExpression = "sdk/storage/" };

            // Act
            var result = codeownersHelper.FindAlphabeticalInsertionPoint(entries, newEntry);

            // Assert
            Assert.That(result.startLine, Is.EqualTo(9));
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
            var newEntry = new CodeownersEntry { ServiceLabels = new List<string> { "Service Bus" } };

            // Act
            var result = codeownersHelper.FindAlphabeticalInsertionPoint(entries, newEntry);

            // Assert
            Assert.That(result.startLine, Is.EqualTo(10));
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
            var newEntry = new CodeownersEntry { ServiceLabels = new List<string> { "Service Bus" } };

            // Act
            var result = codeownersHelper.FindAlphabeticalInsertionPoint(entries, newEntry);

            // Assert
            Assert.That(result.startLine, Is.EqualTo(11));
        }

        #endregion

        #region AddUniqueOwners Tests

        [Test]
        public void AddUniqueOwners_NewOwners_AddsSuccessfully()
        {
            // Arrange
            var existingOwners = new List<string> { "@azure/team1", "@azure/team2" };
            var ownersToAdd = new List<string> { "azure/team3", "@azure/team4" };

            // Act
            var result = codeownersHelper.AddOwners(existingOwners, ownersToAdd);

            // Assert
            Assert.That(result.Count, Is.EqualTo(4));
            Assert.That(result, Contains.Item("@azure/team1"));
            Assert.That(result, Contains.Item("@azure/team2"));
            Assert.That(result, Contains.Item("@azure/team3"));
            Assert.That(result, Contains.Item("@azure/team4"));
        }

        [Test]
        public void AddUniqueOwners_DuplicateOwners_DoesNotAddDuplicates()
        {
            // Arrange
            var existingOwners = new List<string> { "@azure/team1", "@azure/team2" };
            var ownersToAdd = new List<string> { "azure/team1", "@azure/team3" };

            // Act
            var result = codeownersHelper.AddOwners(existingOwners, ownersToAdd);

            // Assert
            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result, Contains.Item("@azure/team1"));
            Assert.That(result, Contains.Item("@azure/team2"));
            Assert.That(result, Contains.Item("@azure/team3"));
        }

        #endregion

        #region RemoveOwners Tests

        [Test]
        public void RemoveOwners_ExistingOwners_RemovesSuccessfully()
        {
            // Arrange
            var existingOwners = new List<string> { "@azure/team1", "@azure/team2", "@azure/team3" };
            var ownersToRemove = new List<string> { "@azure/team1", "@azure/team3" };

            // Act
            var result = codeownersHelper.RemoveOwners(existingOwners, ownersToRemove);

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result, Contains.Item("@azure/team2"));
            Assert.That(result, Does.Not.Contain("@azure/team1"));
            Assert.That(result, Does.Not.Contain("@azure/team3"));
        }

        [Test]
        public void RemoveOwners_NonExistentOwners_DoesNotChangeList()
        {
            // Arrange
            var existingOwners = new List<string> { "@azure/team1", "@azure/team2" };
            var ownersToRemove = new List<string> { "@azure/team3", "@azure/team4" };

            // Act
            var result = codeownersHelper.RemoveOwners(existingOwners, ownersToRemove);

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result, Contains.Item("@azure/team1"));
            Assert.That(result, Contains.Item("@azure/team2"));
        }

        #endregion
    }
}
