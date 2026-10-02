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
            var processHelper = new ProcessHelper(new TestLogger<ProcessHelper>(), Mock.Of<IRawOutputHelper>());
            typeSpecHelper = new TypeSpecHelper(gitHelper, processHelper);
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

        [Test]
        public void Verify_IsValidTypeSpecProject_with_tspconfig_path()
        {
            var configPath = "TypeSpecTestData/specification/testcontoso/Contoso.Management/tspconfig.yaml";
            Assert.That(typeSpecHelper.IsValidTypeSpecProjectPath(configPath), Is.True);
            Assert.That(typeSpecHelper.IsValidTypeSpecProjectPath(Path.GetFullPath(configPath)), Is.True);
        }

        [Test]
        public void Test_GetTypeSpecProjectRelativePath_strips_tspconfig_filename()
        {
            var configPath = "TypeSpecTestData/specification/testcontoso/Contoso.Management/tspconfig.yaml";
            var result = typeSpecHelper.GetTypeSpecProjectRelativePath(configPath);
            Assert.That(result, Is.EqualTo("specification/testcontoso/Contoso.Management"));
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
        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/main/specification/dell/Dell.Storage.Management/tspconfig.yaml", "specification/dell/Dell.Storage.Management")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/main/specification/test/Test.Service/tspconfig.yaml?query=param", "specification/test/Test.Service")]
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
            var processHelper = new ProcessHelper(new TestLogger<ProcessHelper>(), Mock.Of<IRawOutputHelper>());
            var helper = new TypeSpecHelper(gitHelper, processHelper);
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
            var processHelper = new ProcessHelper(new TestLogger<ProcessHelper>(), Mock.Of<IRawOutputHelper>());
            var helper = new TypeSpecHelper(CreateGitHelper(repo), processHelper);
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
              UnknownEmitter:
                packageName: unknown-package
            """;

            string? metadataOutputDirectory = null;

            try
            {
                // Write metadata to the temporary output directory supplied to the emitter.
                var mockNpxHelper = new Mock<INpxHelper>();
                mockNpxHelper.Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
                    .Callback<NpxOptions, CancellationToken>((options, _) =>
                    {
                        var outputIndex = options.Args.IndexOf("--output-dir");
                        Assert.That(outputIndex, Is.GreaterThanOrEqualTo(0));
                        var outputDirectory = options.Args[outputIndex + 1];
                        Assert.That(Path.GetDirectoryName(outputDirectory), Is.EqualTo(Path.TrimEndingDirectorySeparator(Path.GetTempPath())));
                        Assert.That(Path.GetFileName(outputDirectory), Does.Match("^azsdk-spec-metadata-[0-9a-f]{32}$"));
                        metadataOutputDirectory = outputDirectory;
                        var metadataDir = Directory.CreateDirectory(Path.Combine(outputDirectory, "@azure-tools", "typespec-metadata")).FullName;
                        File.WriteAllText(Path.Combine(metadataDir, "typespec-metadata.yaml"), metadataYaml);
                    })
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
                Assert.That(result.Packages, Has.None.Matches<PackageInfo>(p => p.Language == SdkLanguage.Unknown));
                Assert.That(metadataOutputDirectory, Is.Not.Null);
                Assert.That(Directory.Exists(metadataOutputDirectory), Is.False);
            }
            finally
            {
                // Avoid leaking temporary output if the cleanup assertion fails.
                if (metadataOutputDirectory != null && Directory.Exists(metadataOutputDirectory))
                {
                    Directory.Delete(metadataOutputDirectory, recursive: true);
                }
            }
        }
    }
}
