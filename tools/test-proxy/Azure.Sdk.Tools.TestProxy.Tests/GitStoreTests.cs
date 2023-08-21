using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Store;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using System.Text.Json;
using System.Linq;
using System.ComponentModel;
using Azure.Sdk.tools.TestProxy.Common;
using System.Collections.Generic;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class FakeCommandResultsHandler : GitProcessHandler
    {
        public string RunResult = string.Empty;
        public string ErrorResult = string.Empty;
        public int ErrorCode = 0;

        public FakeCommandResultsHandler(string runResult, string errorResult, int errorCode)
        {

        }

        public override bool TryRun(string arguments, string workingDirectory, out CommandResult result)
        {
            result = new CommandResult()
            {
                ExitCode = ErrorCode,
                StdErr = ErrorResult,
                StdOut = RunResult
            };

            return true;
        }

        public FakeCommandResultsHandler() { }
    }

    public class GitStoretests
    {
        #region variable defs
        public static string AssetsJson = "assets.json";
        private GitStore _defaultStore = new GitStore();
        private string[] basicFolderStructure = new string[]
        {
            AssetsJson
        };

        public static Assets DefaultAssets = new Assets
        {
            AssetsRepo = "Azure/azure-sdk-assets-integration",
            AssetsRepoPrefixPath = "python/recordings/",
            AssetsRepoId = "",
            TagPrefix = "scenario_clean_push",
            Tag = "e4a4949a2b6cc2ff75afd0fe0d97cbcabf7b67b7"
        };

        public static string DefaultAssetsJson =
@"
{
    // a json comment that shouldn't break parsing.
    ""AssetsRepo"":""Azure/azure-sdk-assets-integration"",
    ""AssetsRepoPrefixPath"":""python/recordings/"",
    ""AssetsRepoId"":"""",
    ""TagPrefix"":""scenario_clean_push"",
    ""Tag"":""e4a4949a2b6cc2ff75afd0fe0d97cbcabf7b67b7""
}
";
        #endregion

        [Fact]
        public void TestEvaluateDirectoryGitRootExistsWithNoAssets()
        {
            string[] folderStructure = new string[]
            {
                AssetsJson,
                "folder1",
                Path.Join("folder2", "file1.json")
            };

            var testFolder = TestHelpers.DescribeTestFolder(null, folderStructure, malformedJson:String.Empty);
            try
            {
                var evaluation = _defaultStore.EvaluateDirectory(testFolder);

                Assert.True(evaluation.IsGitRoot);
                Assert.False(evaluation.AssetsJsonPresent);
                Assert.False(evaluation.IsRoot);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public void TestEvaluateDirectoryFindsGitAssetsAlongsideGitRoot()
        {
            string[] folderStructure = new string[]
            {
                AssetsJson,
                "folder1",
                Path.Join("folder2", "file1.json")
            };

            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, folderStructure);

            try
            {
                var evaluation = _defaultStore.EvaluateDirectory(testFolder);
                Assert.True(evaluation.IsGitRoot);
                Assert.True(evaluation.AssetsJsonPresent);
                Assert.False(evaluation.IsRoot);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public void TestEvaluateDirectoryIdentifiesIntermediateDirectory()
        {
            string[] folderStructure = new string[]
            {
                AssetsJson,
                "folder1",
                Path.Join("folder2", "file1.json")
            };

            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, folderStructure);
            try
            {
                var evaluationDirectory = Path.Join(testFolder, "folder1");

                var evaluation = _defaultStore.EvaluateDirectory(evaluationDirectory);
                Assert.False(evaluation.IsGitRoot);
                Assert.False(evaluation.AssetsJsonPresent);
                Assert.False(evaluation.IsRoot);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public void ResolveAssetsJsonFindsAssetsInTargetFolder()
        {
            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, basicFolderStructure);
            try
            {
                var path = _defaultStore.ResolveAssetsJson(testFolder);
                Assert.Equal(Path.Join(testFolder, AssetsJson), path);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public void ResolveAssetsJsonFindsAssetsInTargetFolderBelowRoot()
        {
            string[] folderStructure = new string[]
            {
                Path.Join("folder1", AssetsJson)
            };

            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, folderStructure);
            try
            {
                var evaluationDirectory = Path.Join(testFolder, "folder1");

                var path = _defaultStore.ResolveAssetsJson(evaluationDirectory);

                Assert.Equal(Path.Join(testFolder, "folder1", "assets.json"), path);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }


        [Fact]
        public void ResolveAssetsJsonThrowsOnUnableToLocate()
        {
            var testFolder = TestHelpers.DescribeTestFolder(null, new string[] { }, malformedJson:String.Empty);
            try
            {
                var assertion = Assert.Throws<HttpException>(() =>
                {
                    _defaultStore.ResolveAssetsJson(testFolder);
                });
                Assert.StartsWith("Unable to locate an assets.json at", assertion.Message);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }

        }

        [Fact]
        public void ResolveAssetsJsonThrowsOnUnableToLocateAfterTraversal()
        {

            string[] folderStructure = new string[]
            {
                "folder1",
            };

            var testFolder = TestHelpers.DescribeTestFolder(null, folderStructure, malformedJson:String.Empty);
            try
            {
                var evaluationDirectory = Path.Join(testFolder, "folder1");

                var assertion = Assert.Throws<HttpException>(() =>
                {
                    _defaultStore.ResolveAssetsJson(evaluationDirectory);
                });
                Assert.StartsWith("Unable to locate an assets.json at", assertion.Message);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }


        [Theory]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""python/recordings/"",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""auto/test"",
              ""Tag"": ""786b4f3d380d9c36c91f5f146ce4a7661ffee3b9""
        }")]
        // Valid to just pass the assets repo. We can infer everything else.
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration""
        }")]
        public async Task ParseConfigurationEvaluatesValidConfigs(string inputJson)
        {
            string[] folderStructure = new string[]
            {
                AssetsJson
            };

            var testFolder = TestHelpers.DescribeTestFolder(null, folderStructure, malformedJson:inputJson);
            try
            {
                var jsonFileLocation = Path.Join(testFolder, AssetsJson);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Theory]
        [InlineData(
        @"{
              ""AssetsRepo"": """"
        }")]
        [InlineData(
        @"{
              ""AssetsRepo"": ""   ""
        }")]
        [InlineData(
        @"{
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""auto/test"",
              ""Tag"": ""786b4f3d380d9c36c91f5f146ce4a7661ffee3b9""
        }")]
        public async Task ParseConfigurationThrowsOnMissingRequiredProperty(string inputJson)
        {
            string[] folderStructure = new string[]
            {
                AssetsJson
            };

            var testFolder = TestHelpers.DescribeTestFolder(null, folderStructure, malformedJson:inputJson);
            try
            {
                var jsonFileLocation = Path.Join(testFolder, AssetsJson);

                var assertion = await Assert.ThrowsAsync<HttpException>(async () =>
                {
                    await _defaultStore.ParseConfigurationFile(Path.Join(testFolder, AssetsJson));
                });
                Assert.Contains("must contain value for the key \"AssetsRepo\"", assertion.Message);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public async Task ParseConfigurationEvaluatesTargetFolder()
        {
            var folderPath = Path.Join("folder1", "folder2");
            var targetRelPath = Path.Join(folderPath, $"{AssetsJson}");
            string[] folderStructure = new string[]
            {
                targetRelPath
            };

            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, folderStructure);
            try
            {
                var jsonFileLocation = Path.Join(testFolder, folderPath);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
                Assert.NotNull(parsedConfiguration);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Theory]
        [InlineData("folder1", "folder2")]
        [InlineData("folderabc123")]
        public async Task ParseConfigurationEvaluatesRelativePathCorrectly(params string[] inputPath)
        {
            var targetRelPath = Path.Join(inputPath);
            string[] folderStructure = new string[]
            {
                Path.Join(targetRelPath, AssetsJson)
            };

            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, folderStructure);
            try
            {
                var jsonFileLocation = new NormalizedString(Path.Join(testFolder, targetRelPath, AssetsJson));

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
                Assert.Equal(new NormalizedString(Path.Join(targetRelPath, AssetsJson)), parsedConfiguration.AssetsJsonRelativeLocation.ToString());
                Assert.Equal(jsonFileLocation, parsedConfiguration.AssetsJsonLocation.ToString());
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }

        }

        [Theory]
        [InlineData("")]
        [InlineData("{}")]
        public async Task ParseConfigurationThrowsOnEmptyJson(string errorJson)
        {
            var testFolder = TestHelpers.DescribeTestFolder(null, basicFolderStructure, ignoreEmptyAssetsJson: true, malformedJson:errorJson);
            try
            {
                var assertion = await Assert.ThrowsAsync<HttpException>(async () =>
                {
                    await _defaultStore.ParseConfigurationFile(Path.Join(testFolder, AssetsJson));
                });
                Assert.StartsWith("The provided assets.json at ", assertion.Message);
                Assert.EndsWith("did not have valid json present.", assertion.Message);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public async Task ParseConfigurationThrowsOnNonExistentJson()
        {
            var testFolder = TestHelpers.DescribeTestFolder(null, basicFolderStructure, malformedJson:String.Empty);
            try
            {
                var assertion = await Assert.ThrowsAsync<HttpException>(async () =>
                {
                    await _defaultStore.ParseConfigurationFile(Path.Join(testFolder, AssetsJson));
                });
                Assert.StartsWith("The provided assets.json path of ", assertion.Message);
                Assert.EndsWith(" does not exist.", assertion.Message);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public async Task GetDefaultBranchFailsWithInvalidRepo()
        {
            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, basicFolderStructure);

            try
            {
                // we are resetting the default branch so we will see if fallback logic kicks in
                _defaultStore.DefaultBranch = "not-main";
                var assetsConfiguration = await _defaultStore.ParseConfigurationFile(Path.Join(testFolder, AssetsJson));
                assetsConfiguration.AssetsRepo = "Azure/an-invalid-repo";

                var result = await _defaultStore.GetDefaultBranch(assetsConfiguration);
                Assert.Equal("not-main", result);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public async Task UpdateRecordingJsonUpdatesProperly()
        {
            var fakeSha = "FakeReplacementSha";
            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, basicFolderStructure);
            try
            {
                var configuration = await _defaultStore.ParseConfigurationFile(testFolder);
                await _defaultStore.UpdateAssetsJson(fakeSha, configuration);

                Assert.Equal(fakeSha, configuration.Tag);
                var newConfiguration = await _defaultStore.ParseConfigurationFile(testFolder);
                Assert.Equal(fakeSha, newConfiguration.Tag);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Theory]
        [InlineData("assets.json", false, "./")]
        [InlineData("assets.json", true, "python/recordings")]
        [InlineData("sdk/storage/assets.json", false, "sdk/storage")]
        [InlineData("sdk/storage/assets.json", true, "python/recordings/sdk/storage")]
        public async Task ResolveCheckPathsResolvesProperly(string assetsJsonPath, bool includePrefix, string expectedResult)
        {
            var expectedPaths = new string[]
            {
                assetsJsonPath
            };

            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, expectedPaths);
            try
            {
                NormalizedString configLocation;

                if (assetsJsonPath == "assets.json")
                {
                    configLocation = new NormalizedString(testFolder);
                }
                else
                {
                    configLocation = new NormalizedString(Path.Join(testFolder, assetsJsonPath));
                }

                var configuration = await _defaultStore.ParseConfigurationFile(configLocation);

                if (!includePrefix)
                {
                    configuration.AssetsRepoPrefixPath = null;
                }

                var result = _defaultStore.ResolveCheckoutPaths(configuration);
                Assert.Equal(expectedResult, result);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public async Task UpdateRecordingJsonNoOpsProperly()
        {
            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, basicFolderStructure);
            try
            {
                var pathToAssets = Path.Combine(testFolder, "assets.json");
                var creationTime = File.GetLastWriteTime(pathToAssets);

                var configuration = await _defaultStore.ParseConfigurationFile(testFolder);
                await _defaultStore.UpdateAssetsJson(configuration.Tag, configuration);
                var postUpdateLastWrite = File.GetLastWriteTime(pathToAssets);

                Assert.Equal(creationTime, postUpdateLastWrite);
                var newConfiguration = await _defaultStore.ParseConfigurationFile(testFolder);
                Assert.Equal(configuration.Tag, newConfiguration.Tag);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public async Task UpdateRecordingJsonOnlyUpdatesTargetSHA()
        {
            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, basicFolderStructure);
            try
            {
                var fakeSha = "FakeReplacementSha";
                var pathToAssets = Path.Combine(testFolder, "assets.json");
                var contentBeforeUpdate = File.ReadAllText(pathToAssets);
                var configuration = await _defaultStore.ParseConfigurationFile(pathToAssets);
                var originalSHA = configuration.Tag;

                await _defaultStore.UpdateAssetsJson(fakeSha, configuration);

                var newConfiguration = await _defaultStore.ParseConfigurationFile(pathToAssets);
                Assert.NotEqual(originalSHA, newConfiguration.Tag);
                var contentAfterUpdate = File.ReadAllText(pathToAssets);

                Assert.NotEqual(contentBeforeUpdate, contentAfterUpdate);
                Assert.Equal(contentBeforeUpdate.Replace(originalSHA, fakeSha), contentAfterUpdate);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [EnvironmentConditionalSkipTheory]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""python/recordings"",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""python/tables"",
              ""Tag"": ""python/tables89f51431""
        }")]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""python"",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""python/tables"",
              ""Tag"": ""python/tables4f724f0c""
        }")]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": """",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""python/tables"",
              ""Tag"": ""python/tablesdd6aec01""
        }")]
        [Trait("Category", "Integration")]
        public async Task GetPathResolves(string inputJson)
        {
            var folderStructure = new string[]
            {
                Path.Combine("sdk", "tables", GitStoretests.AssetsJson)
            };

            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure, isPushTest: false);

            try
            {
                var jsonFileLocation = Path.Join(testFolder, "sdk/tables", GitStoretests.AssetsJson);
                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);

                await _defaultStore.Restore(jsonFileLocation);

                var result = await _defaultStore.GetPath(jsonFileLocation);
                await TestHelpers.CheckBreadcrumbAgainstAssetsJsons(new string[] { jsonFileLocation });

                Assert.True(File.Exists(Path.Combine(result, "sdk", "tables", "azure-data-tables", "tests", "recordings", "test_retry.pyTestStorageRetrytest_retry_on_server_error.json")));
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [EnvironmentConditionalSkipFact]
        [Trait("Category", "Integration")]
        public async Task BreadCrumbMaintainsMultipleBreadCrumbs()
        {
            var inputJson = @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": """",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""python/tables"",
              ""Tag"": ""python/tablesdd6aec01""
            }";

            var target1 = Path.Combine("sdk", "tables", GitStoretests.AssetsJson);
            var target2 = Path.Combine("sdk", GitStoretests.AssetsJson);
            var target3 = Path.Combine(GitStoretests.AssetsJson);

            var folderStructure = new string[]
            {
                target1,
                target2,
                target3
            };

            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure, isPushTest: false);

            try
            {
                var assetStore = (await _defaultStore.ParseConfigurationFile(Path.Join(testFolder, target1))).ResolveAssetsStoreLocation();

                var breadCrumbs = new List<string>();

                // run 3 restore operations
                foreach (var assetsJson in folderStructure)
                {
                    var jsonFileLocation = Path.Join(testFolder, assetsJson);
                    var parsedJson = await _defaultStore.ParseConfigurationFile(jsonFileLocation);

                    var breadCrumbFile = Path.Join(assetStore.ToString(), "breadcrumb", $"{parsedJson.AssetRepoShortHash}.breadcrumb");

                    breadCrumbs.Add(breadCrumbFile);

                    await _defaultStore.Restore(jsonFileLocation);
                    TestHelpers.CheckBreadcrumbAgainstAssetsConfig(parsedJson);
                }

                // double verify they are where we expect
                foreach(var crumbFile in breadCrumbs)
                {
                    Assert.True(File.Exists(crumbFile));
                }

                // we have already validated that each tag contains what we expect, just confirm we aren't eliminating lines now.
                Assert.Equal(3, breadCrumbs.Count());
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact(Skip = "Skipping because we don't have an integration test suite working yet.")]
        public async Task GitCallHonorsLocalCredential()
        {
            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, basicFolderStructure);
            try
            {
                var config = await _defaultStore.ParseConfigurationFile(testFolder);

                var workDone = _defaultStore.InitializeAssetsRepo(config);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact(Skip = "Skipping due to integration tests not figured out yet.")]
        public async Task GetDefaultBranchWorksWithValidRepo()
        {
            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, basicFolderStructure);
            try
            {
                _defaultStore.DefaultBranch = "not-main";
                var assetsConfiguration = await _defaultStore.ParseConfigurationFile(Path.Join(testFolder, AssetsJson));
                var result = await _defaultStore.GetDefaultBranch(assetsConfiguration);

                Assert.Equal("main", result);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }
    }
}
