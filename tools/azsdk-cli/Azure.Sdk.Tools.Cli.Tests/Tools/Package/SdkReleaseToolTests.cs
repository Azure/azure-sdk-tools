using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.APIView;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package
{
    internal class SdkReleaseToolTests
    {
        private TestLogger<SdkReleaseTool> logger;
        private MockDevOpsService devOpsService;
        private Mock<IAPIViewService> mockApiViewService;
        private SdkReleaseTool sdkReleaseTool;

        [SetUp]
        public void Setup()
        {

            logger = new TestLogger<SdkReleaseTool>();
            devOpsService = new MockDevOpsService();
            mockApiViewService = new Mock<IAPIViewService>();
            sdkReleaseTool = new SdkReleaseTool(
                devOpsService,
                mockApiViewService.Object,
                logger,
                new InputSanitizer(),
                new Mock<IEnvironmentHelper>().Object);
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
            var language = "net";
            var result = await sdkReleaseTool.ReleasePackageAsync(packageName, language);
            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result.PackageName, Is.EqualTo(packageName));
                Assert.That(result.Language, Is.EqualTo(SdkLanguage.Unknown));
                Assert.That(result.ReleaseStatusDetails, Does.Contain("Language must be one of the following"));
            });
        }

        [Test]
        public async Task TestRunReleaseWithCheckReady()
        {
            var packageName = "azure-template";
            var language = "Python";
            var result = await sdkReleaseTool.ReleasePackageAsync(packageName, language, "main", checkReady: true);

            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result.PackageName, Is.EqualTo(packageName));
                Assert.That(result.Language, Is.EqualTo(SdkLanguage.Python));
                Assert.That(result.ReleaseStatusDetails, Does.Contain("Package 'azure-template' is ready for release."));
                Assert.That(result.ReleasePipelineRunUrl, Is.EqualTo(string.Empty));
                Assert.That(result.PipelineBuildId, Is.EqualTo(0));
            });
        }

        [Test]
        public async Task TestRunReleaseWithCsharpLanguage()
        {
            var packageName = "Azure.Template";
            var language = "csharp";
            var result = await sdkReleaseTool.ReleasePackageAsync(packageName, language);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Language, Is.EqualTo(SdkLanguage.DotNet));

            result.SetPackageType("mgmt");
            Assert.That(result.PackageType, Is.EqualTo(SdkType.Management));
            result.SetPackageType("client");
            Assert.That(result.PackageType, Is.EqualTo(SdkType.Dataplane));
            result.SetPackageType("spring");
            Assert.That(result.PackageType, Is.EqualTo(SdkType.Spring));
            result.SetPackageType("data");
            Assert.That(result.PackageType, Is.EqualTo(SdkType.Unknown));
        }

        [Test]
        public async Task TestCheckReadyWithApiViewNotApproved_IncludesApiViewUrl()
        {
            var packageName = "Azure.Security.KeyVault.Secrets";
            var language = ".NET";
            var expectedUrl = "https://apiview.dev/review/abc123";

            devOpsService.ConfiguredAPIViewStatus = "Pending";
            mockApiViewService
                .Setup(x => x.GetReviewUrlByPackageAsync(packageName, "C#", "1.0.0", It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedUrl);

            var result = await sdkReleaseTool.ReleasePackageAsync(packageName, language, "main", checkReady: true);

            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result.ReleaseStatusDetails, Does.Contain("not ready for release"));
                Assert.That(result.ReleaseStatusDetails, Does.Contain(expectedUrl));
            });
        }

        [Test]
        public async Task TestCheckReadyWithApiViewNotApproved_FallbackWhenUrlUnresolvable()
        {
            var packageName = "Azure.Security.KeyVault.Secrets";
            var language = ".NET";

            devOpsService.ConfiguredAPIViewStatus = "Pending";
            mockApiViewService
                .Setup(x => x.GetReviewUrlByPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            var result = await sdkReleaseTool.ReleasePackageAsync(packageName, language, "main", checkReady: true);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ReleaseStatusDetails, Does.Contain("https://apiview.dev"));
        }

        [Test]
        public async Task TestCheckReadyTreatsPythonPep440PrereleaseAsPreview()
        {
            var packageName = "azure-template";
            var language = "Python";

            devOpsService.ConfiguredPackageVersion = "1.0.0b1";
            devOpsService.ConfiguredPackageType = SdkType.Dataplane;
            devOpsService.ConfiguredAPIViewStatus = "Pending";

            var result = await sdkReleaseTool.ReleasePackageAsync(packageName, language, "main", checkReady: true);

            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result.ReleaseStatusDetails, Does.Contain("Package 'azure-template' is ready for release."));
                Assert.That(result.ReleaseStatusDetails, Does.Not.Contain("API view is not approved"));
            });
        }

        [TestCase("Python", "1.0.0.post1", false)]
        [TestCase("Python", "1.0.0.post1+linux-x64", false)]
        [TestCase("Python", "1.0.0+build-beta.1", false)]
        [TestCase("Python", "1.0.0-post1", false)]
        [TestCase("Python", "1.0.0post1", false)]
        [TestCase("Python", "1.0.0.POST.1", false)]
        [TestCase("Python", "1.0.0-1", false)]
        [TestCase("Python", "1.0.0-r1", false)]
        [TestCase("Python", "1.0.0a1", true)]
        [TestCase("Python", "1.0.0b1", true)]
        [TestCase("Python", "1.0.0rc1", true)]
        [TestCase("Python", "1.0.0.dev1", true)]
        [TestCase("Python", "1.0.0.post1.dev1", true)]
        [TestCase("Python", "1.0.0b1.post1", true)]
        [TestCase("Python", "1.0.0-beta.1+linux-x64", true)]
        [TestCase("Python", "1.0.0.dev", true)]
        [TestCase("Python", "1.0.0.post.dev1", true)]
        [TestCase("Python", "1!1.0.0rc1", true)]
        [TestCase(".NET", "1.2.1+build-beta.1", false)]
        [TestCase("Java", "1.2.1+build-beta.1", false)]
        [TestCase("JavaScript", "1.2.1+build-beta.1", false)]
        [TestCase("Go", "1.2.1+build-beta.1", false)]
        [TestCase(".NET", "1.2.1-beta.1+build-123", true)]
        [TestCase("Java", "1.2.1-beta.1+build-123", true)]
        [TestCase("JavaScript", "1.2.1-beta.1+build-123", true)]
        [TestCase("Go", "1.2.1-beta.1+build-123", true)]
        [TestCase("JavaScript", "1.0.0-1", true)]
        [TestCase("JavaScript", "1.0.0-post.1", true)]
        public async Task TestCheckReadyUsesVersionForApprovalGates(string language, string version, bool isPreview)
        {
            var packageName = "azure-template";
            devOpsService.ConfiguredPackageVersion = version;
            devOpsService.ConfiguredPackageType = SdkType.Dataplane;
            devOpsService.ConfiguredPackageNameStatus = "Pending";

            var nameCheck = await sdkReleaseTool.ReleasePackageAsync(packageName, language, checkReady: true);

            devOpsService.ConfiguredPackageNameStatus = "Approved";
            devOpsService.ConfiguredAPIViewStatus = "Pending";

            var apiCheck = await sdkReleaseTool.ReleasePackageAsync(packageName, language, checkReady: true);

            Assert.Multiple(() =>
            {
                Assert.That(nameCheck.ResponseError, Is.Null);
                Assert.That(apiCheck.ResponseError, Is.Null);
                Assert.That(nameCheck.ReleaseStatusDetails.Contains("not approved for preview release"), Is.EqualTo(isPreview));
                Assert.That(apiCheck.ReleaseStatusDetails.Contains("API view is not approved for GA release"), Is.EqualTo(!isPreview));
                Assert.That(nameCheck.ReleaseStatusDetails.Contains("is ready for release."), Is.EqualTo(!isPreview));
                Assert.That(apiCheck.ReleaseStatusDetails.Contains("is ready for release."), Is.EqualTo(isPreview));
                Assert.That(nameCheck.PipelineBuildId, Is.Zero);
                Assert.That(apiCheck.PipelineBuildId, Is.Zero);
                Assert.That(devOpsService.LastRunPipelineTemplateParams, Is.Null);
            });
        }

        [Test]
        public async Task TestCheckReadyRejectsPlannedReleaseTableWithoutDataRows()
        {
            // Match the hidden Markdown table emitted by GetMDVersionValue, with no releases.
            var workItem = new WorkItem
            {
                Fields = new Dictionary<string, object>
                {
                    ["Custom.PlannedPackages"] = "<div style='display:none' id=__md>| Type | Version | Date |\n| - | - | - |\n</div>"
                }
            };
            devOpsService.ConfiguredPlannedReleases = DevOpsService.MapPackageWorkItemToModel(workItem).PlannedReleases;

            var result = await sdkReleaseTool.ReleasePackageAsync("azure-template", "Python", checkReady: true);

            Assert.Multiple(() =>
            {
                Assert.That(result.ReleaseStatusDetails, Does.Contain("No planned release date found"));
                Assert.That(result.ReleaseStatusDetails, Does.Contain("not ready for release"));
                Assert.That(result.PipelineBuildId, Is.Zero);
                Assert.That(devOpsService.LastRunPipelineTemplateParams, Is.Null);
            });
        }

        [Test]
        public async Task TestRunReleaseForJavaPassesPackageNameTemplateParam()
        {
            var packageName = "azure-storage-blob";
            var language = "Java";
            var result = await sdkReleaseTool.ReleasePackageAsync(packageName, language);

            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result.Language, Is.EqualTo(SdkLanguage.Java));
                Assert.That(result.ReleaseStatusDetails, Does.Contain("Release pipeline triggered successfully"));
                Assert.That(devOpsService.LastRunPipelineTemplateParams, Is.Not.Null);
                Assert.That(devOpsService.LastRunPipelineTemplateParams!, Does.ContainKey("release_azurestorageblob"));
                Assert.That(devOpsService.LastRunPipelineTemplateParams!["release_azurestorageblob"], Is.EqualTo("true"));
            });
        }

        [Test]
        public async Task TestRunReleaseForNonJavaDoesNotPassPackageNameTemplateParam()
        {
            var packageName = "azure-template";
            var language = "Python";
            var result = await sdkReleaseTool.ReleasePackageAsync(packageName, language);

            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result.ReleaseStatusDetails, Does.Contain("Release pipeline triggered successfully"));
                Assert.That(devOpsService.LastRunPipelineTemplateParams, Is.Not.Null);
                Assert.That(devOpsService.LastRunPipelineTemplateParams!, Is.Empty);
            });
        }

        [TestCase("azure-storage-blob", "azurestorageblob")]
        [TestCase("azure-sdk-template", "azuresdktemplate")]
        [TestCase("Azure-Storage-Blob", "azurestorageblob")]
        [TestCase("azure_core", "azurecore")]
        [TestCase("", "")]
        public void TestGetJavaSafeName(string packageName, string expected)
        {
            Assert.That(SdkReleaseTool.GetJavaSafeName(packageName), Is.EqualTo(expected));
        }
    }
}
