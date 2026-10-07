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
                Assert.That(result.Packages, Has.None.Matches<PackageInfo>(p => p.Language == SdkLanguage.Unknown));
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

        [Test]
        public async Task Test_ParseTypeSpecProjectAsync_installs_pnpm_dependencies_with_corepack()
        {
            var (repoRoot, projectPath) = CreateTypeSpecProject(
                packageJson: """{"packageManager":"pnpm@12.6.0"}""",
                lockFileName: "pnpm-lock.yaml");
            WriteMetadata(projectPath, ValidMetadata);
            var processHelper = new Mock<IProcessHelper>();
            processHelper
                .Setup(x => x.Run(It.Is<ProcessOptions>(o => IsCommand(o, "corepack", "pnpm", "install", "--frozen-lockfile")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0 });
            var helper = new TypeSpecHelper(gitHelper, processHelper.Object);

            try
            {
                var result = await helper.ParseTypeSpecProjectAsync(projectPath, CreateSuccessfulNpxHelper().Object, NullLogger.Instance, CancellationToken.None);

                Assert.That(result, Is.Not.Null);
                Assert.That(result!.Packages, Has.Count.EqualTo(1));
                processHelper.Verify(
                    x => x.Run(It.Is<ProcessOptions>(o =>
                        IsCommand(o, "corepack", "pnpm", "install", "--frozen-lockfile")
                        && o.WorkingDirectory == repoRoot), It.IsAny<CancellationToken>()),
                    Times.Once);
            }
            finally
            {
                Directory.Delete(repoRoot, recursive: true);
            }
        }

        [Test]
        public async Task Test_ParseTypeSpecProjectAsync_retains_npm_ci_install()
        {
            var (repoRoot, projectPath) = CreateTypeSpecProject(lockFileName: "package-lock.json");
            WriteMetadata(projectPath, ValidMetadata);
            var processHelper = new Mock<IProcessHelper>();
            processHelper
                .Setup(x => x.Run(It.Is<ProcessOptions>(o => IsCommand(o, "npm", "ci")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0 });
            var helper = new TypeSpecHelper(gitHelper, processHelper.Object);

            try
            {
                var result = await helper.ParseTypeSpecProjectAsync(projectPath, CreateSuccessfulNpxHelper().Object, NullLogger.Instance, CancellationToken.None);

                Assert.That(result, Is.Not.Null);
                processHelper.Verify(
                    x => x.Run(It.Is<ProcessOptions>(o =>
                        IsCommand(o, "npm", "ci")
                        && o.WorkingDirectory == repoRoot), It.IsAny<CancellationToken>()),
                    Times.Once);
            }
            finally
            {
                Directory.Delete(repoRoot, recursive: true);
            }
        }

        [Test]
        public async Task Test_ParseTypeSpecProjectAsync_skips_install_without_lockfile()
        {
            var (repoRoot, projectPath) = CreateTypeSpecProject();
            WriteMetadata(projectPath, ValidMetadata);
            var processHelper = new Mock<IProcessHelper>();
            var helper = new TypeSpecHelper(gitHelper, processHelper.Object);

            try
            {
                var result = await helper.ParseTypeSpecProjectAsync(projectPath, CreateSuccessfulNpxHelper().Object, NullLogger.Instance, CancellationToken.None);

                Assert.That(result, Is.Not.Null);
                Assert.That(result!.Packages, Has.Count.EqualTo(1));
                processHelper.Verify(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                Directory.Delete(repoRoot, recursive: true);
            }
        }

        [Test]
        public void Test_ParseTypeSpecProjectAsync_reports_install_failure()
        {
            var (repoRoot, projectPath) = CreateTypeSpecProject(
                packageJson: """{"packageManager":"pnpm@12.6.0"}""",
                lockFileName: "pnpm-lock.yaml");
            var installResult = new ProcessResult { ExitCode = 42 };
            installResult.AppendStderr("install failed");
            var processHelper = new Mock<IProcessHelper>();
            processHelper
                .Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(installResult);
            var npxHelper = new Mock<INpxHelper>();
            var helper = new TypeSpecHelper(gitHelper, processHelper.Object);

            try
            {
                var exception = Assert.ThrowsAsync<InvalidOperationException>(() =>
                    helper.ParseTypeSpecProjectAsync(projectPath, npxHelper.Object, NullLogger.Instance, CancellationToken.None));

                Assert.That(exception!.Message, Does.Contain("corepack pnpm install --frozen-lockfile failed with exit code 42"));
                Assert.That(exception.Message, Does.Contain("install failed"));
                npxHelper.Verify(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                Directory.Delete(repoRoot, recursive: true);
            }
        }

        [Test]
        public void Test_ParseTypeSpecProjectAsync_reports_emitter_failure()
        {
            var (repoRoot, projectPath) = CreateTypeSpecProject();
            var emitterResult = new ProcessResult { ExitCode = 23 };
            emitterResult.AppendStderr("emitter failed");
            var npxHelper = new Mock<INpxHelper>();
            npxHelper
                .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(emitterResult);
            var helper = new TypeSpecHelper(gitHelper, Mock.Of<IProcessHelper>());

            try
            {
                var exception = Assert.ThrowsAsync<InvalidOperationException>(() =>
                    helper.ParseTypeSpecProjectAsync(projectPath, npxHelper.Object, NullLogger.Instance, CancellationToken.None));

                Assert.That(exception!.Message, Does.Contain("TypeSpec metadata emitter failed with exit code 23"));
                Assert.That(exception.Message, Does.Contain("emitter failed"));
            }
            finally
            {
                Directory.Delete(repoRoot, recursive: true);
            }
        }

        [Test]
        public async Task Test_ParseTypeSpecProjectAsync_returns_empty_packages_for_successful_empty_metadata()
        {
            var (repoRoot, projectPath) = CreateTypeSpecProject();
            WriteMetadata(projectPath, "languages: {}");
            var helper = new TypeSpecHelper(gitHelper, Mock.Of<IProcessHelper>());

            try
            {
                var result = await helper.ParseTypeSpecProjectAsync(projectPath, CreateSuccessfulNpxHelper().Object, NullLogger.Instance, CancellationToken.None);

                Assert.That(result, Is.Not.Null);
                Assert.That(result!.Packages, Is.Empty);
            }
            finally
            {
                Directory.Delete(repoRoot, recursive: true);
            }
        }

        [Test]
        public void Test_ParseTypeSpecProjectAsync_reports_missing_emitter_output()
        {
            var (repoRoot, projectPath) = CreateTypeSpecProject();
            var helper = new TypeSpecHelper(gitHelper, Mock.Of<IProcessHelper>());

            try
            {
                var exception = Assert.ThrowsAsync<InvalidOperationException>(() =>
                    helper.ParseTypeSpecProjectAsync(projectPath, CreateSuccessfulNpxHelper().Object, NullLogger.Instance, CancellationToken.None));

                Assert.That(exception!.Message, Does.Contain("completed without producing"));
                Assert.That(exception.Message, Does.Contain("typespec-metadata.yaml"));
            }
            finally
            {
                Directory.Delete(repoRoot, recursive: true);
            }
        }

        private const string ValidMetadata = """
            languages:
              JavaScript:
                packageName: "@azure/arm-contoso"
            """;

        private static (string RepoRoot, string ProjectPath) CreateTypeSpecProject(string? packageJson = null, string? lockFileName = null)
        {
            var repoRoot = Path.Combine(TestContext.CurrentContext.WorkDirectory, "TypeSpecHelperTests", Guid.NewGuid().ToString("N"));
            var projectPath = Path.Combine(repoRoot, "specification", "contoso", "Contoso.Management");
            Directory.CreateDirectory(projectPath);
            File.WriteAllText(Path.Combine(projectPath, "main.tsp"), string.Empty);
            File.WriteAllText(Path.Combine(projectPath, "tspconfig.yaml"), "extends: '@azure-tools/typespec-azure-rulesets/resource-manager'");

            if (packageJson != null)
            {
                File.WriteAllText(Path.Combine(repoRoot, "package.json"), packageJson);
            }
            if (lockFileName != null)
            {
                File.WriteAllText(Path.Combine(repoRoot, lockFileName), string.Empty);
            }

            return (repoRoot, projectPath);
        }

        private static void WriteMetadata(string projectPath, string metadata)
        {
            var metadataDirectory = Path.Combine(projectPath, "tsp-output", "@azure-tools", "typespec-metadata");
            Directory.CreateDirectory(metadataDirectory);
            File.WriteAllText(Path.Combine(metadataDirectory, "typespec-metadata.yaml"), metadata);
        }

        private static Mock<INpxHelper> CreateSuccessfulNpxHelper()
        {
            var npxHelper = new Mock<INpxHelper>();
            npxHelper
                .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0 });
            return npxHelper;
        }

        private static bool IsCommand(ProcessOptions options, string command, params string[] args)
        {
            return (options.Command == command || options.Args.Contains(command))
                && args.All(options.Args.Contains);
        }
    }
}
