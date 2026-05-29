using Microsoft.TeamFoundation.Build.WebApi;
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
