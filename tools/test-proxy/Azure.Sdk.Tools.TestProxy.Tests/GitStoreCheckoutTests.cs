using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Console;
using Azure.Sdk.Tools.TestProxy.Store;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class GitStoreCheckoutTests : IDisposable
    {
        private static readonly TimeSpan ConcurrencyTimeout = TimeSpan.FromMinutes(1);
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

        [Theory]
        [InlineData(null)]
        [InlineData("2.45.4")]
        public async Task RestoreUsesBloblessConeCheckoutAndSparseIndex(string gitVersion)
        {
            EmulateGitVersion(gitVersion);
            var config = await CreateConfiguration();
            _git.Commands.Clear();

            await _store.Restore(config.AssetsJsonLocation);

            Assert.DoesNotContain(_git.Commands, command =>
                command.StartsWith("config --local ", StringComparison.Ordinal) ||
                command.StartsWith("update-index ", StringComparison.Ordinal));
            if (_git.SupportsCloneRevision)
            {
                string clone = Assert.Single(_git.Commands.Where(command => command.Contains(" clone ", StringComparison.Ordinal)));
                Assert.Contains("-c core.safecrlf=false", clone);
                Assert.Contains("-c protocol.version=2", clone);
                Assert.Contains("-c index.version=4", clone);
                Assert.Contains("-c core.untrackedCache=true", clone);
            }
            else
            {
                Assert.DoesNotContain(_git.Commands, command => command.Contains(" clone ", StringComparison.Ordinal));
                // The tag is fetched only once, while creating the repository.
                Assert.Equal(
                    "fetch --filter=blob:none origin refs/tags/recordings_v1:refs/tags/recordings_v1",
                    Assert.Single(_git.Commands.Where(command => command.StartsWith("fetch ", StringComparison.Ordinal))));
            }
            AssertOptimized(config);
            Assert.Equal("true", _git.Run("config --get remote.origin.promisor", config).StdOut.Trim());
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

        [Theory]
        [InlineData(null)]
        [InlineData("2.45.4")]
        public async Task RestorePreservesSelectedTagHistoryWithoutShallowCloning(string gitVersion)
        {
            EmulateGitVersion(gitVersion);
            var config = await CreateConfiguration();
            await _store.UpdateAssetsJson("recordings_v2", config);
            await _store.Restore(config.AssetsJsonLocation);

            Assert.Equal("false", _git.Run("rev-parse --is-shallow-repository", config).StdOut.Trim());
            Assert.Equal(
                _git.Run("rev-parse recordings_v1", _remotePath).StdOut.Trim(),
                _git.Run("rev-parse HEAD^", config).StdOut.Trim());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("2.45.4")]
        public async Task RestoreSupportsAnnotatedTags(string gitVersion)
        {
            EmulateGitVersion(gitVersion);
            _git.Run("tag --no-sign --annotate recordings_annotated --message annotated recordings_v1", _remotePath);
            var config = await CreateConfiguration();
            await _store.UpdateAssetsJson("recordings_annotated", config);
            await _store.Restore(config.AssetsJsonLocation);

            Assert.Equal("tag", _git.Run("cat-file -t refs/tags/recordings_annotated", config).StdOut.Trim());
            Assert.Equal("recording version 1", File.ReadAllText(Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "recording.json")));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("2.45.4")]
        public async Task RestoreReportsMissingTagDuringTargetedClone(string gitVersion)
        {
            EmulateGitVersion(gitVersion);
            var config = await CreateConfiguration();
            await _store.UpdateAssetsJson("INVALID_TAG", config);

            var error = await Assert.ThrowsAsync<HttpException>(() => _store.Restore(config.AssetsJsonLocation));

            Assert.Contains("refs/tags/INVALID_TAG", error.Message);
            Assert.Contains("had a non-zero exit code 128", error.Message);
            // Like a failed clone, a failed initialization leaves nothing behind, including a remote URL with a token.
            Assert.Empty(Directory.GetFileSystemEntries(config.AssetsRepoLocation));
            await _store.UpdateAssetsJson("recordings_v1", config);
            await _store.Restore(config.AssetsJsonLocation);
            Assert.Equal("recording version 1", File.ReadAllText(Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "recording.json")));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("2.45.4")]
        public async Task RestoreInitializesAgainAfterInterruptedInitialCheckout(string gitVersion)
        {
            EmulateGitVersion(gitVersion);
            var config = await CreateConfiguration();
            _git.FailCommandOnce = "-c advice.detachedHead=false checkout recordings_v1";
            await Assert.ThrowsAsync<HttpException>(() => _store.Restore(config.AssetsJsonLocation));
            // An interrupted checkout can leave files behind, including ones of a tag that's no longer targeted.
            string recordingPath = Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "recording.json");
            TestHelpers.WriteTestFile("partial recording", recordingPath);
            TestHelpers.WriteTestFile("partial recording", Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "partial.json"));

            await new GitStore { GitHandler = _git }.Restore(config.AssetsJsonLocation);

            AssertOptimized(config);
            Assert.Equal("recording version 1", File.ReadAllText(recordingPath));
            Assert.Empty(_store.DetectPendingChanges(config));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("2.45.4")]
        public async Task RestoreWaitsWhileAnotherProcessInitializesRepository(string gitVersion)
        {
            EmulateGitVersion(gitVersion);
            var config = await CreateConfiguration();
            // Stores with separate git handlers share only the file system, like separate processes.
            var otherGit = new LocalGitProcessHandler(_remotePath, _repositoryName) { GitVersion = _git.GitVersion };
            var otherConsole = new RecordingConsoleWrapper();
            var otherStore = new GitStore(otherConsole) { GitHandler = otherGit };
            string incompleteMarker = Path.Join(config.AssetsRepoLocation, ".git", "test-proxy-initializing");
            _git.PauseBeforeCommand = "-c advice.detachedHead=false checkout recordings_v1";

            var restores = new List<Task> { Task.Run(() => _store.Restore(config.AssetsJsonLocation)) };
            try
            {
                Assert.True(_git.Paused.Wait(ConcurrencyTimeout), "The first restore didn't reach its initial checkout.");
                Assert.True(Directory.Exists(incompleteMarker));
                restores.Add(Task.Run(() => otherStore.Restore(config.AssetsJsonLocation)));
                Assert.True(otherConsole.WaitForLine("Waiting for another Test Proxy operation", ConcurrencyTimeout),
                    "The second restore didn't wait for the first.");
            }
            finally
            {
                _git.Resume.Set();
                await Task.WhenAny(Task.WhenAll(restores), Task.Delay(ConcurrencyTimeout));
            }

            var allRestores = Task.WhenAll(restores);
            Assert.True(allRestores.IsCompleted, "The restores didn't finish.");
            await allRestores;
            Assert.DoesNotContain(otherGit.Commands, command =>
                command.Contains(" clone ", StringComparison.Ordinal) || command.StartsWith("init ", StringComparison.Ordinal));
            Assert.Equal("recording version 1", File.ReadAllText(Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "recording.json")));
            Assert.False(Directory.Exists(incompleteMarker));
            Assert.Empty(_store.DetectPendingChanges(config));
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task RestoreReloadsConfigurationAfterEachRepositoryLock(bool switchRepositories, bool changeWhileWaitingAgain)
        {
            var original = await CreateConfiguration();
            var alternateRepo = _repositoryName + "-alternate";
            var console = new RecordingConsoleWrapper();
            var git = switchRepositories && !changeWhileWaitingAgain ? new LocalGitProcessHandler(_remotePath, alternateRepo) : _git;
            var otherStore = new GitStore(console) { GitHandler = git };

            var originalLock = HoldRepositoryLock(original);
            FileStream alternateLock = null;
            var restore = Task.Run(() => otherStore.Restore(original.AssetsJsonLocation));
            GitAssetsConfiguration expected;
            try
            {
                Assert.True(console.WaitForLine($"finish with \"{original.AssetsRepoLocation}\"", ConcurrencyTimeout));
                expected = await ChangeConfiguration(original, switchRepositories ? alternateRepo : _repositoryName, "recordings_v2");
                if (switchRepositories)
                {
                    alternateLock = HoldRepositoryLock(expected);
                    originalLock.Dispose();

                    Assert.True(console.WaitForLine($"finish with \"{expected.AssetsRepoLocation}\"", ConcurrencyTimeout));
                    if (changeWhileWaitingAgain)
                    {
                        expected = await ChangeConfiguration(original, _repositoryName, "recordings_v2");
                    }
                }
            }
            finally
            {
                originalLock.Dispose();
                alternateLock?.Dispose();
                await restore.WaitAsync(ConcurrencyTimeout);
            }

            Assert.Equal(expected.AssetsRepoLocation.ToString(), await restore);
            Assert.Equal("recording version 2", File.ReadAllText(RecordingPath(expected)));
            Assert.Equal(new BreadcrumbLine(expected).ToString(), Assert.Single(File.ReadAllLines(otherStore.BreadCrumb.GetBreadCrumbLocation(expected))));
            if (switchRepositories && !changeWhileWaitingAgain)
            {
                Assert.False(File.Exists(otherStore.BreadCrumb.GetBreadCrumbLocation(original)));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ResetReconfirmsWhenTheSamePendingRecordingChanges(bool confirmAgain)
        {
            var config = await CreateConfiguration();
            await _store.Restore(config.AssetsJsonLocation);
            string recording = RecordingPath(config);
            File.WriteAllText(recording, "first edit");
            string[] pending = _store.DetectPendingChanges(config);

            var console = new RecordingConsoleWrapper();
            var resetStore = new GitStore(console) { GitHandler = _git };
            int confirmations = 0;
            console.OnReadLine = () =>
            {
                using var peerLock = HoldRepositoryLock(config);
                switch (++confirmations)
                {
                    case 1:
                        File.WriteAllText(recording, "later edit");
                        Assert.Equal(pending, _store.DetectPendingChanges(config));
                        return "y";
                    case 2:
                        Assert.Equal("later edit", File.ReadAllText(recording));
                        return confirmAgain ? "y" : "n";
                    default:
                        throw new InvalidOperationException("Unexpected reset prompt.");
                }
            };

            await resetStore.Reset(config.AssetsJsonLocation);

            Assert.Equal(2, confirmations);
            Assert.Equal(confirmAgain ? "recording version 1" : "later edit", File.ReadAllText(recording));
        }

        [Fact]
        public async Task ResetReconfirmsWhenIgnoredDirectoryContentsChange()
        {
            var config = await CreateConfiguration();
            await _store.Restore(config.AssetsJsonLocation);
            File.WriteAllText(RecordingPath(config), "first edit");
            File.AppendAllText(Path.Join(config.AssetsRepoLocation, ".git", "info", "exclude"), "\nignored-dir/\n");
            string ignoredFile = Path.Join(config.AssetsRepoLocation, "ignored-dir", "nested", "file.txt");
            TestHelpers.WriteTestFile("first", ignoredFile);

            var console = new RecordingConsoleWrapper();
            var resetStore = new GitStore(console) { GitHandler = _git };
            int confirmations = 0;
            console.OnReadLine = () =>
            {
                using var peerLock = HoldRepositoryLock(config);
                switch (++confirmations)
                {
                    case 1:
                        File.WriteAllText(ignoredFile, "later");
                        return "y";
                    case 2:
                        return "n";
                    default:
                        throw new InvalidOperationException("Unexpected reset prompt.");
                }
            };

            await resetStore.Reset(config.AssetsJsonLocation);

            Assert.Equal(2, confirmations);
            Assert.Equal("later", File.ReadAllText(ignoredFile));
        }

        [Fact]
        public async Task ResetReconfirmsAfterTheConfigurationSwitchesRepositories()
        {
            var original = await CreateConfiguration();
            await _store.Restore(original.AssetsJsonLocation);
            File.WriteAllText(RecordingPath(original), "original edit");
            var alternateRepo = _repositoryName + "-alternate";
            var alternateGit = new LocalGitProcessHandler(_remotePath, alternateRepo);
            var peer = new GitStore { GitHandler = alternateGit };
            var console = new RecordingConsoleWrapper();
            var resetStore = new GitStore(console) { GitHandler = _git };

            GitAssetsConfiguration alternate = null;
            int confirmations = 0;
            console.OnReadLine = () =>
            {
                switch (++confirmations)
                {
                    case 1:
                        alternate = ChangeConfiguration(original, alternateRepo, "recordings_v2").GetAwaiter().GetResult();
                        peer.Restore(alternate.AssetsJsonLocation).GetAwaiter().GetResult();
                        File.WriteAllText(RecordingPath(alternate), "alternate edit");
                        resetStore.GitHandler = alternateGit;
                        return "y";
                    case 2:
                        Assert.Equal("alternate edit", File.ReadAllText(RecordingPath(alternate)));
                        return "y";
                    default:
                        throw new InvalidOperationException("Unexpected reset prompt.");
                }
            };

            await Task.Run(() => resetStore.Reset(original.AssetsJsonLocation)).WaitAsync(ConcurrencyTimeout);

            Assert.Equal(2, confirmations);
            Assert.Equal("original edit", File.ReadAllText(RecordingPath(original)));
            Assert.Equal("recording version 2", File.ReadAllText(RecordingPath(alternate)));
            Assert.Equal(new BreadcrumbLine(alternate).ToString(), Assert.Single(File.ReadAllLines(peer.BreadCrumb.GetBreadCrumbLocation(alternate))));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("2.45.4")]
        public async Task RestoreRecoversInterruptedInitializationDespiteStaleBreadcrumb(string gitVersion)
        {
            EmulateGitVersion(gitVersion);
            var config = await CreateConfiguration();
            await _store.Restore(config.AssetsJsonLocation);
            _git.FailCommandOnce = "-c advice.detachedHead=false checkout recordings_v1";
            Assert.Throws<HttpException>(() => _store.InitializeAssetsRepo(config, forceInit: true));

            // The breadcrumb still records the tag as checked out.
            await new GitStore { GitHandler = _git }.Restore(config.AssetsJsonLocation);

            Assert.Equal("recording version 1", File.ReadAllText(Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "recording.json")));
            Assert.Empty(_store.DetectPendingChanges(config));
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("2.45.4", false)]
        [InlineData(null, true)]
        [InlineData("2.45.4", true)]
        public async Task RestoreRecreatesDeletedRepositoryWithoutChangingLanguageRepository(string gitVersion, bool leftoverGitDirectory)
        {
            EmulateGitVersion(gitVersion);
            var config = await CreateConfiguration();
            await _store.Restore(config.AssetsJsonLocation);
            DirectoryHelper.DeleteGitDirectory(config.AssetsRepoLocation);
            if (leftoverGitDirectory)
            {
                // Earlier versions left this behind after running their setup commands in the language repository instead.
                Directory.CreateDirectory(Path.Join(config.AssetsRepoLocation, ".git", "test-proxy-config-v1"));
            }

            // The breadcrumb still records the tag as checked out.
            await new GitStore { GitHandler = _git }.Restore(config.AssetsJsonLocation);

            Assert.False(_git.TryRun("config --local --get-regexp \"^(remote\\.origin\\.partialclonefilter|protocol\\.version|index\\.version|core\\.sparsecheckout)\"",
                _languageRepo, out var languageConfig), languageConfig.StdOut);
            Assert.Equal($"git@github.com:{_repositoryName}.git", _git.Run("config --local --get remote.origin.url", _languageRepo).StdOut.Trim());
            Assert.Equal("recording version 1", File.ReadAllText(Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "recording.json")));
            AssertOptimized(config);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("2.45.4", false)]
        [InlineData(null, true)]
        [InlineData("2.45.4", true)]
        public async Task RestoreRefusesToCreateRepositoryAmongExistingFiles(string gitVersion, bool leftoverGitDirectory)
        {
            EmulateGitVersion(gitVersion);
            var config = await CreateConfiguration();
            // For example, recordings that were kept after deleting only the repository's .git directory.
            string recordingPath = Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "recording.json");
            TestHelpers.WriteTestFile("unpublished recording", recordingPath);
            if (leftoverGitDirectory)
            {
                // Earlier versions could leave this behind when the repository's .git directory was missing.
                Directory.CreateDirectory(Path.Join(config.AssetsRepoLocation, ".git", "test-proxy-config-v1"));
            }
            string[] entries = ListEntries(config.AssetsRepoLocation);

            // Retrying mustn't delete anything either, for example because the failed attempt left a repository behind.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var error = await Assert.ThrowsAsync<HttpException>(() => new GitStore { GitHandler = _git }.Restore(config.AssetsJsonLocation));

                Assert.Contains(config.AssetsRepoLocation.ToString(), error.Message);
                Assert.Equal(entries, ListEntries(config.AssetsRepoLocation));
                Assert.Equal("unpublished recording", File.ReadAllText(recordingPath));
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("2.45.4")]
        public async Task RestorePrefersTagOverBranchWithSameName(string gitVersion)
        {
            EmulateGitVersion(gitVersion);
            _git.Run("branch recordings_v1 recordings_v2", _remotePath);
            var config = await CreateConfiguration();

            await _store.Restore(config.AssetsJsonLocation);

            Assert.Equal("recording version 1", File.ReadAllText(Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "recording.json")));
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
        [InlineData(false)]
        [InlineData(true)]
        public async Task ContainerRestoreRegistersWorktreeBeforeResolvingGitDirectory(bool alreadyConfigured)
        {
            var config = await CreateConfiguration();
            _git.Run($"remote add origin git@github.com:{_repositoryName}.git", _remotePath);
            _git.Run($"worktree add --detach \"{config.AssetsRepoLocation}\" recordings_v1", _remotePath);
            await _store.BreadCrumb.Update(config);
            if (alreadyConfigured)
            {
                await _store.Restore(config.AssetsJsonLocation);
            }

            string recordingPath = Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "recording.json");
            string newRecordingPath = Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "new.json");
            File.WriteAllText(recordingPath, "unpublished recording");
            File.WriteAllText(newRecordingPath, "new recording");
            _git.GlobalConfigPath = Path.Join(_testFolder, "container.gitconfig");
            _git.AssumeDifferentOwner = true;
            Assert.False(_git.TryRun("rev-parse --absolute-git-dir", config.AssetsRepoLocation, out var ownershipError));
            Assert.Equal(128, ownershipError.ExitCode);
            Assert.Contains("dubious ownership", ownershipError.StdErr);

            string tracePath = Path.Join(_testFolder, "container.trace.json");
            var startInfo = _git.CreateGitProcessInfo(_languageRepo);
            startInfo.FileName = "dotnet";
            foreach (string argument in new[]
            {
                "exec",
                "--runtimeconfig", Path.ChangeExtension(typeof(GitStoreCheckoutTests).Assembly.Location, ".runtimeconfig.json"),
                "--depsfile", Path.ChangeExtension(typeof(GitStoreCheckoutTests).Assembly.Location, ".deps.json"),
                typeof(GitStore).Assembly.Location,
                "restore",
                "--assets-json-path", config.AssetsJsonLocation.ToString(),
                "--storage-location", _languageRepo
            })
            {
                startInfo.ArgumentList.Add(argument);
            }
            startInfo.Environment["TEST_PROXY_CONTAINER"] = "true";
            startInfo.Environment["GIT_TRACE2_EVENT"] = tracePath;
            startInfo.Environment.Remove("GIT_TOKEN");

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            string output = await outputTask;
            string error = await errorTask;
            Assert.True(process.ExitCode == 0, $"Container restore failed ({process.ExitCode}).\n{output}\n{error}");

            var commands = new List<string[]>();
            foreach (string line in File.ReadLines(tracePath))
            {
                using var document = JsonDocument.Parse(line);
                var entry = document.RootElement;
                if (entry.GetProperty("event").GetString() == "start" &&
                    !entry.GetProperty("sid").GetString().Contains("/", StringComparison.Ordinal))
                {
                    commands.Add(entry.GetProperty("argv").EnumerateArray().Select(argument => argument.GetString()).ToArray());
                }
            }

            string[] registration = Assert.Single(commands.Where(command => command.Contains("safe.directory")));
            string[] resolution = Assert.Single(commands.Where(command => command.Contains("--absolute-git-dir")));
            Assert.True(commands.IndexOf(registration) < commands.IndexOf(resolution));
            Assert.Equal(config.AssetsRepoLocation.ToString(), registration.Last());
            Assert.DoesNotContain(commands, command => command.Contains("checkout") || command.Contains("clean"));
            string registeredPath = Assert.Single(_git.Run("config --global --get-all safe.directory", config).StdOut
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
            Assert.Equal(config.AssetsRepoLocation.ToString(), registeredPath);
            Assert.Equal("unpublished recording", File.ReadAllText(recordingPath));
            Assert.Equal("new recording", File.ReadAllText(newRecordingPath));
            if (!alreadyConfigured)
            {
                Assert.Contains(commands, command => command.Contains("sparse-checkout"));
            }
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

        [Theory]
        [InlineData(null)]
        [InlineData("2.45.4")]
        public async Task PushPreservesHistoryOutsideSparseCheckout(string gitVersion)
        {
            EmulateGitVersion(gitVersion);
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

        private async Task<GitAssetsConfiguration> ChangeConfiguration(GitAssetsConfiguration original, string repository, string tag)
        {
            await File.WriteAllTextAsync(original.AssetsJsonLocation, JsonSerializer.Serialize(new Assets
            {
                AssetsRepo = repository,
                AssetsRepoPrefixPath = original.AssetsRepoPrefixPath,
                TagPrefix = original.TagPrefix,
                Tag = tag
            }));
            return await _store.ParseConfigurationFile(original.AssetsJsonLocation);
        }

        private static FileStream HoldRepositoryLock(GitAssetsConfiguration config) =>
            new FileStream(Path.Join(config.ResolveAssetsStoreLocation(), $"{config.AssetRepoShortHash}.lock"),
                FileMode.OpenOrCreate, FileAccess.Read, FileShare.None);

        private static string RecordingPath(GitAssetsConfiguration config) =>
            Path.Join(config.AssetsRepoLocation, "python", "sdk", "service", "recording.json");

        private void WriteRemoteFile(string relativePath, string content)
        {
            TestHelpers.WriteTestFile(content, Path.Join(_remotePath, relativePath));
        }

        // null uses the installed git; older versions create repositories without "clone --revision".
        private void EmulateGitVersion(string gitVersion)
        {
            if (gitVersion != null)
            {
                _git.GitVersion = Version.Parse(gitVersion);
            }
        }

        private static string[] ListEntries(string directory)
        {
            return Directory.GetFileSystemEntries(directory, "*", SearchOption.AllDirectories).OrderBy(entry => entry, StringComparer.Ordinal).ToArray();
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
            public string PauseBeforeCommand { get; set; }
            public ManualResetEventSlim Paused { get; } = new ManualResetEventSlim();
            public ManualResetEventSlim Resume { get; } = new ManualResetEventSlim();
            public string GlobalConfigPath { get; set; }
            public bool AssumeDifferentOwner { get; set; }

            public LocalGitProcessHandler(string remotePath, string repositoryName)
            {
                _remoteUrl = new Uri(remotePath + Path.DirectorySeparatorChar).AbsoluteUri;
                _repositoryName = repositoryName;
            }

            public override ProcessStartInfo CreateGitProcessInfo(string workingDirectory)
            {
                var startInfo = base.CreateGitProcessInfo(workingDirectory);
                startInfo.EnvironmentVariables["GIT_CONFIG_GLOBAL"] = GlobalConfigPath ?? (OperatingSystem.IsWindows() ? "NUL" : "/dev/null");
                startInfo.EnvironmentVariables["GIT_CONFIG_NOSYSTEM"] = "1";
                startInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
                if (AssumeDifferentOwner)
                {
                    startInfo.EnvironmentVariables["GIT_TEST_ASSUME_DIFFERENT_OWNER"] = "1";
                }
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
                if (arguments == PauseBeforeCommand)
                {
                    PauseBeforeCommand = null;
                    Paused.Set();
                    Resume.Wait(ConcurrencyTimeout);
                }
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

            public override bool TryRun(string arguments, string workingDirectory, out CommandResult result)
            {
                Commands.Add(arguments);
                return base.TryRun(arguments, workingDirectory, out result);
            }
        }

        private class RecordingConsoleWrapper : IConsoleWrapper
        {
            private readonly ConcurrentQueue<string> _lines = new ConcurrentQueue<string>();
            public Func<string> OnReadLine { get; set; }

            public void Write(string message) => _lines.Enqueue(message);

            public void WriteLine(string message) => _lines.Enqueue(message);

            public string ReadLine() => OnReadLine == null ? throw new NotSupportedException() : OnReadLine();

            public bool WaitForLine(string text, TimeSpan timeout) =>
                SpinWait.SpinUntil(() => _lines.Any(line => line.Contains(text, StringComparison.Ordinal)), timeout);
        }
    }
}
