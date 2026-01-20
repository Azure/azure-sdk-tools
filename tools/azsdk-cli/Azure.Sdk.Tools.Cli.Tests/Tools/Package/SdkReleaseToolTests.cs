using Microsoft.TeamFoundation.Build.WebApi;
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
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
        private IDevOpsService devOpsService;
        private SdkReleaseTool sdkReleaseTool;

        [SetUp]
        public void Setup()
        {

            logger = new TestLogger<SdkReleaseTool>();
            devOpsService = new MockDevOpsService();
            sdkReleaseTool = new SdkReleaseTool(
                devOpsService,
                logger,
                new InputSanitizer());
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
    }
}
