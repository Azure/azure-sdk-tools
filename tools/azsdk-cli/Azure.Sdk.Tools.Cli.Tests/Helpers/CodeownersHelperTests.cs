using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Moq;
using NUnit.Framework;

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.CodeownersUtils.Editing;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers

{
    [TestFixture]
    internal class CodeownersHelperTests
    {
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

        #region updateCodeownersEntry Tests

        [Test]
        // Adding codeowners tests.
        [TestCase(new string[] { "@test1", "@test2" }, new string[] { "@test3" }, true, new string[] { "@test1", "@test2" }, new string[] { "@test1", "@test2", "@test3" })]
        [TestCase(new string[] { "test1", "@test2" }, new string[] { }, true, new string[] { "@test1", "@test2" }, new string[] { "@test1", "@test2" })]
        [TestCase(new string[] { "Test1", "@test1" }, new string[] { }, true, new string[] { "@test1" }, new string[] { "@test1", "@test2" })]
        [TestCase(new string[] { }, new string[] { }, true, new string[] { "@test1" }, new string[] { "@test1", "@test2" })]
        // Removing codeowners tests.
        [TestCase(new string[] { "@test1", "@test2" }, new string[] { "@test3" }, false, new string[] { }, new string[] { "@test1", "@test2" })]
        [TestCase(new string[] { "test1", "@test2" }, new string[] { }, false, new string[] { }, new string[] { "@test1", "@test2" })]
        [TestCase(new string[] { "Test1", "@test1" }, new string[] {  }, false, new string[] {  }, new string[] { "@test1", "@test2" })]
        [TestCase(new string[] { "@test1" }, new string[] { "@test1", "@test2" }, false, new string[] { }, new string[] { })]
        [TestCase(new string[] { "@notfound" }, new string[] { "@notfound2" }, false, new string[] { "@test1" }, new string[] { "@test1", "@test2" })]
        public void UpdateCodeownersEntryTest(
            string[] serviceOwners,
            string[] sourceOwners,
            bool isAdding,
            string[] expectedServiceOwners,
            string[] expectedSourceOwners)
        {
            // Arrange
            CodeownersEntry existingEntry = new CodeownersEntry()
            {
                ServiceLabels = new List<string>() { "TestLabel" },
                ServiceOwners = new List<string>() { "@test1" },
                PathExpression = "testpath",
                SourceOwners = new List<string>() { "@test1", "@test2" }
            };

            // Act
            var result = CodeownersHelper.UpdateCodeownersEntry(
                existingEntry,
                serviceOwners.ToList(),
                sourceOwners.ToList(),
                isAdding);

            // Assert
            Assert.That(result.ServiceOwners.Count, Is.EqualTo(expectedServiceOwners.ToList().Count));
            Assert.That(result.SourceOwners.Count, Is.EqualTo(expectedSourceOwners.ToList().Count));
        }

        #endregion

        #region createCodeownersEntry

        [Test]
        // Basic creation with all fields
        [TestCase(new string[] { "@serviceowner" }, new string[] { "@sourceowner" }, false, new string[] { "@serviceowner" }, new string[] { "@sourceowner" })]
        // Creation with empty owners
        [TestCase(new string[] { }, new string[] { }, false, new string[] { }, new string[] { })]
        // Creation with duplicate owners (should not duplicate)
        [TestCase(new string[] { "@owner", "@owner" }, new string[] { "@owner", "@owner" }, false, new string[] { "@owner" }, new string[] { "@owner" })]
        // Creation with different casing (should normalize if logic applies)
        [TestCase(new string[] { "@Owner", "@owner2" }, new string[] { "@Owner", "@OWner2" }, false, new string[] { "@Owner", "@owner2" }, new string[] { "@Owner", "@OWner2" })]
        // Creation with special characters in owners and no @ symbol in input
        [TestCase(new string[] { "@owner-1" }, new string[] { "owner_2" }, false, new string[] { "@owner-1" }, new string[] { "@owner_2" })]
        // Creation with mgmt plane
        [TestCase(new string[] { "@owner" }, new string[] { "owner" }, true, new string[] { "@owner" }, new string[] { "@owner" })]
        public void CreateCodeownersEntryTest(
            string[] serviceOwners,
            string[] sourceOwners,
            bool isMgmtPlane,
            string[] expectedServiceOwners,
            string[] expectedSourceOwners)
        {
            // Act
            var entry = CodeownersHelper.CreateCodeownersEntry(
                "sdk/testpath/",
                "TestLabel",
                serviceOwners.ToList(),
                sourceOwners.ToList(),
                isMgmtPlane);

            // Assert
            Assert.That(entry.ServiceOwners, Is.EquivalentTo(expectedServiceOwners));
            Assert.That(entry.SourceOwners, Is.EquivalentTo(expectedSourceOwners));

            if (isMgmtPlane)
            {
                Assert.That(entry.ServiceLabels.Count, Is.EqualTo(2));
                Assert.That(entry.ServiceLabels, Does.Contain("Mgmt"));
            }
        }

        #endregion


        #region addCodeownersEntryToFile Tests

        [Test]
        public void AddCodeownersEntryToFile_InsertsWithProperSpacing_MiddleOfFile()
        {
            // Arrange: file with two real entries, insert a new one alphabetically in the middle
            var content = "# PRLabel: %Alpha\nsdk/alpha/ @alpha\n\n# PRLabel: %Omega\nsdk/omega/ @omega";
            var entries = new List<CodeownersEntry>
            {
                new CodeownersEntry { PathExpression = "sdk/alpha/", startLine = 1, endLine = 1 },
                new CodeownersEntry { PathExpression = "sdk/omega/", startLine = 4, endLine = 4 }
            };
            // New entry should be inserted between alpha and omega
            var newEntry = new CodeownersEntry
            {
                PathExpression = "sdk/middle/",
                ServiceLabels = new List<string> { "Middle Service" },
                SourceOwners = new List<string> { "@middleowner" },
                ServiceOwners = new List<string>(),
                AzureSdkOwners = new List<string>()
            };

            // Act
            var result = CodeownersHelper.AddCodeownersEntryToFile(entries, content, newEntry, false);

            // Assert: should have blank line before and after the inserted entry
            var lines = result.Split('\n');
            // Find the inserted entry line
            var entryIndex = Array.FindIndex(lines, l => l.Contains("sdk/middle/"));
            Assert.That(entryIndex, Is.GreaterThan(0), "Entry should not be at the top");
            Assert.That(string.IsNullOrWhiteSpace(lines[entryIndex - 1]), "Should be blank line before inserted entry");
            // Check for blank line after if not at end
            if (entryIndex + 1 < lines.Length)
            {
                Assert.That(string.IsNullOrWhiteSpace(lines[entryIndex + 1]), "Should be blank line after inserted entry");
            }
            // Entry content should be present
            Assert.That(result, Does.Contain("sdk/middle/"));
            Assert.That(result, Does.Contain("Middle Service"));
        }

        [Test]
        public void TestAddCodeownersEntryToFile_NewEntry()
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
            var result = CodeownersHelper.AddCodeownersEntryToFile(new List<CodeownersEntry>(), content, codeownersEntry, codeownersEntryExists);

            // Assert
            Assert.That(result, Does.Contain("line1"));
            Assert.That(result, Does.Contain("line2"));
            Assert.That(result, Does.Contain("line3"));
            Assert.That(result, Does.Contain("Test Service"));
            Assert.That(result, Does.Contain("sdk/test/"));
        }

        [Test]
        public void TestAddCodeownersEntryToFile_ExistingEntry()
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
            var result = CodeownersHelper.AddCodeownersEntryToFile(new List<CodeownersEntry>(), content, codeownersEntry, codeownersEntryExists);

            // Assert
            Assert.That(result, Does.Contain("Test Service"));
            Assert.That(result, Does.Contain("sdk/test/"));
        }

        [Test]
        public void AddCodeownersEntryToFile_InvalidReplacement_Throws()
        {
            var entry = new CodeownersEntry { startLine = 100, endLine = 200 };
            var ex = Assert.Throws<InvalidOperationException>(() =>
                CodeownersHelper.AddCodeownersEntryToFile(new List<CodeownersEntry>(), "line1\nline2", entry, true));
            Assert.That(ex.Message, Does.Contain("Invalid replacement point:"));
        }
        
        [Test]
        public void AddCodeownersEntryToFile_InvalidInsertion_AddsAtEndWithSpacing()
        {
            var entry = new CodeownersEntry
            {
                PathExpression = "sdk/test/",
                SourceOwners = new List<string>() { "owner" }
            };
            var content = "line1\nline2";
            var result = CodeownersHelper.AddCodeownersEntryToFile(new List<CodeownersEntry>(), content, entry, false);
            Assert.That(result, Does.Contain("sdk/test/"));
            Assert.That(result, Does.Contain("@owner"));
            Assert.That(result, Does.Contain("line1"));
            Assert.That(result, Does.Contain("line2"));
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
            var result = CodeownersHelper.FindBlock(content, "# ######## Services ########");

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
            var result = CodeownersHelper.FindBlock(content, "# ######## Services ########");

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
            var result = CodeownersHelper.FindAlphabeticalInsertionPoint(entries, newEntry);

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
            var result = CodeownersHelper.FindAlphabeticalInsertionPoint(entries, newEntry);

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
            var result = CodeownersHelper.FindAlphabeticalInsertionPoint(entries, newEntry);

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
            var result = CodeownersHelper.FindAlphabeticalInsertionPoint(entries, newEntry);

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
            var result = CodeownersHelper.FindAlphabeticalInsertionPoint(entries, newEntry);

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
            var result = CodeownersHelper.AddOwners(existingOwners, ownersToAdd);

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
            var result = CodeownersHelper.AddOwners(existingOwners, ownersToAdd);

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
            var result = CodeownersHelper.RemoveOwners(existingOwners, ownersToRemove);

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
            var result = CodeownersHelper.RemoveOwners(existingOwners, ownersToRemove);

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result, Contains.Item("@azure/team1"));
            Assert.That(result, Contains.Item("@azure/team2"));
        }

        #endregion
    }
}
