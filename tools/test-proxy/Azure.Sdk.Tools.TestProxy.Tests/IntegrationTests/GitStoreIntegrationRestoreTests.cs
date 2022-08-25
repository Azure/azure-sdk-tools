using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Store;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests.IntegrationTests
{
    // Pull Test Scenarios involving https://github.com/Azure/azure-sdk-assets-integration

    // Setup:
    // The files live under https://github.com/Azure/azure-sdk-assets-integration/tree/main/pull/scenarios.
    // Each file contains nothing but a single version digit, which is used for verification purposes.
    // There are 3 pull test scenarios and each uses a different SHA. The scenarios are detailed down
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
        // SHA fc54d000d0427c4a68bc8962d40f957f59e14577
        // This was the initial push of the test files:
        // Added file1.txt
        // Added file2.txt
        // Added file3.txt
        // Expect: each file should be version 1
        [Theory(Skip = "Skipping because we the integration branch permissions set for the test suite to run.")]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""pull/scenarios"",
              ""AssetsRepoId"": """",
              ""AssetsRepoBranch"": ""main"",
              ""SHA"": ""fc54d000d0427c4a68bc8962d40f957f59e14577""
        }")]
        public async Task Scenario1(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };

            var testFolder = TestHelpers.DescribeTestFolder(inputJson, folderStructure);
            try
            {
                var jsonFileLocation = Path.Join(testFolder, GitStoretests.AssetsJson);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
                await _defaultStore.Restore(jsonFileLocation);

                // Calling Path.GetFullPath of the Path.Combine will ensure any directory separators are normalized for
                // the OS the test is running on. The reason being is that AssetsRepoPrefixPath, if there's a separator,
                // will be a forward one as expected by git but on Windows this won't result in a usable path.
                string localFilePath = Path.GetFullPath(Path.Combine(parsedConfiguration.AssetsRepoLocation, parsedConfiguration.AssetsRepoPrefixPath));

                Assert.Equal(3, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file3.txt", 1));
            } 
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }


        // Scenario2
        // SHA 9e81fbb7d08c2df4cbdbfaffe79cde5d72f560d1
        // This was the second push of the test files.
        // Unchanged file1.txt
        // Updated file2.txt
        // Updated file3.txt
        // Added file4.txt
        // Expect: file1 version 1
        //         file2 version 2
        //         file3 version 2
        //         file4 version 1
        [Theory(Skip = "Skipping because we the integration branch permissions set for the test suite to run.")]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""pull/scenarios"",
              ""AssetsRepoId"": """",
              ""AssetsRepoBranch"": ""main"",
              ""SHA"": ""9e81fbb7d08c2df4cbdbfaffe79cde5d72f560d1""
        }")]
        public async Task Scenario2(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };

            var testFolder = TestHelpers.DescribeTestFolder(inputJson, folderStructure);
            try
            {
                var jsonFileLocation = Path.Join(testFolder, GitStoretests.AssetsJson);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
                await _defaultStore.Restore(jsonFileLocation);

                // Calling Path.GetFullPath of the Path.Combine will ensure any directory separators are normalized for
                // the OS the test is running on. The reason being is that AssetsRepoPrefixPath, if there's a separator,
                // will be a forward one as expected by git but on Windows this won't result in a usable path.
                string localFilePath = Path.GetFullPath(Path.Combine(parsedConfiguration.AssetsRepoLocation, parsedConfiguration.AssetsRepoPrefixPath));

                Assert.Equal(4, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file1.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file3.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file4.txt", 1));
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        // Scenario3
        // SHA bb2223a3aa0472ff481f8e1850e7647dc39fbfdd
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
        [Theory(Skip = "Skipping because we the integration branch permissions set for the test suite to run.")]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""pull/scenarios"",
              ""AssetsRepoId"": """",
              ""AssetsRepoBranch"": ""main"",
              ""SHA"": ""bb2223a3aa0472ff481f8e1850e7647dc39fbfdd""
        }")]
        public async Task Scenario3(string inputJson)
        {
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };

            var testFolder = TestHelpers.DescribeTestFolder(inputJson, folderStructure);

            try
            {
                var jsonFileLocation = Path.Join(testFolder, GitStoretests.AssetsJson);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
                await _defaultStore.Restore(jsonFileLocation);

                // Calling Path.GetFullPath of the Path.Combine will ensure any directory separators are normalized for
                // the OS the test is running on. The reason being is that AssetsRepoPrefixPath, if there's a separator,
                // will be a forward one as expected by git but on Windows this won't result in a usable path.
                string localFilePath = Path.GetFullPath(Path.Combine(parsedConfiguration.AssetsRepoLocation, parsedConfiguration.AssetsRepoPrefixPath));

                Assert.Equal(3, System.IO.Directory.EnumerateFiles(localFilePath).Count());
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file2.txt", 2));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file4.txt", 1));
                Assert.True(TestHelpers.VerifyFileVersion(localFilePath, "file5.txt", 1));
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }
    }
}
