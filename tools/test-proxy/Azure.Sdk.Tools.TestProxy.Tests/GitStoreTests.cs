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
        public static string DefaultAssetsJson =
@"
{
    ""AssetsRepo"" = ""Azure/azure-sdk-assets-integration""
    ""AssetsRepoPrefixPath"" = ""python/recordings/""
    ""AssetsRepoId"" = """"
    ""AssetsRepoBranch"" = ""scenario_clean_push""
    ""SHA"" = ""e4a4949a2b6cc2ff75afd0fe0d97cbcabf7b67b7""
}
";
        #endregion

        [Fact]
        public void TestEvaluateDirectoryGitRootExistsWithNoAssets()
        {
            string[] folderStructure = new string[]
            {
                "assets.json",
                "folder1\\",
                "folder2\\file1.json"
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
                "assets.json",
                "folder1\\",
                "folder2\\file1.json"
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
                "assets.json",
                "folder1\\",
                "folder2\\file1.json"
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
            string[] folderStructure = new string[]
            {
                "assets.json"
            };

            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssetsJson, folderStructure);
            GitStore store = new GitStore();

            var path = store.ResolveAssetsJson(testFolder.FullName);

            Assert.Equal(Path.Join(testFolder.FullName, "assets.json"), path);
        }

        [Fact]
        public void ResolveAssetsJsonFindsAssetsInTargetFolderBelowRoot()
        {
            string[] folderStructure = new string[]
            {
                "folder1\\assets.json",
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
                "assets.json",
                "folder1\\",
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

            string[] folderStructure = new string[]
            {
            };

            var testFolder = TestHelpers.DescribeTestFolder(String.Empty, folderStructure);
            GitStore store = new GitStore();

            Assert.Throws<HttpException>(() =>
            {
                store.ResolveAssetsJson(testFolder.FullName);
            });
        }

        [Fact]
        public void ResolveAssetsJsonThrowsOnUnableToLocateAfterTraversal()
        {

            string[] folderStructure = new string[]
            {
                "folder1\\",
            };

            var testFolder = TestHelpers.DescribeTestFolder(String.Empty, folderStructure);
            var evaluationDirectory = Path.Join(testFolder.FullName, "folder1");
            GitStore store = new GitStore();

            Assert.Throws<HttpException>(() =>
            {
                store.ResolveAssetsJson(evaluationDirectory);
            });
        }

        [Fact]
        public void ParseConfigurationThrowsOnEmptyJson()
        {

        }

        [Fact]
        public void ParseConfigurationThrowsOnNonExistentJson()
        {

        }

        [Fact]
        public void ParseConfigurationThrowsOn()
        {

        }
    }
}
