using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using PipelineGenerator.CommandParserOptions;
using Xunit;

namespace PipelineGenerator.Tests
{
    public class PostMergePreparationTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "preparation-tests-" + Guid.NewGuid().ToString("N"));
        private readonly Dictionary<string, string> _environment = new Dictionary<string, string>();
        private readonly FakeOperations _operations = new FakeOperations();
        private readonly PreparePostMergeOptions _options;

        public PostMergePreparationTests()
        {
            foreach (var pair in new Dictionary<string, string>
            {
                ["BUILD_REPOSITORY_NAME"] = "Azure/azure-sdk-tools",
                ["BUILD_SOURCEBRANCH"] = "refs/heads/main",
                ["SYSTEM_TEAMPROJECT"] = "internal",
                ["SYSTEM_COLLECTIONURI"] = "https://dev.azure.com/azure-sdk/"
            })
            {
                _environment[pair.Key] = Environment.GetEnvironmentVariable(pair.Key);
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
            _options = new PreparePostMergeOptions
            {
                RepositoryName = "azure-sdk-for-net",
                MergeCommitSha = new string('a', 40),
                DescriptorPaths = "[{\"path\":\"sdk/a/ci.yml\"}]",
                SdkDirectory = Path.Combine(_directory, "sdk"),
                ResultsDirectory = Path.Combine(_directory, "results")
            };
        }

        [Fact]
        public async Task PreparesBeforeSubmittingUpdatedDefinitionsAndWritesTheExactContract()
        {
            await RunAsync();
            Assert.Equal(new[] { "prepare", "find:123", "queue:123" },
                _operations.Events);
            using var manifest = Results();
            Assert.Equal(new[] { "schemaVersion", "repository", "sourceVersion", "releaseRuns" },
                manifest.RootElement.EnumerateObject().Select(p => p.Name));
            Assert.Equal(1, manifest.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("Azure/azure-sdk-for-net", manifest.RootElement.GetProperty("repository").GetString());
            Assert.Equal(_options.MergeCommitSha, manifest.RootElement.GetProperty("sourceVersion").GetString());
            var run = Assert.Single(manifest.RootElement.GetProperty("releaseRuns").EnumerateArray());
            Assert.Equal("sdk/a/ci.yml", run.GetProperty("descriptorPath").GetString());
            Assert.Equal(123, run.GetProperty("definitionId").GetInt32());
            Assert.Equal(456, run.GetProperty("runId").GetInt32());
            Assert.Equal("https://dev.azure.com/azure-sdk/internal/_build/results?buildId=456", run.GetProperty("url").GetString());
        }

        [Theory]
        [InlineData("prepare")]
        [InlineData("find")]
        public async Task BlocksQueueingWhenAPrerequisiteFails(string failure)
        {
            _operations.FailAt = failure;
            await Assert.ThrowsAsync<InvalidOperationException>(RunAsync);
            Assert.DoesNotContain(_operations.Events, e => e.StartsWith("queue:", StringComparison.Ordinal));
            using var manifest = Results();
            Assert.Empty(manifest.RootElement.GetProperty("releaseRuns").EnumerateArray());
        }

        [Fact]
        public async Task RejectsUntrustedToolsBranchBeforePrivilegedOperations()
        {
            Environment.SetEnvironmentVariable("BUILD_SOURCEBRANCH", "refs/heads/untrusted");
            await Assert.ThrowsAsync<InvalidOperationException>(RunAsync);
            Assert.Empty(_operations.Events);
        }

        [Theory]
        [InlineData("unrelated")]
        [InlineData("duplicate")]
        [InlineData("test")]
        public async Task RejectsBroadenedOrAmbiguousGeneratorResults(string invalid)
        {
            _operations.Definitions = invalid switch
            {
                "unrelated" => new[] { Definition() with { DescriptorPath = "sdk/unrelated/ci.yml" } },
                "duplicate" => new[] { Definition(), Definition() },
                _ => new[] { Definition() with { Convention = "tests", Category = "tests" } }
            };
            await Assert.ThrowsAsync<InvalidOperationException>(RunAsync);
            Assert.DoesNotContain(_operations.Events, e => e.StartsWith("find:", StringComparison.Ordinal));
        }

        [Fact]
        public async Task RecognizesExistingNormalCiAndDoesNotSubmitAgain()
        {
            _operations.Existing[123] = BuildFor(123);
            await RunAsync();
            Assert.DoesNotContain("queue:123", _operations.Events);
            using var manifest = Results();
            Assert.Equal(456, Assert.Single(manifest.RootElement.GetProperty("releaseRuns").EnumerateArray())
                .GetProperty("runId").GetInt32());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReplayAfterLostQueueResponseUsesBuildHistory(bool runWasCreated)
        {
            _operations.FailAt = "queue";
            _operations.QueueCreatesRunBeforeFailing = runWasCreated;
            await Assert.ThrowsAsync<InvalidOperationException>(RunAsync);
            _operations.FailAt = null;
            await RunAsync();
            // A created run is recognized from history; only a run that was never created is queued again.
            Assert.Equal(runWasCreated ? 1 : 2, _operations.Events.Count(e => e == "queue:123"));
            using var manifest = Results();
            Assert.Equal(456, Assert.Single(manifest.RootElement.GetProperty("releaseRuns").EnumerateArray())
                .GetProperty("runId").GetInt32());
        }

        [Fact]
        public async Task KeepsFirstResultWhenLaterSubmissionFails()
        {
            AddSecondTarget();
            _operations.FailDefinition = 124;
            _operations.FailAt = "queue";
            await Assert.ThrowsAsync<InvalidOperationException>(RunAsync);
            using var manifest = Results();
            Assert.Equal(456, Assert.Single(manifest.RootElement.GetProperty("releaseRuns").EnumerateArray())
                .GetProperty("runId").GetInt32());
        }

        [Fact]
        public async Task RecordsMultipleActualQueueResponses()
        {
            AddSecondTarget();
            await RunAsync();
            using var manifest = Results();
            var runs = manifest.RootElement.GetProperty("releaseRuns").EnumerateArray().ToArray();
            Assert.Equal(new[] { 456, 457 }, runs.Select(r => r.GetProperty("runId").GetInt32()));
            Assert.True(File.Exists(Path.Combine(_options.ResultsDirectory, "queue-response-123.json")));
            Assert.True(File.Exists(Path.Combine(_options.ResultsDirectory, "queue-response-124.json")));
        }

        [Fact]
        public async Task ResolvesMissingWebLinkUsingReturnedRunId()
        {
            _operations.OmitQueueWebLink = true;
            await RunAsync();
            Assert.Contains("get:456", _operations.Events);
            using var manifest = Results();
            Assert.DoesNotContain("_apis", manifest.RootElement.GetProperty("releaseRuns")[0].GetProperty("url").GetString());
        }

        [Theory]
        [InlineData("reason")]
        [InlineData("sha")]
        [InlineData("definition")]
        [InlineData("repository")]
        [InlineData("branch")]
        [InlineData("web")]
        public async Task RejectsWrongRunIdentityOrUnsupportedWebUrl(string mismatch)
        {
            var build = BuildFor(123);
            switch (mismatch)
            {
                case "reason": build.Reason = BuildReason.Manual; break;
                case "sha": build.SourceVersion = new string('b', 40); break;
                case "definition": build.Definition.Id = 124; break;
                case "repository": build.Repository.Name = "Azure/another"; break;
                case "branch": build.SourceBranch = "refs/heads/feature"; break;
                case "web":
                    ((ReferenceLink)build.Links.Links["web"]).Href = "https://untrusted.example/run";
                    break;
            }
            _operations.Existing[123] = build;
            await Assert.ThrowsAsync<InvalidOperationException>(RunAsync);
            using var manifest = Results();
            Assert.Empty(manifest.RootElement.GetProperty("releaseRuns").EnumerateArray());
        }

        private Task RunAsync() => new PostMergePreparation(_operations).RunAsync(_options, CancellationToken.None);
        private JsonDocument Results() => JsonDocument.Parse(File.ReadAllText(Path.Combine(_options.ResultsDirectory, "release-runs.json")));

        private void AddSecondTarget()
        {
            _options.DescriptorPaths = "[{\"path\":\"sdk/a/ci.yml\"},{\"path\":\"sdk/b/ci.yml\"}]";
            _operations.Definitions = new[] { Definition(), new PipelineGenerationResult("sdk/b/ci.yml", 124, "up", "unified", "unchanged") };
        }

        private static PipelineGenerationResult Definition() => new PipelineGenerationResult("sdk/a/ci.yml", 123, "up", "unified", "updated");

        private static Build BuildFor(int definitionId, bool includeWebLink = true)
        {
            var runId = definitionId + 333;
            var build = new Build
            {
                Id = runId, Definition = new DefinitionReference { Id = definitionId },
                Repository = new BuildRepository { Name = "Azure/azure-sdk-for-net" },
                SourceVersion = new string('a', 40), SourceBranch = "refs/heads/main", Reason = BuildReason.IndividualCI
            };
            if (includeWebLink)
            {
                build.Links.AddLink("web", $"https://dev.azure.com/azure-sdk/internal/_build/results?buildId={runId}");
            }
            return build;
        }

        public void Dispose()
        {
            foreach (var pair in _environment)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        private sealed class FakeOperations : IPostMergeOperations
        {
            public List<string> Events { get; } = new List<string>();
            public Dictionary<int, Build> Existing { get; } = new Dictionary<int, Build>();
            public IReadOnlyList<PipelineGenerationResult> Definitions { get; set; } = new[] { Definition() };
            public string FailAt { get; set; }
            public int? FailDefinition { get; set; }
            public bool OmitQueueWebLink { get; set; }
            public bool QueueCreatesRunBeforeFailing { get; set; }

            private void Record(string action, int? id = null)
            {
                Events.Add(id == null ? action : $"{action}:{id}");
                if (FailAt == action && (FailDefinition == null || FailDefinition == id))
                {
                    throw new InvalidOperationException($"{action} failed");
                }
            }

            public Task<IReadOnlyList<PipelineGenerationResult>> PrepareAsync(PreparePostMergeOptions options,
                DescriptorRequest[] descriptors, string branch, CancellationToken cancellationToken)
            {
                Record("prepare");
                return Task.FromResult(Definitions);
            }

            public Task<Build> FindBuildAsync(int definitionId, string repository, string sha, string branch, CancellationToken cancellationToken)
            {
                Record("find", definitionId);
                return Task.FromResult(Existing.GetValueOrDefault(definitionId));
            }

            public Task<Build> GetBuildAsync(int runId, CancellationToken cancellationToken)
            {
                Record("get", runId);
                return Task.FromResult(BuildFor(runId - 333));
            }

            public Task<Build> QueueBuildAsync(int definitionId, string repository, string sha, string branch, CancellationToken cancellationToken)
            {
                if (QueueCreatesRunBeforeFailing && FailAt == "queue")
                {
                    // Simulates a lost response: the server created the run but the caller never saw it.
                    Existing[definitionId] = BuildFor(definitionId);
                }
                Record("queue", definitionId);
                var build = BuildFor(definitionId, includeWebLink: !OmitQueueWebLink);
                if (OmitQueueWebLink)
                {
                    build.Url = $"https://dev.azure.com/azure-sdk/internal/_apis/build/builds/{build.Id}";
                }
                return Task.FromResult(build);
            }
        }
    }
}
