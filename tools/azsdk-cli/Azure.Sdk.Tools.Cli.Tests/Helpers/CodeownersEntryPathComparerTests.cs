using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers
{
    [TestFixture]
    internal class CodeownersEntryPathComparerTests
    {
        private CodeownersEntryPathComparer comparer;

        [SetUp]
        public void Setup()
        {
            comparer = new CodeownersEntryPathComparer();
        }

        #region Global Catch-All Pattern Tests

        [Test]
        public void Compare_GlobalCatchAllPattern_ComesBeforeRegularPath()
        {
            // Arrange
            var globalCatchAll = new CodeownersEntry
            {
                PathExpression = "/**/*Management*/",
                PRLabels = new List<string> { "%Mgmt" }
            };

            var regularPath = new CodeownersEntry
            {
                PathExpression = "/sdk/storage/",
                PRLabels = new List<string> { "%Storage" }
            };

            // Act
            var result = comparer.Compare(globalCatchAll, regularPath);

            // Assert
            Assert.That(result, Is.LessThan(0), "Global catch-all should come before regular paths");
        }

        [Test]
        public void Compare_MultipleGlobalCatchAllPatterns_SortedBySpecificity()
        {
            // Arrange
            var entries = new List<CodeownersEntry>
            {
                new CodeownersEntry
                {
                    PathExpression = "/**/Azure.ResourceManager*/",
                    PRLabels = new List<string> { "%Mgmt" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/**/*Management*/",
                    PRLabels = new List<string> { "%Mgmt" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/**/",
                    PRLabels = new List<string> { "%Root" }
                }
            };

            // Act
            entries.Sort(comparer);

            // Assert
            // More general patterns should come first
            //Assert.That(entries[0].PathExpression, Is.EqualTo("/**/"));
            Assert.That(entries[1].PathExpression, Is.EqualTo("/**/*Management*/"));
            Assert.That(entries[2].PathExpression, Is.EqualTo("/**/Azure.ResourceManager*/"));
        }

        [Test]
        public void Compare_GlobalCatchAllWithSameSpecificity_SortedAlphabetically()
        {
            // Arrange
            var entryB = new CodeownersEntry
            {
                PathExpression = "/**/*Beta*/",
                PRLabels = new List<string> { "%Beta" }
            };

            var entryA = new CodeownersEntry
            {
                PathExpression = "/**/*Alpha*/",
                PRLabels = new List<string> { "%Alpha" }
            };

            // Act
            var result = comparer.Compare(entryA, entryB);

            // Assert
            Assert.That(result, Is.LessThan(0), "Alpha should come before Beta alphabetically");
        }

        #endregion

        #region Service Label Grouping Tests

        [Test]
        public void Compare_SameServiceLabel_GroupedTogether()
        {
            // Arrange
            var entry1 = new CodeownersEntry
            {
                PathExpression = "/sdk/communication/Azure.Communication.Chat/",
                PRLabels = new List<string> { "%Communication - Chat" }
            };

            var entry2 = new CodeownersEntry
            {
                PathExpression = "/sdk/communication/",
                PRLabels = new List<string> { "%Communication" }
            };

            var entry3 = new CodeownersEntry
            {
                PathExpression = "/sdk/storage/",
                PRLabels = new List<string> { "%Storage" }
            };

            var entries = new List<CodeownersEntry> { entry3, entry1, entry2 };

            // Act
            entries.Sort(comparer);

            // Assert
            // Communication entries should be grouped together
            Assert.That(entries[0].PRLabels[0], Does.Contain("Communication"));
            Assert.That(entries[1].PRLabels[0], Does.Contain("Communication"));
            Assert.That(entries[2].PRLabels[0], Does.Contain("Storage"));
        }

        [Test]
        public void Compare_HierarchicalServices_BaseServiceNameExtracted()
        {
            // Arrange
            var cognitiveVision = new CodeownersEntry
            {
                ServiceLabels = new List<string> { "%Cognitive - Computer Vision" }
            };

            var cognitiveLanguage = new CodeownersEntry
            {
                ServiceLabels = new List<string> { "%Cognitive - Language" }
            };

            var storage = new CodeownersEntry
            {
                ServiceLabels = new List<string> { "%Storage" }
            };

            var entries = new List<CodeownersEntry> { storage, cognitiveVision, cognitiveLanguage };

            // Act
            entries.Sort(comparer);

            // Assert
            // All Cognitive services should be grouped together, before Storage
            Assert.That(entries[0].ServiceLabels[0], Does.Contain("Cognitive"));
            Assert.That(entries[1].ServiceLabels[0], Does.Contain("Cognitive"));
            Assert.That(entries[2].ServiceLabels[0], Does.Contain("Storage"));
        }

        [Test]
        public void Compare_ServiceLabelTakesPriorityOverPRLabel()
        {
            // Arrange
            var entryWithServiceLabel = new CodeownersEntry
            {
                PathExpression = "/sdk/storage/",
                ServiceLabels = new List<string> { "%Storage" },
                PRLabels = new List<string> { "%SomeOtherLabel" }
            };

            var entryWithPRLabelOnly = new CodeownersEntry
            {
                PathExpression = "/sdk/communication/",
                PRLabels = new List<string> { "%Communication" }
            };

            // Act
            var result = comparer.Compare(entryWithPRLabelOnly, entryWithServiceLabel);

            // Assert
            // Should compare "Communication" vs "Storage", not "Communication" vs "SomeOtherLabel"
            Assert.That(result, Is.LessThan(0), "Communication should come before Storage alphabetically");
        }

        #endregion

        #region Path Hierarchy Tests

        [Test]
        public void Compare_ParentAndChildPaths_ParentComesFirst()
        {
            // Arrange
            var parentPath = new CodeownersEntry
            {
                PathExpression = "/sdk/communication/",
                PRLabels = new List<string> { "%Communication" }
            };

            var childPath = new CodeownersEntry
            {
                PathExpression = "/sdk/communication/Azure.Communication.Chat/",
                PRLabels = new List<string> { "%Communication - Chat" }
            };

            // Act
            var result = comparer.Compare(parentPath, childPath);

            // Assert
            Assert.That(result, Is.LessThan(0), "Parent path should come before child path");
        }

        [Test]
        public void Compare_SamePathDifferentWildcards_WildcardComesFirst()
        {
            // Arrange
            var wildcardPath = new CodeownersEntry
            {
                PathExpression = "/sdk/storage*",
                PRLabels = new List<string> { "%Storage" }
            };

            var exactPath = new CodeownersEntry
            {
                PathExpression = "/sdk/storage",
                PRLabels = new List<string> { "%Storage" }
            };

            // Act
            var result = comparer.Compare(wildcardPath, exactPath);

            // Assert
            Assert.That(result, Is.LessThan(0), "Wildcard path should come before exact path");
        }

        [Test]
        public void Compare_DifferentPaths_SortedAlphabetically()
        {
            // Arrange
            var pathA = new CodeownersEntry
            {
                PathExpression = "/sdk/ai/",
                PRLabels = new List<string> { "%AI" }
            };

            var pathB = new CodeownersEntry
            {
                PathExpression = "/sdk/batch/",
                PRLabels = new List<string> { "%Batch" }
            };

            // Act
            var result = comparer.Compare(pathA, pathB);

            // Assert
            Assert.That(result, Is.LessThan(0), "ai should come before batch alphabetically");
        }

        #endregion

        #region Edge Cases

        [Test]
        public void Compare_NullEntries_HandledCorrectly()
        {
            // Arrange
            var validEntry = new CodeownersEntry
            {
                PathExpression = "/sdk/storage/",
                PRLabels = new List<string> { "%Storage" }
            };

            // Act & Assert
            Assert.That(comparer.Compare(null, null), Is.EqualTo(0));
            Assert.That(comparer.Compare(null, validEntry), Is.LessThan(0));
            Assert.That(comparer.Compare(validEntry, null), Is.GreaterThan(0));
        }

        [Test]
        public void Compare_EmptyOrWhitespacePaths_HandledCorrectly()
        {
            // Arrange
            var emptyPath = new CodeownersEntry
            {
                PathExpression = "",
                ServiceLabels = new List<string> { "%Storage" }
            };

            var whitespacePath = new CodeownersEntry
            {
                PathExpression = "   ",
                ServiceLabels = new List<string> { "%Communication" }
            };

            // Act
            var result = comparer.Compare(whitespacePath, emptyPath);

            // Assert
            // Should compare by service labels: Communication vs Storage
            Assert.That(result, Is.LessThan(0), "Communication should come before Storage");
        }

        [Test]
        public void Compare_NoLabelsOrPaths_AreEqual()
        {
            // Arrange
            var entry1 = new CodeownersEntry();
            var entry2 = new CodeownersEntry();

            // Act
            var result = comparer.Compare(entry1, entry2);

            // Assert
            Assert.That(result, Is.EqualTo(0), "Entries with no labels or paths should be equal");
        }

        #endregion

        #region Integration Tests

        [Test]
        public void Compare_ComplexRealWorldScenario_SortsCorrectly()
        {
            // Arrange - This mimics the actual CODEOWNERS file structure
            var entries = new List<CodeownersEntry>
            {
                // Regular service entries
                new CodeownersEntry
                {
                    PathExpression = "/sdk/storage/",
                    PRLabels = new List<string> { "%Storage" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/sdk/communication/Azure.Communication.Chat/",
                    PRLabels = new List<string> { "%Communication - Chat" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/sdk/communication/",
                    PRLabels = new List<string> { "%Communication" }
                },
                // Global catch-all patterns
                new CodeownersEntry
                {
                    PathExpression = "/**/*Management*/",
                    PRLabels = new List<string> { "%Mgmt" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/**/Azure.ResourceManager*/",
                    PRLabels = new List<string> { "%Mgmt" }
                },
                // Another service
                new CodeownersEntry
                {
                    PathExpression = "/sdk/ai/",
                    PRLabels = new List<string> { "%AI Model Inference", "%AI Projects" }
                }
            };

            // Act
            entries.Sort(comparer);

            // Assert
            // Global catch-all patterns should be first
            Assert.That(entries[0].PathExpression, Is.EqualTo("/**/*Management*/"));
            Assert.That(entries[1].PathExpression, Is.EqualTo("/**/Azure.ResourceManager*/"));
            
            // Then services in alphabetical order by service name
            Assert.That(entries[2].PRLabels[0], Does.Contain("AI"));
            Assert.That(entries[3].PRLabels[0], Does.Contain("Communication"));
            Assert.That(entries[4].PRLabels[0], Does.Contain("Communication"));
            Assert.That(entries[5].PRLabels[0], Does.Contain("Storage"));
            
            // Within Communication group, parent path should come before child path
            var commEntries = entries.Where(e => e.PRLabels?.Any(l => l.Contains("Communication")) == true).ToList();
            Assert.That(commEntries[0].PathExpression, Is.EqualTo("/sdk/communication/"));
            Assert.That(commEntries[1].PathExpression, Is.EqualTo("/sdk/communication/Azure.Communication.Chat/"));
        }

        [Test]
        public void Compare_ManagementPlaneScenario_GlobalPatternsFirst()
        {
            // Arrange - Management plane specific scenario
            var entries = new List<CodeownersEntry>
            {
                new CodeownersEntry
                {
                    PathExpression = "/sdk/selfhelp/Azure.ResourceManager.SelfHelp/",
                    PRLabels = new List<string> { "%Self Help" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/sdk/servicefabric/Azure.ResourceManager.ServiceFabric/",
                    PRLabels = new List<string> { "%Service Fabric" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/**/*Management*/",
                    PRLabels = new List<string> { "%Mgmt" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/**/Azure.ResourceManager*/",
                    PRLabels = new List<string> { "%Mgmt" }
                }
            };

            // Act
            entries.Sort(comparer);

            // Assert
            // Global management patterns should be first
            Assert.That(entries[0].PathExpression, Is.EqualTo("/**/*Management*/"));
            Assert.That(entries[1].PathExpression, Is.EqualTo("/**/Azure.ResourceManager*/"));
            
            // Then specific services alphabetically
            Assert.That(entries[2].PRLabels[0], Does.Contain("Self Help"));
            Assert.That(entries[3].PRLabels[0], Does.Contain("Service Fabric"));
        }

        #endregion
    }
}
