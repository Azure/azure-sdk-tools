using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Store;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using System.Text.Json;
using System.Linq;
using System.ComponentModel;
using Azure.Sdk.tools.TestProxy.Common;
using System.Collections.Generic;
using Moq;
using System.Diagnostics;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class FakeCommandResultsHandler : GitProcessHandler
    {
        public string RunResult = string.Empty;
        public string ErrorResult = string.Empty;
        public int ErrorCode = 0;

        public FakeCommandResultsHandler(string runResult, string errorResult, int errorCode)
        {

        }

        public override bool TryRun(string arguments, string workingDirectory, out CommandResult result)
        {
            result = new CommandResult()
            {
                ExitCode = ErrorCode,
                StdErr = ErrorResult,
                StdOut = RunResult
            };

            return true;
        }

        public FakeCommandResultsHandler() { }
    }

    public class GitStoretests
    {
        #region variable defs
        public static string AssetsJson = "assets.json";
        private GitStore _defaultStore = new GitStore();
        private string[] basicFolderStructure = new string[]
        {
            AssetsJson
        };

        public static Assets DefaultAssets = new Assets
        {
            AssetsRepo = "Azure/azure-sdk-assets-integration",
            AssetsRepoPrefixPath = "python/recordings/",
            AssetsRepoId = "",
            TagPrefix = "scenario_clean_push",
            Tag = "e4a4949a2b6cc2ff75afd0fe0d97cbcabf7b67b7"
        };

        public static string DefaultAssetsJson =
@"
{
    // a json comment that shouldn't break parsing.
    ""AssetsRepo"":""Azure/azure-sdk-assets-integration"",
    ""AssetsRepoPrefixPath"":""python/recordings/"",
    ""AssetsRepoId"":"""",
    ""TagPrefix"":""scenario_clean_push"",
    ""Tag"":""e4a4949a2b6cc2ff75afd0fe0d97cbcabf7b67b7""
}
";
        #endregion

        [Fact]
        public void TestEvaluateDirectoryGitRootExistsWithNoAssets()
        {
            string[] folderStructure = new string[]
            {
                AssetsJson,
                "folder1",
                Path.Join("folder2", "file1.json")
            };

            var testFolder = TestHelpers.DescribeTestFolder(null, folderStructure, malformedJson: String.Empty);
            try
            {
                var evaluation = _defaultStore.EvaluateDirectory(testFolder);

                Assert.True(evaluation.IsGitRoot);
                Assert.False(evaluation.AssetsJsonPresent);
                Assert.False(evaluation.IsRoot);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public void TestEvaluateDirectoryFindsGitAssetsAlongsideGitRoot()
        {
            string[] folderStructure = new string[]
            {
                AssetsJson,
                "folder1",
                Path.Join("folder2", "file1.json")
            };

            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, folderStructure);

            try
            {
                var evaluation = _defaultStore.EvaluateDirectory(testFolder);
                Assert.True(evaluation.IsGitRoot);
                Assert.True(evaluation.AssetsJsonPresent);
                Assert.False(evaluation.IsRoot);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public void TestEvaluateDirectoryIdentifiesIntermediateDirectory()
        {
            string[] folderStructure = new string[]
            {
                AssetsJson,
                "folder1",
                Path.Join("folder2", "file1.json")
            };

            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, folderStructure);
            try
            {
                var evaluationDirectory = Path.Join(testFolder, "folder1");

                var evaluation = _defaultStore.EvaluateDirectory(evaluationDirectory);
                Assert.False(evaluation.IsGitRoot);
                Assert.False(evaluation.AssetsJsonPresent);
                Assert.False(evaluation.IsRoot);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public void ResolveAssetsJsonFindsAssetsInTargetFolder()
        {
            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, basicFolderStructure);
            try
            {
                var path = _defaultStore.ResolveAssetsJson(testFolder);
                Assert.Equal(Path.Join(testFolder, AssetsJson), path);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public void ResolveAssetsJsonFindsAssetsInTargetFolderBelowRoot()
        {
            string[] folderStructure = new string[]
            {
                Path.Join("folder1", AssetsJson)
            };

            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, folderStructure);
            try
            {
                var evaluationDirectory = Path.Join(testFolder, "folder1");

                var path = _defaultStore.ResolveAssetsJson(evaluationDirectory);

                Assert.Equal(Path.Join(testFolder, "folder1", "assets.json"), path);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }


        [Fact]
        public void ResolveAssetsJsonThrowsOnUnableToLocate()
        {
            var testFolder = TestHelpers.DescribeTestFolder(null, new string[] { }, malformedJson: String.Empty);
            try
            {
                var assertion = Assert.Throws<HttpException>(() =>
                {
                    _defaultStore.ResolveAssetsJson(testFolder);
                });
                Assert.StartsWith("Unable to locate an assets.json at", assertion.Message);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }

        }

        [Fact]
        public void ResolveAssetsJsonThrowsOnUnableToLocateAfterTraversal()
        {

            string[] folderStructure = new string[]
            {
                "folder1",
            };

            var testFolder = TestHelpers.DescribeTestFolder(null, folderStructure, malformedJson: String.Empty);
            try
            {
                var evaluationDirectory = Path.Join(testFolder, "folder1");

                var assertion = Assert.Throws<HttpException>(() =>
                {
                    _defaultStore.ResolveAssetsJson(evaluationDirectory);
                });
                Assert.StartsWith("Unable to locate an assets.json at", assertion.Message);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }


        [Theory]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""python/recordings/"",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""auto/test"",
              ""Tag"": ""786b4f3d380d9c36c91f5f146ce4a7661ffee3b9""
        }")]
        // Valid to just pass the assets repo. We can infer everything else.
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration""
        }")]
        public async Task ParseConfigurationEvaluatesValidConfigs(string inputJson)
        {
            string[] folderStructure = new string[]
            {
                AssetsJson
            };

            var testFolder = TestHelpers.DescribeTestFolder(null, folderStructure, malformedJson: inputJson);
            try
            {
                var jsonFileLocation = Path.Join(testFolder, AssetsJson);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Theory]
        [InlineData(
        @"{
              ""AssetsRepo"": """"
        }")]
        [InlineData(
        @"{
              ""AssetsRepo"": ""   ""
        }")]
        [InlineData(
        @"{
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""auto/test"",
              ""Tag"": ""786b4f3d380d9c36c91f5f146ce4a7661ffee3b9""
        }")]
        public async Task ParseConfigurationThrowsOnMissingRequiredProperty(string inputJson)
        {
            string[] folderStructure = new string[]
            {
                AssetsJson
            };

            var testFolder = TestHelpers.DescribeTestFolder(null, folderStructure, malformedJson: inputJson);
            try
            {
                var jsonFileLocation = Path.Join(testFolder, AssetsJson);

                var assertion = await Assert.ThrowsAsync<HttpException>(async () =>
                {
                    await _defaultStore.ParseConfigurationFile(Path.Join(testFolder, AssetsJson));
                });
                Assert.Contains("must contain value for the key \"AssetsRepo\"", assertion.Message);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public async Task ParseConfigurationEvaluatesTargetFolder()
        {
            var folderPath = Path.Join("folder1", "folder2");
            var targetRelPath = Path.Join(folderPath, $"{AssetsJson}");
            string[] folderStructure = new string[]
            {
                targetRelPath
            };

            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, folderStructure);
            try
            {
                var jsonFileLocation = Path.Join(testFolder, folderPath);

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
                Assert.NotNull(parsedConfiguration);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Theory]
        [InlineData("folder1", "folder2")]
        [InlineData("folderabc123")]
        public async Task ParseConfigurationEvaluatesRelativePathCorrectly(params string[] inputPath)
        {
            var targetRelPath = Path.Join(inputPath);
            string[] folderStructure = new string[]
            {
                Path.Join(targetRelPath, AssetsJson)
            };

            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, folderStructure);
            try
            {
                var jsonFileLocation = new NormalizedString(Path.Join(testFolder, targetRelPath, AssetsJson));

                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);
                Assert.Equal(new NormalizedString(Path.Join(targetRelPath, AssetsJson)), parsedConfiguration.AssetsJsonRelativeLocation.ToString());
                Assert.Equal(jsonFileLocation, parsedConfiguration.AssetsJsonLocation.ToString());
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }

        }

        [Theory]
        [InlineData("")]
        [InlineData("{}")]
        public async Task ParseConfigurationThrowsOnEmptyJson(string errorJson)
        {
            var testFolder = TestHelpers.DescribeTestFolder(null, basicFolderStructure, ignoreEmptyAssetsJson: true, malformedJson: errorJson);
            try
            {
                var assertion = await Assert.ThrowsAsync<HttpException>(async () =>
                {
                    await _defaultStore.ParseConfigurationFile(Path.Join(testFolder, AssetsJson));
                });
                Assert.StartsWith("The provided assets.json at ", assertion.Message);
                Assert.EndsWith("did not have valid json present.", assertion.Message);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public async Task ParseConfigurationThrowsOnNonExistentJson()
        {
            var testFolder = TestHelpers.DescribeTestFolder(null, basicFolderStructure, malformedJson: String.Empty);
            try
            {
                var assertion = await Assert.ThrowsAsync<HttpException>(async () =>
                {
                    await _defaultStore.ParseConfigurationFile(Path.Join(testFolder, AssetsJson));
                });
                Assert.StartsWith("The provided assets.json path of ", assertion.Message);
                Assert.EndsWith(" does not exist.", assertion.Message);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public async Task GetDefaultBranchFailsWithInvalidRepo()
        {
            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, basicFolderStructure);

            try
            {
                // we are resetting the default branch so we will see if fallback logic kicks in
                _defaultStore.DefaultBranch = "not-main";
                var assetsConfiguration = await _defaultStore.ParseConfigurationFile(Path.Join(testFolder, AssetsJson));
                assetsConfiguration.AssetsRepo = "Azure/an-invalid-repo";

                var result = await _defaultStore.GetDefaultBranch(assetsConfiguration);
                Assert.Equal("not-main", result);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public async Task UpdateRecordingJsonUpdatesProperly()
        {
            var fakeSha = "FakeReplacementSha";
            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, basicFolderStructure);
            try
            {
                var configuration = await _defaultStore.ParseConfigurationFile(testFolder);
                await _defaultStore.UpdateAssetsJson(fakeSha, configuration);

                Assert.Equal(fakeSha, configuration.Tag);
                var newConfiguration = await _defaultStore.ParseConfigurationFile(testFolder);
                Assert.Equal(fakeSha, newConfiguration.Tag);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public async Task InitializeAssetsRepoAvoidsRedundantSetupCommands()
        {
            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, basicFolderStructure);
            try
            {
                var configuration = await _defaultStore.ParseConfigurationFile(testFolder);
                var commands = new List<string>();
                var handler = CreateGitCommandRecorder(commands);
                _defaultStore.GitHandler = handler.Object;

                Assert.True(_defaultStore.InitializeAssetsRepo(configuration));

                var clone = Assert.Single(commands.Where(command => command.StartsWith("clone ", StringComparison.Ordinal)));
                Assert.Contains("--filter=blob:none", clone);
                Assert.Contains("-c core.safecrlf=false", clone);
                Assert.Contains("--no-tags", clone);
                Assert.Contains($"--revision=refs/tags/{configuration.Tag}", clone);
                Assert.DoesNotContain("config --local core.safecrlf false", commands);
                Assert.DoesNotContain("sparse-checkout init", commands);
                Assert.DoesNotContain(commands, command => command.StartsWith("fetch ", StringComparison.Ordinal));
                Assert.Contains("-c advice.detachedHead=false checkout --detach --force HEAD --", commands);
                var credentialCommands = clone.Contains("https://x-access-token:", StringComparison.Ordinal) ? 1 : 0;
                var containerCommands = Environment.GetEnvironmentVariable("TEST_PROXY_CONTAINER") == "true" ? 1 : 0;
                Assert.Equal(3 + credentialCommands + containerCommands, commands.Count);

                commands.Clear();
                Assert.True(_defaultStore.InitializeAssetsRepo(configuration, forceInit: true));
                Assert.Contains(commands, command => command.StartsWith("sparse-checkout ", StringComparison.Ordinal));
                Assert.Equal(3 + credentialCommands, commands.Count);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public async Task CheckoutAvoidsRepeatingSparseSetupForTagChanges()
        {
            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, basicFolderStructure);
            try
            {
                var configuration = await _defaultStore.ParseConfigurationFile(testFolder);
                var commands = new List<string>();
                _defaultStore.GitHandler = CreateGitCommandRecorder(commands).Object;
                _defaultStore.InitializeAssetsRepo(configuration);
                var usesToken = commands[0].Contains("https://x-access-token:", StringComparison.Ordinal);
                commands.Clear();

                configuration.Tag = "second-tag";
                _defaultStore.CheckoutRepoAtConfig(configuration);

                Assert.DoesNotContain(commands, command => command.StartsWith("sparse-checkout ", StringComparison.Ordinal));
                Assert.DoesNotContain("checkout .", commands);
                Assert.Contains("clean -xdf", commands);
                Assert.Contains("fetch --no-tags --filter=blob:none origin refs/tags/second-tag:refs/tags/second-tag", commands);
                Assert.Contains("-c advice.detachedHead=false checkout --detach --force refs/tags/second-tag --", commands);
                Assert.Equal(3 + (usesToken ? 2 : 0), commands.Count);

                commands.Clear();
                _defaultStore.CheckoutRepoAtConfig(configuration);
                Assert.Empty(commands);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Theory]
        [InlineData("assets.json", null)]
        [InlineData("sdk/service/assets.json", null)]
        [InlineData("sdk/service/assets.json", "recordings")]
        [InlineData("sdk/service/assets.json", "records with spaces")]
        [InlineData("sdk/service/assets.json", "records[1]")]
        public async Task LocalRestorePreservesRecordingsAndUsesSparseIndex(string assetsJsonPath, string prefix)
        {
            var fixture = await CreateLocalAssetsFixture(assetsJsonPath, prefix);
            try
            {
                await _defaultStore.Restore(fixture.AssetsJsonPath);

                var containerCommands = Environment.GetEnvironmentVariable("TEST_PROXY_CONTAINER") == "true" ? 1 : 0;
                Assert.Equal(3 + containerCommands, fixture.Handler.Commands.Count);
                Assert.Equal("{\"version\":1}", File.ReadAllText(fixture.RecordingPath));
                Assert.False(fixture.Handler.TryRun("show-ref --verify --quiet refs/heads/main", fixture.Configuration.AssetsRepoLocation, out _));
                var wholeRepository = prefix == null && assetsJsonPath == "assets.json";
                Assert.Equal(wholeRepository, File.Exists(Path.Join(fixture.Configuration.AssetsRepoLocation, "unrelated", "recording.json")));
                var sparseIndex = fixture.Handler.TryRun("config --get index.sparse", fixture.Configuration.AssetsRepoLocation, out var sparseResult);
                Assert.Equal(!wholeRepository, sparseIndex && sparseResult.StdOut.Trim() == "true");

                TestHelpers.WriteTestFile("{\"version\":3}", fixture.RecordingPath);
                var newRecording = Path.Join(Path.GetDirectoryName(fixture.RecordingPath), "new-recording.json");
                TestHelpers.WriteTestFile("{\"version\":4}", newRecording);
                fixture.Handler.Commands.Clear();

                await _defaultStore.Restore(fixture.AssetsJsonPath);

                Assert.Empty(fixture.Handler.Commands);
                Assert.Equal("{\"version\":3}", File.ReadAllText(fixture.RecordingPath));
                Assert.True(File.Exists(newRecording));

                var assets = TestHelpers.LoadAssetsFromFile(fixture.AssetsJsonPath);
                assets.Tag = "missing-tag";
                TestHelpers.UpdateAssetsFile(assets, fixture.AssetsJsonPath);
                await Assert.ThrowsAsync<HttpException>(() => _defaultStore.Restore(fixture.AssetsJsonPath));
                Assert.Equal("{\"version\":3}", File.ReadAllText(fixture.RecordingPath));
                Assert.True(File.Exists(newRecording));

                assets.Tag = "recording-v2";
                TestHelpers.UpdateAssetsFile(assets, fixture.AssetsJsonPath);
                fixture.Handler.Commands.Clear();
                await _defaultStore.Restore(fixture.AssetsJsonPath);

                Assert.Equal(3, fixture.Handler.Commands.Count);
                Assert.DoesNotContain(fixture.Handler.Commands, command => command.StartsWith("sparse-checkout ", StringComparison.Ordinal));
                Assert.Equal("{\"version\":2}", File.ReadAllText(fixture.RecordingPath));
                Assert.False(File.Exists(newRecording));
            }
            finally
            {
                DeleteLocalAssetsFixture(fixture.TestFolder, fixture.Configuration);
            }
        }

        [Fact]
        public async Task LocalRestoreChangesSparseDirectoryWithoutLeavingModifiedRecordings()
        {
            var fixture = await CreateLocalAssetsFixture("sdk/service/assets.json", "recordings");
            try
            {
                await _defaultStore.Restore(fixture.AssetsJsonPath);
                TestHelpers.WriteTestFile("{\"version\":3}", fixture.RecordingPath);
                var newRelativePath = Path.Join("new-recordings", "sdk", "service", "SessionRecords", "recording.json");
                TestHelpers.WriteTestFile("{\"version\":4}", Path.Join(fixture.Source, newRelativePath));
                var handler = new GitProcessHandler();
                handler.Run(new[] { "add", "-A" }, fixture.Source);
                handler.Run(new[] { "commit", "--no-gpg-sign", "-m", "Move recording scope" }, fixture.Source);
                handler.Run(new[] { "tag", "--no-sign", "recording-v3" }, fixture.Source);
                var assets = TestHelpers.LoadAssetsFromFile(fixture.AssetsJsonPath);
                assets.AssetsRepoPrefixPath = "new-recordings";
                assets.Tag = "recording-v3";
                TestHelpers.UpdateAssetsFile(assets, fixture.AssetsJsonPath);

                await _defaultStore.Restore(fixture.AssetsJsonPath);

                Assert.False(File.Exists(fixture.RecordingPath));
                Assert.Equal("{\"version\":4}", File.ReadAllText(Path.Join(fixture.Configuration.AssetsRepoLocation, newRelativePath)));
                Assert.True(fixture.Handler.TryRun("status --porcelain", fixture.Configuration.AssetsRepoLocation, out var status));
                Assert.True(string.IsNullOrWhiteSpace(status.StdOut), status.StdOut);
            }
            finally
            {
                DeleteLocalAssetsFixture(fixture.TestFolder, fixture.Configuration);
            }
        }

        [Fact]
        public async Task LocalResetDiscardsChangesWithoutRefetchingCachedTag()
        {
            var console = new Mock<Azure.Sdk.Tools.TestProxy.Console.IConsoleWrapper>();
            console.Setup(wrapper => wrapper.ReadLine()).Returns("y");
            _defaultStore = new GitStore(console.Object);
            var fixture = await CreateLocalAssetsFixture("sdk/service/assets.json", "recordings");
            try
            {
                await _defaultStore.Restore(fixture.AssetsJsonPath);
                TestHelpers.WriteTestFile("{\"version\":3}", fixture.RecordingPath);
                var newRecording = Path.Join(Path.GetDirectoryName(fixture.RecordingPath), "new-recording.json");
                TestHelpers.WriteTestFile("{\"version\":4}", newRecording);
                fixture.Handler.Run(new[] { "add", "--", Path.GetRelativePath(fixture.Configuration.AssetsRepoLocation, fixture.RecordingPath) }, fixture.Configuration);
                fixture.Handler.Commands.Clear();

                await _defaultStore.Reset(fixture.AssetsJsonPath);

                Assert.Equal("{\"version\":1}", File.ReadAllText(fixture.RecordingPath));
                Assert.False(File.Exists(newRecording));
                Assert.Equal(3, fixture.Handler.Commands.Count);
                Assert.DoesNotContain(fixture.Handler.Commands, command => command.StartsWith("fetch ", StringComparison.Ordinal));
                Assert.DoesNotContain("checkout .", fixture.Handler.Commands);
            }
            finally
            {
                DeleteLocalAssetsFixture(fixture.TestFolder, fixture.Configuration);
            }
        }

        [Fact]
        public async Task LocalRestoreUpgradesExistingNonConeClone()
        {
            var fixture = await CreateLocalAssetsFixture("sdk/service/assets.json", "recordings");
            try
            {
                fixture.Handler.Run(new[] { "clone", "--no-checkout", "--filter=tree:0", new Uri(fixture.Source + Path.DirectorySeparatorChar).AbsoluteUri, "." }, fixture.Configuration);
                fixture.Handler.Run(new[] { "sparse-checkout", "set", "--no-cone", "recordings/sdk/service", "eng/", ".gitignore" }, fixture.Configuration);
                fixture.Handler.Run(new[] { "checkout", "--detach", "refs/tags/recording-v1", "--" }, fixture.Configuration);
                TestHelpers.WriteTestFile("{\"version\":3}", fixture.RecordingPath);

                await _defaultStore.Restore(fixture.AssetsJsonPath);

                Assert.Equal("{\"version\":1}", File.ReadAllText(fixture.RecordingPath));
                Assert.True(fixture.Handler.TryRun("config --get index.sparse", fixture.Configuration.AssetsRepoLocation, out var sparseResult));
                Assert.Equal("true", sparseResult.StdOut.Trim());
                Assert.Contains("sparse-checkout set --cone --sparse-index --skip-checks recordings/sdk/service eng", fixture.Handler.Commands);
            }
            finally
            {
                DeleteLocalAssetsFixture(fixture.TestFolder, fixture.Configuration);
            }
        }

        [Fact]
        public async Task LocalRestoreWithoutTagUsesDefaultBranch()
        {
            var fixture = await CreateLocalAssetsFixture("sdk/service/assets.json", "recordings");
            try
            {
                var assets = TestHelpers.LoadAssetsFromFile(fixture.AssetsJsonPath);
                assets.Tag = "missing-tag";
                TestHelpers.UpdateAssetsFile(assets, fixture.AssetsJsonPath);
                var exception = await Assert.ThrowsAsync<HttpException>(() => _defaultStore.Restore(fixture.AssetsJsonPath));
                Assert.Contains("Invocation of \"git clone", exception.Message);
                Assert.Contains("--revision=refs/tags/missing-tag", exception.Message);

                assets.Tag = string.Empty;
                TestHelpers.UpdateAssetsFile(assets, fixture.AssetsJsonPath);
                fixture.Handler.Commands.Clear();
                await _defaultStore.Restore(fixture.AssetsJsonPath);

                Assert.Equal("{\"version\":2}", File.ReadAllText(fixture.RecordingPath));
                var clone = Assert.Single(fixture.Handler.Commands.Where(command => command.StartsWith("clone ", StringComparison.Ordinal)));
                Assert.Contains("--single-branch", clone);
                Assert.DoesNotContain("--revision", clone);
                Assert.DoesNotContain(fixture.Handler.Commands, command => command.StartsWith("fetch ", StringComparison.Ordinal));
            }
            finally
            {
                DeleteLocalAssetsFixture(fixture.TestFolder, fixture.Configuration);
            }
        }

        [Fact]
        public async Task LocalPushFetchesMainAndCreatesBranchWithOneCommand()
        {
            var fixture = await CreateLocalAssetsFixture("sdk/service/assets.json", "recordings");
            try
            {
                await _defaultStore.Restore(fixture.AssetsJsonPath);
                TestHelpers.WriteTestFile("{\"version\":3}", fixture.RecordingPath);
                fixture.Handler.Commands.Clear();

                Assert.Equal(0, await _defaultStore.Push(fixture.AssetsJsonPath));

                Assert.Single(fixture.Handler.Commands.Where(command => command.StartsWith("switch -c ", StringComparison.Ordinal)));
                Assert.DoesNotContain(fixture.Handler.Commands, command => command.StartsWith("branch ", StringComparison.Ordinal));
                Assert.Contains("fetch --no-tags --filter=blob:none origin refs/heads/main:refs/heads/main", fixture.Handler.Commands);
                Assert.Equal("{\"version\":2}", File.ReadAllText(Path.Join(fixture.Configuration.AssetsRepoLocation, "eng", "common", "config.json")));
                Assert.Equal("{\"version\":3}", File.ReadAllText(fixture.RecordingPath));
                var assets = TestHelpers.LoadAssetsFromFile(fixture.AssetsJsonPath);
                Assert.StartsWith("local-recording_", assets.Tag);
                var handler = new GitProcessHandler();
                Assert.True(handler.TryRun($"show-ref --verify refs/tags/{assets.Tag}", fixture.Source, out _));

                fixture.Handler.Commands.Clear();
                Assert.Equal(0, await _defaultStore.Push(fixture.AssetsJsonPath));
                Assert.Equal("status --porcelain", Assert.Single(fixture.Handler.Commands));
            }
            finally
            {
                DeleteLocalAssetsFixture(fixture.TestFolder, fixture.Configuration);
            }
        }

        private async Task<(string TestFolder, string Source, string AssetsJsonPath, GitAssetsConfiguration Configuration, string RecordingPath, LocalAssetsGitHandler Handler)> CreateLocalAssetsFixture(string assetsJsonPath, string prefix)
        {
            var assets = new Assets
            {
                AssetsRepo = "test/local-assets-" + Guid.NewGuid().ToString("N"),
                AssetsRepoPrefixPath = prefix,
                TagPrefix = "local-recording",
                Tag = "recording-v1"
            };
            var testFolder = TestHelpers.DescribeTestFolder(assets, new[] { assetsJsonPath });
            var configuration = await _defaultStore.ParseConfigurationFile(Path.Join(testFolder, assetsJsonPath));
            try
            {
                var source = Path.Join(testFolder, "source");
                Directory.CreateDirectory(source);
                var handler = new GitProcessHandler();
                handler.Run(new[] { "init", "--initial-branch=main" }, source);
                handler.Run(new[] { "config", "user.name", "Test Proxy" }, source);
                handler.Run(new[] { "config", "user.email", "test-proxy@example.invalid" }, source);
                handler.Run(new[] { "config", "uploadpack.allowFilter", "true" }, source);
                handler.Run(new[] { "config", "uploadpack.allowAnySHA1InWant", "true" }, source);
                var recordingRelativePath = Path.Join(prefix ?? string.Empty, Path.GetDirectoryName(assetsJsonPath) ?? string.Empty, "SessionRecords", "recording.json");
                var sourceRecording = Path.Join(source, recordingRelativePath);
                var engFile = Path.Join(source, "eng", "common", "config.json");
                TestHelpers.WriteTestFile("{\"version\":1}", sourceRecording);
                TestHelpers.WriteTestFile("{\"version\":1}", engFile);
                TestHelpers.WriteTestFile("{\"unrelated\":true}", Path.Join(source, "unrelated", "recording.json"));
                TestHelpers.WriteTestFile("*.tmp\n", Path.Join(source, ".gitignore"));
                handler.Run(new[] { "add", "-A" }, source);
                handler.Run(new[] { "commit", "--no-gpg-sign", "-m", "Recording version one" }, source);
                handler.Run(new[] { "tag", "--no-sign", "recording-v1" }, source);
                TestHelpers.WriteTestFile("{\"version\":2}", sourceRecording);
                TestHelpers.WriteTestFile("{\"version\":2}", engFile);
                TestHelpers.WriteTestFile("*.tmp\n*.log\n", Path.Join(source, ".gitignore"));
                handler.Run(new[] { "add", "-A" }, source);
                handler.Run(new[] { "commit", "--no-gpg-sign", "-m", "Recording version two" }, source);
                handler.Run(new[] { "tag", "--no-sign", "recording-v2" }, source);
                var cloneUrl = $"git@github.com:{assets.AssetsRepo}.git";
                handler.Run(new[] { "remote", "set-url", "test", cloneUrl }, testFolder);
                var localHandler = new LocalAssetsGitHandler(cloneUrl, new Uri(source + Path.DirectorySeparatorChar).AbsoluteUri);
                _defaultStore.GitHandler = localHandler;
                return (testFolder, source, Path.Join(testFolder, assetsJsonPath), configuration, Path.Join(configuration.AssetsRepoLocation, recordingRelativePath), localHandler);
            }
            catch
            {
                DeleteLocalAssetsFixture(testFolder, configuration);
                throw;
            }
        }

        private void DeleteLocalAssetsFixture(string testFolder, GitAssetsConfiguration configuration)
        {
            File.Delete(_defaultStore.BreadCrumb.GetBreadCrumbLocation(configuration));
            DirectoryHelper.DeleteGitDirectory(configuration.AssetsRepoLocation);
            DirectoryHelper.DeleteGitDirectory(testFolder);
        }

        private sealed class LocalAssetsGitHandler : GitProcessHandler
        {
            private readonly string _cloneUrl;
            private readonly string _sourceUrl;
            public List<string> Commands = new List<string>();

            public LocalAssetsGitHandler(string cloneUrl, string sourceUrl)
            {
                _cloneUrl = cloneUrl;
                _sourceUrl = sourceUrl;
            }

            public override ProcessStartInfo CreateGitProcessInfo(string workingDirectory)
            {
                var processInfo = base.CreateGitProcessInfo(workingDirectory);
                processInfo.Environment.TryGetValue("GIT_CONFIG_COUNT", out var configuredCount);
                int.TryParse(configuredCount, out var configCount);
                var settings = new[]
                {
                    ($"url.{_sourceUrl}.insteadOf", _cloneUrl),
                    ("protocol.file.allow", "always"),
                    ("user.name", "Test Proxy"),
                    ("user.email", "test-proxy@example.invalid")
                };
                foreach (var (key, value) in settings)
                {
                    processInfo.EnvironmentVariables[$"GIT_CONFIG_KEY_{configCount}"] = key;
                    processInfo.EnvironmentVariables[$"GIT_CONFIG_VALUE_{configCount}"] = value;
                    configCount++;
                }
                processInfo.EnvironmentVariables["GIT_CONFIG_COUNT"] = configCount.ToString();
                return processInfo;
            }

            public override CommandResult Run(string arguments, string workingDirectory)
            {
                Commands.Add(arguments);
                return base.Run(arguments, workingDirectory);
            }

            public override CommandResult Run(IReadOnlyList<string> arguments, string workingDirectory)
            {
                Commands.Add(string.Join(" ", arguments));
                return base.Run(arguments, workingDirectory);
            }

            public override bool TryRun(string arguments, string workingDirectory, out CommandResult result)
            {
                Commands.Add(arguments);
                return base.TryRun(arguments, workingDirectory, out result);
            }
        }

        private static Mock<GitProcessHandler> CreateGitCommandRecorder(List<string> commands)
        {
            CommandResult CaptureCommand(string arguments, GitAssetsConfiguration config)
            {
                commands.Add(arguments);
                if (arguments.StartsWith("clone ", StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(Path.Combine(config.AssetsRepoLocation, ".git"));
                }

                return new CommandResult { ExitCode = 0, StdOut = string.Empty, StdErr = string.Empty };
            }

            var handler = new Mock<GitProcessHandler>();
            handler.Setup(git => git.Run(It.IsAny<string>(), It.IsAny<GitAssetsConfiguration>()))
                .Returns((string arguments, GitAssetsConfiguration config) => CaptureCommand(arguments, config));
            handler.Setup(git => git.Run(It.IsAny<IReadOnlyList<string>>(), It.IsAny<GitAssetsConfiguration>()))
                .Returns((IReadOnlyList<string> arguments, GitAssetsConfiguration config) => CaptureCommand(string.Join(" ", arguments), config));
            return handler;
        }

        [Theory]
        [InlineData("assets.json", null, "")]
        [InlineData("sdk/storage/assets.json", null, "sdk/storage")]
        [InlineData("sdk/storage/assets.json", "python/recordings/", "python/recordings/sdk/storage")]
        [InlineData("sdk/storage/assets.json", "python with spaces/recordings/", "python with spaces/recordings/sdk/storage")]
        [InlineData("sdk/storage/assets.json", "python[1]/recordings/", "python[1]/recordings/sdk/storage")]
        public async Task CheckoutUsesConeSparseIndex(string assetsJsonPath, string prefix, string expectedDirectory)
        {
            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, new[] { assetsJsonPath });
            try
            {
                var configuration = await _defaultStore.ParseConfigurationFile(Path.Combine(testFolder, assetsJsonPath));
                configuration.AssetsRepoPrefixPath = prefix == null ? null : new NormalizedString(prefix);
                var commands = new List<IReadOnlyList<string>>();
                var handler = new Mock<GitProcessHandler>();
                handler.Setup(git => git.Run(It.IsAny<string>(), It.IsAny<GitAssetsConfiguration>()))
                    .Returns(new CommandResult { ExitCode = 0, StdOut = string.Empty, StdErr = string.Empty });
                handler.Setup(git => git.Run(It.IsAny<IReadOnlyList<string>>(), It.IsAny<GitAssetsConfiguration>()))
                    .Callback((IReadOnlyList<string> arguments, GitAssetsConfiguration config) => commands.Add(arguments))
                    .Returns(new CommandResult { ExitCode = 0, StdOut = string.Empty, StdErr = string.Empty });
                _defaultStore.GitHandler = handler.Object;

                _defaultStore.CheckoutRepoAtConfig(configuration, cleanEnabled: false);

                var sparseCommand = Assert.Single(commands.Where(command => command[0] == "sparse-checkout"));
                if (string.IsNullOrEmpty(expectedDirectory))
                {
                    Assert.Equal(new[] { "sparse-checkout", "disable" }, sparseCommand);
                }
                else
                {
                    Assert.Equal(new[] { "sparse-checkout", "set", "--cone", "--sparse-index", "--skip-checks", expectedDirectory, "eng" }, sparseCommand);
                }
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Theory]
        [InlineData("assets.json", false, "./ eng/ .gitignore")]
        [InlineData("assets.json", true, "python/recordings eng/ .gitignore")]
        [InlineData("sdk/storage/assets.json", false, "sdk/storage eng/ .gitignore")]
        [InlineData("sdk/storage/assets.json", true, "python/recordings/sdk/storage eng/ .gitignore")]
        public async Task ResolveCheckPathsResolvesProperly(string assetsJsonPath, bool includePrefix, string expectedResult)
        {
            var expectedPaths = new string[]
            {
                assetsJsonPath
            };

            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, expectedPaths);
            try
            {
                NormalizedString configLocation;

                if (assetsJsonPath == "assets.json")
                {
                    configLocation = new NormalizedString(testFolder);
                }
                else
                {
                    configLocation = new NormalizedString(Path.Join(testFolder, assetsJsonPath));
                }

                var configuration = await _defaultStore.ParseConfigurationFile(configLocation);

                if (!includePrefix)
                {
                    configuration.AssetsRepoPrefixPath = null;
                }

                var result = _defaultStore.ResolveCheckoutPaths(configuration);
                Assert.Equal(expectedResult, result);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public async Task UpdateRecordingJsonNoOpsProperly()
        {
            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, basicFolderStructure);
            try
            {
                var pathToAssets = Path.Combine(testFolder, "assets.json");
                var creationTime = File.GetLastWriteTime(pathToAssets);

                var configuration = await _defaultStore.ParseConfigurationFile(testFolder);
                await _defaultStore.UpdateAssetsJson(configuration.Tag, configuration);
                var postUpdateLastWrite = File.GetLastWriteTime(pathToAssets);

                Assert.Equal(creationTime, postUpdateLastWrite);
                var newConfiguration = await _defaultStore.ParseConfigurationFile(testFolder);
                Assert.Equal(configuration.Tag, newConfiguration.Tag);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact]
        public async Task UpdateRecordingJsonOnlyUpdatesTargetSHA()
        {
            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, basicFolderStructure);
            try
            {
                var fakeSha = "FakeReplacementSha";
                var pathToAssets = Path.Combine(testFolder, "assets.json");
                var contentBeforeUpdate = File.ReadAllText(pathToAssets);
                var configuration = await _defaultStore.ParseConfigurationFile(pathToAssets);
                var originalSHA = configuration.Tag;

                await _defaultStore.UpdateAssetsJson(fakeSha, configuration);

                var newConfiguration = await _defaultStore.ParseConfigurationFile(pathToAssets);
                Assert.NotEqual(originalSHA, newConfiguration.Tag);
                var contentAfterUpdate = File.ReadAllText(pathToAssets);

                Assert.NotEqual(contentBeforeUpdate, contentAfterUpdate);
                Assert.Equal(contentBeforeUpdate.Replace(originalSHA, fakeSha), contentAfterUpdate);
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
              ""AssetsRepoPrefixPath"": ""python/recordings"",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""python/tables"",
              ""Tag"": ""python/tables89f51431""
        }")]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": ""python"",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""python/tables"",
              ""Tag"": ""python/tables4f724f0c""
        }")]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": """",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""python/tables"",
              ""Tag"": ""python/tablesdd6aec01""
        }")]
        [Trait("Category", "Integration")]
        public async Task GetPathResolves(string inputJson)
        {
            var folderStructure = new string[]
            {
                Path.Combine("sdk", "tables", GitStoretests.AssetsJson)
            };

            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure, isPushTest: false);

            try
            {
                var jsonFileLocation = Path.Join(testFolder, "sdk/tables", GitStoretests.AssetsJson);
                var parsedConfiguration = await _defaultStore.ParseConfigurationFile(jsonFileLocation);

                await _defaultStore.Restore(jsonFileLocation);

                var result = await _defaultStore.GetPath(jsonFileLocation);
                await TestHelpers.CheckBreadcrumbAgainstAssetsJsons(new string[] { jsonFileLocation });

                Assert.True(File.Exists(Path.Combine(result, "sdk", "tables", "azure-data-tables", "tests", "recordings", "test_retry.pyTestStorageRetrytest_retry_on_server_error.json")));
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [EnvironmentConditionalSkipFact]
        [Trait("Category", "Integration")]
        public async Task BreadCrumbMaintainsMultipleBreadCrumbs()
        {
            var inputJson = @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets-integration"",
              ""AssetsRepoPrefixPath"": """",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""python/tables"",
              ""Tag"": ""python/tablesdd6aec01""
            }";

            var target1 = Path.Combine("sdk", "tables", GitStoretests.AssetsJson);
            var target2 = Path.Combine("sdk", GitStoretests.AssetsJson);
            var target3 = Path.Combine(GitStoretests.AssetsJson);

            var folderStructure = new string[]
            {
                target1,
                target2,
                target3
            };

            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure, isPushTest: false);

            try
            {
                var assetStore = (await _defaultStore.ParseConfigurationFile(Path.Join(testFolder, target1))).ResolveAssetsStoreLocation();

                var breadCrumbs = new List<string>();

                // run 3 restore operations
                foreach (var assetsJson in folderStructure)
                {
                    var jsonFileLocation = Path.Join(testFolder, assetsJson);
                    var parsedJson = await _defaultStore.ParseConfigurationFile(jsonFileLocation);

                    var breadCrumbFile = Path.Join(assetStore.ToString(), "breadcrumb", $"{parsedJson.AssetRepoShortHash}.breadcrumb");

                    breadCrumbs.Add(breadCrumbFile);

                    await _defaultStore.Restore(jsonFileLocation);
                    TestHelpers.CheckBreadcrumbAgainstAssetsConfig(parsedJson);
                }

                // double verify they are where we expect
                foreach (var crumbFile in breadCrumbs)
                {
                    Assert.True(File.Exists(crumbFile));
                }

                // we have already validated that each tag contains what we expect, just confirm we aren't eliminating lines now.
                Assert.Equal(3, breadCrumbs.Count());
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact(Skip = "Skipping because we don't have an integration test suite working yet.")]
        public async Task GitCallHonorsLocalCredential()
        {
            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, basicFolderStructure);
            try
            {
                var config = await _defaultStore.ParseConfigurationFile(testFolder);

                var workDone = _defaultStore.InitializeAssetsRepo(config);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Fact(Skip = "Skipping due to integration tests not figured out yet.")]
        public async Task GetDefaultBranchWorksWithValidRepo()
        {
            var testFolder = TestHelpers.DescribeTestFolder(DefaultAssets, basicFolderStructure);
            try
            {
                _defaultStore.DefaultBranch = "not-main";
                var assetsConfiguration = await _defaultStore.ParseConfigurationFile(Path.Join(testFolder, AssetsJson));
                var result = await _defaultStore.GetDefaultBranch(assetsConfiguration);

                Assert.Equal("main", result);
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }
    }
}
