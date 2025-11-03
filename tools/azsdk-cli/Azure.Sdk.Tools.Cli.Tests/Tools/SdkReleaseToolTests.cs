using Microsoft.TeamFoundation.Build.WebApi;
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    internal class SdkReleaseToolTests
    {
        private TestLogger<SdkReleaseTool> logger;
        private IDevOpsService devOpsService;
        private SdkReleaseTool sdkReleaseTool;

        [SetUp]
        public void Setup()
        {

            logger = new TestLogger<SdkReleaseTool>();
            var mockDevOpsService = new Mock<IDevOpsService>();
            mockDevOpsService.Setup(x => x.GetPackageWorkItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new PackageResponse
                {
                    PackageName = "azure-template",
                    Language = SdkLanguage.Python,
                    ResponseError = null,
                    PipelineDefinitionUrl = "https://dev.azure.com/fake-org/fake-project/_build?definitionId=1",
                    WorkItemId = 12345,
                    changeLogStatus = "Approved",
                    APIViewStatus = "Approved",
                    PackageNameStatus = "Approved",
                    PackageRepoPath = "template",
                    LatestPipelineRun = "https://dev.azure.com/fake-org/fake-project/_build/results?buildId=1",
                    LatestPipelineStatus = "Succeeded",
                    WorkItemUrl = "https://dev.azure.com/fake-org/fake-project/_workitems/edit/12345",
                    State = "Active",
                    PlannedReleaseDate = "06/30/2025",
                    DisplayName = "Azure Template",
                    Version = "1.0.0",
                    PlannedReleases = new List<SDKReleaseInfo>
                    {
                        new() {
                            Version = "1.0.0",
                            ReleaseDate = "06/30/2025",
                            ReleaseType = "GA"
                        }
                    },
                });
            mockDevOpsService.Setup(x => x.RunPipelineAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .ReturnsAsync(new Build
                {
                    Id = 1,
                    Status = BuildStatus.InProgress,
                    Result = BuildResult.None,
                    Url = "https://dev.azure.com/fake-org/fake-project/_build/results?buildId=1"
                });
            mockDevOpsService.Setup(x => x.GetPipelineRunAsync(It.IsAny<int>()))
                .ReturnsAsync(new Build
                {
                    Id = 1,
                    Status = BuildStatus.Completed,
                    Result = BuildResult.Succeeded,
                    Url = "https://dev.azure.com/azure-sdk/internal/_build/results?buildId=1"
                });
            devOpsService = mockDevOpsService.Object;

            var releaseReadinessToolLogger = new TestLogger<ReleaseReadinessTool>();

            sdkReleaseTool = new SdkReleaseTool(
                devOpsService,
                logger,
                releaseReadinessToolLogger);
        }

        [Test]
        public async Task TestRunRelease()
        {
            var packageName = "azure-template";
            var language = "Python";
            var result = await sdkReleaseTool.ReleasePackageAsync(packageName, language);
            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result.PackageName, Is.EqualTo(packageName));
                Assert.That(result.Language, Is.EqualTo(SdkLanguage.Python));
                Assert.That(result.ReleaseStatusDetails, Does.Contain("Release pipeline triggered successfully for package 'azure-template'"));
                Assert.That(result.ReleasePipelineRunUrl, Is.EqualTo("https://dev.azure.com/azure-sdk/internal/_build/results?buildId=1"));
            });

        }

        [Test]
        public async Task TestRunReleaseWithInvalidLanguage()
        {
            var packageName = "Azure.Template";
            var language = "csharp";
            var result = await sdkReleaseTool.ReleasePackageAsync(packageName, language);
            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result.PackageName, Is.EqualTo(packageName));
                Assert.That(result.Language, Is.EqualTo(SdkLanguage.DotNet));
                Assert.That(result.ReleaseStatusDetails, Does.Contain("Language must be one of the following"));
            });
        }
    }
}
