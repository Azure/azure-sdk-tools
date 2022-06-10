using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Store;
using Azure.Sdk.Tools.TestProxy.Transforms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
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
    ""AssetsRepoBranch"" = ""scenario_new_push""
    ""SHA"" = ""786b4f3d380d9c36c91f5f146ce4a7661ffee3b9""
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

            var testFolder = TestHelpers.DescribeTestFolder(string.Empty, folderStructure);
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
        public void TestEvaluateDirectoryFindAssetsAboveFolder()
        {

        }
    }
}
