using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers
{
    [TestFixture]
    internal class CodeOwnerValidationHelperTests
    {
        private ICodeOwnerValidationHelper validationHelper;

        [SetUp]
        public void Setup()
        {
            validationHelper = new CodeOwnerValidationHelper();
        }

        [Test]
        public void ExtractUniqueOwners_ValidEntry_ReturnsUniqueOwners()
        {
            // Arrange - Create a mock CodeownersEntry
            var entry = new CodeownersEntry()
            {
                PathExpression = "test/path",
                SourceOwners = new List<string> { "@owner1", "@owner2" },
                ServiceOwners = new List<string> { "@owner2", "@owner3" }, // owner2 is duplicate
                AzureSdkOwners = new List<string> { "@owner4" }
            };

            // Act
            var uniqueOwners = validationHelper.ExtractUniqueOwners(entry);

            // Assert
            Assert.That(uniqueOwners, Is.Not.Null);
            Assert.That(uniqueOwners.Count, Is.EqualTo(4));
            Assert.That(uniqueOwners, Contains.Item("@owner1"));
            Assert.That(uniqueOwners, Contains.Item("@owner2"));
            Assert.That(uniqueOwners, Contains.Item("@owner3"));
            Assert.That(uniqueOwners, Contains.Item("@owner4"));
        }

        [Test]
        public void FindServiceEntries_VariousMatchingCriteria_ReturnsCorrectEntry()
        {
            // Test matching by ServiceLabels
            var entriesWithServiceLabels = new List<CodeownersEntry>
            {
                new CodeownersEntry()
                {
                    PathExpression = "path1",
                    ServiceLabels = new List<string> { "Storage", "Blob" }
                },
                new CodeownersEntry()
                {
                    PathExpression = "path2",
                    ServiceLabels = new List<string> { "Compute", "VM" }
                }
            };

            var result = validationHelper.FindServiceEntries(entriesWithServiceLabels, "Storage");
            Assert.That(result, Is.Not.Null);
            Assert.That(result.PathExpression, Is.EqualTo("path1"));

            // Test matching by PRLabels
            var entriesWithPRLabels = new List<CodeownersEntry>
            {
                new CodeownersEntry()
                {
                    PathExpression = "pr-path1",
                    PRLabels = new List<string> { "Database", "CosmosDB" }
                },
                new CodeownersEntry()
                {
                    PathExpression = "pr-path2",
                    PRLabels = new List<string> { "KeyVault", "Security" }
                }
            };

            result = validationHelper.FindServiceEntries(entriesWithPRLabels, "Database");
            Assert.That(result, Is.Not.Null);
            Assert.That(result.PathExpression, Is.EqualTo("pr-path1"));

            // Test matching by PathExpression
            var entriesWithPaths = new List<CodeownersEntry>
            {
                new CodeownersEntry() { PathExpression = "/sdk/compute/" },
                new CodeownersEntry() { PathExpression = "/sdk/storage/azure-storage-blob/" }
            };

            result = validationHelper.FindServiceEntries(entriesWithPaths, "storage");
            Assert.That(result, Is.Not.Null);
            Assert.That(result.PathExpression, Is.EqualTo("/sdk/storage/azure-storage-blob/"));

            // Test no match scenario
            result = validationHelper.FindServiceEntries(entriesWithServiceLabels, "NonExistentService");
            Assert.That(result, Is.Null);
        }
    }
}
