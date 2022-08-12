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

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    // Test Scenarios involving https://github.com/Azure/azure-sdk-assets-integration
    // Current state:
    // Outside of the common files in the root, the only recordings in the repository
    // are at https://github.com/Azure/azure-sdk-assets-integration/tree/main/python/recordings/sdk/tables/azure-data-tables/tests/recordings
    // and for those there's only a single set of 3 recording files with no history.

    // Additional Setup:
    // We're going to want to push another commit that adds a couple of new recordings and changes
    public class GitStoreIntegrationTests
    {
        public GitStoreIntegrationTests()
        {
            var loggerFactory = new LoggerFactory();
            DebugLogger.ConfigureLogger(loggerFactory);
        }

        private GitStore _defaultStore = new GitStore();

        [Theory]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""python/recordings/"",
              ""AssetsRepoId"": """",
              ""AssetsRepoBranch"": ""auto/test"",
              ""SHA"": ""786b4f3d380d9c36c91f5f146ce4a7661ffee3b9""
        }")]
        public async Task TestBasicRestoreFromAssetsJson(string inputJson)
        {
            string[] folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };

            var testFolder = TestHelpers.DescribeTestFolder(inputJson, folderStructure);
            var jsonFileLocation = Path.Join(testFolder, GitStoretests.AssetsJson);

            var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
            await _defaultStore.Restore(jsonFileLocation);
        }
    }
}
