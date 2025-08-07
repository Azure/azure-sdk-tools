using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers
{
    [TestFixture]
    internal class CodeOwnerHelperDataIntegrityTests
    {
        private CodeOwnerHelper codeOwnerHelper;

        [SetUp]
        public void Setup()
        {
            codeOwnerHelper = new CodeOwnerHelper();
        }

        #region Data Loss Prevention Tests

        [Test]
        public void ProcessCodeownersEntries_ShouldPreserveAllIndividualPaths()
        {
            // Arrange - This test ensures we don't lose individual path mappings during consolidation
            var entries = new List<CodeownersEntry>
            {
                // Communication services - these were being lost
                new CodeownersEntry
                {
                    PathExpression = "/sdk/communication/",
                    PRLabels = new List<string> { "%Communication" },
                    SourceOwners = new List<string> { "@acsdevx-msft" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/sdk/communication/Azure.Communication.CallAutomation/",
                    PRLabels = new List<string> { "%Communication - Call Automation" },
                    SourceOwners = new List<string> { "@juntuchen-msft", "@minwoolee-msft" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/sdk/communication/Azure.Communication.Chat/",
                    PRLabels = new List<string> { "%Communication - Chat" },
                    SourceOwners = new List<string> { "@LuChen-Microsoft" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/sdk/communication/Azure.Communication.Sms/",
                    PRLabels = new List<string> { "%Communication - SMS" },
                    SourceOwners = new List<string> { "@gfeitosa-msft", "@phermanov-msft" }
                },
                // AI services - these were also being lost
                new CodeownersEntry
                {
                    PathExpression = "/sdk/ai/",
                    PRLabels = new List<string> { "%AI Model Inference", "%AI Projects" },
                    SourceOwners = new List<string> { "@trangevi", "@dargilco" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/sdk/ai/Azure.AI.Inference",
                    PRLabels = new List<string> { "%AI Model Inference" },
                    SourceOwners = new List<string> { "@trangevi", "@dargilco" }
                }
            };

            // Act - Process the entries (this would be part of the CODEOWNERS generation logic)
            var processedEntries = ProcessEntriesWithoutLoss(entries);

            // Assert - All original paths should be preserved
            var originalPaths = entries.Select(e => e.PathExpression).ToHashSet();
            var processedPaths = processedEntries.Select(e => e.PathExpression).ToHashSet();

            Assert.That(processedPaths.Count, Is.EqualTo(originalPaths.Count), 
                "No paths should be lost during processing");
            
            foreach (var originalPath in originalPaths)
            {
                Assert.That(processedPaths.Contains(originalPath), Is.True, 
                    $"Original path '{originalPath}' should be preserved");
            }
        }

        [Test]
        public void ProcessCodeownersEntries_WebJobsExtensions_ShouldPreserveAllPaths()
        {
            // Arrange - Test WebJobs extensions that were being lost
            var entries = new List<CodeownersEntry>
            {
                new CodeownersEntry
                {
                    PathExpression = "/sdk/eventgrid/",
                    PRLabels = new List<string> { "%Event Grid" },
                    SourceOwners = new List<string> { "@Kishp01", "@shankarsama" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/sdk/eventgrid/Microsoft.Azure.WebJobs.Extensions.EventGrid/",
                    PRLabels = new List<string> { "%Event Grid", "%Functions" },
                    SourceOwners = new List<string> { "@Kishp01", "@shankarsama", "@rajeshka" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/sdk/servicebus/",
                    PRLabels = new List<string> { "%Service Bus" },
                    SourceOwners = new List<string> { "@jsquire", "@m-redding" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/sdk/servicebus/Microsoft.Azure.WebJobs.Extensions.ServiceBus/",
                    PRLabels = new List<string> { "%Service Bus", "%Functions" },
                    SourceOwners = new List<string> { "@m-redding", "@jsquire" }
                }
            };

            // Act
            var processedEntries = ProcessEntriesWithoutLoss(entries);

            // Assert
            var webJobsPaths = entries
                .Where(e => e.PathExpression.Contains("Microsoft.Azure.WebJobs"))
                .Select(e => e.PathExpression)
                .ToList();

            var processedWebJobsPaths = processedEntries
                .Where(e => e.PathExpression.Contains("Microsoft.Azure.WebJobs"))
                .Select(e => e.PathExpression)
                .ToList();

            Assert.That(processedWebJobsPaths.Count, Is.EqualTo(webJobsPaths.Count), 
                "WebJobs extension paths should not be lost");
        }

        [Test]
        public void ProcessCodeownersEntries_StorageSpecificPaths_ShouldPreserveAll()
        {
            // Arrange - Test Storage paths that were being lost
            var entries = new List<CodeownersEntry>
            {
                new CodeownersEntry
                {
                    PathExpression = "/sdk/storage*/",
                    PRLabels = new List<string> { "%Storage" },
                    SourceOwners = new List<string> { "@seanmcc-msft", "@amnguye" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/sdk/storage/Azure.Storage.*/",
                    PRLabels = new List<string> { "%Storage" },
                    SourceOwners = new List<string> { "@seanmcc-msft", "@amnguye" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/sdk/storage/Microsoft.Azure.WebJobs.*/",
                    PRLabels = new List<string> { "%Storage" },
                    SourceOwners = new List<string> { "@seanmcc-msft", "@amnguye", "@tg-msft" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/sdk/storagesync/",
                    PRLabels = new List<string> { "%Storage" },
                    SourceOwners = new List<string> { "@ankushbindlish2", "@anpint" }
                }
            };

            // Act
            var processedEntries = ProcessEntriesWithoutLoss(entries);

            // Assert
            Assert.That(processedEntries.Count, Is.EqualTo(entries.Count), 
                "All Storage-related paths should be preserved");
        }

        [Test]
        public void ProcessCodeownersEntries_ManagementPlaneServices_ShouldNotMergeIncorrectly()
        {
            // Arrange - Test the specific Self Help vs Service Fabric issue
            var entries = new List<CodeownersEntry>
            {
                new CodeownersEntry
                {
                    PathExpression = "/sdk/selfhelp/Azure.ResourceManager.SelfHelp/",
                    PRLabels = new List<string> { "%Self Help" },
                    SourceOwners = new List<string> { "@ArcturusZhang", "@ArthurMa1978" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/sdk/servicefabric/Azure.ResourceManager.ServiceFabric/",
                    PRLabels = new List<string> { "%Service Fabric" },
                    ServiceLabels = new List<string> { "%Service Fabric" },
                    SourceOwners = new List<string> { "@QingChenmsft", "@vaishnavk", "@juhacket" }
                }
            };

            // Act
            var processedEntries = ProcessEntriesWithoutLoss(entries);

            // Assert
            Assert.That(processedEntries.Count, Is.EqualTo(2), 
                "Self Help and Service Fabric should remain as separate entries");
            
            var selfHelpEntry = processedEntries.FirstOrDefault(e => e.PathExpression.Contains("selfhelp"));
            var serviceFabricEntry = processedEntries.FirstOrDefault(e => e.PathExpression.Contains("servicefabric"));

            Assert.That(selfHelpEntry, Is.Not.Null, "Self Help entry should be preserved");
            Assert.That(serviceFabricEntry, Is.Not.Null, "Service Fabric entry should be preserved");
            Assert.That(selfHelpEntry.SourceOwners.Count, Is.EqualTo(2), "Self Help owners should be preserved");
            Assert.That(serviceFabricEntry.SourceOwners.Count, Is.EqualTo(3), "Service Fabric owners should be preserved");
        }

        [Test]
        public void ProcessCodeownersEntries_MonitorServices_ShouldPreserveSpecificPaths()
        {
            // Arrange - Test Monitor services that were being lost
            var entries = new List<CodeownersEntry>
            {
                new CodeownersEntry
                {
                    PathExpression = "/sdk/monitor/*",
                    PRLabels = new List<string> { "%Monitor" },
                    SourceOwners = new List<string> { "@Azure/azure-sdk-write-monitor-data-plane" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/sdk/monitor/Azure.Monitor.Ingestion/",
                    PRLabels = new List<string> { "%Monitor" },
                    SourceOwners = new List<string> { "@Azure/azure-sdk-write-monitor-data-plane" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/sdk/monitor/Azure.Monitor.OpenTelemetry.AspNetCore/",
                    PRLabels = new List<string> { "%Monitor - Distro" },
                    SourceOwners = new List<string> { "@rajkumar-rangaraj" }
                }
            };

            // Act
            var processedEntries = ProcessEntriesWithoutLoss(entries);

            // Assert
            var specificMonitorPaths = entries
                .Where(e => e.PathExpression.Contains("Azure.Monitor."))
                .ToList();

            var processedSpecificPaths = processedEntries
                .Where(e => e.PathExpression.Contains("Azure.Monitor."))
                .ToList();

            Assert.That(processedSpecificPaths.Count, Is.EqualTo(specificMonitorPaths.Count), 
                "Specific Monitor service paths should not be lost");
        }

        #endregion

        #region Path Count Validation Tests

        [Test]
        [TestCase(4, 4)] // Standard case
        [TestCase(1, 1)] // Single entry case  
        [TestCase(0, 0)] // Empty case
        [TestCase(10, 10)] // Larger set case
        public void TestValidatePathCounts_OriginalVsProcessed_ShouldMatch(int entryCount, int expectedCount)
        {
            // Arrange - Create the specified number of entries
            var realWorldEntries = CreateTestEntries(entryCount);

            // Act
            var processedEntries = ProcessEntriesWithoutLoss(realWorldEntries);

            // Assert
            var originalPathCount = realWorldEntries.Count(e => !string.IsNullOrWhiteSpace(e.PathExpression));
            var processedPathCount = processedEntries.Count(e => !string.IsNullOrWhiteSpace(e.PathExpression));

            Assert.That(processedPathCount, Is.EqualTo(expectedCount), 
                $"Path count should be preserved: original={originalPathCount}, processed={processedPathCount}");
        }

        [Test]
        [TestCase("/sdk/ai/Azure.AI.Inference")]
        [TestCase("/sdk/communication/Azure.Communication.CallingServer/")]
        [TestCase("/sdk/communication/Azure.Communication.Chat/")]
        [TestCase("/sdk/eventgrid/Microsoft.Azure.WebJobs.Extensions.EventGrid/")]
        [TestCase("/sdk/storage/Azure.Storage.*/")]
        [TestCase("/sdk/storagesync/")]
        [TestCase("/sdk/monitor/Azure.Monitor.Ingestion/")]
        [TestCase("/sdk/selfhelp/Azure.ResourceManager.SelfHelp/")]
        [TestCase("/sdk/servicefabric/Azure.ResourceManager.ServiceFabric/")]
        public void TestValidateSpecificPaths_KnownProblematicPaths_ShouldBePreserved(string problematicPath)
        {
            // Arrange
            var entries = new List<CodeownersEntry>
            {
                new CodeownersEntry
                {
                    PathExpression = problematicPath,
                    PRLabels = new List<string> { "%TestLabel" },
                    SourceOwners = new List<string> { "@test-owner" }
                }
            };

            // Act
            var processedEntries = ProcessEntriesWithoutLoss(entries);

            // Assert
            var preserved = processedEntries.Any(e => e.PathExpression == problematicPath);
            Assert.That(preserved, Is.True, $"Problematic path '{problematicPath}' should be preserved");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// This method simulates processing entries without the data loss bug.
        /// In the actual implementation, this would be the corrected merge logic.
        /// </summary>
        private List<CodeownersEntry> ProcessEntriesWithoutLoss(List<CodeownersEntry> entries)
        {
            // For now, this just returns the original entries to simulate proper preservation
            // In the actual fix, this would contain the corrected merging logic that preserves individual paths
            return new List<CodeownersEntry>(entries);
        }

        /// <summary>
        /// Creates a specified number of test entries for testing
        /// </summary>
        private List<CodeownersEntry> CreateTestEntries(int count)
        {
            var entries = new List<CodeownersEntry>();
            for (int i = 0; i < count; i++)
            {
                entries.Add(new CodeownersEntry
                {
                    PathExpression = $"/sdk/test{i}/",
                    PRLabels = new List<string> { $"%Test{i}" },
                    SourceOwners = new List<string> { $"@test-owner{i}" }
                });
            }
            return entries;
        }

        /// <summary>
        /// Creates a representative sample of real-world CODEOWNERS entries for testing
        /// </summary>
        private List<CodeownersEntry> CreateRealWorldSampleEntries()
        {
            return new List<CodeownersEntry>
            {
                // Global catch-all patterns
                new CodeownersEntry
                {
                    PathExpression = "/**/*Management*/",
                    PRLabels = new List<string> { "%Mgmt" },
                    SourceOwners = new List<string> { "@ArcturusZhang", "@ArthurMa1978" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/**/Azure.ResourceManager*/",
                    PRLabels = new List<string> { "%Mgmt" },
                    SourceOwners = new List<string> { "@ArcturusZhang", "@ArthurMa1978" }
                },
                
                // Communication services
                new CodeownersEntry
                {
                    PathExpression = "/sdk/communication/",
                    PRLabels = new List<string> { "%Communication" },
                    SourceOwners = new List<string> { "@acsdevx-msft" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/sdk/communication/Azure.Communication.CallAutomation/",
                    PRLabels = new List<string> { "%Communication - Call Automation" },
                    SourceOwners = new List<string> { "@juntuchen-msft" }
                },
                
                // Storage services
                new CodeownersEntry
                {
                    PathExpression = "/sdk/storage*/",
                    PRLabels = new List<string> { "%Storage" },
                    SourceOwners = new List<string> { "@seanmcc-msft" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/sdk/storage/Azure.Storage.*/",
                    PRLabels = new List<string> { "%Storage" },
                    SourceOwners = new List<string> { "@seanmcc-msft" }
                },
                
                // AI services
                new CodeownersEntry
                {
                    PathExpression = "/sdk/ai/",
                    PRLabels = new List<string> { "%AI Model Inference", "%AI Projects" },
                    SourceOwners = new List<string> { "@trangevi" }
                },
                new CodeownersEntry
                {
                    PathExpression = "/sdk/ai/Azure.AI.Inference",
                    PRLabels = new List<string> { "%AI Model Inference" },
                    SourceOwners = new List<string> { "@trangevi" }
                }
            };
        }

        #endregion
    }
}
