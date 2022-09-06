using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Store;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests.IntegrationTests
{
    public class GitStoreIntegrationPushTests
    {
        public GitStoreIntegrationPushTests()
        {
            var loggerFactory = new LoggerFactory();
            DebugLogger.ConfigureLogger(loggerFactory);
        }

        private GitStore _defaultStore = new GitStore();

        /// <summary>
        /// New push scenario
        /// 1. Auto Branch Doesn't Exist Yet
        /// 2. New branch is created off of main
        /// 3. Add/Delete/Update files
        /// 4. Push to new branch
        /// 5. Verify local files are what is expected
        /// 6. Verify assets.json was updated with the new commit SHA
        /// </summary>
        /// <param name="inputJson"></param>
        /// <returns></returns>
        [EnvironmentConditionalSkipTheory]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""pull/scenarios"",
              ""AssetsRepoId"": """",
              ""AssetsRepoBranch"": ""scenario_new_push"",
              ""SHA"": ""fc54d000d0427c4a68bc8962d40f957f59e14577""
        }")]
        [Trait("Category", "Integration")]
        public async Task ScenarioNewPush(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };
            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            string originalAssetsRepoBranch = assets.AssetsRepoBranch;
            string originalSHA = assets.SHA;
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure, isPushTest:true);
            try
            {
                // Ensure that the AssetsRepoBranch was updated
                Assert.NotEqual(originalAssetsRepoBranch, assets.AssetsRepoBranch);

                var jsonFileLocation = Path.Join(testFolder, GitStoretests.AssetsJson);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
                await _defaultStore.Restore(jsonFileLocation);

                // Calling Path.GetFullPath of the Path.Combine will ensure any directory separators are normalized for
                // the OS the test is running on. The reason being is that AssetsRepoPrefixPath, if there's a separator,
                // will be a forward one as expected by git but on Windows this won't result in a usable path.
                string localFilePath = Path.GetFullPath(Path.Combine(parsedConfiguration.AssetsRepoLocation, parsedConfiguration.AssetsRepoPrefixPath));

                // These are the files pulled down with the original SHA
                Assert.Equal(3, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file3.txt", 1));

                // Create a new file, update an existing file and delete an existing file.
                TestHelpers.CreateFileWithInitialVersion(localFilePath, "file4.txt");
                TestHelpers.IncrementFileVersion(localFilePath, "file1.txt");
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file4.txt", 1));
                File.Delete(Path.Combine(localFilePath, "file2.txt"));

                // Push the update, it should be a clean push
                await _defaultStore.Push(jsonFileLocation);

                // Verify that after the push the directory still contains our updated files
                Assert.Equal(3, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file3.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file4.txt", 1));

                // Ensure that the config was updated with the new SHA as part of the push
                Assets updatedAssets = TestHelpers.LoadAssetsFromFile(jsonFileLocation);
                Assert.NotEqual(originalSHA, updatedAssets.SHA);
                // Ensure that the latest commit SHA and the updated assets file SHA are equal
                string latestSHA = TestHelpers.GetLatestCommitSHA(updatedAssets, localFilePath);
                Assert.Equal(latestSHA, updatedAssets.SHA);

            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
                TestHelpers.CleanupIntegrationTestBranch(assets);
            }
        }

        /// <summary>
        /// Clean Push Scenario
        /// 1. Branch already exists and we're on the latest SHA
        /// 2. Add/Delete/Update files
        /// 3. Push commit to branch
        /// 4. Verify local files are what is expected
        /// 5. Verify assets.json was updated with the new commit SHA
        /// </summary>
        /// <param name="inputJson"></param>
        /// <returns></returns>
        [EnvironmentConditionalSkipTheory]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""pull/scenarios"",
              ""AssetsRepoId"": """",
              ""AssetsRepoBranch"": ""scenario_clean_push"",
              ""SHA"": ""bb2223a3aa0472ff481f8e1850e7647dc39fbfdd""
        }")]
        [Trait("Category", "Integration")]
        public async Task ScenarioCleanPush(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };
            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            string originalAssetsRepoBranch = assets.AssetsRepoBranch;
            string originalSHA = assets.SHA;
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure, isPushTest: true);
            try
            {
                // Ensure that the AssetsRepoBranch was updated
                Assert.NotEqual(originalAssetsRepoBranch, assets.AssetsRepoBranch);

                var jsonFileLocation = Path.Join(testFolder, GitStoretests.AssetsJson);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
                await _defaultStore.Restore(jsonFileLocation);

                // Calling Path.GetFullPath of the Path.Combine will ensure any directory separators are normalized for
                // the OS the test is running on. The reason being is that AssetsRepoPrefixPath, if there's a separator,
                // will be a forward one as expected by git but on Windows this won't result in a usable path.
                string localFilePath = Path.GetFullPath(Path.Combine(parsedConfiguration.AssetsRepoLocation, parsedConfiguration.AssetsRepoPrefixPath));

                // These are the files pulled down with the original SHA
                Assert.Equal(3, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file4.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file5.txt", 1));

                // Create a new file, update an existing file and delete an existing file.
                TestHelpers.CreateFileWithInitialVersion(localFilePath, "file6.txt");
                TestHelpers.IncrementFileVersion(localFilePath, "file2.txt");
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 3));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file6.txt", 1));
                File.Delete(Path.Combine(localFilePath, "file5.txt"));

                // Push the update, it should be a clean push
                await _defaultStore.Push(jsonFileLocation);

                // Verify that after the push the directory still contains our updated files
                Assert.Equal(3, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 3));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file4.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file6.txt", 1));

                // Ensure that the config was updated with the new SHA as part of the push
                Assets updatedAssets = TestHelpers.LoadAssetsFromFile(jsonFileLocation);
                Assert.NotEqual(originalSHA, updatedAssets.SHA);
                // Ensure that the latest commit SHA and the updated assets file SHA are equal
                string latestSHA = TestHelpers.GetLatestCommitSHA(updatedAssets, localFilePath);
                Assert.Equal(latestSHA, updatedAssets.SHA);

            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
                TestHelpers.CleanupIntegrationTestBranch(assets);
            }
        }

        /// <summary>
        /// Conflict Push Scenario
        /// 1. Branch already exists and we're not on the latest SHA
        /// 2. Add/Delete/Update files
        /// 3. Push commit to branch, detects a conflict
        /// 4. Verify local files are what is expected
        /// 5. Verify assets.json was updated with the new commit SHA
        /// </summary>
        /// <param name="inputJson"></param>
        /// <returns></returns>
        //[EnvironmentConditionalSkipTheory]
        [Theory(Skip = "Skipping, scenario is currently broken the way push is done on conflict.")]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""pull/scenarios"",
              ""AssetsRepoId"": """",
              ""AssetsRepoBranch"": ""scenario_conflict_push"",
              ""SHA"": ""9e81fbb7d08c2df4cbdbfaffe79cde5d72f560d1""
        }")]
        [Trait("Category", "Integration")]
        public async Task ScenarioConflictPush(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };
            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            string originalAssetsRepoBranch = assets.AssetsRepoBranch;
            string originalSHA = assets.SHA;
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure, isPushTest: true);
            try
            {
                // Ensure that the AssetsRepoBranch was updated
                Assert.NotEqual(originalAssetsRepoBranch, assets.AssetsRepoBranch);

                var jsonFileLocation = Path.Join(testFolder, GitStoretests.AssetsJson);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
                await _defaultStore.Restore(jsonFileLocation);

                // Calling Path.GetFullPath of the Path.Combine will ensure any directory separators are normalized for
                // the OS the test is running on. The reason being is that AssetsRepoPrefixPath, if there's a separator,
                // will be a forward one as expected by git but on Windows this won't result in a usable path.
                string localFilePath = Path.GetFullPath(Path.Combine(parsedConfiguration.AssetsRepoLocation, parsedConfiguration.AssetsRepoPrefixPath));

                // These are the files pulled down with the original SHA
                Assert.Equal(4, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file3.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file4.txt", 1));

                // Create a new file, update an existing file and delete an existing file.
                TestHelpers.CreateFileWithInitialVersion(localFilePath, "file6.txt");
                TestHelpers.IncrementFileVersion(localFilePath, "file1.txt");
                TestHelpers.IncrementFileVersion(localFilePath, "file2.txt");
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 3));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file6.txt", 1));
                File.Delete(Path.Combine(localFilePath, "file3.txt"));
                File.Delete(Path.Combine(localFilePath, "file4.txt"));

                // Push the update, it should detect a conflict:
                // The latest commit only has files 2,4,5 with versions 2,1,1 respectively
                // We've made the the following changes against the previous SHA
                // 1. File1's version was incremented to 2, but deleted in the latest SHA
                // 2. File2's version was incremented to 3
                // 3. File3 was deleted
                // 4. File4 was deleted
                // 5. File6 was added
                await _defaultStore.Push(jsonFileLocation);

                // Verify that after the push the directory still contains our updated files
                Assert.Equal(5, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 3));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file4.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file5.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file6.txt", 1));

                // Ensure that the config was updated with the new SHA as part of the push
                Assets updatedAssets = TestHelpers.LoadAssetsFromFile(jsonFileLocation);
                Assert.NotEqual(originalSHA, updatedAssets.SHA);
                // Ensure that the latest commit SHA and the updated assets file SHA are equal
                string latestSHA = TestHelpers.GetLatestCommitSHA(updatedAssets, localFilePath);
                Assert.Equal(latestSHA, updatedAssets.SHA);

            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
                TestHelpers.CleanupIntegrationTestBranch(assets);
            }
        }
    }
}
