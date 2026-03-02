using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services
{
    internal class GoLanguageServicesPackageDiscoveryTests
    {
        private TempDirectory tempDir = null!;
        private GoLanguageService langService = null!;

        [SetUp]
        public async Task SetUp()
        {
            tempDir = TempDirectory.Create("golang_package_discovery");

            var processHelper = new ProcessHelper(NullLogger<ProcessHelper>.Instance, Mock.Of<IRawOutputHelper>());
            var pr = await processHelper.Run(new ProcessOptions("git", "git.exe", ["init", "."], workingDirectory: tempDir.DirectoryPath), CancellationToken.None);
            Assert.That(pr.ExitCode, Is.EqualTo(0));

            langService = new GoLanguageService(
                processHelper,
                new PowershellHelper(NullLogger<PowershellHelper>.Instance, Mock.Of<IRawOutputHelper>()),
                new GitHelper(Mock.Of<IGitHubService>(), new GitCommandHelper(NullLogger<GitCommandHelper>.Instance, Mock.Of<IRawOutputHelper>()), NullLogger<GitHelper>.Instance),
                NullLogger<GoLanguageService>.Instance, Mock.Of<ICommonValidationHelpers>(),
                Mock.Of<IPackageInfoHelper>(),
                Mock.Of<IFileHelper>(),
                Mock.Of<ISpecGenSdkConfigHelper>(),
                Mock.Of<IChangelogHelper>());
        }

        [TearDown]
        public void TearDown()
        {
            tempDir.Dispose();
        }

        [Test]
        public async Task DiscoverPackagesAsync_SkipsTestdataAndSamplesPackages()
        {
            var repoRoot = tempDir.DirectoryPath;
            var validPackagePath = Path.Combine(repoRoot, "sdk", "storage", "azblob");
            var samplesPackagePath = Path.Combine(repoRoot, "sdk", "samples", "fakes");
            var azcoreTestdataPackagePath = Path.Combine(repoRoot, "sdk", "azcore", "testdata", "perf");
            var aztablesTestdataPackagePath = Path.Combine(repoRoot, "sdk", "data", "aztables", "testdata", "perf");

            await CreateTestGoPackageAsync(validPackagePath, "github.com/Azure/azure-sdk-for-go/sdk/storage/azblob");
            await CreateTestGoPackageAsync(samplesPackagePath, "github.com/Azure/azure-sdk-for-go/sdk/samples/fakes");
            await CreateTestGoPackageAsync(azcoreTestdataPackagePath, "github.com/Azure/azure-sdk-for-go/sdk/azcore/testdata/perf");
            await CreateTestGoPackageAsync(aztablesTestdataPackagePath, "github.com/Azure/azure-sdk-for-go/sdk/data/aztables/testdata/perf");

            var packages = await langService.DiscoverPackagesAsync(repoRoot, null, CancellationToken.None);
            var packageNames = packages
                .Select(p => p.PackageName)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(packageNames, Does.Contain("sdk/storage/azblob"));
                Assert.That(packageNames, Does.Not.Contain("sdk/samples/fakes"));
                Assert.That(packageNames, Does.Not.Contain("sdk/azcore/testdata/perf"));
                Assert.That(packageNames, Does.Not.Contain("sdk/data/aztables/testdata/perf"));
            });
        }

        [Test]
        public async Task DiscoverPackagesAsync_ServiceDirectory_ExcludesNestedModule()
        {
            var repoRoot = tempDir.DirectoryPath;
            var azidentityPath = Path.Combine(repoRoot, "sdk", "azidentity");
            var cachePath = Path.Combine(azidentityPath, "cache");

            await CreateTestGoPackageAsync(azidentityPath, "github.com/Azure/azure-sdk-for-go/sdk/azidentity");
            await CreateTestGoPackageAsync(cachePath, "github.com/Azure/azure-sdk-for-go/sdk/azidentity/cache");

            var packages = await langService.DiscoverPackagesAsync(repoRoot, "azidentity", CancellationToken.None);
            var packageNames = packages
                .Select(p => p.PackageName)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(packageNames, Does.Contain("sdk/azidentity"));
                Assert.That(packageNames, Does.Not.Contain("sdk/azidentity/cache"));
            });
        }

        [Test]
        public async Task DiscoverPackagesAsync_ServiceDirectory_IncludesExplicitNestedModule()
        {
            var repoRoot = tempDir.DirectoryPath;
            var azidentityPath = Path.Combine(repoRoot, "sdk", "azidentity");
            var cachePath = Path.Combine(azidentityPath, "cache");

            await CreateTestGoPackageAsync(azidentityPath, "github.com/Azure/azure-sdk-for-go/sdk/azidentity");
            await CreateTestGoPackageAsync(cachePath, "github.com/Azure/azure-sdk-for-go/sdk/azidentity/cache");

            var packages = await langService.DiscoverPackagesAsync(repoRoot, "azidentity/cache", CancellationToken.None);
            var packageNames = packages
                .Select(p => p.PackageName)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            Assert.That(packageNames, Does.Contain("sdk/azidentity/cache"));
            Assert.That(packageNames, Does.Not.Contain("sdk/azidentity"));
        }

        [Test]
        public async Task DiscoverPackagesAsync_ServiceDirectory_SdkPrefixedPath_Equivalent()
        {
            var repoRoot = tempDir.DirectoryPath;
            var azidentityPath = Path.Combine(repoRoot, "sdk", "azidentity");
            var cachePath = Path.Combine(azidentityPath, "cache");

            await CreateTestGoPackageAsync(azidentityPath, "github.com/Azure/azure-sdk-for-go/sdk/azidentity");
            await CreateTestGoPackageAsync(cachePath, "github.com/Azure/azure-sdk-for-go/sdk/azidentity/cache");

            var nestedPathPackages = await langService.DiscoverPackagesAsync(repoRoot, "azidentity/cache", CancellationToken.None);
            var sdkPrefixedPackages = await langService.DiscoverPackagesAsync(repoRoot, "sdk/azidentity/cache", CancellationToken.None);

            var nestedNames = nestedPathPackages
                .Select(p => p.PackageName)
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n)
                .ToList();

            var sdkPrefixedNames = sdkPrefixedPackages
                .Select(p => p.PackageName)
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n)
                .ToList();

            Assert.That(sdkPrefixedNames, Is.EqualTo(nestedNames));
        }

        [Test]
        public async Task DiscoverPackagesAsync_NoServiceDirectory_IncludesTopLevelAndNestedModules()
        {
            var repoRoot = tempDir.DirectoryPath;
            var azidentityPath = Path.Combine(repoRoot, "sdk", "azidentity");
            var cachePath = Path.Combine(azidentityPath, "cache");

            await CreateTestGoPackageAsync(azidentityPath, "github.com/Azure/azure-sdk-for-go/sdk/azidentity");
            await CreateTestGoPackageAsync(cachePath, "github.com/Azure/azure-sdk-for-go/sdk/azidentity/cache");

            var packages = await langService.DiscoverPackagesAsync(repoRoot, null, CancellationToken.None);
            var packageNames = packages
                .Select(p => p.PackageName)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(packageNames, Does.Contain("sdk/azidentity"));
                Assert.That(packageNames, Does.Contain("sdk/azidentity/cache"));
            });
        }

        private static async Task CreateTestGoPackageAsync(string packageDirectory, string modulePath)
        {
            Directory.CreateDirectory(packageDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(packageDirectory, "go.mod"),
                $"module {modulePath}\ngo 1.24.0\n");

            var internalDirectory = Path.Combine(packageDirectory, "internal");
            Directory.CreateDirectory(internalDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(internalDirectory, "version.go"),
                "package internal\n\nconst Version = \"v1.2.3\"\n");
        }
    }
}
