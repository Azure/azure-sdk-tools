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
        /// 6. Verify assets.json was updated with the new commit Tag
        /// </summary>
        /// <param name="inputJson"></param>
        /// <returns></returns>
        [EnvironmentConditionalSkipTheory]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""pull/scenarios"",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""language/tables"",
              ""Tag"": ""language/tables_fc54d0""
        }")]
        [Trait("Category", "Integration")]
        public async Task ScenarioNewPush(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };
            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            Assets updatedAssets = null;
            string originalAssetsRepoBranch = assets.TagPrefix;
            string originalSHA = assets.Tag;
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure, isPushTest:true);
            try
            {
                // Ensure that the TagPrefix was updated
                Assert.NotEqual(originalAssetsRepoBranch, assets.TagPrefix);

                var jsonFileLocation = Path.Join(testFolder, GitStoretests.AssetsJson);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
                await _defaultStore.Restore(jsonFileLocation);

                // Calling Path.GetFullPath of the Path.Combine will ensure any directory separators are normalized for
                // the OS the test is running on. The reason being is that AssetsRepoPrefixPath, if there's a separator,
                // will be a forward one as expected by git but on Windows this won't result in a usable path.
                string localFilePath = Path.GetFullPath(Path.Combine(parsedConfiguration.AssetsRepoLocation, parsedConfiguration.AssetsRepoPrefixPath));

                // These are the files pulled down with the original Tag
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

                // Ensure that the config was updated with the new Tag as part of the push
                updatedAssets = TestHelpers.LoadAssetsFromFile(jsonFileLocation);
                Assert.NotEqual(originalSHA, updatedAssets.Tag);

                // Ensure that the targeted tag is present on the repo
                TestHelpers.CheckExistenceOfTag(updatedAssets, localFilePath);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
                TestHelpers.CleanupIntegrationTestTag(updatedAssets);
            }
        }

        /// <summary>
        /// Clean Push Scenario
        /// 1. Branch already exists and we're on the latest Tag
        /// 2. Add/Delete/Update files
        /// 3. Push commit to branch
        /// 4. Verify local files are what is expected
        /// 5. Verify assets.json was updated with the new commit Tag
        /// </summary>
        /// <param name="inputJson"></param>
        /// <returns></returns>
        [EnvironmentConditionalSkipTheory]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""pull/scenarios"",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""language/tables"",
              ""Tag"": ""language/tables_bb2223""
        }")]
        [Trait("Category", "Integration")]
        public async Task ScenarioCleanPush(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };
            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            Assets updatedAssets = null;
            string originalAssetsRepoBranch = assets.TagPrefix;
            string originalSHA = assets.Tag;
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure, isPushTest: true);
            try
            {
                // Ensure that the TagPrefix was updated
                Assert.NotEqual(originalAssetsRepoBranch, assets.TagPrefix);

                var jsonFileLocation = Path.Join(testFolder, GitStoretests.AssetsJson);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
                await _defaultStore.Restore(jsonFileLocation);

                // Calling Path.GetFullPath of the Path.Combine will ensure any directory separators are normalized for
                // the OS the test is running on. The reason being is that AssetsRepoPrefixPath, if there's a separator,
                // will be a forward one as expected by git but on Windows this won't result in a usable path.
                string localFilePath = Path.GetFullPath(Path.Combine(parsedConfiguration.AssetsRepoLocation, parsedConfiguration.AssetsRepoPrefixPath));

                // These are the files pulled down with the original Tag
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

                // Ensure that the config was updated with the new Tag as part of the push
                updatedAssets = TestHelpers.LoadAssetsFromFile(jsonFileLocation);
                Assert.NotEqual(originalSHA, updatedAssets.Tag);

                // Ensure that the targeted tag is present on the repo
                TestHelpers.CheckExistenceOfTag(updatedAssets, localFilePath);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
                TestHelpers.CleanupIntegrationTestTag(updatedAssets);
            }
        }

        /// <summary>
        /// Conflict Push Scenario
        /// 1. Branch already exists and we're not on the latest Tag
        /// 2. Add/Delete/Update files
        /// 3. Push commit to branch, detects a conflict
        /// 4. Verify local files are what is expected
        /// 5. Verify assets.json was updated with the new commit Tag
        /// </summary>
        /// <param name="inputJson"></param>
        /// <returns></returns>
        [EnvironmentConditionalSkipTheory]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""pull/scenarios"",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""language/tables"",
              ""Tag"": ""language/tables_9e81fb""
        }")]
        [Trait("Category", "Integration")]
        public async Task ScenarioConflictPush(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };
            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            Assets updatedAssets = null;
            string originalAssetsRepoBranch = assets.TagPrefix;
            string originalSHA = assets.Tag;
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure, isPushTest: true);
            try
            {
                // Ensure that the TagPrefix was updated
                Assert.NotEqual(originalAssetsRepoBranch, assets.TagPrefix);

                var jsonFileLocation = Path.Join(testFolder, GitStoretests.AssetsJson);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
                await _defaultStore.Restore(jsonFileLocation);

                // Calling Path.GetFullPath of the Path.Combine will ensure any directory separators are normalized for
                // the OS the test is running on. The reason being is that AssetsRepoPrefixPath, if there's a separator,
                // will be a forward one as expected by git but on Windows this won't result in a usable path.
                string localFilePath = Path.GetFullPath(Path.Combine(parsedConfiguration.AssetsRepoLocation, parsedConfiguration.AssetsRepoPrefixPath));

                // These are the files pulled down with the original Tag
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
                // We've made the the following changes against the previous Tag
                // 1. File1's version was incremented to 2, but deleted in the latest Tag
                // 2. File2's version was incremented to 3
                // 3. File3 was deleted
                // 4. File4 was deleted
                // 5. File6 was added
                await _defaultStore.Push(jsonFileLocation);

                // Verify that after the push the directory still contains our updated files
                Assert.Equal(3, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 3));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file6.txt", 1));

                // Ensure that the config was updated with the new Tag as part of the push
                updatedAssets = TestHelpers.LoadAssetsFromFile(jsonFileLocation);
                Assert.NotEqual(originalSHA, updatedAssets.Tag);

                // Ensure that the targeted tag is present on the repo
                TestHelpers.CheckExistenceOfTag(updatedAssets, localFilePath);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
                TestHelpers.CleanupIntegrationTestTag(updatedAssets);
            }
        }
    }
}
