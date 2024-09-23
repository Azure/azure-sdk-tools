using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Store;
using Microsoft.AspNetCore.Http;
using Castle.Components.DictionaryAdapter;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests.IntegrationTests
{
    // Restore Test Scenarios involving https://github.com/Azure/azure-sdk-assets-integration

    // Setup:
    // The files live under https://github.com/Azure/azure-sdk-assets-integration/tree/main/pull/scenarios.
    // Each file contains nothing but a single version digit, which is used for verification purposes.
    // There are restore test scenarios and each uses a different Tag. The scenarios are detailed down
    // below with their test functions.
    public class GitStoreIntegrationRestoreTests
    {
        // Right now, this is necessary for testing purposes but the real server won't have
        // this issue.
        public GitStoreIntegrationRestoreTests()
        {
            var loggerFactory = new LoggerFactory();
            DebugLogger.ConfigureLogger(loggerFactory);
        }

        private GitStore _defaultStore = new GitStore();

        // Scenario1
        // Tag python/tables_fc54d0
        // This was the initial push of the test files:
        // Added file1.txt
        // Added file2.txt
        // Added file3.txt
        // Expect: each file should be version 1
        [EnvironmentConditionalSkipTheory]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""pull/scenarios"",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""main"",
              ""Tag"": ""python/tables_fc54d0""
        }")]
        [Trait("Category", "Integration")]
        public async Task Scenario1(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };

            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure);
            try
            {
                var jsonFileLocation = Path.Join(testFolder, GitStoretests.AssetsJson);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
                await _defaultStore.Restore(jsonFileLocation);

                // Calling Path.GetFullPath of the Path.Combine will ensure any directory separators are normalized for
                // the OS the test is running on. The reason being is that AssetsRepoPrefixPath, if there's a separator,
                // will be a forward one as expected by git but on Windows this won't result in a usable path.
                string localFilePath = Path.GetFullPath(Path.Combine(parsedConfiguration.AssetsRepoLocation.ToString(), parsedConfiguration.AssetsRepoPrefixPath.ToString()));

                Assert.Equal(3, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file3.txt", 1));
                await TestHelpers.CheckBreadcrumbAgainstAssetsJsons(new string[] { jsonFileLocation });
            } 
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }


        // Scenario2
        // Tag python/tables_9e81fb
        // This was the second push of the test files.
        // Unchanged file1.txt
        // Updated file2.txt
        // Updated file3.txt
        // Added file4.txt
        // Expect: file1 version 1
        //         file2 version 2
        //         file3 version 2
        //         file4 version 1
        [EnvironmentConditionalSkipTheory]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""pull/scenarios"",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""main"",
              ""Tag"": ""python/tables_9e81fb""
        }")]
        [Trait("Category", "Integration")]
        public async Task Scenario2(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };

            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure);
            try
            {
                var jsonFileLocation = Path.Join(testFolder, GitStoretests.AssetsJson);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
                await _defaultStore.Restore(jsonFileLocation);

                // Calling Path.GetFullPath of the Path.Combine will ensure any directory separators are normalized for
                // the OS the test is running on. The reason being is that AssetsRepoPrefixPath, if there's a separator,
                // will be a forward one as expected by git but on Windows this won't result in a usable path.
                string localFilePath = Path.GetFullPath(Path.Combine(parsedConfiguration.AssetsRepoLocation.ToString(), parsedConfiguration.AssetsRepoPrefixPath.ToString()));

                Assert.Equal(4, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file3.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file4.txt", 1));
                await TestHelpers.CheckBreadcrumbAgainstAssetsJsons(new string[] { jsonFileLocation });
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        // Scenario3
        // Tag language/tables_bb2223
        // This was the third push of the test files.
        // Deleted   file1.txt
        // Unchanged file2.txt
        // Deleted   file3.txt
        // Unchanged file4.txt
        // Added     file5.txt
        // Expect: file1 deleted
        //         file2 version 2
        //         file3 deleted
        //         file4 version 1
        //         file5 version 1
        [EnvironmentConditionalSkipTheory]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""pull/scenarios"",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""main"",
              ""Tag"": ""language/tables_bb2223""
        }")]
        [Trait("Category", "Integration")]
        public async Task Scenario3(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };

            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure);

            try
            {
                var jsonFileLocation = Path.Join(testFolder, GitStoretests.AssetsJson);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
                await _defaultStore.Restore(jsonFileLocation);

                // Calling Path.GetFullPath of the Path.Combine will ensure any directory separators are normalized for
                // the OS the test is running on. The reason being is that AssetsRepoPrefixPath, if there's a separator,
                // will be a forward one as expected by git but on Windows this won't result in a usable path.
                string localFilePath = Path.GetFullPath(Path.Combine(parsedConfiguration.AssetsRepoLocation.ToString(), parsedConfiguration.AssetsRepoPrefixPath.ToString()));

                Assert.Equal(3, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file4.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file5.txt", 1));
                await TestHelpers.CheckBreadcrumbAgainstAssetsJsons(new string[] { jsonFileLocation });
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        // Scenario4
        // Sometime in the past, our assets repo was restored to language/tables_bb2223
        // Our local copy of the assets repo has set of tags A, B, and C.
        //
        // However, after we update our language repo, our assets.json is also updated to a new tag. If we attempt  a restore
        // operation, we need to have a way to ensure that the asset-sync repo doesn't just return "yes I'm initialized" and
        // actually updates itself from origin prior to checking out the targeted tag.
        //
        // Order of operations
        //   - restore language/tables_bb2223 on a fresh gitstore
        //   - push a new test tag under test/<blah>
        //   - restore the new test tag on a fresh gitstore.
        //
        // Using a fresh gitstore for each restore will simulate different CLI calls, as whether or not an assets repo is
        // initialized is aggressively cached within each given gitstore.
        [EnvironmentConditionalSkipTheory]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""pull/scenarios"",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""main"",
              ""Tag"": ""language/tables_bb2223""
        }")]
        [Trait("Category", "Integration")]
        public async Task Scenario4(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };
            GitStore additionalStore = new GitStore();

            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure);
            var jsonFileLocation = Path.Join(testFolder, GitStoretests.AssetsJson);
            var tempTag = string.Format("test_{0}", Guid.NewGuid().ToString());

            try
            {
                await _defaultStore.Restore(jsonFileLocation);

                // manually update our tag to one that doesn't exist it
                assets.Tag = tempTag;
                // We also update the assets TagPrefix because that's what we use in InitIntegrationTag.
                // TODO: Cleanup TagPrefix usage. Covered in Azure/azure-sdk-tools#4497
                assets.TagPrefix = tempTag;
                TestHelpers.InitIntegrationTag(assets, tempTag);

                TestHelpers.WriteTestFile(JsonSerializer.Serialize(assets), jsonFileLocation);
                // this is the first time this Gitstore has seen this assets.json. This allows
                // us to simulate a re-entrant command on an already initialized repo.
                await additionalStore.Restore(jsonFileLocation);
                await TestHelpers.CheckBreadcrumbAgainstAssetsJsons(new string[] { jsonFileLocation });
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
                TestHelpers.CleanupIntegrationTestTag(assets);
            }
        }

        [EnvironmentConditionalSkipTheory]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""pull/scenarios"",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""main"",
              ""Tag"": """"
        }")]
        [Trait("Category", "Integration")]
        public async Task NonexistentTagFallsBack(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };

            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure);

            try
            {
                var jsonFileLocation = Path.Join(testFolder, GitStoretests.AssetsJson);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);

                await _defaultStore.Restore(jsonFileLocation);

                string localFilePath = Path.GetFullPath(Path.Combine(parsedConfiguration.AssetsRepoLocation.ToString(), parsedConfiguration.AssetsRepoPrefixPath.ToString()));

                Assert.Equal(3, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                await TestHelpers.CheckBreadcrumbAgainstAssetsJsons(new string[] { jsonFileLocation });
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
              ""AssetsRepoPrefixPath"": ""pull/scenarios"",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""main"",
              ""Tag"": ""INVALID_TAG""
        }", "Invocation of \"git fetch origin refs/tags/INVALID_TAG:refs/tags/INVALID_TAG\" had a non-zero exit code -1")]
        [Trait("Category", "Integration")]
        public async Task InvalidTagThrows(string inputJson, string httpException)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };

            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure);

            try
            {
                var jsonFileLocation = Path.Join(testFolder, GitStoretests.AssetsJson);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);

                var assertion = await Assert.ThrowsAsync<HttpException>(async () =>
                {
                    await _defaultStore.Restore(jsonFileLocation);
                });
                Assert.Contains(httpException, assertion.Message);
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
              ""AssetsRepoPrefixPath"": ""pull/scenarios"",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""main"",
              ""Tag"": ""python/tables_9e81fb""
        }")]
        [Trait("Category", "Integration")]
        public async Task VerifyRestoreDiscardsChanges(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };

            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure);
            try
            {
                var jsonFileLocation = Path.Join(testFolder, GitStoretests.AssetsJson);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
                await _defaultStore.Restore(jsonFileLocation);

                // Calling Path.GetFullPath of the Path.Combine will ensure any directory separators are normalized for
                // the OS the test is running on. The reason being is that AssetsRepoPrefixPath, if there's a separator,
                // will be a forward one as expected by git but on Windows this won't result in a usable path.
                string localFilePath = Path.GetFullPath(Path.Combine(parsedConfiguration.AssetsRepoLocation.ToString(), parsedConfiguration.AssetsRepoPrefixPath.ToString()));

                // Verify files from Tag
                Assert.Equal(4, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file3.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file4.txt", 1));

                // Delete a couple of files
                File.Delete(Path.Combine(localFilePath, "file2.txt"));
                File.Delete(Path.Combine(localFilePath, "file4.txt"));

                // Add a file
                TestHelpers.CreateFileWithInitialVersion(localFilePath, "file5.txt");

                // Verify the set of files after the additions/deletions
                Assert.Equal(3, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file3.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file5.txt", 1));

                // simulate a feature branch checkout in the langauge repo. change the targeted
                // tag in place. The changes from lines above should be automatically discarded.
                var existingAssets = TestHelpers.LoadAssetsFromFile(jsonFileLocation);
                existingAssets.Tag = "python/tables_fc54d0";
                TestHelpers.UpdateAssetsFile(existingAssets, jsonFileLocation);

                await _defaultStore.Restore(jsonFileLocation);

                // Verify the only files there are ones from the Tag
                Assert.Equal(3, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file3.txt", 1));
                await TestHelpers.CheckBreadcrumbAgainstAssetsJsons(new string[] { jsonFileLocation });
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
              ""AssetsRepoPrefixPath"": ""pull/scenarios"",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""main"",
              ""Tag"": ""python/tables_9e81fb""
        }")]
        [Trait("Category", "Integration")]
        public async Task VerifyRestoreMaintainsChanges(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };

            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure);
            try
            {
                var jsonFileLocation = Path.Join(testFolder, GitStoretests.AssetsJson);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
                await _defaultStore.Restore(jsonFileLocation);

                // Calling Path.GetFullPath of the Path.Combine will ensure any directory separators are normalized for
                // the OS the test is running on. The reason being is that AssetsRepoPrefixPath, if there's a separator,
                // will be a forward one as expected by git but on Windows this won't result in a usable path.
                string localFilePath = Path.GetFullPath(Path.Combine(parsedConfiguration.AssetsRepoLocation.ToString(), parsedConfiguration.AssetsRepoPrefixPath.ToString()));

                // Verify files from Tag
                Assert.Equal(4, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file3.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file4.txt", 1));

                // Delete a couple of files
                File.Delete(Path.Combine(localFilePath, "file2.txt"));
                File.Delete(Path.Combine(localFilePath, "file4.txt"));

                // Add a file
                TestHelpers.CreateFileWithInitialVersion(localFilePath, "file5.txt");

                // Verify the set of files after the additions/deletions
                Assert.Equal(3, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file3.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file5.txt", 1));

                // This restore operates on the same tag, so we expect it to _properly keep the changes around_
                await _defaultStore.Restore(jsonFileLocation);

                // Verify that the same set of 3 files is still present!
                Assert.Equal(3, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file3.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file5.txt", 1));
                await TestHelpers.CheckBreadcrumbAgainstAssetsJsons(new string[] { jsonFileLocation });
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
              ""AssetsRepoPrefixPath"": ""pull/scenarios"",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""main"",
              ""Tag"": ""language/tables_bb2223""
        }")]
        [Trait("Category", "Integration")]
        public async Task RestoreUpdatesContext(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };

            Assets assets = System.Text.Json.JsonSerializer.Deserialize<Assets>(inputJson);
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure);
            var pathToAssets = Path.Join(testFolder, "assets.json");
            var currentDirectory = Environment.CurrentDirectory;
            var recordingHandler = new RecordingHandler(currentDirectory, _defaultStore);

            try
            {
                await recordingHandler.Restore(pathToAssets);

                var result = (await _defaultStore.ParseConfigurationFile(pathToAssets)).AssetsRepoLocation.ToString();

                Assert.Equal(result, recordingHandler.ContextDirectory);
                await TestHelpers.CheckBreadcrumbAgainstAssetsJsons(new string[] { pathToAssets });
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }
    }
}
