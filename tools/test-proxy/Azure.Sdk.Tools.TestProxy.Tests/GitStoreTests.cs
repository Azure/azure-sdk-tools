using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Store;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using System.Text.Json;

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

        public override bool TryRun(string arguments, GitAssetsConfiguration config, out CommandResult result)
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
            AssetsRepoBranch = "scenario_clean_push",
            SHA = "e4a4949a2b6cc2ff75afd0fe0d97cbcabf7b67b7"
        };

        public static string DefaultAssetsJson =
@"
{
    // a json comment that shouldn't break parsing.
    ""AssetsRepo"":""Azure/azure-sdk-assets-integration"",
    ""AssetsRepoPrefixPath"":""python/recordings/"",
    ""AssetsRepoId"":"""",
    ""AssetsRepoBranch"":""scenario_clean_push"",
    ""SHA"":""e4a4949a2b6cc2ff75afd0fe0d97cbcabf7b67b7""
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
        public void ResolveAssetsJsonFindsAssetsAboveTargetFolder()
        {
            string[] folderStructure = new string[]
            {
                AssetsJson,
                "folder1",
            };

            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, folderStructure);
            try
            {
                var evaluationDirectory = Path.Join(testFolder, "folder1");

                var path = _defaultStore.ResolveAssetsJson(evaluationDirectory);

                Assert.Equal(Path.Join(testFolder, "assets.json"), path);
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
              ""AssetsRepoBranch"": ""auto/test"",
              ""SHA"": ""786b4f3d380d9c36c91f5f146ce4a7661ffee3b9""
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
              ""AssetsRepoBranch"": ""auto/test"",
              ""SHA"": ""786b4f3d380d9c36c91f5f146ce4a7661ffee3b9""
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
                var jsonFileLocation = Path.Join(testFolder, targetRelPath, AssetsJson);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
                Assert.Equal(Path.Join(targetRelPath, AssetsJson), parsedConfiguration.AssetsJsonRelativeLocation);
                Assert.Equal(jsonFileLocation, parsedConfiguration.AssetsJsonLocation);
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

                Assert.Equal(fakeSha, configuration.SHA);
                var newConfiguration = await _defaultStore.ParseConfigurationFile(testFolder);
                Assert.Equal(fakeSha, newConfiguration.SHA);
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
                string configLocation;

                if (assetsJsonPath == "assets.json")
                {
                    configLocation = testFolder;
                }
                else
                {
                    configLocation = Path.Join(testFolder, assetsJsonPath);
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
                await _defaultStore.UpdateAssetsJson(configuration.SHA, configuration);
                var postUpdateLastWrite = File.GetLastWriteTime(pathToAssets);

                Assert.Equal(creationTime, postUpdateLastWrite);
                var newConfiguration = await _defaultStore.ParseConfigurationFile(testFolder);
                Assert.Equal(configuration.SHA, newConfiguration.SHA);
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
                var originalSHA = configuration.SHA;

                await _defaultStore.UpdateAssetsJson(fakeSha, configuration);

                var newConfiguration = await _defaultStore.ParseConfigurationFile(pathToAssets);
                Assert.NotEqual(originalSHA, newConfiguration.SHA);
                var contentAfterUpdate = File.ReadAllText(pathToAssets);

                Assert.NotEqual(contentBeforeUpdate, contentAfterUpdate);
                Assert.Equal(contentBeforeUpdate.Replace(originalSHA, fakeSha), contentAfterUpdate);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact(Skip ="Skipping because we don't have an integration test suite working yet.")]
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

        [Theory(Skip = "Skipping because we don't have an integration test suite working yet.")]
        [InlineData("scenario_clean_push", "scenario_clean_push")]
        [InlineData("nonexistent_branch", "main")]
        public async Task ResolveTargetBranchIntegration(string targetBranch, string result)
        {
            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, basicFolderStructure);
            try
            {
                var config = await _defaultStore.ParseConfigurationFile(testFolder);
                config.AssetsRepoBranch = targetBranch;

                var defaultBranch = _defaultStore.ResolveCheckoutBranch(config);

                Assert.Equal(result, targetBranch);
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
