using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Models;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using Azure.Sdk.Tools.TestProxy.Store;
using Azure.Sdk.tools.TestProxy.Common;
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

        [Theory]
        [InlineData("recordings_v1")]
        [InlineData("")]
        [InlineData(null)]
        public async Task UpdateDoesNotRewriteUnchangedEntry(string tag)
        {
            var testFolder = TestHelpers.GetTmpPath();
            try
            {
                var config = CreateBreadcrumbConfiguration(testFolder, tag);
                var breadcrumb = new GitStoreBreadcrumb();
                await breadcrumb.Update(config);
                string path = breadcrumb.GetBreadCrumbLocation(config);
                byte[] contents = File.ReadAllBytes(path);
                File.SetLastWriteTimeUtc(path, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                DateTime timestamp = File.GetLastWriteTimeUtc(path);

                await breadcrumb.Update(config);
                await new GitStoreBreadcrumb().Update(config);

                Assert.Equal(contents, File.ReadAllBytes(path));
                Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path));
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public async Task UnchangedUpdatePreservesFormattingAndOtherEntries()
        {
            var testFolder = TestHelpers.GetTmpPath();
            try
            {
                var config = CreateBreadcrumbConfiguration(testFolder);
                var breadcrumb = new GitStoreBreadcrumb();
                string path = breadcrumb.GetBreadCrumbLocation(config);
                string contents = $"  {config.AssetsJsonRelativeLocation} ; {config.AssetRepoShortHash} ; {config.Tag}  \r\n" +
                    "sdk/other/assets.json;other-hash;other-tag\r\n";
                File.WriteAllText(path, contents, new UTF8Encoding(true));
                byte[] originalBytes = File.ReadAllBytes(path);
                File.SetLastWriteTimeUtc(path, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                DateTime timestamp = File.GetLastWriteTimeUtc(path);

                await breadcrumb.Update(config);

                Assert.Equal(originalBytes, File.ReadAllBytes(path));
                Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path));
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Theory]
        [InlineData("changed-tag", false)]
        [InlineData("recordings_v1", true)]
        public async Task UpdateRepairsExternallyChangedEntry(string externalTag, bool changedHash)
        {
            var testFolder = TestHelpers.GetTmpPath();
            try
            {
                var config = CreateBreadcrumbConfiguration(testFolder);
                var breadcrumb = new GitStoreBreadcrumb();
                await breadcrumb.Update(config);
                string path = breadcrumb.GetBreadCrumbLocation(config);
                string hash = changedHash ? "changed-hash" : config.AssetRepoShortHash;
                const string otherEntry = "sdk/other/assets.json;other-hash;other-tag";
                File.WriteAllLines(path, new[]
                {
                    $"{config.AssetsJsonRelativeLocation};{hash};{externalTag}",
                    otherEntry
                });
                File.SetLastWriteTimeUtc(path, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                DateTime timestamp = File.GetLastWriteTimeUtc(path);

                await breadcrumb.Update(config);

                string[] lines = File.ReadAllLines(path);
                Assert.Equal(2, lines.Length);
                Assert.Contains(new BreadcrumbLine(config).ToString(), lines);
                Assert.Contains(otherEntry, lines);
                Assert.NotEqual(timestamp, File.GetLastWriteTimeUtc(path));
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public async Task UpdateWritesChangedConfigurationTag()
        {
            var testFolder = TestHelpers.GetTmpPath();
            try
            {
                var config = CreateBreadcrumbConfiguration(testFolder);
                var breadcrumb = new GitStoreBreadcrumb();
                await breadcrumb.Update(config);
                config.Tag = "recordings_v2";

                await breadcrumb.Update(config);

                string line = Assert.Single(File.ReadAllLines(breadcrumb.GetBreadCrumbLocation(config)));
                Assert.Equal("recordings_v2", new BreadcrumbLine(line).Tag);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task UpdateAddsMissingEntryWithoutRemovingOtherEntries(bool includeOtherEntry)
        {
            var testFolder = TestHelpers.GetTmpPath();
            try
            {
                var config = CreateBreadcrumbConfiguration(testFolder);
                var breadcrumb = new GitStoreBreadcrumb();
                string path = breadcrumb.GetBreadCrumbLocation(config);
                const string otherEntry = "sdk/other/assets.json;other-hash;other-tag";
                File.WriteAllLines(path, includeOtherEntry ? new[] { otherEntry } : Array.Empty<string>());

                await breadcrumb.Update(config);

                string[] lines = File.ReadAllLines(path);
                Assert.Equal(includeOtherEntry ? 2 : 1, lines.Length);
                Assert.Contains(new BreadcrumbLine(config).ToString(), lines);
                if (includeOtherEntry)
                {
                    Assert.Contains(otherEntry, lines);
                }
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public async Task UpdateRecreatesDeletedBreadcrumb()
        {
            var testFolder = TestHelpers.GetTmpPath();
            try
            {
                var config = CreateBreadcrumbConfiguration(testFolder);
                var breadcrumb = new GitStoreBreadcrumb();
                await breadcrumb.Update(config);
                string path = breadcrumb.GetBreadCrumbLocation(config);
                File.Delete(path);

                await breadcrumb.Update(config);

                Assert.Equal(new BreadcrumbLine(config).ToString(), Assert.Single(File.ReadAllLines(path)));
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public async Task UpdateStillRejectsMalformedOtherEntries()
        {
            var testFolder = TestHelpers.GetTmpPath();
            try
            {
                var config = CreateBreadcrumbConfiguration(testFolder);
                var breadcrumb = new GitStoreBreadcrumb();
                string path = breadcrumb.GetBreadCrumbLocation(config);
                File.WriteAllLines(path, new[] { new BreadcrumbLine(config).ToString(), "malformed-entry" });
                byte[] contents = File.ReadAllBytes(path);

                await Assert.ThrowsAsync<HttpException>(() => breadcrumb.Update(config));

                Assert.Equal(contents, File.ReadAllBytes(path));
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        private static GitAssetsConfiguration CreateBreadcrumbConfiguration(string testFolder, string tag = "recordings_v1")
        {
            return new GitAssetsConfiguration
            {
                AssetsRepo = $"test-proxy/{Path.GetFileName(testFolder)}",
                RepoRoot = new NormalizedString(testFolder),
                AssetsJsonRelativeLocation = new NormalizedString("sdk/service/assets.json"),
                Tag = tag
            };
        }
    }
}
