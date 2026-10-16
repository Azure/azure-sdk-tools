using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Store;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class GitStoreCheckoutTests : IDisposable
    {
        private readonly string _testFolder = TestHelpers.GetTmpPath();
        private readonly string _remotePath;
        private readonly string _languageRepo;
        private readonly string _repositoryName = $"test-proxy/{Guid.NewGuid():N}";
        private readonly LocalGitProcessHandler _git;
        private readonly GitStore _store;

        public GitStoreCheckoutTests()
        {
            DebugLogger.ConfigureLogger(new LoggerFactory());
            _remotePath = Path.Join(_testFolder, "remote assets");
            _languageRepo = Path.Join(_testFolder, "language repo");
            Directory.CreateDirectory(_remotePath);
            Directory.CreateDirectory(_languageRepo);
            _git = new LocalGitProcessHandler(_remotePath, _repositoryName);
            _store = new GitStore { GitHandler = _git };

            _git.Run("init --initial-branch=main", _remotePath);
            _git.Run("config uploadpack.allowFilter true", _remotePath);
            WriteRemoteFile(".gitignore", "*.ignored\n");
            WriteRemoteFile("README.md", "root file");
            WriteRemoteFile(Path.Join("eng", "common", "tool.txt"), "engineering file");
            WriteRemoteFile(Path.Join("python", "README.md"), "language file");
            WriteRemoteFile(Path.Join("python", "sdk", "README.md"), "ancestor file");
            WriteRemoteFile(Path.Join("python", "sdk", "service", "recording.json"), "recording version 1");
            WriteRemoteFile(Path.Join("python", "sdk", "service", "old.json"), "old recording");
            WriteRemoteFile(Path.Join("python", "sdk", "service with spaces", "recording.json"), "spaced recording");
            WriteRemoteFile(Path.Join("python", "sdk", "other", "recording.json"), "other service");
            WriteRemoteFile(Path.Join("java", "sdk", "service", "recording.json"), "other language");
            _git.Run("add .", _remotePath);
            _git.Run("commit --no-gpg-sign -m first", _remotePath);
            _git.Run("tag --no-sign recordings_v1", _remotePath);

            WriteRemoteFile(Path.Join("python", "sdk", "service", "recording.json"), "recording version 2");
            File.Delete(Path.Join(_remotePath, "python", "sdk", "service", "old.json"));
            _git.Run("add -A .", _remotePath);
            _git.Run("commit --no-gpg-sign -m second", _remotePath);
            _git.Run("tag --no-sign recordings_v2", _remotePath);

            _git.Run("init --initial-branch=main", _languageRepo);
            _git.Run($"remote add origin git@github.com:{_repositoryName}.git", _languageRepo);
        }

        [Fact]
        public async Task RestoreUsesBloblessConeCheckoutAndSparseIndex()
        {
            var config = await CreateConfiguration();
            _git.Commands.Clear();

            await _store.Restore(config.AssetsJsonLocation);

            Assert.DoesNotContain(_git.Commands, command =>
                command.StartsWith("config --local ", StringComparison.Ordinal) ||
                command.StartsWith("update-index ", StringComparison.Ordinal));
            string clone = Assert.Single(_git.Commands.Where(command => command.Contains(" clone ", StringComparison.Ordinal)));
            Assert.Contains("-c core.safecrlf=false", clone);
            Assert.Contains("-c protocol.version=2", clone);
            Assert.Contains("-c index.version=4", clone);
            Assert.Contains("-c core.untrackedCache=true", clone);
            AssertOptimized(config);
            Assert.Equal("false", _git.Run("config --get core.safecrlf", config).StdOut.Trim());
            Assert.Equal("true", _git.Run("config --get core.longpaths", config).StdOut.Trim());
            Assert.Equal("recording version 1", File.ReadAllText(Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "recording.json")));
            Assert.True(File.Exists(Path.Join(config.AssetsRepoLocation, "eng", "common", "tool.txt")));
            Assert.True(File.Exists(Path.Join(config.AssetsRepoLocation, ".gitignore")));
            Assert.True(File.Exists(Path.Join(config.AssetsRepoLocation, "README.md")));
            Assert.True(File.Exists(Path.Join(config.AssetsRepoLocation, "python", "sdk", "README.md")));
            Assert.False(Directory.Exists(Path.Join(config.AssetsRepoLocation, "python", "sdk", "other")));
            Assert.False(Directory.Exists(Path.Join(config.AssetsRepoLocation, "java")));

            string[] indexPaths = _git.Run("ls-files --sparse", config).StdOut
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Contains("java/", indexPaths);
            Assert.Contains("python/sdk/other/", indexPaths);

            string excludedBlob = _git.Run("rev-parse HEAD:java/sdk/service/recording.json", config).StdOut.Trim();
            Assert.False(_git.TryRun($"--no-lazy-fetch cat-file -e {excludedBlob}", config.AssetsRepoLocation, out _));
            string unrelatedCommit = _git.Run("rev-parse recordings_v2", _remotePath).StdOut.Trim();
            Assert.False(_git.TryRun($"--no-lazy-fetch cat-file -e {unrelatedCommit}", config.AssetsRepoLocation, out _));
            Assert.False(_git.TryRun("show-ref --verify --quiet refs/tags/recordings_v2", config.AssetsRepoLocation, out _));
            Assert.Equal("--no-tags", _git.Run("config --get remote.origin.tagOpt", config).StdOut.Trim());

            _git.Commands.Clear();
            await _store.Restore(config.AssetsJsonLocation);
            Assert.Empty(_git.Commands);
        }

        [Fact]
        public async Task RestorePreservesSelectedTagHistoryWithoutShallowCloning()
        {
            var config = await CreateConfiguration();
            await _store.UpdateAssetsJson("recordings_v2", config);
            await _store.Restore(config.AssetsJsonLocation);

            Assert.Equal("false", _git.Run("rev-parse --is-shallow-repository", config).StdOut.Trim());
            Assert.Equal(
                _git.Run("rev-parse recordings_v1", _remotePath).StdOut.Trim(),
                _git.Run("rev-parse HEAD^", config).StdOut.Trim());
        }

        [Fact]
        public async Task RestoreSupportsAnnotatedTags()
        {
            _git.Run("tag --no-sign --annotate recordings_annotated --message annotated recordings_v1", _remotePath);
            var config = await CreateConfiguration();
            await _store.UpdateAssetsJson("recordings_annotated", config);
            await _store.Restore(config.AssetsJsonLocation);

            Assert.Equal("tag", _git.Run("cat-file -t refs/tags/recordings_annotated", config).StdOut.Trim());
            Assert.Equal("recording version 1", File.ReadAllText(Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "recording.json")));
        }

        [Fact]
        public async Task RestoreReportsMissingTagDuringTargetedClone()
        {
            var config = await CreateConfiguration();
            await _store.UpdateAssetsJson("INVALID_TAG", config);

            var error = await Assert.ThrowsAsync<HttpException>(() => _store.Restore(config.AssetsJsonLocation));

            Assert.Contains("--revision=refs/tags/INVALID_TAG", error.Message);
            Assert.Contains("had a non-zero exit code 128", error.Message);
        }

        [Fact]
        public async Task NewStoreReusesConfigurationWithoutGitCommandsOrDiscardingChanges()
        {
            var config = await CreateConfiguration();
            await _store.Restore(config.AssetsJsonLocation);
            string recordingPath = Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "recording.json");
            File.WriteAllText(recordingPath, "unpublished recording");
            var reopenedStore = new GitStore { GitHandler = _git };
            _git.Commands.Clear();

            await reopenedStore.Restore(config.AssetsJsonLocation);

            Assert.Empty(_git.Commands);
            Assert.Equal("unpublished recording", File.ReadAllText(recordingPath));
        }

        [Fact]
        public async Task FailedConfigurationIsRetriedInsteadOfMarkedComplete()
        {
            var config = await CreateConfiguration();
            _git.Run($"clone -c core.longpaths=true --no-checkout --filter=tree:0 git@github.com:{_repositoryName}.git .", config);
            _git.Run("checkout recordings_v1", config);
            await _store.BreadCrumb.Update(config);
            _git.FailCommandOnce = "update-index --index-version=4";
            await Assert.ThrowsAsync<HttpException>(() => _store.Restore(config.AssetsJsonLocation));
            _git.Commands.Clear();

            await _store.Restore(config.AssetsJsonLocation);

            Assert.Contains("config --local index.version 4", _git.Commands);
            Assert.Contains("update-index --index-version=4", _git.Commands);
            AssertOptimized(config);
            var reopenedStore = new GitStore { GitHandler = _git };
            _git.Commands.Clear();
            await reopenedStore.Restore(config.AssetsJsonLocation);
            Assert.Empty(_git.Commands);
        }

        [Fact]
        public async Task RestorePersistsConfigurationInWorktreeGitDirectory()
        {
            var config = await CreateConfiguration();
            _git.Run($"remote add origin git@github.com:{_repositoryName}.git", _remotePath);
            _git.Run($"worktree add --detach \"{config.AssetsRepoLocation}\" recordings_v1", _remotePath);
            await _store.BreadCrumb.Update(config);
            await _store.Restore(config.AssetsJsonLocation);
            var reopenedStore = new GitStore { GitHandler = _git };
            _git.Commands.Clear();

            await reopenedStore.Restore(config.AssetsJsonLocation);

            Assert.Equal(new[] { "rev-parse --absolute-git-dir" }, _git.Commands);
            Assert.Equal("recording version 1", File.ReadAllText(Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "recording.json")));
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("python", false)]
        public async Task RestoreSupportsRootAssetsJson(string prefix, bool includesOtherLanguages)
        {
            var config = await CreateConfiguration("assets.json", prefix);
            await _store.Restore(config.AssetsJsonLocation);

            AssertOptimized(config, sparseIndex: !includesOtherLanguages);
            Assert.True(File.Exists(Path.Join(config.AssetsRepoLocation, "python", "sdk", "other", "recording.json")));
            Assert.Equal(includesOtherLanguages, Directory.Exists(Path.Join(config.AssetsRepoLocation, "java")));
        }

        [Fact]
        public async Task RestoreSupportsRecordingPathsWithSpaces()
        {
            var config = await CreateConfiguration(Path.Join("sdk", "service with spaces", "assets.json"));
            await _store.Restore(config.AssetsJsonLocation);

            Assert.Equal("spaced recording", File.ReadAllText(Path.Join(config.AssetsRepoLocation, "python", "sdk", "service with spaces", "recording.json")));
            Assert.False(Directory.Exists(Path.Join(config.AssetsRepoLocation, "python", "sdk", "service")));
        }

        [Fact]
        public async Task RestoreSupportsNewRecordingDirectories()
        {
            var config = await CreateConfiguration(Path.Join("sdk", "new service", "assets.json"));
            config.Tag = "";
            await _store.UpdateAssetsJson("", config);
            await _store.Restore(config.AssetsJsonLocation);

            AssertOptimized(config);
            Assert.False(Directory.Exists(Path.Join(config.AssetsRepoLocation, "python", "sdk", "new service")));
            Assert.True(File.Exists(Path.Join(config.AssetsRepoLocation, "eng", "common", "tool.txt")));
            TestHelpers.WriteTestFile("new recording", Path.Join(config.AssetsRepoLocation, "python", "sdk", "new service", "recording.json"));
            Assert.Equal(0, await _store.Push(config.AssetsJsonLocation));
        }

        [Fact]
        public async Task RestoreMigratesCachedCheckoutWithoutDiscardingRecordings()
        {
            var config = await CreateConfiguration();
            _git.Run($"clone -c core.longpaths=true --no-checkout --filter=tree:0 git@github.com:{_repositoryName}.git .", config);
            _git.Run("sparse-checkout init", config);
            _git.Run("sparse-checkout set --no-cone python/sdk/service eng/ .gitignore", config);
            _git.Run("checkout recordings_v1", config);
            _git.Run("update-index --index-version=2", config);
            await _store.BreadCrumb.Update(config);

            string recordingPath = Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "recording.json");
            File.WriteAllText(recordingPath, "staged recording");
            _git.Run("add python/sdk/service/recording.json", config);
            File.WriteAllText(recordingPath, "unstaged recording");
            File.Delete(Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "old.json"));
            TestHelpers.WriteTestFile("new recording", Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "new.json"));
            TestHelpers.WriteTestFile("ignored recording", Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "keep.ignored"));
            string pendingChanges = _git.Run("status --porcelain", config).StdOut;
            _git.Commands.Clear();

            await _store.Restore(config.AssetsJsonLocation);

            AssertOptimized(config);
            Assert.Equal("unstaged recording", File.ReadAllText(recordingPath));
            Assert.Equal("staged recording", _git.Run("show :python/sdk/service/recording.json", config).StdOut.Trim());
            Assert.False(File.Exists(Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "old.json")));
            Assert.Equal("new recording", File.ReadAllText(Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "new.json")));
            Assert.Equal("ignored recording", File.ReadAllText(Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "keep.ignored")));
            Assert.Equal(pendingChanges, _git.Run("status --porcelain", config).StdOut);
            Assert.DoesNotContain(_git.Commands, command => command.StartsWith("checkout ") || command.Contains(" checkout ") || command.StartsWith("clean "));
        }

        [Fact]
        public async Task RestoreSwitchesTagsAndDiscardsChangesOnlyWhenTagChanges()
        {
            var config = await CreateConfiguration();
            await _store.Restore(config.AssetsJsonLocation);
            string recordingPath = Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "recording.json");
            File.WriteAllText(recordingPath, "local recording");
            string untrackedPath = Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "new.json");
            File.WriteAllText(untrackedPath, "new recording");

            await _store.Restore(config.AssetsJsonLocation);
            Assert.Equal("local recording", File.ReadAllText(recordingPath));
            Assert.True(File.Exists(untrackedPath));

            await _store.UpdateAssetsJson("recordings_v2", config);
            await _store.Restore(config.AssetsJsonLocation);

            Assert.Equal("recording version 2", File.ReadAllText(recordingPath));
            Assert.False(File.Exists(untrackedPath));
            Assert.False(File.Exists(Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "old.json")));
            AssertOptimized(config);
        }

        [Fact]
        public async Task PushPreservesHistoryOutsideSparseCheckout()
        {
            var config = await CreateConfiguration();
            await _store.Restore(config.AssetsJsonLocation);
            File.WriteAllText(Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "recording.json"), "updated recording");
            Assert.False(_git.TryRun("show-ref --verify --quiet refs/heads/main", config.AssetsRepoLocation, out _));
            _git.Commands.Clear();

            Assert.Equal(0, await _store.Push(config.AssetsJsonLocation));

            Assert.Contains("fetch origin refs/heads/main:refs/heads/main", _git.Commands);
            var pushedConfig = await _store.ParseConfigurationFile(config.AssetsJsonLocation);
            Assert.NotEqual("recordings_v1", pushedConfig.Tag);
            Assert.Equal("updated recording", _git.Run($"show {pushedConfig.Tag}:python/sdk/service/recording.json", _remotePath).StdOut.Trim());
            Assert.Equal("other language", _git.Run($"show {pushedConfig.Tag}:java/sdk/service/recording.json", _remotePath).StdOut.Trim());
            AssertOptimized(pushedConfig);
        }

        [Fact]
        public async Task FailedMainFetchPreservesRecordingsForPushRetry()
        {
            var config = await CreateConfiguration();
            await _store.Restore(config.AssetsJsonLocation);
            string recordingPath = Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "recording.json");
            File.WriteAllText(recordingPath, "updated recording");
            _git.FailCommandOnce = "fetch origin refs/heads/main:refs/heads/main";

            await Assert.ThrowsAsync<HttpException>(() => _store.Push(config.AssetsJsonLocation));

            Assert.Equal("updated recording", File.ReadAllText(recordingPath));
            Assert.Equal(0, await _store.Push(config.AssetsJsonLocation));
        }

        [Fact]
        public async Task ForcedInitializationReconfiguresRepository()
        {
            var config = await CreateConfiguration();
            await _store.Restore(config.AssetsJsonLocation);

            Assert.True(_store.InitializeAssetsRepo(config, forceInit: true));

            AssertOptimized(config);
            Assert.Equal("recording version 1", File.ReadAllText(Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "recording.json")));
        }

        private async Task<GitAssetsConfiguration> CreateConfiguration(string relativePath = "sdk/service/assets.json", string prefix = "python")
        {
            string assetsJsonPath = Path.Join(_languageRepo, relativePath);
            TestHelpers.WriteTestFile(JsonSerializer.Serialize(new Assets
            {
                AssetsRepo = _repositoryName,
                AssetsRepoPrefixPath = prefix,
                TagPrefix = "recordings",
                Tag = "recordings_v1"
            }), assetsJsonPath);

            return await _store.ParseConfigurationFile(assetsJsonPath);
        }

        private void WriteRemoteFile(string relativePath, string content)
        {
            TestHelpers.WriteTestFile(content, Path.Join(_remotePath, relativePath));
        }

        private void AssertOptimized(GitAssetsConfiguration config, bool sparseIndex = true)
        {
            Assert.Equal("blob:none", _git.Run("config --get remote.origin.partialCloneFilter", config).StdOut.Trim());
            Assert.Equal("2", _git.Run("config --get protocol.version", config).StdOut.Trim());
            Assert.Equal("4", _git.Run("config --get index.version", config).StdOut.Trim());
            Assert.Equal("true", _git.Run("config --get core.untrackedCache", config).StdOut.Trim());
            if (sparseIndex)
            {
                Assert.Equal("true", _git.Run("config --get core.sparseCheckoutCone", config).StdOut.Trim());
                Assert.Equal("true", _git.Run("config --get index.sparse", config).StdOut.Trim());
            }
            else
            {
                Assert.Equal("false", _git.Run("config --get core.sparseCheckout", config).StdOut.Trim());
            }
            byte[] index = File.ReadAllBytes(Path.Join(config.AssetsRepoLocation, ".git", "index"));
            Assert.Equal(new byte[] { 0, 0, 0, 4 }, index.Skip(4).Take(4).ToArray());
            Assert.Contains(index.TakeLast(20), value => value != 0);
        }

        public void Dispose()
        {
            DirectoryHelper.DeleteGitDirectory(_testFolder);
        }

        private class LocalGitProcessHandler : GitProcessHandler
        {
            private readonly string _remoteUrl;
            private readonly string _repositoryName;

            public List<string> Commands { get; } = new List<string>();
            public string FailCommandOnce { get; set; }

            public LocalGitProcessHandler(string remotePath, string repositoryName)
            {
                _remoteUrl = new Uri(remotePath + Path.DirectorySeparatorChar).AbsoluteUri;
                _repositoryName = repositoryName;
            }

            public override ProcessStartInfo CreateGitProcessInfo(string workingDirectory)
            {
                var startInfo = base.CreateGitProcessInfo(workingDirectory);
                startInfo.EnvironmentVariables["GIT_CONFIG_GLOBAL"] = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";
                startInfo.EnvironmentVariables["GIT_CONFIG_NOSYSTEM"] = "1";
                startInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
                startInfo.EnvironmentVariables["GIT_CONFIG_COUNT"] = "4";
                startInfo.EnvironmentVariables["GIT_CONFIG_KEY_0"] = $"url.{_remoteUrl}.insteadOf";
                startInfo.EnvironmentVariables["GIT_CONFIG_VALUE_0"] = $"git@github.com:{_repositoryName}.git";
                startInfo.EnvironmentVariables["GIT_CONFIG_KEY_1"] = $"url.{_remoteUrl}.insteadOf";
                startInfo.EnvironmentVariables["GIT_CONFIG_VALUE_1"] = $"https://github.com/{_repositoryName}";
                startInfo.EnvironmentVariables["GIT_CONFIG_KEY_2"] = "user.name";
                startInfo.EnvironmentVariables["GIT_CONFIG_VALUE_2"] = "Test Proxy Tests";
                startInfo.EnvironmentVariables["GIT_CONFIG_KEY_3"] = "user.email";
                startInfo.EnvironmentVariables["GIT_CONFIG_VALUE_3"] = "test-proxy@example.com";
                return startInfo;
            }

            public override CommandResult Run(string arguments, string workingDirectory)
            {
                Commands.Add(arguments);
                if (arguments == FailCommandOnce)
                {
                    FailCommandOnce = null;
                    throw new GitProcessException(new CommandResult
                    {
                        Arguments = arguments,
                        ExitCode = 1,
                        StdErr = "Injected Git failure."
                    });
                }
                return base.Run(arguments, workingDirectory);
            }
        }
    }
}
