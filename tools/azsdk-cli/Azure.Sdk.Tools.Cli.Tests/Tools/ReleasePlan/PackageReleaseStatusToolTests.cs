// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Notification;
using Azure.Sdk.Tools.Cli.Services.Notification.Templates;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using Microsoft.TeamFoundation.Build.WebApi;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.ReleasePlan
{
    [TestFixture]
    internal class PackageReleaseStatusToolTests
    {
        private Mock<IDevOpsService> mockDevOpsService;
        private Mock<INotificationService> mockNotificationService;
        private TestLogger<PackageReleaseStatusTool> logger;
        private PackageReleaseStatusTool packageReleaseStatusTool;

        [SetUp]
        public void Setup()
        {
            mockDevOpsService = new Mock<IDevOpsService>();
            mockDevOpsService
                .Setup(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                    It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            logger = new TestLogger<PackageReleaseStatusTool>();
            mockNotificationService = new Mock<INotificationService>();
            packageReleaseStatusTool = new PackageReleaseStatusTool(mockDevOpsService.Object, logger, mockNotificationService.Object);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithNullPackageName_ReturnsError()
        {
            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus(null!, "python", "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Does.Contain("Package name cannot be null or empty"));
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithEmptyPackageName_ReturnsError()
        {
            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("", "python", "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Does.Contain("Package name cannot be null or empty"));
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithWhitespacePackageName_ReturnsError()
        {
            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("   ", "python", "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Does.Contain("Package name cannot be null or empty"));
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithNullLanguage_ReturnsError()
        {
            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", null!, "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Does.Contain("Language cannot be null or empty"));
            Assert.That(result.PackageName, Is.EqualTo("azure-test-package"));
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithEmptyLanguage_ReturnsError()
        {
            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "", "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Does.Contain("Language cannot be null or empty"));
            Assert.That(result.PackageName, Is.EqualTo("azure-test-package"));
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithWhitespaceLanguage_ReturnsError()
        {
            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "   ", "Released", null, 0, null, null, null, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", language, "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.Message, Does.Contain($"Language '{language}' is not supported"));
            Assert.That(result.Message, Does.Contain("Supported languages:"));
            Assert.That(result.ResponseError, Is.Null);
        }

        [TestCase("python")]
        [TestCase(".net")]
        [TestCase("javascript")]
        [TestCase("go")]
        [TestCase("Python")]
        [TestCase(".NET")]
        [TestCase("JavaScript")]
        [TestCase("Go")]
        public async Task UpdatePackageReleaseStatus_WithSupportedLanguage_NoReleasePlansFound_ReturnsError(string language)
        {
            // Arrange
            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem>());

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", language, "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.Message, Does.Contain("No in-progress release plans found"));
            Assert.That(result.Message, Does.Contain("azure-test-package"));
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleaseStatus, Is.EqualTo("Released"));
        }

        [TestCase("java")]
        [TestCase("Java")]
        public async Task UpdatePackageReleaseStatus_JavaWithSupportedLanguage_NoReleasePlansFound_ReturnsError(string language)
        {
            // Arrange
            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem>());

            // Act - Java packages no longer require groupName:packageName format
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-resourcemanager-containerservice", language, "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.Message, Does.Contain("No in-progress release plans found"));
            Assert.That(result.Message, Does.Contain("azure-resourcemanager-containerservice"));
            Assert.That(result.ResponseError, Is.Null);
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
                },
                APISpecProjectPath = "specification/test/project"
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test-package", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(12345, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 12345 });

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleaseStatus, Is.EqualTo("Released"));
            Assert.That(result.PackageName, Is.EqualTo("azure-test-package"));
            Assert.That(result.TypeSpecProject, Is.EqualTo("specification/test/project"));

            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(d => 
                    d.ContainsKey("Custom.ReleaseStatusForPython") && d["Custom.ReleaseStatusForPython"] == "Released"), It.IsAny<CancellationToken>()),
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
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test-package", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlanWithOpenPR, releasePlanWithMergedPR });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 11111 });

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleaseStatus, Is.EqualTo("Released"));

            // Verify the one with merged PR was selected (work item 11111)
            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(11111, It.Is<Dictionary<string, string>>(d =>
                    d.ContainsKey("Custom.ReleaseStatusForPython")), It.IsAny<CancellationToken>()),
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
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test-package", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { firstReleasePlan, secondReleasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 11111 });

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Is.Null);

            // Verify the first one was selected (work item 11111)
            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(11111, It.Is<Dictionary<string, string>>(d =>
                    d.ContainsKey("Custom.ReleaseStatusForPython")), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithMultipleReleasePlans_AndSdkPullRequest_SelectsMatchingReleasePlan()
        {
            // Arrange
            const string sdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-python/pull/200";

            var nonMatchingReleasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 11111,
                ReleasePlanId = 101,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "python",
                        PackageName = "azure-test-package",
                        SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-python/pull/100",
                        PullRequestStatus = "Open"
                    }
                }
            };

            var matchingReleasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 22222,
                ReleasePlanId = 102,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "python",
                        PackageName = "azure-test-package",
                        SdkPullRequestUrl = sdkPullRequestUrl,
                        PullRequestStatus = "Open"
                    }
                }
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test-package", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { nonMatchingReleasePlan, matchingReleasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 22222 });

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus(
                "azure-test-package", "python", "Released", null, 0, null, null, sdkPullRequestUrl, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleasePlanId, Is.EqualTo(102));

            // Verify only the matching release plan is updated.
            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(22222, It.Is<Dictionary<string, string>>(d =>
                    d.ContainsKey("Custom.ReleaseStatusForPython") && d["Custom.ReleaseStatusForPython"] == "Released"), It.IsAny<CancellationToken>()),
                Times.Once);
            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(11111, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [TestCase("python", "Custom.ReleaseStatusForPython", "azure-test-package")]
        [TestCase(".net", "Custom.ReleaseStatusForDotnet", "azure-test-package")]
        [TestCase("javascript", "Custom.ReleaseStatusForJavaScript", "azure-test-package")]
        [TestCase("java", "Custom.ReleaseStatusForJava", "com.azure:azure-test-package")]
        [TestCase("go", "Custom.ReleaseStatusForGo", "azure-test-package")]
        public async Task UpdatePackageReleaseStatus_UsesCorrectFieldNameForLanguage(string language, string expectedFieldName, string packageName)
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
                        PackageName = packageName,
                        PullRequestStatus = "InProgress"
                    }
                }
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 12345 });

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus(packageName, language, "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Is.Null);

            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(d => d.ContainsKey(expectedFieldName)), It.IsAny<CancellationToken>()),
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
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test-package", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 12345 });

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Pending", null, 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleaseStatus, Is.EqualTo("Pending"));
            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(d => 
                    d["Custom.ReleaseStatusForPython"] == "Pending"), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WhenDevOpsServiceThrowsException_ReturnsError()
        {
            // Arrange
            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("DevOps service error"));

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released", null, 0, null, null, null, CancellationToken.None);

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
                .Setup(x => x.GetReleasePlansForPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Failed to update work item"));

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released", null, 0, null, null, null, CancellationToken.None);

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
                        PackageName = "com.azure:azure-test",
                        PullRequestStatus = "Merged"
                    }
                }
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("com.azure:azure-test", "java", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 12345 });

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("com.azure:azure-test", "java", "Released", null, 0, null, null, null, CancellationToken.None);

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
                .Setup(x => x.GetReleasePlansForPackageAsync(packageName, language, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem>());

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus(packageName, language, "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.Message, Does.Contain("No in-progress release plans found"));
            Assert.That(result.Message, Does.Contain(packageName));
            Assert.That(result.Message, Does.Contain(language));
            Assert.That(result.ReleaseStatus, Is.EqualTo("Released"));

            // Verify UpdateWorkItemAsync was never called since no release plan was found
            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()),
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

        [Test]
        public void Verify_cli_parses_package_version()
        {
            var command = packageReleaseStatusTool.GetCommandInstances().First();
            var parseConfig = new CommandLineConfiguration(command)
            {
                ResponseFileTokenReplacer = null
            };

            var parseResult = command.Parse("--package-name azure-template --language Python --package-version 1.2.3", parseConfig);
            Assert.That(parseResult.Errors, Is.Empty);

            // package-version is optional
            parseResult = command.Parse("--package-name azure-template --language Python", parseConfig);
            Assert.That(parseResult.Errors, Is.Empty);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithVersion_UpdatesVersionField()
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
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test-package", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 12345 });

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released", "1.2.3", 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.PackageVersion, Is.EqualTo("1.2.3"));

            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(d =>
                    d.ContainsKey("Custom.ReleaseStatusForPython") && d["Custom.ReleaseStatusForPython"] == "Released" &&
                    d.ContainsKey("Custom.ReleasedVersionForPython") && d["Custom.ReleasedVersionForPython"] == "1.2.3"), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithVersionAndNonReleasedStatus_UpdatesVersionField()
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
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test-package", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 12345 });

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Pending", "1.2.3", 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Is.Null);

            // Version field should be written regardless of release status
            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(d =>
                    d.ContainsKey("Custom.ReleaseStatusForPython") &&
                    d.ContainsKey("Custom.ReleasedVersionForPython") && d["Custom.ReleasedVersionForPython"] == "1.2.3"), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithNullVersion_DoesNotUpdateVersionField()
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
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test-package", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 12345 });

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.PackageVersion, Is.Null);

            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(d =>
                    d.ContainsKey("Custom.ReleaseStatusForPython") &&
                    !d.ContainsKey("Custom.ReleasedVersionForPython")), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithReleasePlanId_SelectsMatchingPlanFromPackageSearch()
        {
            // Arrange
            var releasePlan1 = new ReleasePlanWorkItem
            {
                WorkItemId = 11111,
                ReleasePlanId = 100,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "python",
                        PackageName = "azure-test-package",
                        PullRequestStatus = "InProgress"
                    }
                },
                APISpecProjectPath = "specification/test/project1"
            };

            var releasePlan2 = new ReleasePlanWorkItem
            {
                WorkItemId = 22222,
                ReleasePlanId = 200,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "python",
                        PackageName = "azure-test-package",
                        PullRequestStatus = "Merged"
                    }
                },
                APISpecProjectPath = "specification/test/project2"
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test-package", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan1, releasePlan2 });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(22222, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 22222 });

            // Act - provide release plan ID 200 to select the second plan
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus(
                "azure-test-package", "python", "Released", null, 200, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleaseStatus, Is.EqualTo("Released"));
            Assert.That(result.ReleasePlanId, Is.EqualTo(200));

            // Verify it always searched by package name first
            mockDevOpsService.Verify(
                x => x.GetReleasePlansForPackageAsync("azure-test-package", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Once);
            // Verify the correct plan was updated
            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(22222, It.Is<Dictionary<string, string>>(d =>
                    d.ContainsKey("Custom.ReleaseStatusForPython")), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithReleasePlanId_NotInResults_ReturnsMessage()
        {
            // Arrange
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 11111,
                ReleasePlanId = 100,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "python",
                        PackageName = "azure-test-package",
                        PullRequestStatus = "InProgress"
                    }
                },
                APISpecProjectPath = "specification/test/project"
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test-package", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            // Act - provide release plan ID 999 that doesn't match any result
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus(
                "azure-test-package", "python", "Released", null, 999, null, null, null, CancellationToken.None);

            // Assert - returns message, does not update any work item
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.Message, Does.Contain("999"));
            Assert.That(result.Message, Does.Contain("azure-test-package"));
            Assert.That(result.Message, Does.Contain("python"));

            // Verify it searched by package name but did NOT update any work item
            mockDevOpsService.Verify(
                x => x.GetReleasePlansForPackageAsync("azure-test-package", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Once);
            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public void Verify_cli_parses_release_plan_id()
        {
            var command = packageReleaseStatusTool.GetCommandInstances().First();
            var parseConfig = new CommandLineConfiguration(command)
            {
                ResponseFileTokenReplacer = null
            };

            var parseResult = command.Parse("--package-name azure-template --language Python --release-plan-id 12345", parseConfig);
            Assert.That(parseResult.Errors, Is.Empty);

            // release-plan-id is optional
            parseResult = command.Parse("--package-name azure-template --language Python", parseConfig);
            Assert.That(parseResult.Errors, Is.Empty);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_MgmtPlane_AllReleased_MarksFinished()
        {
            // Arrange
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 12345,
                ReleasePlanId = 100,
                IsManagementPlane = true,
                IsDataPlane = false,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo { Language = ".NET", PackageName = "Azure.Test", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Java", PackageName = "azure-test", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Python", PackageName = "azure-test", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable", PullRequestStatus = "Merged" },
                    new SDKInfo { Language = "JavaScript", PackageName = "@azure/test", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Go", PackageName = "sdk/test/aztest", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" }
                }
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 12345 });

            // Act - releasing the last language (Python)
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleasePlanFinished, Is.True);

            // Verify state was set to Finished
            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(d =>
                    d.ContainsKey("System.State") && d["System.State"] == "Finished"), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_MgmtPlane_ReleasedAndExcluded_MarksFinished()
        {
            // Arrange
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 12345,
                ReleasePlanId = 100,
                IsManagementPlane = true,
                IsDataPlane = false,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo { Language = ".NET", PackageName = "Azure.Test", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Java", PackageName = "azure-test", ReleaseStatus = "", ReleaseExclusionStatus = "Approved" },
                    new SDKInfo { Language = "Python", PackageName = "azure-test", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable", PullRequestStatus = "Merged" },
                    new SDKInfo { Language = "JavaScript", PackageName = "@azure/test", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Go", PackageName = "sdk/test/aztest", ReleaseStatus = "", ReleaseExclusionStatus = "Approved" }
                }
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 12345 });

            // Act - releasing Python (last non-excluded language)
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleasePlanFinished, Is.True);

            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(d =>
                    d.ContainsKey("System.State") && d["System.State"] == "Finished"), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_MgmtPlane_NotAllComplete_DoesNotFinish()
        {
            // Arrange
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 12345,
                ReleasePlanId = 100,
                IsManagementPlane = true,
                IsDataPlane = false,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo { Language = ".NET", PackageName = "Azure.Test", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Java", PackageName = "azure-test", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Python", PackageName = "azure-test", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable", PullRequestStatus = "Merged" },
                    new SDKInfo { Language = "JavaScript", PackageName = "@azure/test", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Go", PackageName = "sdk/test/aztest", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" }
                }
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 12345 });

            // Act - releasing Python but Java, JS, Go still pending
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleasePlanFinished, Is.False);

            // Verify state was NOT set to Finished (only the release status update happened)
            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(d =>
                    d.ContainsKey("System.State")), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_DataPlane_AllFourLanguagesReleased_MarksFinished()
        {
            // Arrange - data plane: Go should be ignored
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 12345,
                ReleasePlanId = 100,
                IsManagementPlane = false,
                IsDataPlane = true,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo { Language = ".NET", PackageName = "Azure.Test", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Java", PackageName = "azure-test", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Python", PackageName = "azure-test", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable", PullRequestStatus = "Merged" },
                    new SDKInfo { Language = "JavaScript", PackageName = "@azure/test", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Go", PackageName = "sdk/test/aztest", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" }
                }
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 12345 });

            // Act - releasing Python (last of the 4 data plane languages)
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert - Go is not released but should be ignored for data plane
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleasePlanFinished, Is.True);

            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(d =>
                    d.ContainsKey("System.State") && d["System.State"] == "Finished"), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_DataPlane_NotAllFourComplete_DoesNotFinish()
        {
            // Arrange
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 12345,
                ReleasePlanId = 100,
                IsManagementPlane = false,
                IsDataPlane = true,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo { Language = ".NET", PackageName = "Azure.Test", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Java", PackageName = "azure-test", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Python", PackageName = "azure-test", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable", PullRequestStatus = "Merged" },
                    new SDKInfo { Language = "JavaScript", PackageName = "@azure/test", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Go", PackageName = "sdk/test/aztest", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" }
                }
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 12345 });

            // Act - releasing Python but Java and JS still pending
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleasePlanFinished, Is.False);

            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(d =>
                    d.ContainsKey("System.State")), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_StatusNotReleased_DoesNotTriggerFinishCheck()
        {
            // Arrange
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 12345,
                ReleasePlanId = 100,
                IsManagementPlane = true,
                IsDataPlane = false,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo { Language = ".NET", PackageName = "Azure.Test", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Java", PackageName = "azure-test", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Python", PackageName = "azure-test", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable", PullRequestStatus = "Merged" },
                    new SDKInfo { Language = "JavaScript", PackageName = "@azure/test", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Go", PackageName = "sdk/test/aztest", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" }
                }
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 12345 });

            // Act - setting status to "Pending" (not "Released")
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Pending", null, 0, null, null, null, CancellationToken.None);

            // Assert - finish check should not be triggered
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleasePlanFinished, Is.False);

            // Only one UpdateWorkItemAsync call for the status update, none for System.State
            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(d =>
                    d.ContainsKey("System.State")), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_DataPlane_GoNotReleasedOthersComplete_MarksFinished()
        {
            // Arrange - specifically testing that Go being unreleased doesn't block data plane finish
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 12345,
                ReleasePlanId = 100,
                IsManagementPlane = false,
                IsDataPlane = true,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo { Language = ".NET", PackageName = "Azure.Test", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Java", PackageName = "azure-test", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Python", PackageName = "azure-test", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "JavaScript", PackageName = "@azure/test", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable", PullRequestStatus = "Merged" },
                    new SDKInfo { Language = "Go", PackageName = "sdk/test/aztest", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" }
                }
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("@azure/test", "javascript", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 12345 });

            // Act - releasing JavaScript (last of the 4 data plane languages)
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("@azure/test", "javascript", "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleasePlanFinished, Is.True);

            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(d =>
                    d.ContainsKey("System.State") && d["System.State"] == "Finished"), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_FinishFails_StillReturnsSuccessfulStatusUpdate()
        {
            // Arrange - all languages complete, but the Finished state update will throw
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 12345,
                ReleasePlanId = 100,
                IsManagementPlane = true,
                IsDataPlane = false,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo { Language = ".NET", PackageName = "Azure.Test", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Java", PackageName = "azure-test", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Python", PackageName = "azure-test", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable", PullRequestStatus = "Merged" },
                    new SDKInfo { Language = "JavaScript", PackageName = "@azure/test", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Go", PackageName = "sdk/test/aztest", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" }
                }
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { releasePlan });

            // Release status update succeeds
            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(d =>
                    d.ContainsKey("Custom.ReleaseStatusForPython")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 12345 });

            // Finished state update fails
            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(d =>
                    d.ContainsKey("System.State")), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("State transition not allowed"));

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert - release status update succeeded, no ResponseError
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleaseStatus, Is.EqualTo("Released"));
            Assert.That(result.ReleasePlanFinished, Is.False);
            Assert.That(result.Message, Does.Contain("failed to auto-finish"));
            mockDevOpsService.Verify(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()), Times.Never);
            VerifyNoAutomation();
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_FinishedManagementPlan_QueuesNearestNewerReleasePlan()
        {
            var finishedReleasePlan = CreateCompleteManagementPlaneReleasePlan("2025-01-01-preview");
            var nearestNewerPlan = new ReleasePlanWorkItem
            {
                WorkItemId = 200,
                ReleasePlanId = 200,
                Status = "In Progress",
                APISpecProjectPath = finishedReleasePlan.APISpecProjectPath,
                SpecAPIVersion = "2025-01-01",
                ApiReleaseType = ApiReleaseType.GA
            };
            var laterPlan = new ReleasePlanWorkItem
            {
                WorkItemId = 300,
                ReleasePlanId = 300,
                Status = "In Progress",
                APISpecProjectPath = finishedReleasePlan.APISpecProjectPath,
                SpecAPIVersion = "2025-06-01-preview",
                ApiReleaseType = ApiReleaseType.PublicPreview
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync(
                    "azure-test", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([finishedReleasePlan]);
            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(
                    finishedReleasePlan.WorkItemId,
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = finishedReleasePlan.WorkItemId });
            mockDevOpsService
                .Setup(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                    finishedReleasePlan.APISpecProjectPath,
                    ApiReleaseType.Unknown,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([laterPlan, nearestNewerPlan]);
            mockDevOpsService
                .Setup(x => x.RunPipelineAsync(
                    8254,
                    It.IsAny<Dictionary<string, string>>(),
                    "main",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Build { Id = 9876 });

            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus(
                "azure-test", "python", "Released", null, ct: CancellationToken.None);

            Assert.That(result.ReleasePlanFinished, Is.True);
            Assert.That(result.ReleasePlanAutomationTriggered, Is.True);
            Assert.That(result.QueuedReleasePlanId, Is.EqualTo(200));
            Assert.That(result.ReleasePlanAutomationPipelineUrl, Does.Contain("buildId=9876"));
            mockDevOpsService.Verify(x => x.EnsureReleasePlanAutomationRelationAsync(
                nearestNewerPlan.WorkItemId, finishedReleasePlan.WorkItemId, It.IsAny<CancellationToken>()), Times.Once);
            mockNotificationService.Verify(x => x.SendEmailNotificationAsync(
                It.Is<EmailPayload>(email => email is ReleasePlanSdkGenerationEmail
                    && email.Body.Contains("?releasePlan=100")
                    && email.Body.Contains("?releasePlan=200")),
                It.IsAny<CancellationToken>()), Times.Once);
            mockDevOpsService.Verify(x => x.RunPipelineAsync(
                8254,
                It.Is<Dictionary<string, string>>(parameters =>
                    parameters.Count == 1 && parameters["ReleasePlanId"] == "200"),
                "main",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_FinishedDataPlane_DoesNotQueueAutomation()
        {
            var releasePlan = CreateCompleteManagementPlaneReleasePlan("2025-01-01");
            releasePlan.IsManagementPlane = false;
            releasePlan.IsDataPlane = true;

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync(
                    "azure-test", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([releasePlan]);
            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(
                    releasePlan.WorkItemId,
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = releasePlan.WorkItemId });

            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus(
                "azure-test", "python", "Released", null, ct: CancellationToken.None);

            Assert.That(result.ReleasePlanFinished, Is.True);
            Assert.That(result.ReleasePlanAutomationTriggered, Is.False);
            mockDevOpsService.Verify(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()), Times.Never);
            mockDevOpsService.Verify(x => x.RunPipelineAsync(
                It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_AutomationFailure_PreservesFinishedResult()
        {
            var finishedReleasePlan = CreateCompleteManagementPlaneReleasePlan("2025-01-01");
            var newerPlan = new ReleasePlanWorkItem
            {
                WorkItemId = 200,
                ReleasePlanId = 200,
                Status = "In Progress",
                SpecAPIVersion = "2025-02-01-preview",
                ApiReleaseType = ApiReleaseType.PublicPreview
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync(
                    "azure-test", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([finishedReleasePlan]);
            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(
                    finishedReleasePlan.WorkItemId,
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = finishedReleasePlan.WorkItemId });
            mockDevOpsService
                .Setup(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                    finishedReleasePlan.APISpecProjectPath,
                    ApiReleaseType.Unknown,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([newerPlan]);
            mockDevOpsService
                .Setup(x => x.RunPipelineAsync(
                    8254,
                    It.IsAny<Dictionary<string, string>>(),
                    "main",
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Pipeline unavailable"));

            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus(
                "azure-test", "python", "Released", null, ct: CancellationToken.None);

            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleasePlanFinished, Is.True);
            Assert.That(result.ReleasePlanAutomationTriggered, Is.False);
            Assert.That(result.Message, Does.Contain("could not be queued"));
            Assert.That(result.NextSteps, Has.Some.Contains("azsdk agent"));
            mockNotificationService.Verify(x => x.SendEmailNotificationAsync(
                It.Is<EmailPayload>(email => email.Subject.Contains("Action required")
                    && email.Body.Contains("unable to queue")
                    && email.Body.Contains("azsdk agent")
                    && email.Body.Contains("?releasePlan=200")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_RejectsIneligibleAndInvalidCandidates()
        {
            var finishedReleasePlan = CreateCompleteManagementPlaneReleasePlan("2025-01-01");
            var candidates = new[]
            {
                new ReleasePlanWorkItem { WorkItemId = 101, ReleasePlanId = 101, Status = "Not Started", SpecAPIVersion = "2025-02-01", ApiReleaseType = ApiReleaseType.GA },
                new ReleasePlanWorkItem { WorkItemId = 102, ReleasePlanId = 102, Status = "In Progress", SpecAPIVersion = "2025-02-01", ApiReleaseType = ApiReleaseType.PrivatePreview },
                new ReleasePlanWorkItem { WorkItemId = 103, ReleasePlanId = 103, Status = "In Progress", SpecAPIVersion = "invalid", ApiReleaseType = ApiReleaseType.GA },
                new ReleasePlanWorkItem { WorkItemId = 104, ReleasePlanId = 104, Status = "In Progress", SpecAPIVersion = "2024-12-01", ApiReleaseType = ApiReleaseType.GA }
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync(
                    "azure-test", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([finishedReleasePlan]);
            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(
                    finishedReleasePlan.WorkItemId,
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = finishedReleasePlan.WorkItemId });
            mockDevOpsService
                .Setup(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                    finishedReleasePlan.APISpecProjectPath,
                    ApiReleaseType.Unknown,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([.. candidates]);

            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus(
                "azure-test", "python", "Released", null, ct: CancellationToken.None);

            Assert.That(result.ReleasePlanFinished, Is.True);
            Assert.That(result.ReleasePlanAutomationTriggered, Is.False);
            Assert.That(result.Warnings, Has.Some.Contains("103"));
            Assert.That(result.Message, Does.Contain("valid metadata"));
            mockDevOpsService.Verify(x => x.RunPipelineAsync(
                It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private static ReleasePlanWorkItem CreateCompleteManagementPlaneReleasePlan(string apiVersion)
        {
            return new ReleasePlanWorkItem
            {
                WorkItemId = 12345,
                ReleasePlanId = 100,
                Status = "In Progress",
                IsManagementPlane = true,
                APISpecProjectPath = "specification/test/Contoso.Management",
                SpecAPIVersion = apiVersion,
                SDKInfo =
                [
                    new SDKInfo { Language = ".NET", ReleaseStatus = "Released" },
                    new SDKInfo { Language = "Java", ReleaseStatus = "Released" },
                    new SDKInfo { Language = "Python", ReleaseStatus = "", PullRequestStatus = "Merged" },
                    new SDKInfo { Language = "JavaScript", ReleaseStatus = "Released" },
                    new SDKInfo { Language = "Go", ReleaseStatus = "Released" }
                ]
            };
        }

        private ReleasePlanWorkItem ConfigureAutomation(string currentVersion = "2025-01-01", string nextVersion = "2025-02-01")
        {
            var completed = CreateCompleteManagementPlaneReleasePlan(currentVersion);
            completed.IsTestReleasePlan = bool.TryParse(Environment.GetEnvironmentVariable("AZSDKTOOLS_AGENT_TESTING"), out var testing) && testing;
            var next = new ReleasePlanWorkItem
            {
                WorkItemId = 23456,
                ReleasePlanId = 200,
                Status = "In Progress",
                IsManagementPlane = true,
                IsTestReleasePlan = completed.IsTestReleasePlan,
                APISpecProjectPath = completed.APISpecProjectPath,
                ApiReleaseType = ApiReleaseType.PublicPreview,
                SpecAPIVersion = nextVersion,
                ReleasePlanSubmittedByEmail = "submitter@microsoft.com"
            };
            mockDevOpsService.Setup(x => x.GetReleasePlansForPackageAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([completed]);
            mockDevOpsService.Setup(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                completed.APISpecProjectPath, ApiReleaseType.Unknown, It.IsAny<CancellationToken>()))
                .ReturnsAsync([next]);
            mockDevOpsService.Setup(x => x.UpdateWorkItemAsync(
                completed.WorkItemId, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = completed.WorkItemId });
            mockDevOpsService.Setup(x => x.RunPipelineAsync(
                8254, It.IsAny<Dictionary<string, string>>(), "main", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Build { Id = 9876 });
            return completed;
        }

        private void VerifyNoAutomation()
        {
            mockDevOpsService.Verify(x => x.RunPipelineAsync(
                It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            mockNotificationService.Verify(x => x.SendEmailNotificationAsync(
                It.IsAny<EmailPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        public async Task UpdatePackageReleaseStatus_DisabledPlane_DoesNotQueue(bool management, bool data)
        {
            var completed = ConfigureAutomation();
            completed.IsManagementPlane = management;
            completed.IsDataPlane = data;
            var response = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null);
            Assert.That(response.ReleasePlanFinished, Is.True);
            Assert.That(response.Message, Does.Contain("only for management-plane"));
            VerifyNoAutomation();
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_LinksBeforeQueueingAndNotifiesAfterQueueing()
        {
            var completed = ConfigureAutomation();
            var calls = new List<string>();
            mockDevOpsService.Setup(x => x.UpdateWorkItemAsync(
                completed.WorkItemId, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .Callback<int, Dictionary<string, string>, CancellationToken>((_, fields, _) =>
                    calls.Add(fields.ContainsKey("System.State") ? "finish" : "release"))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = completed.WorkItemId });
            mockDevOpsService.Setup(x => x.EnsureReleasePlanAutomationRelationAsync(
                23456, completed.WorkItemId, It.IsAny<CancellationToken>()))
                .Callback(() => calls.Add("relate")).Returns(Task.CompletedTask);
            mockDevOpsService.Setup(x => x.RunPipelineAsync(
                8254, It.IsAny<Dictionary<string, string>>(), "main", It.IsAny<CancellationToken>()))
                .Callback(() => calls.Add("queue")).ReturnsAsync(new Build { Id = 9876 });
            mockNotificationService.Setup(x => x.SendEmailNotificationAsync(It.IsAny<EmailPayload>(), It.IsAny<CancellationToken>()))
                .Callback(() => calls.Add("notify")).Returns(Task.CompletedTask);

            await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null);
            Assert.That(calls, Is.EqualTo(new[] { "release", "finish", "relate", "queue", "notify" }));
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_RelationFailure_NotifiesWithoutQueueing()
        {
            ConfigureAutomation();
            mockDevOpsService.Setup(x => x.EnsureReleasePlanAutomationRelationAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Relation denied"));

            var response = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null);
            Assert.That(response.ReleasePlanFinished, Is.True);
            Assert.That(response.Message, Does.Contain("Relation denied"));
            Assert.That(response.NextSteps, Has.Some.Contains("azsdk agent"));
            mockDevOpsService.Verify(x => x.RunPipelineAsync(
                It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            mockNotificationService.Verify(x => x.SendEmailNotificationAsync(
                It.Is<EmailPayload>(email => email.Subject.Contains("Action required")
                    && email.Body.Contains("unable to queue")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_LookupFailure_ReportsFailureWithoutQueueing()
        {
            ConfigureAutomation();
            mockDevOpsService.Setup(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Lookup unavailable"));
            var response = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null);
            Assert.That(response.ReleasePlanFinished, Is.True);
            Assert.That(response.Message, Does.Contain("Lookup unavailable"));
            Assert.That(response.NextSteps, Has.Some.Contains("azsdk agent"));
            VerifyNoAutomation();
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_NoCandidates_IsSuccessfulNoOp()
        {
            ConfigureAutomation();
            mockDevOpsService.Setup(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            var response = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null);
            Assert.That(response.Message, Does.Contain("No newer"));
            Assert.That(response.Warnings, Is.Null);
            Assert.That(response.NextSteps, Is.Null);
            VerifyNoAutomation();
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_NotificationFailure_DoesNotMisreportQueueFailure()
        {
            ConfigureAutomation();
            mockNotificationService.Setup(x => x.SendEmailNotificationAsync(It.IsAny<EmailPayload>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Email unavailable"));
            var response = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null);
            Assert.That(response.ReleasePlanAutomationTriggered, Is.True);
            Assert.That(response.ReleasePlanAutomationPipelineUrl, Does.Contain("buildId=9876"));
            Assert.That(response.Warnings, Has.Some.Contains("notification failed"));
            Assert.That(response.NextSteps, Is.Null);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task UpdatePackageReleaseStatus_QueueOutcome_IsAccurateInPlainAndJson(bool queued)
        {
            ConfigureAutomation();
            if (!queued)
            {
                mockDevOpsService.Setup(x => x.RunPipelineAsync(
                    8254, It.IsAny<Dictionary<string, string>>(), "main", It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new InvalidOperationException("Queue unavailable"));
            }

            var response = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null);
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));
            Assert.That(json.RootElement.GetProperty("release_plan_finished").GetBoolean(), Is.True);
            if (queued)
            {
                Assert.That(json.RootElement.GetProperty("release_plan_automation_triggered").GetBoolean(), Is.True);
                Assert.That(json.RootElement.GetProperty("queued_release_plan_id").GetInt32(), Is.EqualTo(200));
                Assert.That(json.RootElement.GetProperty("release_plan_automation_pipeline_url").GetString(), Does.Contain("buildId=9876"));
                Assert.That(response.ToString(), Does.Contain("Queued release plan 200"));
            }
            else
            {
                Assert.That(json.RootElement.TryGetProperty("release_plan_automation_triggered", out _), Is.False);
                Assert.That(json.RootElement.TryGetProperty("queued_release_plan_id", out _), Is.False);
                Assert.That(json.RootElement.TryGetProperty("release_plan_automation_pipeline_url", out _), Is.False);
                Assert.That(response.ToString(), Does.Contain("Queue unavailable").And.Contain("azsdk agent"));
                Assert.That(response.ToString(), Does.Not.Contain("Queued release plan"));
            }
            mockNotificationService.Verify(x => x.SendEmailNotificationAsync(
                It.Is<EmailPayload>(email => email.Subject.StartsWith("Action required") == !queued),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_MissingProjectPath_ReportsMetadataError()
        {
            var completed = ConfigureAutomation();
            completed.APISpecProjectPath = "";
            var response = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null);
            Assert.That(response.ReleasePlanFinished, Is.True);
            Assert.That(response.Message, Does.Contain("no TypeSpec project path"));
            mockDevOpsService.Verify(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()), Times.Never);
            VerifyNoAutomation();
        }

        [TestCase("2025-01-01", "2025-02-01", true)]
        [TestCase("2025-02-01", "2025-01-01", false)]
        [TestCase("2025-01-01-preview", "2025-01-01", true)]
        [TestCase("2025-01-01", "2025-01-01-preview", false)]
        [TestCase("2025-01-01", "2025-01-01", false)]
        [TestCase("2025-01-01-preview", "2025-01-01-preview", false)]
        [TestCase("2025-01-01", "2025-02-01-preview", true)]
        [TestCase("2024-12-31", "2025-01-01-preview", true)]
        [TestCase("2024-02-28", "2024-02-29", true)]
        public async Task UpdatePackageReleaseStatus_VersionOrdering(string current, string next, bool expected)
        {
            ConfigureAutomation(current, next);
            var response = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null);
            Assert.That(response.ReleasePlanAutomationTriggered, Is.EqualTo(expected));
            if (!expected)
            {
                VerifyNoAutomation();
            }
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("invalid")]
        [TestCase("2025-02-29")]
        [TestCase("2025-2-01")]
        [TestCase("2025-02-01-beta")]
        public async Task UpdatePackageReleaseStatus_InvalidCandidateVersion_IsVisibleInPlainAndJson(string? version)
        {
            ConfigureAutomation(nextVersion: version!);
            var response = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null);
            Assert.That(response.Warnings, Has.Some.Contains("Skipped release plan 200"));
            Assert.That(response.ToString(), Does.Contain("[WARNING]"));
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));
            Assert.That(json.RootElement.GetProperty("warnings").GetArrayLength(), Is.EqualTo(1));
            Assert.That(response.Message, Does.Contain("correct the release plan metadata"));
            VerifyNoAutomation();
        }

        [TestCase("")]
        [TestCase("invalid")]
        [TestCase("2025-02-29")]
        public async Task UpdatePackageReleaseStatus_InvalidCompletedVersion_ReportsMetadataError(string version)
        {
            ConfigureAutomation(currentVersion: version);
            var response = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null);
            Assert.That(response.ReleasePlanFinished, Is.True);
            Assert.That(response.Message, Does.Contain("invalid API version"));
            Assert.That(response.Message, Does.Not.Contain("No newer"));
            Assert.That(response.NextSteps, Has.Some.Contains("azsdk agent"));
            VerifyNoAutomation();
        }

        [TestCase("finish")]
        [TestCase("lookup")]
        [TestCase("queue")]
        public void UpdatePackageReleaseStatus_Cancellation_Propagates(string stage)
        {
            var completed = ConfigureAutomation();
            using var cts = new CancellationTokenSource();
            if (stage == "finish")
            {
                mockDevOpsService.Setup(x => x.UpdateWorkItemAsync(
                    completed.WorkItemId, It.Is<Dictionary<string, string>>(d => d.ContainsKey("System.State")), cts.Token))
                    .Callback(() => cts.Cancel()).ThrowsAsync(new OperationCanceledException(cts.Token));
            }
            else if (stage == "lookup")
            {
                mockDevOpsService.Setup(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                    completed.APISpecProjectPath, ApiReleaseType.Unknown, cts.Token))
                    .Callback(() => cts.Cancel()).ThrowsAsync(new OperationCanceledException(cts.Token));
            }
            else
            {
                mockDevOpsService.Setup(x => x.RunPipelineAsync(
                    8254, It.IsAny<Dictionary<string, string>>(), "main", cts.Token))
                    .Callback(() => cts.Cancel()).ThrowsAsync(new OperationCanceledException(cts.Token));
            }
            Assert.ThrowsAsync<OperationCanceledException>(() =>
                packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null, ct: cts.Token));
            mockNotificationService.Verify(x => x.SendEmailNotificationAsync(
                It.IsAny<EmailPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_AlreadyReleasedPackage_ReturnsNoReleasePlansFound()
        {
            // Arrange - GetReleasePlansForPackageAsync now filters out released packages at query level,
            // so it returns an empty list when all matching release plans already have "Released" status
            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test-package", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem>());

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released", "2.0.0", 0, null, null, null, CancellationToken.None);

            // Assert - The tool should report no in-progress release plans found
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.Message, Does.Contain("No in-progress release plans found"));
            Assert.That(result.Message, Does.Contain("azure-test-package"));
            Assert.That(result.Message, Does.Contain("python"));

            // Verify no work item update was attempted
            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_OnlyNonReleasedPlansReturned_UpdatesCorrectPlan()
        {
            // Arrange - Simulate that GetReleasePlansForPackageAsync only returns plans where
            // the package has NOT been released (the released ones are filtered out by the query)
            var nonReleasedPlan = new ReleasePlanWorkItem
            {
                WorkItemId = 33333,
                ReleasePlanId = 300,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "python",
                        PackageName = "azure-test-package",
                        PullRequestStatus = "Merged"
                    }
                },
                APISpecProjectPath = "specification/test/project"
            };

            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test-package", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem> { nonReleasedPlan });

            mockDevOpsService
                .Setup(x => x.UpdateWorkItemAsync(33333, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 33333 });

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released", "1.0.0", 0, null, null, null, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleaseStatus, Is.EqualTo("Released"));
            Assert.That(result.ReleasePlanId, Is.EqualTo(300));
            Assert.That(result.TypeSpecProject, Is.EqualTo("specification/test/project"));

            // Verify the correct work item was updated
            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(33333, It.Is<Dictionary<string, string>>(d =>
                    d.ContainsKey("Custom.ReleaseStatusForPython") && d["Custom.ReleaseStatusForPython"] == "Released"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestCase("python")]
        [TestCase(".net")]
        [TestCase("javascript")]
        [TestCase("java")]
        [TestCase("go")]
        public async Task UpdatePackageReleaseStatus_FiltersByCorrectReleaseStatusField_PerLanguage(string language)
        {
            // Arrange - When GetReleasePlansForPackageAsync is called, it should use
            // the language-specific release status field in the filter query.
            // Here we verify the tool correctly passes language to the service method.
            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test-package", language, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem>());

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", language, "Released", null, 0, null, null, null, CancellationToken.None);

            // Assert - Verify the service was called with the correct language
            mockDevOpsService.Verify(
                x => x.GetReleasePlansForPackageAsync("azure-test-package", language, It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
