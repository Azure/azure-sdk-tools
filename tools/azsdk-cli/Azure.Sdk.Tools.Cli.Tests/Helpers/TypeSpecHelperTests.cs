using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers
{
    internal class TypeSpecHelperTests
    {
        private ITypeSpecHelper typeSpecHelper;
        private IGitHelper gitHelper;
        private Mock<IGitHubService> gitHubService;


        [SetUp]
        public void setup()
        {
            var logger = new TestLogger<GitHelper>();
            gitHubService = new Mock<IGitHubService>();
            var gitCommandHelper = new GitCommandHelper(NullLogger<GitCommandHelper>.Instance, Mock.Of<IRawOutputHelper>());
            gitHelper = new GitHelper(gitHubService.Object, gitCommandHelper, logger);
            typeSpecHelper = new TypeSpecHelper(gitHelper);
        }

        [Test]
        public void Verify_IsValidTypeSpecProject()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var result = typeSpecHelper.IsValidTypeSpecProjectPath(testCodeFilePath);
            Assert.That(result, Is.True);
            testCodeFilePath = "TypeSpecTestData/specification/testcontoso";
            result = typeSpecHelper.IsValidTypeSpecProjectPath(testCodeFilePath);
            Assert.That(result, Is.False);
        }

        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/main/specification/dell/Dell.Storage.Management")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/feature-branch/specification/contoso/Contoso.Management")]
        [TestCase("https://github.com/myorg/azure-rest-api-specs/blob/main/specification/test/Test.Service")]
        [Test]
        public void Verify_IsValidTypeSpecProjectUrl_WithUrls(string url)
        {
            var result = typeSpecHelper.IsValidTypeSpecProjectUrl(url);
            Assert.That(result, Is.True);
        }

        [TestCase("https://github.com/Azure/azure-rest-api-specs-pr/blob/main/specification/test/Test.Service")]
        [TestCase("https://github.com/Azure/wrong-repo/blob/main/specification/test/Test.Service")]
        [TestCase("https://example.com/specification/test/Test.Service")]
        [TestCase("not-a-url")]
        [Test]
        public void Verify_IsValidTypeSpecProjectUrl_WithInvalidUrls(string url)
        {
            var result = typeSpecHelper.IsValidTypeSpecProjectUrl(url);
            Assert.That(result, Is.False);
        }

        [Test]
        public void Verify_IsTypeSpecProjectForMgmtPlane()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var result = typeSpecHelper.IsTypeSpecProjectForMgmtPlane(testCodeFilePath);
            Assert.That(result, Is.True);
        }

        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/main/specification/dell/Dell.Storage.Management")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contoso/resource-manager/Contoso.Service")]
        [Test]
        public void Verify_IsTypeSpecUrlForMgmtPlane_WithUrls(string url)
        {
            var result = typeSpecHelper.IsTypeSpecUrlForMgmtPlane(url);
            Assert.That(result, Is.True);
        }

        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contoso/Contoso.DataPlane")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/main/specification/test/Test.Service")]
        [Test]
        public void Verify_IsTypeSpecUrlForMgmtPlane_WithDataPlaneUrls(string url)
        {
            var result = typeSpecHelper.IsTypeSpecUrlForMgmtPlane(url);
            Assert.That(result, Is.False);
        }

        [Test]
        public void Test_GetSpecRepoPath()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var result = typeSpecHelper.GetSpecRepoRootPath(testCodeFilePath);
            Assert.That(result.EndsWith("TypeSpecTestData"), Is.True);
        }

        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/main/specification/dell/Dell.Storage.Management", "specification/dell/Dell.Storage.Management")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/feature/specification/contoso/Contoso.Service", "specification/contoso/Contoso.Service")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/main/specification/test/Test.Service?query=param#L123", "specification/test/Test.Service")]
        [Test]
        public void Test_GetTypeSpecProjectRelativePathFromUrl(string url, string expected)
        {
            var result = typeSpecHelper.GetTypeSpecProjectRelativePathFromUrl(url);
            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase("https://github.com/Azure/azure-rest-api-specs.git")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs")]
        [TestCase("https://github.com/myuser/azure-rest-api-specs.git")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs-pr.git")]
        [TestCase("https://github.com/myuser/azure-rest-api-specs-pr.git")]
        [TestCase("git@github.com:Azure/azure-rest-api-specs.git")]
        [TestCase("git@github.com:myuser/azure-rest-api-specs.git")]
        [Test]
        public async Task Test_IsRepoPathForSpecRepo(Uri repo)
        {
            var gitHelper = CreateGitHelper(repo);
            var helper = new TypeSpecHelper(gitHelper);
            Assert.That(await helper.IsRepoPathForSpecRepoAsync("unused because of mock"), "is a specs repo (public or private)");
        }

        [TestCase("https://github.com/Azure/azure-rest-api-specs-pr.git")]
        [TestCase("https://github.com/myuser/azure-rest-api-specs-pr.git")]
        [TestCase("git@github.com:Azure/azure-rest-api-specs-pr.git")]
        [TestCase("git@github.com:myuser/azure-rest-api-specs-pr.git")]
        [TestCase("git@github.com:Azure/azure-sdk-for-php.git")]
        [Test]
        public async Task Test_IsRepoPathForPublicSpecRepo(Uri repo)
        {
            var helper = new TypeSpecHelper(CreateGitHelper(repo));
            Assert.That(!await helper.IsRepoPathForPublicSpecRepoAsync("unused because of the mock"), "not the public specs repo");
        }

        private static IGitHelper CreateGitHelper(Uri getRepoRemoteUri)
        {
            var gitHelperMock = new Mock<IGitHelper>();
            gitHelperMock.Setup(ghm => ghm.GetRepoRemoteUriAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(getRepoRemoteUri);
            return gitHelperMock.Object;
        }
        [Test]
        public async Task Test_ParseTypeSpecProjectAsync_parses_package_names_from_typespec_project()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var logger = new TestLogger<TypeSpecHelperTests>();

            // Metadata YAML that the emitter would produce
            var metadataYaml = """
            languages:
              .NET:
                packageName: Azure.ResourceManager.Contoso
              Java:
                packageName: com.azure.resourcemanager.contoso
              Python:
                packageName: azure-mgmt-contoso
              JavaScript:
                packageName: "@azure/arm-contoso"
              Go:
                packageName: sdk/resourcemanager/contoso/armcontoso
            """;

            // Set up the metadata output directory and file as the emitter would
            var metadataDir = Path.Combine(testCodeFilePath, "tsp-output", "@azure-tools", "typespec-metadata");
            Directory.CreateDirectory(metadataDir);
            var metadataFilePath = Path.Combine(metadataDir, "typespec-metadata.yaml");
            await File.WriteAllTextAsync(metadataFilePath, metadataYaml);

            try
            {
                // Mock npx to return success (emitter ran successfully)
                var mockNpxHelper = new Mock<INpxHelper>();
                mockNpxHelper.Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ProcessResult { ExitCode = 0 });

                var result = await typeSpecHelper.ParseTypeSpecProjectAsync(testCodeFilePath, mockNpxHelper.Object, logger, CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.That(result.Packages.Count, Is.EqualTo(5));
                Assert.That(result.IsManagementPlane, Is.True);

                Assert.That(result.Packages.Any(p => p.Language == SdkLanguage.DotNet && p.PackageName == "Azure.ResourceManager.Contoso"));
                Assert.That(result.Packages.Any(p => p.Language == SdkLanguage.Java && p.PackageName == "com.azure.resourcemanager.contoso"));
                Assert.That(result.Packages.Any(p => p.Language == SdkLanguage.Python && p.PackageName == "azure-mgmt-contoso"));
                Assert.That(result.Packages.Any(p => p.Language == SdkLanguage.JavaScript && p.PackageName == "@azure/arm-contoso"));
                Assert.That(result.Packages.Any(p => p.Language == SdkLanguage.Go && p.PackageName == "sdk/resourcemanager/contoso/armcontoso"));
            }
            finally
            {
                // Clean up the generated metadata directory
                var tspOutputDir = Path.Combine(testCodeFilePath, "tsp-output");
                if (Directory.Exists(tspOutputDir))
                {
                    Directory.Delete(tspOutputDir, recursive: true);
                }
            }
        }
    }
}
