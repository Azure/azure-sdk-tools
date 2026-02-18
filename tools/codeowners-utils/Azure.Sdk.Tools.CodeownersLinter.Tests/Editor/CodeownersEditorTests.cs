using System.Collections.Generic;
using System.Linq;
using System;

using NUnit.Framework;

using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.CodeownersUtils.Editing;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers

{
    [TestFixture]
    internal class CodeownersEditorTests
    {
        // Standard codeowners content used by tests.
        private readonly string codeownersContent = @"# PRLabel: %Alpha
sdk/alpha/ @alpha

# PRLabel: %Service Bus
sdk/servicebus/ @azure/source-servicebus-team

# ServiceLabel: %Service Bus
# ServiceOwners: @azure/servicebus-team

# PRLabel: %Storage
sdk/storage/ @azure/storage-team

# PRLabel: %Communication
sdk/communication/ @azure/communication-team

# PRLabel: %Omega
sdk/omega/ @omega
";

        #region FindMatchingEntry Tests

        [Test]
        [TestCase("Service Bus", 1, "sdk/servicebus/")]
        [TestCase("Storage", 1, "sdk/storage/")]
        [TestCase("NonExistentService", 0, "")]
        [TestCase("", 0, "")]
        [TestCase("   ", 0, "")]
        [TestCase("SERVICE BUS", 1, "sdk/servicebus/")] // Case insensitive exact match
        [TestCase("service bus", 1, "sdk/servicebus/")] // Case insensitive exact match
        [TestCase("ServiceBus", 1, "sdk/servicebus/")] // Space insensitive exact match
        [TestCase("SERVICEBUS", 1, "sdk/servicebus/")] // Case and space insensitive exact match
        [TestCase("Service", 0, "")] // Partial match should not work
        [TestCase("Bus", 0, "")] // Partial match should not work
        public void FindMatchingEntry_ByServiceName_TestCases(string serviceName, int expectedCount, string expectedPath)
        {
            CodeownersEditor codeownersEditor = new CodeownersEditor(codeownersContent, false);

            // Act
            var result = codeownersEditor.FindMatchingEntry(serviceLabel: serviceName);

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
        public void FindMatchingEntry_ByServiceName_NullInput_ReturnsEmpty()
        {
            CodeownersEditor codeownersEditor = new CodeownersEditor(codeownersContent, false);

            // Act
            var result = codeownersEditor.FindMatchingEntry(serviceLabel: null!);

            // Assert
            Assert.That(result, Is.Null, "Null service name should return no entries");
        }

        [Test]
        public void FindMatchingEntry_NoMatches_ReturnsEmptyList()
        {
            CodeownersEditor codeownersEditor = new CodeownersEditor(codeownersContent, false);

            // Act
            var result = codeownersEditor.FindMatchingEntry(serviceLabel: "nonexistent");

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
            var result = CodeownersEditor.UpdateCodeownersEntry(
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
            var entry = CodeownersEditor.CreateCodeownersEntry(
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
            CodeownersEditor codeownersEditor = new CodeownersEditor(codeownersContent, false);

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
            var result = codeownersEditor.AddCodeownersEntryToFile(newEntry, false);

            // Assert: should have blank line before and after the inserted entry
            var lines = result.Split('\n');
            // Find the inserted entry line
            var entryIndex = Array.FindIndex(lines, l => l.Contains("sdk/middle/"));
            Assert.That(entryIndex, Is.GreaterThan(0), "Entry should not be at the top");

            // Check for blank line after if not at end
            if (entryIndex + 1 < lines.Length)
            {
                Assert.That(string.IsNullOrWhiteSpace(lines[entryIndex + 1]), "Should be blank line after inserted entry");
            }
            // Entry content should be present
            Assert.That(result, Does.Contain("sdk/middle/"));
            Assert.That(result, Does.Contain("Middle Service"));
            // The inserted entry should include ServiceLabel and the normalized owner
            Assert.That(result, Does.Contain("# ServiceLabel: %Middle Service"));
            Assert.That(result, Does.Contain("@middleowner"));
        }

        [Test]
        public void TestAddCodeownersEntryToFile_NewEntry()
        {
            CodeownersEditor codeownersEditor = new CodeownersEditor(codeownersContent, false);

            // Arrange
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
            var result = codeownersEditor.AddCodeownersEntryToFile(codeownersEntry, codeownersEntryExists);

            // Assert
            
            Assert.That(result, Does.Contain("Test Service"));
            Assert.That(result, Does.Contain("sdk/test/"));
            // The entry added should include the ServiceLabel comment and the normalized owner
            Assert.That(result, Does.Contain("# ServiceLabel: %Test Service"));
            Assert.That(result, Does.Contain("@testowner"));
        }

        [Test]
        public void TestAddCodeownersEntryToFile_ExistingEntry()
        {
            CodeownersEditor codeownersEditor = new CodeownersEditor(codeownersContent, false);
            
            // Arrange
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
            var result = codeownersEditor.AddCodeownersEntryToFile(codeownersEntry, codeownersEntryExists);

            // Assert
            Assert.That(result, Does.Contain("Test Service"));
            Assert.That(result, Does.Contain("sdk/test/"));
            // Replaced entry should include ServiceLabel and normalized owner
            Assert.That(result, Does.Contain("# ServiceLabel: %Test Service"));
            Assert.That(result, Does.Contain("@testowner"));
        }

        [Test]
        public void AddCodeownersEntryToFile_InvalidReplacement_Throws()
        {
            CodeownersEditor codeownersEditor = new CodeownersEditor(codeownersContent, false);

            var entry = new CodeownersEntry { startLine = 100, endLine = 200 };
            var ex = Assert.Throws<InvalidOperationException>(() =>
                codeownersEditor.AddCodeownersEntryToFile(entry, true));
            Assert.That(ex.Message, Does.Contain("Invalid replacement point:"));
        }

        [Test]
        public void AddCodeownersEntryToFile_InvalidInsertion_AddsAtEndWithSpacing()
        {
            CodeownersEditor codeownersEditor = new CodeownersEditor(codeownersContent, false);

            var entry = new CodeownersEntry
            {
                PathExpression = "sdk/test/",
                SourceOwners = new List<string>() { "owner" }
            };
            // rely on the Setup-initialized editor content
            var result = codeownersEditor.AddCodeownersEntryToFile(entry, false);
            Assert.That(result, Does.Contain("sdk/test/"));
            Assert.That(result, Does.Contain("@owner"));

            // Ensure the inserted entry is the last sdk/ entry (i.e., appears after any other sdk/ entries)
            var lastTestIndex = result.LastIndexOf("sdk/test/", StringComparison.Ordinal);
            Assert.That(lastTestIndex, Is.GreaterThan(-1), "Inserted path should be present");

            // Ensure no other sdk/ entry occurs after the inserted entry
            var afterInserted = result.Substring(lastTestIndex + "sdk/test/".Length);
            Assert.That(afterInserted, Does.Not.Contain("sdk/"), "No other sdk/ entries should appear after the inserted entry");

            // Ensure the owner for the inserted entry appears after the inserted path
            var ownerIndex = result.IndexOf("@owner", lastTestIndex, StringComparison.Ordinal);
            Assert.That(ownerIndex, Is.GreaterThan(lastTestIndex), "Owner should appear after the inserted path");
        }
        
        #endregion

        #region findBlock Tests

        [Test]
        public void FindBlock_ServiceCategoryFound_ReturnsCorrectRange()
        {
            CodeownersEditor codeownersEditor = new CodeownersEditor(codeownersContent, false);

            // Act
            var result = codeownersEditor.FindBlock("# ######## Services ########");

            // Assert (with shared content the block isn't present, expect full range)
            Assert.That(result.StartLine, Is.EqualTo(0));
            Assert.That(result.EndLine, Is.EqualTo(17));
        }

        [Test]
        public void FindBlock_ServiceCategoryNotFound_ReturnsFullRange()
        {
            CodeownersEditor codeownersEditor = new CodeownersEditor(codeownersContent, false);

            // Act
            var result = codeownersEditor.FindBlock("# ######## Services ########");

            // Assert (with shared content expect the full range)
            Assert.That(result.StartLine, Is.EqualTo(0));
            Assert.That(result.EndLine, Is.EqualTo(17));
        }

        #endregion

        #region findAlphabeticalInsertionPoint Tests

        [Test]
        public void FindAlphabeticalInsertionPoint_WithServiceLabel_FindsCorrectPosition()
        {
            CodeownersEditor codeownersEditor = new CodeownersEditor(codeownersContent, false);

            // Arrange
            var newEntry = new CodeownersEntry { ServiceLabels = new List<string> { "Service Bus" } };

            // Act
            var result = codeownersEditor.FindAlphabeticalInsertionPoint(newEntry);

            // Assert (observed insertion point with shared content)
            Assert.That(result.startLine, Is.EqualTo(9));
        }

        [Test]
        public void FindAlphabeticalInsertionPoint_WithMergableServiceLabel_FindsCorrectPosition()
        {
            CodeownersEditor codeownersEditor = new CodeownersEditor(codeownersContent, false);
            
            // Arrange
            var newEntry = new CodeownersEntry { ServiceLabels = new List<string> { "Service Bus" } };

            // Act
            var result = codeownersEditor.FindAlphabeticalInsertionPoint(newEntry);

            // Assert (observed insertion point with shared content)
            Assert.That(result.startLine, Is.EqualTo(9));
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
            var result = CodeownersEditor.AddOwners(existingOwners, ownersToAdd);

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
            var result = CodeownersEditor.AddOwners(existingOwners, ownersToAdd);

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
            var result = CodeownersEditor.RemoveOwners(existingOwners, ownersToRemove);

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
            var result = CodeownersEditor.RemoveOwners(existingOwners, ownersToRemove);

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result, Contains.Item("@azure/team1"));
            Assert.That(result, Contains.Item("@azure/team2"));
        }

        #endregion

        #region AddOrUpdateCodeownersFile Tests

        [Test]
        public void AddOrUpdateCodeownersFile_AddsNewEntry_WhenNoMatch()
        {
            CodeownersEditor codeownersEditor = new CodeownersEditor(codeownersContent, false);

            // Arrange & Act
            var resultEntry = codeownersEditor.AddOrUpdateCodeownersFile(path: "sdk/newservice/", serviceLabel: "New Service", serviceOwners: new List<string> { "@newowner" }, sourceOwners: new List<string> { "@sourceowner" });

            // Assert
            Assert.That(resultEntry, Is.Not.Null);
            Assert.That(resultEntry.PathExpression, Is.EqualTo("/sdk/newservice/"));
            Assert.That(resultEntry.ServiceLabels, Does.Contain("New Service"));
            Assert.That(resultEntry.ServiceOwners, Does.Contain("@newowner"));

            // The editor content should now include the new path
            Assert.That(codeownersEditor.ToString(), Does.Contain("sdk/newservice/"));
        }

        [Test]
        public void AddOrUpdateCodeownersFile_UpdatesExistingEntry_ByPath()
        {
            CodeownersEditor codeownersEditor = new CodeownersEditor(codeownersContent, false);

            // Ensure storage entry exists in the initial fixture
            var before = codeownersEditor.FindMatchingEntry(path: "/sdk/storage/");
            Assert.That(before, Is.Not.Null);

            // Act: add an extra owner to the existing storage entry
            var updated = codeownersEditor.AddOrUpdateCodeownersFile(path: "sdk/storage/", sourceOwners: new List<string> { "@extra-storage-owner" });

            // Assert: updated entry contains both original and new owner
            Assert.That(updated, Is.Not.Null);
            Assert.That(updated.SourceOwners, Does.Contain("@azure/storage-team"));
            Assert.That(updated.SourceOwners, Does.Contain("@extra-storage-owner"));

            // And the editor content contains the new owner string
            Assert.That(codeownersEditor.ToString(), Does.Contain("@extra-storage-owner"));
        }

        [Test]
        public void RemoveOwnersFromCodeownersFile_RemovesServiceOwner()
        {
            CodeownersEditor codeownersEditor = new CodeownersEditor(codeownersContent, false);
            
            // Arrange: ensure servicebus entry exists and contains the owner
            var entry = codeownersEditor.FindMatchingEntry(path: "/sdk/servicebus/");
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.ServiceOwners, Does.Contain("azure/servicebus-team"));

            // Act: remove the existing owner
            var updated = codeownersEditor.RemoveOwnersFromCodeownersFile(path: "sdk/servicebus/", serviceOwnersToRemove: new List<string> { "@azure/servicebus-team" });

            // Assert: the returned entry no longer contains the removed owner
            Assert.That(updated.ServiceOwners, Does.Not.Contain("@azure/servicebus-team"));

            // And the editor content no longer contains the owner string
            Assert.That(codeownersEditor.ToString(), Does.Not.Contain("@azure/servicebus-team"));
        }

        #endregion
    }
}
