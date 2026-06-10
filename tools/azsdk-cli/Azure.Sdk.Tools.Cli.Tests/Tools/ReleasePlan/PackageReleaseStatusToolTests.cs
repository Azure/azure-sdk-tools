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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus(null!, "python", "Released", null, 0, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Does.Contain("Package name cannot be null or empty"));
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithEmptyPackageName_ReturnsError()
        {
            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("", "python", "Released", null, 0, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Does.Contain("Package name cannot be null or empty"));
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithWhitespacePackageName_ReturnsError()
        {
            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("   ", "python", "Released", null, 0, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Does.Contain("Package name cannot be null or empty"));
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithNullLanguage_ReturnsError()
        {
            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", null!, "Released", null, 0, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Does.Contain("Language cannot be null or empty"));
            Assert.That(result.PackageName, Is.EqualTo("azure-test-package"));
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithEmptyLanguage_ReturnsError()
        {
            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "", "Released", null, 0, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Does.Contain("Language cannot be null or empty"));
            Assert.That(result.PackageName, Is.EqualTo("azure-test-package"));
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_WithWhitespaceLanguage_ReturnsError()
        {
            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "   ", "Released", null, 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", language, "Released", null, 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", language, "Released", null, 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-resourcemanager-containerservice", language, "Released", null, 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released", null, 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released", null, 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released", null, 0, CancellationToken.None);

            // Assert
            Assert.That(result.ResponseError, Is.Null);

            // Verify the first one was selected (work item 11111)
            mockDevOpsService.Verify(
                x => x.UpdateWorkItemAsync(11111, It.Is<Dictionary<string, string>>(d =>
                    d.ContainsKey("Custom.ReleaseStatusForPython")), It.IsAny<CancellationToken>()),
                Times.Once);
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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus(packageName, language, "Released", null, 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Pending", null, 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released", null, 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released", null, 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("com.azure:azure-test", "java", "Released", null, 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus(packageName, language, "Released", null, 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released", "1.2.3", 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Pending", "1.2.3", 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released", null, 0, CancellationToken.None);

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
                "azure-test-package", "python", "Released", null, 200, CancellationToken.None);

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
                "azure-test-package", "python", "Released", null, 999, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null, 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null, 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null, 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null, 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null, 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Pending", null, 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("@azure/test", "javascript", "Released", null, 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test", "python", "Released", null, 0, CancellationToken.None);

            // Assert - release status update succeeded, no ResponseError
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleaseStatus, Is.EqualTo("Released"));
            Assert.That(result.ReleasePlanFinished, Is.False);
            Assert.That(result.Message, Does.Contain("failed to auto-finish"));
        public async Task UpdatePackageReleaseStatus_AlreadyReleasedPackage_ReturnsNoReleasePlansFound()
        {
            // Arrange - GetReleasePlansForPackageAsync now filters out released packages at query level,
            // so it returns an empty list when all matching release plans already have "Released" status
            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test-package", "python", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem>());

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released", "2.0.0", 0, CancellationToken.None);

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
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", "python", "Released", "1.0.0", 0, CancellationToken.None);

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

        [TestCase("python", "Custom.ReleaseStatusForPython")]
        [TestCase(".net", "Custom.ReleaseStatusForDotnet")]
        [TestCase("javascript", "Custom.ReleaseStatusForJavaScript")]
        [TestCase("java", "Custom.ReleaseStatusForJava")]
        [TestCase("go", "Custom.ReleaseStatusForGo")]
        public async Task UpdatePackageReleaseStatus_FiltersByCorrectReleaseStatusField_PerLanguage(string language, string expectedField)
        {
            // Arrange - When GetReleasePlansForPackageAsync is called, it should use
            // the language-specific release status field in the filter query.
            // Here we verify the tool correctly passes language to the service method.
            mockDevOpsService
                .Setup(x => x.GetReleasePlansForPackageAsync("azure-test-package", language, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem>());

            // Act
            var result = await packageReleaseStatusTool.UpdatePackageReleaseStatus("azure-test-package", language, "Released", null, 0, CancellationToken.None);

            // Assert - Verify the service was called with the correct language
            mockDevOpsService.Verify(
                x => x.GetReleasePlansForPackageAsync("azure-test-package", language, It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
