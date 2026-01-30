// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.ReleasePlan
{
    [TestFixture]
    internal class PackageReleaseStatusToolTests
    {
        private Mock<IDevOpsService> mockDevOpsService;
        private TestLogger<PackageReleaseStatusTool> logger;
        private PackageReleaseStatusTool packageReleaseStatusTool;

        [SetUp]
        public void Setup()
        {
            mockDevOpsService = new Mock<IDevOpsService>();
            logger = new TestLogger<PackageReleaseStatusTool>();
            packageReleaseStatusTool = new PackageReleaseStatusTool(mockDevOpsService.Object, logger);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithNullPackageName_ReturnsError()
        {
            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus(null!, "python", "Released");

            // Assert
            Assert.That(result.ResponseError, Does.Contain("Package name cannot be null or empty"));
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithEmptyPackageName_ReturnsError()
        {
            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("", "python", "Released");

            // Assert
            Assert.That(result.ResponseError, Does.Contain("Package name cannot be null or empty"));
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithWhitespacePackageName_ReturnsError()
        {
            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("   ", "python", "Released");

            // Assert
            Assert.That(result.ResponseError, Does.Contain("Package name cannot be null or empty"));
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithNullLanguage_ReturnsError()
        {
            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", null!, "Released");

            // Assert
            Assert.That(result.ResponseError, Does.Contain("Language cannot be null or empty"));
            Assert.That(result.PackageName, Is.EqualTo("azure-test-package"));
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithEmptyLanguage_ReturnsError()
        {
            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "", "Released");

            // Assert
            Assert.That(result.ResponseError, Does.Contain("Language cannot be null or empty"));
            Assert.That(result.PackageName, Is.EqualTo("azure-test-package"));
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithWhitespaceLanguage_ReturnsError()
        {
            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "   ", "Released");

            // Assert
            Assert.That(result.ResponseError, Does.Contain("Language cannot be null or empty"));
        }

        [TestCase("rust")]
        [TestCase("swift")]
        [TestCase("cpp")]
        [TestCase("invalid-language")]
        public async Task UpdatePackageReleaseStatus_WithUnsupportedLanguage_ReturnsError(string language)
        {
            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", language, "Released");

            // Assert
            Assert.That(result.ResponseError, Does.Contain($"Language '{language}' is not supported"));
            Assert.That(result.ResponseError, Does.Contain("Supported languages:"));
        }

        [TestCase("python")]
        [TestCase(".net")]
        [TestCase("javascript")]
        [TestCase("java")]
        [TestCase("go")]
        [TestCase("Python")]
        [TestCase(".NET")]
        [TestCase("JavaScript")]
        [TestCase("Java")]
        [TestCase("Go")]
        public async Task UpdatePackageReleaseStatus_WithSupportedLanguage_NoReleasePlansFound_ReturnsError(string language)
        {
            // Arrange
            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem>());

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", language, "Released");

            // Assert
            Assert.That(result.ResponseError, Does.Contain("No in-progress release plans found"));
            Assert.That(result.ResponseError, Does.Contain("azure-test-package"));
            Assert.That(result.ReleaseStatus, Is.EqualTo("Released"));
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithSingleReleasePlan_UpdatesSuccessfully()
        {
            // Arrange
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 12345,
                ReleasePlanId = 100,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "python",
                        PackageName = "azure-test-package",
                        PullRequestStatus = "InProgress"
                    }
                }
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test-package", "python", It.IsAny<bool>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(12345, It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 12345 });

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released");

            // Assert
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleaseStatus, Is.EqualTo("Released"));
            Assert.That(result.PackageName, Is.EqualTo("azure-test-package"));

            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(d => 
                    d.ContainsKey("Custom.ReleaseStatusForPython") && d["Custom.ReleaseStatusForPython"] == "Released")),
                Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithMultipleReleasePlans_SelectsMergedPullRequest()
        {
            // Arrange
            var releasePlanWithMergedPR = new ReleasePlanWorkItem
            {
                WorkItemId = 11111,
                ReleasePlanId = 101,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "python",
                        PackageName = "azure-test-package",
                        PullRequestStatus = "Merged"
                    }
                }
            };

            var releasePlanWithOpenPR = new ReleasePlanWorkItem
            {
                WorkItemId = 22222,
                ReleasePlanId = 102,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "python",
                        PackageName = "azure-test-package",
                        PullRequestStatus = "Open"
                    }
                }
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test-package", "python", It.IsAny<bool>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlanWithOpenPR, releasePlanWithMergedPR });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 11111 });

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released");

            // Assert
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleaseStatus, Is.EqualTo("Released"));

            // Verify the one with merged PR was selected (work item 11111)
            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(11111, It.IsAny<Dictionary<string, string>>()),
                Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithMultipleReleasePlans_NoMergedPR_SelectsFirst()
        {
            // Arrange
            var firstReleasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 11111,
                ReleasePlanId = 101,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "python",
                        PackageName = "azure-test-package",
                        PullRequestStatus = "Open"
                    }
                }
            };

            var secondReleasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 22222,
                ReleasePlanId = 102,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "python",
                        PackageName = "azure-test-package",
                        PullRequestStatus = "InProgress"
                    }
                }
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test-package", "python", It.IsAny<bool>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { firstReleasePlan, secondReleasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 11111 });

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released");

            // Assert
            Assert.That(result.ResponseError, Is.Null);

            // Verify the first one was selected (work item 11111)
            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(11111, It.IsAny<Dictionary<string, string>>()),
                Times.Once);
        }

        [TestCase("python", "Custom.ReleaseStatusForPython")]
        [TestCase(".net", "Custom.ReleaseStatusForDotnet")]
        [TestCase("javascript", "Custom.ReleaseStatusForJavaScript")]
        [TestCase("java", "Custom.ReleaseStatusForJava")]
        [TestCase("go", "Custom.ReleaseStatusForGo")]
        public async Task UpdatePackageReleaseStatus_UsesCorrectFieldNameForLanguage(string language, string expectedFieldName)
        {
            // Arrange
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 12345,
                ReleasePlanId = 100,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = language,
                        PackageName = "azure-test-package",
                        PullRequestStatus = "InProgress"
                    }
                }
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 12345 });

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", language, "Released");

            // Assert
            Assert.That(result.ResponseError, Is.Null);

            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(d => d.ContainsKey(expectedFieldName))),
                Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithCustomReleaseStatus_UsesProvidedStatus()
        {
            // Arrange
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 12345,
                ReleasePlanId = 100,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "python",
                        PackageName = "azure-test-package",
                        PullRequestStatus = "Merged"
                    }
                }
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test-package", "python", It.IsAny<bool>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 12345 });

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Pending");

            // Assert
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleaseStatus, Is.EqualTo("Pending"));
            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(d => 
                    d["Custom.ReleaseStatusForPython"] == "Pending")),
                Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WhenDevOpsServiceThrowsException_ReturnsError()
        {
            // Arrange
            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ThrowsAsync(new Exception("DevOps service error"));

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released");

            // Assert
            Assert.That(result.ResponseError, Does.Contain("Failed to update release status"));
            Assert.That(result.ResponseError, Does.Contain("DevOps service error"));
            Assert.That(result.PackageName, Is.EqualTo("azure-test-package"));
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WhenUpdateWorkItemThrowsException_ReturnsError()
        {
            // Arrange
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 12345,
                ReleasePlanId = 100,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "python",
                        PackageName = "azure-test-package",
                        PullRequestStatus = "Merged"
                    }
                }
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>()))
                .ThrowsAsync(new Exception("Failed to update work item"));

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released");

            // Assert
            Assert.That(result.ResponseError, Does.Contain("Failed to update release status"));
            Assert.That(result.ResponseError, Does.Contain("Failed to update work item"));
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_SetsCorrectLanguageOnResponse()
        {
            // Arrange
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 12345,
                ReleasePlanId = 100,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "java",
                        PackageName = "com.azure.test",
                        PullRequestStatus = "Merged"
                    }
                }
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("com.azure.test", "java", It.IsAny<bool>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 12345 });

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("com.azure.test", "java", "Released");

            // Assert
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.Language, Is.EqualTo(SdkLanguage.Java));
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WhenNoMatchingReleasePlanFound_ReturnsErrorWithPackageAndLanguage()
        {
            // Arrange
            var packageName = "azure-nonexistent-package";
            var language = "python";

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync(packageName, language, It.IsAny<bool>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem>());

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus(packageName, language, "Released");

            // Assert
            Assert.That(result.ResponseError, Is.Not.Null);
            Assert.That(result.ResponseError, Does.Contain("No in-progress release plans found"));
            Assert.That(result.ResponseError, Does.Contain(packageName));
            Assert.That(result.ResponseError, Does.Contain(language));
            Assert.That(result.ReleaseStatus, Is.EqualTo("Released"));

            // Verify UpdateWorkItemAsync was never called since no release plan was found
            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>()),
                Times.Never);
        }

        [Test]
        public void Verify_cli_parses_package_name()
        {
            var command = packageReleaseStatusTool.GetCommandInstances().First();
            var parseConfig = new CommandLineConfiguration(command)
            {
                ResponseFileTokenReplacer = null
            };

            var parseResult = command.Parse("--package-name @azure/template --language JavaScript", parseConfig);
            Assert.That(parseResult.Errors, Is.Empty);

            parseResult = command.Parse("--package-name sdk/template/aztemplate --language Go", parseConfig);
            Assert.That(parseResult.Errors, Is.Empty);

            parseResult = command.Parse("--package-name azure-template --language Python", parseConfig);
            Assert.That(parseResult.Errors, Is.Empty);

            parseResult = command.Parse("--package-name Azure.Template --language .NET", parseConfig);
            Assert.That(parseResult.Errors, Is.Empty);
        }
    }
}
