using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Store;
using Azure.Sdk.Tools.TestProxy.Transforms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class GitStoretests
    {
        #region variable defs
        public static string AssetsJson = "assets.json";
        string[] basicFolderStructure = new string[]
        {
                AssetsJson
        };

        public static string DefaultAssetsJson =
@"
{
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

            var testFolder = TestHelpers.DescribeTestFolder(String.Empty, folderStructure);
            GitStore store = new GitStore();

            var evaluation = store.EvaluateDirectory(testFolder.FullName);
            Assert.True(evaluation.IsGitRoot);
            Assert.False(evaluation.AssetsJsonPresent);
            Assert.False(evaluation.IsRoot);
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

            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssetsJson, folderStructure);
            GitStore store = new GitStore();

            var evaluation = store.EvaluateDirectory(testFolder.FullName);
            Assert.True(evaluation.IsGitRoot);
            Assert.True(evaluation.AssetsJsonPresent);
            Assert.False(evaluation.IsRoot);
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

            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssetsJson, folderStructure);
            var evaluationDirectory = Path.Join(testFolder.FullName, "folder1");

            GitStore store = new GitStore();

            var evaluation = store.EvaluateDirectory(evaluationDirectory);
            Assert.False(evaluation.IsGitRoot);
            Assert.False(evaluation.AssetsJsonPresent);
            Assert.False(evaluation.IsRoot);
        }

        [Fact]
        public void ResolveAssetsJsonFindsAssetsInTargetFolder()
        {
            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssetsJson, basicFolderStructure);
            GitStore store = new GitStore();

            var path = store.ResolveAssetsJson(testFolder.FullName);

            Assert.Equal(Path.Join(testFolder.FullName, AssetsJson), path);
        }

        [Fact]
        public void ResolveAssetsJsonFindsAssetsInTargetFolderBelowRoot()
        {
            string[] folderStructure = new string[]
            {
                Path.Join("folder1", AssetsJson)
            };

            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssetsJson, folderStructure);
            var evaluationDirectory = Path.Join(testFolder.FullName, "folder1");
            GitStore store = new GitStore();

            var path = store.ResolveAssetsJson(evaluationDirectory);

            Assert.Equal(Path.Join(testFolder.FullName, "folder1", "assets.json"), path);
        }


        [Fact]
        public void ResolveAssetsJsonFindsAssetsAboveTargetFolder()
        {
            string[] folderStructure = new string[]
            {
                AssetsJson,
                "folder1",
            };

            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssetsJson, folderStructure);
            var evaluationDirectory = Path.Join(testFolder.FullName, "folder1");
            GitStore store = new GitStore();

            var path = store.ResolveAssetsJson(evaluationDirectory);

            Assert.Equal(Path.Join(testFolder.FullName, "assets.json"), path);
        }

        [Fact]
        public void ResolveAssetsJsonThrowsOnUnableToLocate()
        {
            var testFolder = TestHelpers.DescribeTestFolder(String.Empty, new string[] { });
            GitStore store = new GitStore();

            var assertion = Assert.Throws<HttpException>(() =>
            {
                store.ResolveAssetsJson(testFolder.FullName);
            });
            Assert.StartsWith("Unable to locate an assets.json at", assertion.Message);
        }

        [Fact]
        public void ResolveAssetsJsonThrowsOnUnableToLocateAfterTraversal()
        {

            string[] folderStructure = new string[]
            {
                "folder1",
            };

            var testFolder = TestHelpers.DescribeTestFolder(String.Empty, folderStructure);
            var evaluationDirectory = Path.Join(testFolder.FullName, "folder1");
            GitStore store = new GitStore();

            var assertion = Assert.Throws<HttpException>(() =>
            {
                store.ResolveAssetsJson(evaluationDirectory);
            });
            Assert.StartsWith("Unable to locate an assets.json at", assertion.Message);
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
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
        }")]
        public async Task ParseConfigurationEvaluatesValidConfigs(string inputJson)
        {
            string[] folderStructure = new string[]
            {
                AssetsJson
            };

            var testFolder = TestHelpers.DescribeTestFolder(inputJson, folderStructure);
            var jsonFileLocation = Path.Join(testFolder.FullName, AssetsJson);
            GitStore store = new GitStore();

            var parsedConfiguration = await store.ParseConfigurationFile(jsonFileLocation);
        }

        [Theory]
        [InlineData(
        @"{
              ""AssetsRepo"": """",
        }")]
        [InlineData(
        @"{
              ""AssetsRepo"": ""   "",
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

            var testFolder = TestHelpers.DescribeTestFolder(inputJson, folderStructure);
            var jsonFileLocation = Path.Join(testFolder.FullName, AssetsJson);
            GitStore store = new GitStore();

            var assertion = await Assert.ThrowsAsync<HttpException>(async () =>
            {
                await store.ParseConfigurationFile(Path.Join(testFolder.FullName, AssetsJson));
            });
            Assert.Contains("must contain value for the key \"AssetsRepo\"", assertion.Message);
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

            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssetsJson, folderStructure);
            var jsonFileLocation = Path.Join(testFolder.FullName, folderPath);
            GitStore store = new GitStore();

            var parsedConfiguration = await store.ParseConfigurationFile(jsonFileLocation);
            Assert.NotNull(parsedConfiguration);
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

            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssetsJson, folderStructure);
            var jsonFileLocation = Path.Join(testFolder.FullName, targetRelPath, AssetsJson);
            GitStore store = new GitStore();

            var parsedConfiguration = await store.ParseConfigurationFile(jsonFileLocation);
            Assert.Equal(Path.Join(targetRelPath, AssetsJson), parsedConfiguration.AssetsJsonRelativeLocation);
            Assert.Equal(jsonFileLocation, parsedConfiguration.AssetsJsonLocation);
        }

        [Theory]
        [InlineData("")]
        [InlineData("{}")]
        public async Task ParseConfigurationThrowsOnEmptyJson(string errorJson)
        {
            var testFolder = TestHelpers.DescribeTestFolder(errorJson, basicFolderStructure, ignoreEmptyAssetsJson: true);
            GitStore store = new GitStore();

            var assertion = await Assert.ThrowsAsync<HttpException>(async () =>
            {
                await store.ParseConfigurationFile(Path.Join(testFolder.FullName, AssetsJson));
            });
            Assert.StartsWith("The provided assets json at ", assertion.Message);
            Assert.EndsWith("did not have valid json present.", assertion.Message);
        }

        [Fact]
        public async Task ParseConfigurationThrowsOnNonExistentJson()
        {
            var testFolder = TestHelpers.DescribeTestFolder(string.Empty, basicFolderStructure);
            GitStore store = new GitStore();

            var assertion = await Assert.ThrowsAsync<HttpException>(async () =>
            {
                await store.ParseConfigurationFile(Path.Join(testFolder.FullName, AssetsJson));
            });
            Assert.StartsWith("The provided assets json path of ", assertion.Message);
            Assert.EndsWith(" does not exist.", assertion.Message);
        }
    }
}
