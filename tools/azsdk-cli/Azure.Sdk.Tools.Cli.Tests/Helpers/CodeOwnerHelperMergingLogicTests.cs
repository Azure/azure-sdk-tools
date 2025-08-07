using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers
{
    [TestFixture]
    internal class CodeOwnerHelperMergingLogicTests
    {
        private CodeOwnerHelper codeOwnerHelper;

        [SetUp]
        public void Setup()
        {
            codeOwnerHelper = new CodeOwnerHelper();
        }

        #region AreEntriesRelatedByPath Tests

        [Test]
        public void AreEntriesRelatedByPath_SameServiceLabel_ReturnsTrue()
        {
            // Arrange
            var entry1 = new CodeownersEntry
            {
                PathExpression = "/sdk/storage/",
                ServiceLabels = new List<string> { "%Storage" }
            };

            var entry2 = new CodeownersEntry
            {
                PathExpression = "/sdk/storage/Azure.Storage.Blobs/",
                ServiceLabels = new List<string> { "%Storage" }
            };

            // Act
            var result = InvokeAreEntriesRelatedByPath(entry1, entry2);

            // Assert
            Assert.That(result, Is.True, "Entries with same service label should be related");
        }

        [Test]
        public void AreEntriesRelatedByPath_ServiceLabelToPRLabel_ReturnsTrue()
        {
            // Arrange
            var entry1 = new CodeownersEntry
            {
                PathExpression = "/sdk/communication/",
                ServiceLabels = new List<string> { "%Communication" }
            };

            var entry2 = new CodeownersEntry
            {
                PathExpression = "/sdk/communication/Azure.Communication.Chat/",
                PRLabels = new List<string> { "%Communication - Chat" }
            };

            // Act
            var result = InvokeAreEntriesRelatedByPath(entry1, entry2);

            // Assert
            Assert.That(result, Is.True, "Service label should match hierarchical PR label base");
        }

        [Test]
        public void AreEntriesRelatedByPath_DifferentServices_ReturnsFalse()
        {
            // Arrange
            var entry1 = new CodeownersEntry
            {
                PathExpression = "/sdk/storage/",
                ServiceLabels = new List<string> { "%Storage" }
            };

            var entry2 = new CodeownersEntry
            {
                PathExpression = "/sdk/communication/",
                ServiceLabels = new List<string> { "%Communication" }
            };

            // Act
            var result = InvokeAreEntriesRelatedByPath(entry1, entry2);

            // Assert
            Assert.That(result, Is.False, "Entries with different service labels should not be related");
        }

        [Test]
        public void AreEntriesRelatedByPath_DifferentDirectories_ReturnsFalse()
        {
            // Arrange
            var entry1 = new CodeownersEntry
            {
                PathExpression = "/sdk/selfhelp/Azure.ResourceManager.SelfHelp/",
                PRLabels = new List<string> { "%Self Help" }
            };

            var entry2 = new CodeownersEntry
            {
                PathExpression = "/sdk/servicefabric/Azure.ResourceManager.ServiceFabric/",
                PRLabels = new List<string> { "%Service Fabric" }
            };

            // Act
            var result = InvokeAreEntriesRelatedByPath(entry1, entry2);

            // Assert
            Assert.That(result, Is.False, "Entries in different directories should not be related");
        }

        [Test]
        public void AreEntriesRelatedByPath_PathToServiceLabelMatch_ReturnsTrue()
        {
            // Arrange
            var entry1 = new CodeownersEntry
            {
                PathExpression = "/sdk/storage/",
                PRLabels = new List<string> { "%Storage" }
            };

            var entry2 = new CodeownersEntry
            {
                ServiceLabels = new List<string> { "%Storage" }
            };

            // Act
            var result = InvokeAreEntriesRelatedByPath(entry1, entry2);

            // Assert
            Assert.That(result, Is.True, "Path containing service name should match service label");
        }

        #endregion

        #region ExtractDirectoryName Tests

        [Test]
        [TestCase("/sdk/selfhelp/Azure.ResourceManager.SelfHelp/", "Azure.ResourceManager.SelfHelp")]
        [TestCase("/sdk/storage/Azure.Storage.Blobs/", "Azure.Storage.Blobs")]
        [TestCase("/eng/common/scripts/", "scripts")]
        [TestCase("", "")]
        [TestCase("   ", "")]
        [TestCase("///sdk/storage/Azure.Storage.Blobs///", "Azure.Storage.Blobs")]
        [TestCase("/sdk/servicefabric/Azure.ResourceManager.ServiceFabric/", "Azure.ResourceManager.ServiceFabric")]
        [TestCase("/sdk/compute/Azure.ResourceManager.Compute/", "Azure.ResourceManager.Compute")]
        [TestCase("/sdk/communication/Azure.Communication.Chat/", "Azure.Communication.Chat")]
        [TestCase("/sdk/keyvault/", "keyvault")]
        [TestCase("sdk/keyvault", "keyvault")]
        [TestCase("sdk/keyvault/", "keyvault")]
        [TestCase("/sdk/", "")]
        [TestCase("sdk", "sdk")]
        [TestCase("/", "")]
        [TestCase("//", "")]
        [TestCase("///", "")]
        [TestCase("/sdk//", "")]
        [TestCase("/sdk///storage//", "storage")]
        [TestCase("/sdk/storage//Azure.Storage.Blobs//", "Azure.Storage.Blobs")]
        [TestCase("/SDK/STORAGE/Azure.Storage.Blobs/", "Azure.Storage.Blobs")]
        [TestCase("/sdk/very-long-service-name/Azure.VeryLongServiceName.Package/", "Azure.VeryLongServiceName.Package")]
        [TestCase("/sdk/test/a/b/c/d/e/", "e")]
        [TestCase("/sdk/test-service_name.with.dots/", "test-service_name.with.dots")]
        [TestCase("/sdk/123numeric/", "123numeric")]
        [TestCase("/sdk/service@special#chars$/", "service@special#chars$")]
        [TestCase("/sdk/service with spaces/", "service with spaces")]
        public void TestExtractDirectoryName(string path, string expected)
        {
            var actual = InvokeExtractDirectoryName(path);
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void ExtractDirectoryName_NullPath_ReturnsEmpty()
        {
            // Act & Assert
            Assert.That(InvokeExtractDirectoryName(""), Is.EqualTo(""));
        }

        #endregion

        #region GetPrimaryLabel Tests

        [Test]
        public void GetPrimaryLabel_ServiceLabelPresent_ReturnsServiceLabel()
        {
            // Arrange
            var entry = new CodeownersEntry
            {
                ServiceLabels = new List<string> { "%Storage" },
                PRLabels = new List<string> { "%SomeOtherLabel" }
            };

            // Act
            var (serviceLabel, prLabel) = InvokeGetPrimaryLabel(entry);

            // Assert
            Assert.That(serviceLabel, Is.EqualTo("%Storage"));
            Assert.That(prLabel, Is.EqualTo("%SomeOtherLabel"));
        }

        [Test]
        public void GetPrimaryLabel_OnlyPRLabel_ReturnsPRLabel()
        {
            // Arrange
            var entry = new CodeownersEntry
            {
                PRLabels = new List<string> { "%Communication - Chat" }
            };

            // Act
            var (serviceLabel, prLabel) = InvokeGetPrimaryLabel(entry);

            // Assert
            Assert.That(serviceLabel, Is.EqualTo(""));
            Assert.That(prLabel, Is.EqualTo("%Communication - Chat"));
        }

        [Test]
        public void GetPrimaryLabel_EmptyLabels_ReturnsEmpty()
        {
            // Arrange
            var entry = new CodeownersEntry
            {
                ServiceLabels = new List<string>(),
                PRLabels = new List<string>()
            };

            // Act
            var (serviceLabel, prLabel) = InvokeGetPrimaryLabel(entry);

            // Assert
            Assert.That(serviceLabel, Is.EqualTo(""));
            Assert.That(prLabel, Is.EqualTo(""));
        }

        [Test]
        public void GetPrimaryLabel_WhitespaceLabels_ReturnsEmpty()
        {
            // Arrange
            var entry = new CodeownersEntry
            {
                ServiceLabels = new List<string> { "   ", "" },
                PRLabels = new List<string> { " " }
            };

            // Act
            var (serviceLabel, prLabel) = InvokeGetPrimaryLabel(entry);

            // Assert
            Assert.That(serviceLabel, Is.EqualTo(""));
            Assert.That(prLabel, Is.EqualTo(""));
        }

        #endregion

        #region Integration Scenarios

        [Test]
        public void MergingLogic_CommunicationServices_ShouldNotMerge()
        {
            // Arrange - This tests the scenario that was causing the data loss
            var communicationGeneral = new CodeownersEntry
            {
                PathExpression = "/sdk/communication/",
                PRLabels = new List<string> { "%Communication" }
            };

            var communicationChat = new CodeownersEntry
            {
                PathExpression = "/sdk/communication/Azure.Communication.Chat/",
                PRLabels = new List<string> { "%Communication - Chat" }
            };

            var communicationCallAutomation = new CodeownersEntry
            {
                PathExpression = "/sdk/communication/Azure.Communication.CallAutomation/",
                PRLabels = new List<string> { "%Communication - Call Automation" }
            };

            // Act - Test if they would be considered related
            var generalToChatRelated = InvokeAreEntriesRelatedByPath(communicationGeneral, communicationChat);
            var chatToCallAutomationRelated = InvokeAreEntriesRelatedByPath(communicationChat, communicationCallAutomation);

            // Assert - They should be related by service but have different paths
            Assert.That(generalToChatRelated, Is.False, "General Communication should be related to Communication - Chat");
            Assert.That(chatToCallAutomationRelated, Is.False, "Different Communication services should not be related by path");
        }

        [Test]
        public void MergingLogic_SelfHelpVsServiceFabric_ShouldNotMerge()
        {
            // Arrange - This tests the specific bug that was reported
            var selfHelp = new CodeownersEntry
            {
                PathExpression = "/sdk/selfhelp/Azure.ResourceManager.SelfHelp/",
                PRLabels = new List<string> { "%Self Help" }
            };

            var serviceFabric = new CodeownersEntry
            {
                PathExpression = "/sdk/servicefabric/Azure.ResourceManager.ServiceFabric/",
                PRLabels = new List<string> { "%Service Fabric" },
                ServiceLabels = new List<string> { "%Service Fabric" }
            };

            // Act
            var areRelated = InvokeAreEntriesRelatedByPath(selfHelp, serviceFabric);

            // Assert
            Assert.That(areRelated, Is.False, "Self Help and Service Fabric should not be merged - they are different services");
        }

        [Test]
        public void MergingLogic_SameServiceDifferentPaths_ShouldMerge()
        {
            // Arrange - Test valid merging scenario
            var storageGeneral = new CodeownersEntry
            {
                PathExpression = "/sdk/storage/",
                ServiceLabels = new List<string> { "%Storage" }
            };

            var storageSpecific = new CodeownersEntry
            {
                PathExpression = "/sdk/storage/Azure.Storage.Blobs/",
                ServiceLabels = new List<string> { "%Storage" }
            };

            // Act
            var areRelated = InvokeAreEntriesRelatedByPath(storageGeneral, storageSpecific);

            // Assert
            Assert.That(areRelated, Is.True, "Same service with different paths should be merged");
        }

        [Test]
        public void MergingLogic_ManagementPlaneServices_CorrectDirectoryExtraction()
        {
            // Arrange - Test various Azure.ResourceManager paths
            var testCases = new[]
            {
                ("/sdk/selfhelp/Azure.ResourceManager.SelfHelp/", "Azure.ResourceManager.SelfHelp"),
                ("/sdk/servicefabric/Azure.ResourceManager.ServiceFabric/", "Azure.ResourceManager.ServiceFabric"),
                ("/sdk/compute/Azure.ResourceManager.Compute/", "Azure.ResourceManager.Compute"),
                ("/sdk/storage/Azure.ResourceManager.Storage/", "Azure.ResourceManager.Storage")
            };

            foreach (var (path, expectedDirectory) in testCases)
            {
                // Act
                var result = InvokeExtractDirectoryName(path);

                // Assert
                Assert.That(result, Is.EqualTo(expectedDirectory), 
                    $"Path {path} should extract directory {expectedDirectory}");
            }
        }

        [Test]
        public void MergingLogic_WebJobsExtensions_ShouldNotLoseIndividualPaths()
        {
            // Arrange - Test the WebJobs scenario that was causing path loss
            var eventGridGeneral = new CodeownersEntry
            {
                PathExpression = "/sdk/eventgrid/",
                PRLabels = new List<string> { "%Event Grid" }
            };

            var eventGridWebJobs = new CodeownersEntry
            {
                PathExpression = "/sdk/eventgrid/Microsoft.Azure.WebJobs.Extensions.EventGrid/",
                PRLabels = new List<string> { "%Event Grid", "%Functions" }
            };

            // Act
            var areRelated = InvokeAreEntriesRelatedByPath(eventGridGeneral, eventGridWebJobs);

            // Assert
            Assert.That(areRelated, Is.True, "Event Grid WebJobs extension should be related to general Event Grid");
        }

        #endregion

        #region Helper Methods

        private bool InvokeAreEntriesRelatedByPath(CodeownersEntry entry1, CodeownersEntry entry2)
        {
            var method = typeof(CodeOwnerHelper).GetMethod("AreEntriesRelatedByPath", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            return (bool)(method?.Invoke(codeOwnerHelper, new object[] { entry1, entry2 }) ?? false);
        }

        private string InvokeExtractDirectoryName(string path)
        {
            var method = typeof(CodeOwnerHelper).GetMethod("ExtractDirectoryName", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            return (string)(method?.Invoke(codeOwnerHelper, new object[] { path }) ?? "");
        }

        private (string serviceLabel, string prLabel) InvokeGetPrimaryLabel(CodeownersEntry entry)
        {
            var method = typeof(CodeOwnerHelper).GetMethod("GetPrimaryLabel", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            return ((string serviceLabel, string prLabel))(method?.Invoke(codeOwnerHelper, new object[] { entry }) ?? ("", ""));
        }

        #endregion
    }
}
