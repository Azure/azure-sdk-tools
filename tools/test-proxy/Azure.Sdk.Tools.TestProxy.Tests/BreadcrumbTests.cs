using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Models;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using Azure.Sdk.Tools.TestProxy.Store;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class BreadCrumbtests
    {
        GitStore _defaultStore = new GitStore();

        [Theory]
        [InlineData("sdk", "tables", "assets.json")]
        [InlineData("assets.json")]
        public async Task TestbreadcrumbLine(params string[] args)
        {
            var folderStructure = new string[]
            {
                Path.Combine(args)
            };
            var inputJson = @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""pull/scenarios"",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""main"",
              ""Tag"": ""language/tables_bb2223""
            }";

            Assets assets = System.Text.Json.JsonSerializer.Deserialize<Assets>(inputJson);
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure);
            var pathToAssets = Path.Join(testFolder, Path.Combine(args));

            try
            {
                var parsedConfig = await _defaultStore.ParseConfigurationFile(pathToAssets);
                var breadcrumbLine = new BreadcrumbLine(parsedConfig);

                Assert.Equal(parsedConfig.Tag, breadcrumbLine.Tag);
                Assert.Equal(parsedConfig.AssetsJsonRelativeLocation, breadcrumbLine.PathToAssetsJson);
                Assert.Equal(parsedConfig.AssetRepoShortHash, breadcrumbLine.ShortHash);

                // now tostring the line, then reparse it.
                var reparsedLine = new BreadcrumbLine(breadcrumbLine.ToString());

                Assert.Equal(breadcrumbLine.Tag, reparsedLine.Tag);
                Assert.Equal(breadcrumbLine.ShortHash, reparsedLine.ShortHash);
                Assert.Equal(breadcrumbLine.PathToAssetsJson.Replace("\\", "/"), reparsedLine.PathToAssetsJson);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Theory]
        [InlineData("assets.json;abcdemeep;atargetedTag", "assets.json", "abcdemeep", "atargetedTag")]
        [InlineData("sdk/assets.json;12341516;ADifferentTag", "sdk/assets.json", "12341516", "ADifferentTag")]
        [InlineData("sdk/blahblah/coolÑ/assets.json;  hello!  ;BlahTag", "sdk/blahblah/coolÑ/assets.json", "hello!", "BlahTag")]
        [InlineData("sdk/blahblah/coolÑ/assets.json;abcde12345;  ", "sdk/blahblah/coolÑ/assets.json", "abcde12345", "")]
        public void TestBreadcrumbParsing(string incomingString, string expectedPath, string expectedHash, string expectedTag)
        {
            BreadcrumbLine breadcrumbLine = new BreadcrumbLine(incomingString);
            Assert.Equal(expectedPath, breadcrumbLine.PathToAssetsJson);
            Assert.Equal(expectedHash, breadcrumbLine.ShortHash);
            Assert.Equal(expectedTag, breadcrumbLine.Tag);
        }

        [Theory]
        [InlineData("sdk/assets.json;12341516;   ")]
        [InlineData("sdk/assets.json;12341516;")]
        public void TestBreadCrumbHandlesEmptyTag(string testString)
        {
            BreadcrumbLine breadcrumbLine = new BreadcrumbLine(testString);

            Assert.Equal("12341516", breadcrumbLine.ShortHash);
            Assert.Equal("sdk/assets.json", breadcrumbLine.PathToAssetsJson);
            Assert.Empty(breadcrumbLine.Tag);
        }

        [Fact]
        public void TestBreadCrumbThrows()
        {
            var testString = "sdk/assets.json;12341516";

            var assertion = Assert.Throws<HttpException>(() =>
            {
                BreadcrumbLine breadcrumbLine = new BreadcrumbLine(testString);
            });
        }
    }
}
