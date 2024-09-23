using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Console;
using Azure.Sdk.Tools.TestProxy.Store;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests.IntegrationTests
{
    // Reset Test Scenarios involving https://github.com/Azure/azure-sdk-assets-integration

    // Setup:
    // The files live under https://github.com/Azure/azure-sdk-assets-integration/tree/main/pull/scenarios.
    // Each file contains nothing but a single version digit, which is used for verification purposes.
    // Most of the scenarios involve restoring from a Tag, updating, adding, and/or deleting files locally
    // and then performing a Reset and verifying that the only the files in the original Tag are there and
    // they've been restored to what they were in the original Tag.
    public class GitStoreIntegrationResetTests
    {
        private GitStore _defaultStore;
        private ConsoleWrapperTester _consoleWrapperTester;

        // Right now, this is necessary for testing purposes but the real server won't have
        // this issue.
        public GitStoreIntegrationResetTests()
        {
            var loggerFactory = new LoggerFactory();
            DebugLogger.ConfigureLogger(loggerFactory);
            _consoleWrapperTester = new ConsoleWrapperTester();
            _defaultStore = new GitStore(_consoleWrapperTester);
        }

        // Scenario 1 - Changes to existing files only are detected and overridden with Reset response Y
        // 1. Restore from Tag python/tables_fc54d0
        // 2. Expect: 3 files with versions they were checked in with
        // 3. Update one or more files, incrementing their version
        // 4. Expect: files updated should be at version 2
        // 5. Reset with Y
        // 6. Expect: each file should be at it's initial version, the version that was in the original Tag
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

                // Increment file versions to cause a change on disk
                TestHelpers.IncrementFileVersion(localFilePath, "file1.txt");
                TestHelpers.IncrementFileVersion(localFilePath, "file3.txt");
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file3.txt", 2));

                // Reset the cloned assets, reponse for overwrite = Y
                _consoleWrapperTester.SetReadLineResponse("Y");
                await _defaultStore.Reset(jsonFileLocation);

                // Verify all files have been set back to their original versions
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

        // Scenario 2 - Changes to existing files only are detected and retained with Reset response N
        // 1. Restore from Tag python/tables_fc54d0
        // 2. Expect: 3 files with versions they were checked in with
        // 3. Update one or more files, incrementing their version
        // 4. Expect: files updated should be at version 2
        // 5. Reset with N
        // 6. Expect: file versions should be what they were in step 4
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

                Assert.Equal(3, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file3.txt", 1));

                // Increment file versions to cause a change on disk
                TestHelpers.IncrementFileVersion(localFilePath, "file1.txt");
                TestHelpers.IncrementFileVersion(localFilePath, "file3.txt");
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file3.txt", 2));

                // Reset the cloned assets, reponse for overwrite = N
                _consoleWrapperTester.SetReadLineResponse("N");
                await _defaultStore.Reset(jsonFileLocation);

                // Verify all files have been set back to their original versions
                Assert.Equal(3, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file3.txt", 2));
                await TestHelpers.CheckBreadcrumbAgainstAssetsJsons(new string[] { jsonFileLocation });

            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        // Scenario 3 - Restore from Tag, add and remove files, Reset response Y
        // 1. Restore from Tag python/tables_9e81fb
        // 2. Expect: 4 files with versions they were checked in with
        // 3. Update add/remove files
        // 4. Expect: Untouched files are the same versions as step 2, added files are version 1, removed files are gone
        // 5. Reset with Y
        // 6. Expect: each file should be at it's initial version, the version that was in the original Tag
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

                // Reset the cloned assets, reponse for overwrite = Y
                _consoleWrapperTester.SetReadLineResponse("Y");
                await _defaultStore.Reset(jsonFileLocation);

                // Verify the only files there are ones from the Tag
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

        // Scenario 4 - Restore from Tag, add and remove files, Reset response N
        // 1. Restore from Tag python/tables_9e81fb
        // 2. Expect: 4 files with versions they were checked in with
        // 3. Update add/remove files
        // 4. Expect: Untouched files are the same versions as step 2, added files are version 1, removed files are gone
        // 5. Reset with N
        // 6. Expect: same files and same versions as step 4
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
        public async Task Scenario4(string inputJson)
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

                // Reset the cloned assets, reponse for overwrite = N
                _consoleWrapperTester.SetReadLineResponse("N");
                await _defaultStore.Reset(jsonFileLocation);

                // Verify the only files were not restored from the Tag
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

        // Scenario 5 - Restore from Tag, add and remove files, Reset response N, then Reset response Y
        // 1. Restore from Tag python/tables_9e81fb
        // 2. Expect: 3 files with versions they were checked in with
        // 3. Update add/remove files
        // 4. Expect: Untouched files are the same versions as step 2, added files are version 1, removed files are gone
        // 5. Reset with N
        // 6. Expect: same files and same versions as step 4
        // 7. Reset with Y
        // 8. Expect: same files and same versions as step 2
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
        public async Task Scenario5(string inputJson)
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
                Assert.Equal(3, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file4.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file5.txt", 1));


                // Delete a file
                File.Delete(Path.Combine(localFilePath, "file4.txt"));
                // Add a couple of files
                TestHelpers.CreateFileWithInitialVersion(localFilePath, "file1.txt");
                TestHelpers.CreateFileWithInitialVersion(localFilePath, "file3.txt");

                // Verify the set of files after the additions/deletions
                Assert.Equal(4, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file3.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file5.txt", 1));

                // Reset the cloned assets, reponse for overwrite = N
                _consoleWrapperTester.SetReadLineResponse("N");
                await _defaultStore.Reset(jsonFileLocation);

                // Verify the files were not restored from the Tag
                Assert.Equal(4, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file3.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file5.txt", 1));

                // Reset the cloned assets, reponse for overwrite = Y
                _consoleWrapperTester.SetReadLineResponse("Y");
                await _defaultStore.Reset(jsonFileLocation);

                // Verify files are from the Tag
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
    }
}
