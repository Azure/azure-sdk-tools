using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Store;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests.IntegrationTests
{

    // With the use of tags, the push scenarios are going to be somewhat redundant since
    // tags are vastly simplified, in terms of processing compared to branches. The
    // Scenarios below are similar but use 3 different starting tags with different
    // files and different file versions.
    public class GitStoreIntegrationPushTests
    {
        public GitStoreIntegrationPushTests()
        {
            var loggerFactory = new LoggerFactory();
            DebugLogger.ConfigureLogger(loggerFactory);
        }

        private GitStore _defaultStore = new GitStore();

        /// <summary>
        /// 1. Restore from the initial tag in pull/scenarios
        /// 2. Add/Delete/Update files
        /// 3. Push to new branch
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
              ""Tag"": ""language/tables_fc54d0""
        }")]
        [Trait("Category", "Integration")]
        public async Task Scenario1(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };

            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            Assets updatedAssets = null;
            string originalTagPrefix = assets.TagPrefix;
            string originalTag = assets.Tag;
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure, isPushTest:true);
            try
            {
                // Ensure that the TagPrefix was updated
                Assert.NotEqual(originalTagPrefix, assets.TagPrefix);

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
                Assert.NotEqual(originalTag, updatedAssets.Tag);

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
        /// 1. Restore from the second tag in pull/scenarios
        /// 2. Add/Delete/Update files
        /// 3. Push to new branch
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
        public async Task Scenario2(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };
            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            Assets updatedAssets = null;
            string originalTagPrefix = assets.TagPrefix;
            string originalTag = assets.Tag;
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure, isPushTest: true);
            try
            {
                // Ensure that the TagPrefix was updated
                Assert.NotEqual(originalTagPrefix, assets.TagPrefix);

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
                Assert.NotEqual(originalTag, updatedAssets.Tag);

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
        /// 1. Restore from the third tag in pull/scenarios
        /// 2. Add/Delete/Update files
        /// 3. Push to new branch
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
        public async Task Scenario3(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };
            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            Assets updatedAssets = null;
            string originalTagPrefix = assets.TagPrefix;
            string originalTag = assets.Tag;
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure, isPushTest: true);
            try
            {
                // Ensure that the TagPrefix was updated
                Assert.NotEqual(originalTagPrefix, assets.TagPrefix);

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
                Assert.NotEqual(originalTag, updatedAssets.Tag);

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
        /// 1. Restore from empty tag
        /// 2. Add/Delete/Update files
        /// 3. Push to new branch
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
              ""Tag"": """"
        }")]
        [Trait("Category", "Integration")]
        public async Task Scenario4(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };
            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            Assets updatedAssets = null;
            string originalTagPrefix = assets.TagPrefix;
            string originalTag = assets.Tag;
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure, isPushTest: true);
            try
            {
                // Ensure that the TagPrefix was updated
                Assert.NotEqual(originalTagPrefix, assets.TagPrefix);

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
                Assert.NotEqual(originalTag, updatedAssets.Tag);

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
        /// 1. Restore from a tag that has minimal existing files and a long destination path
        /// 2. Add a _bunch_ of files
        /// 3. Push
        /// 4. Verify local files are what is expected
        /// 5. Verify assets.json was updated with the new commit Tag
        /// </summary>
        /// <param name="inputJson"></param>
        /// <returns></returns>
        [EnvironmentConditionalSkipTheory]
        [InlineData(1000, 0.15)] // 1000 0.15MB files
        [InlineData(150, 0.25)]  // 150  0.25MB files
        [InlineData(100, 0.50)]  // 100  0.5MB files
        [InlineData(10, 90)]     // 10   90MB files
        [Trait("Category", "Integration")]
        public async Task LargePushPerformance(int numberOfFiles, double fileSize)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };
            Assets assets = JsonSerializer.Deserialize<Assets>(@"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""python"",
              ""TagPrefix"": ""python/tables"",
              ""Tag"": ""python/tables4f724f0c""
            }");
            Assets updatedAssets = null;
            List<string> testFiles = new List<string>();
            string originalTagPrefix = assets.TagPrefix;
            string originalTag = assets.Tag;
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure, isPushTest: true);

            try
            {
                // Ensure that the TagPrefix was updated
                Assert.NotEqual(originalTagPrefix, assets.TagPrefix);

                var jsonFileLocation = Path.Join(testFolder, GitStoretests.AssetsJson);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
                await _defaultStore.Restore(jsonFileLocation);

                var assetRepoRoot = await _defaultStore.GetPath(jsonFileLocation);
                var deepPath = Path.Join(assetRepoRoot, "sdk", "tables", "azure-data-tables", "tests", "recordings");

                // generate a bunch of files
                for (var i = 0; i < numberOfFiles; i++)
                {
                    testFiles.Add(TestHelpers.GenerateRandomFile(fileSize, deepPath));
                }

                await _defaultStore.Push(jsonFileLocation);

                // Verify that after the push the directory still contains our updated files
                Assert.Equal(3 + testFiles.Count, System.IO.Directory.EnumerateFiles(deepPath).Count());
                foreach (var path in testFiles)
                {
                    Assert.True(File.Exists(path));
                }

                // Ensure that the config was updated with the new Tag as part of the push
                updatedAssets = TestHelpers.LoadAssetsFromFile(jsonFileLocation);
                Assert.NotEqual(originalTag, updatedAssets.Tag);

                // Ensure that the targeted tag is present on the repo
                TestHelpers.CheckExistenceOfTag(updatedAssets, assetRepoRoot);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
                TestHelpers.CleanupIntegrationTestTag(updatedAssets);
            }
        }
    }
}
