using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using PipelineGenerator.CommandParserOptions;
using Xunit;

namespace PipelineGenerator.Tests
{
    public class PostMergeOperationsTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "preparation-operations-" + Guid.NewGuid().ToString("N"));

        [Fact]
        public async Task QueueUsesOnePostWithExactShaCiReasonAndStableCorrelation()
        {
            using var handler = new QueueHandler();
            using var client = new HttpClient(handler);
            var sha = new string('a', 40);
            var build = await PostMergeOperations.QueueBuildAsync(client, 123, "Azure/azure-sdk-for-net", sha, "main", CancellationToken.None);
            Assert.Equal(1, handler.Attempts);
            Assert.Equal(456, build.Id);
            Assert.Equal(456, PostMergePreparation.GetRunResult(build,
                new PipelineGenerationResult("sdk/a/ci.yml", 123, "up", "unified", "updated"),
                "Azure/azure-sdk-for-net", sha, "main").RunId);
            using var body = JsonDocument.Parse(handler.Body);
            Assert.Equal(123, body.RootElement.GetProperty("definition").GetProperty("id").GetInt32());
            Assert.Equal(sha, body.RootElement.GetProperty("sourceVersion").GetString());
            Assert.Equal("refs/heads/main", body.RootElement.GetProperty("sourceBranch").GetString());
            Assert.Equal("individualCI", body.RootElement.GetProperty("reason").GetString());
            Assert.Equal($"Azure/azure-sdk-for-net/{sha}/123",
                body.RootElement.GetProperty("properties").GetProperty("postMergePreparation").GetString());
            Assert.False(body.RootElement.TryGetProperty("templateParameters", out _));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DoesNotRetryRejectedOrLostQueueResponses(bool lostResponse)
        {
            using var handler = new QueueHandler { Fail = true, LoseResponse = lostResponse };
            using var client = new HttpClient(handler);
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                PostMergeOperations.QueueBuildAsync(client, 123, "Azure/azure-sdk-for-net", new string('a', 40), "main", CancellationToken.None));
            Assert.Equal(1, handler.Attempts);
        }

        [Fact]
        public async Task DeduplicatesScansAndPreparesOnlyExactReleaseTargetsAndApplicableTests()
        {
            var options = Setup();
            var generator = new RecordingGenerator();
            var operations = new PostMergeOperations(generator, NullLogger.Instance, options.RepositoryName);
            var targets = new[] { "sdk/a/ci.yml", "sdk/a/nested/ci.preview.yml" }
                .Select(p => new DescriptorRequest { Path = p }).ToArray();
            var definitions = await operations.PrepareAsync(options, targets, "main", CancellationToken.None);
            Assert.Equal(targets.Select(t => t.Path), definitions.Select(d => d.DescriptorPath));
            Assert.Equal(new[] { "up", "tests" }, generator.Calls.Select(c => c.Convention));
            var unified = generator.Calls[0];
            Assert.Equal(new[] { 13, 58, 93, 64 }, unified.Groups);
            Assert.Contains("Azure SDK Symbols Publishing", unified.Connections);
            Assert.Contains("Nuget.org", unified.Connections);
            Assert.All(generator.Calls, c => Assert.Equal("refs/heads/main", c.Branch));
            Assert.DoesNotContain("sdk/a/ci.unrelated.yml", unified.Requests.Select(r => r.Path));
            Assert.Single(generator.Calls[1].Requests);
            // One diagnostic file lists every prepared definition, release and test.
            var written = JsonSerializer.Deserialize<PipelineGenerationResult[]>(
                File.ReadAllText(Path.Combine(options.ResultsDirectory, "definitions.json")));
            Assert.Equal(new[] { "up", "up", "tests" }, written.Select(d => d.Convention));
            Assert.Equal(new[] { "definitions.json" }, Directory.GetFiles(options.ResultsDirectory).Select(Path.GetFileName));
        }

        [Fact]
        public async Task TestPreparationFailureDoesNotReturnReleaseDefinitions()
        {
            var options = Setup();
            var operations = new PostMergeOperations(new RecordingGenerator { FailConvention = "tests" },
                NullLogger.Instance, options.RepositoryName);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => operations.PrepareAsync(options,
                new[] { new DescriptorRequest { Path = "sdk/a/ci.yml" } }, "main", CancellationToken.None));
            Assert.Contains("generation/authorization failed", error.Message);
        }

        [Fact]
        public async Task EmptyTestScopeIsNotAnErrorAndGoDoesNotAddTestDefinitions()
        {
            var options = Setup();
            options.RepositoryName = "azure-sdk-for-go";
            var generator = new RecordingGenerator();
            var operations = new PostMergeOperations(generator, NullLogger.Instance, options.RepositoryName);
            await operations.PrepareAsync(options, new[] { new DescriptorRequest { Path = "sdk/a/ci.yml" } }, "main", CancellationToken.None);
            Assert.Single(generator.Calls);
            Assert.Equal(new[] { 58, 93, 64 }, generator.Calls[0].Groups);
        }

        [Fact]
        public async Task LooksAcrossPagesForMatchingCiAndRejectsCrossPageDuplicates()
        {
            using var client = new PagedBuildClient();
            var build = await PostMergeOperations.FindBuildAsync(client, 123, "Azure/azure-sdk-for-net",
                new string('a', 40), "main", CancellationToken.None);
            Assert.Equal(456, build.Id);
            Assert.Equal(new[] { null, "page-2" }, client.Continuations);
            client.Duplicate = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => PostMergeOperations.FindBuildAsync(client,
                123, "Azure/azure-sdk-for-net", new string('a', 40), "main", CancellationToken.None));
        }

        private PreparePostMergeOptions Setup()
        {
            Directory.CreateDirectory(Path.Combine(_directory, "sdk", "a", "nested"));
            File.WriteAllText(Path.Combine(_directory, "sdk", "a", "ci.yml"), "");
            File.WriteAllText(Path.Combine(_directory, "sdk", "a", "ci.unrelated.yml"), "");
            File.WriteAllText(Path.Combine(_directory, "sdk", "a", "tests.weekly.yml"), "");
            Directory.CreateDirectory(Path.Combine(_directory, "results"));
            return new PreparePostMergeOptions
            {
                RepositoryName = "azure-sdk-for-net", SdkDirectory = _directory,
                ResultsDirectory = Path.Combine(_directory, "results")
            };
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        private sealed class QueueHandler : HttpMessageHandler
        {
            public bool Fail { get; set; }
            public bool LoseResponse { get; set; }
            public int Attempts { get; private set; }
            public string Body { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Attempts++;
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("https://dev.azure.com/azure-sdk/internal/_apis/build/builds?api-version=7.1", request.RequestUri.AbsoluteUri);
                Body = await request.Content.ReadAsStringAsync(cancellationToken);
                if (LoseResponse)
                {
                    throw new HttpRequestException("Response lost");
                }
                return new HttpResponseMessage(Fail ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"id\":456,\"definition\":{\"id\":123},\"repository\":{\"name\":\"Azure/azure-sdk-for-net\"}," +
                        "\"sourceBranch\":\"refs/heads/main\",\"sourceVersion\":\"" + new string('a', 40) +
                        "\",\"reason\":\"individualCI\",\"_links\":{\"web\":{\"href\":\"https://dev.azure.com/azure-sdk/internal/_build/results?buildId=456\"}}}")
                };
            }
        }

        private sealed class PagedBuildClient : BuildHttpClient
        {
            public bool Duplicate { get; set; }
            public List<string> Continuations { get; } = new List<string>();

            public PagedBuildClient() : base(new Uri("https://dev.azure.com/azure-sdk"), new QueueHandler(), true) { }

            public override Task<IPagedList<Build>> GetBuildsAsync2(string project, IEnumerable<int> definitions = null,
                IEnumerable<int> queues = null, string buildNumber = null, DateTime? minFinishTime = null,
                DateTime? maxFinishTime = null, string requestedFor = null, BuildReason? reasonFilter = null,
                BuildStatus? statusFilter = null, BuildResult? resultFilter = null, IEnumerable<string> tagFilters = null,
                IEnumerable<string> properties = null, int? top = null, string continuationToken = null,
                int? maxBuildsPerDefinition = null, QueryDeletedOption? deletedFilter = null, BuildQueryOrder? queryOrder = null,
                string branchName = null, IEnumerable<int> buildIds = null, object userState = null,
                CancellationToken cancellationToken = default)
            {
                Assert.Equal("internal", project);
                Assert.Equal(123, Assert.Single(definitions));
                Assert.Equal("refs/heads/main", branchName);
                Assert.Equal(BuildReason.IndividualCI, reasonFilter);
                Continuations.Add(continuationToken);
                var match = new Build
                {
                    Id = 456, Definition = new DefinitionReference { Id = 123 },
                    Repository = new BuildRepository { Name = "Azure/azure-sdk-for-net" },
                    SourceVersion = new string('a', 40), SourceBranch = "refs/heads/main", Reason = BuildReason.IndividualCI
                };
                IPagedList<Build> result = continuationToken == null
                    ? new PagedList<Build>(Duplicate ? new[] { match } : Array.Empty<Build>(), "page-2")
                    : new PagedList<Build>(new[] { match }, null);
                return Task.FromResult(result);
            }
        }

        private sealed class RecordingGenerator : Program
        {
            public List<(string Convention, string Branch, int[] Groups, string[] Connections, DescriptorRequest[] Requests)> Calls { get; } =
                new List<(string, string, int[], string[], DescriptorRequest[])>();
            public string FailConvention { get; set; }

            public RecordingGenerator() : base(null, NullLogger<Program>.Instance) { }

            public override Task<ExitCondition> RunAsync(string organization, string project, string prefix, string path,
                string endpoint, string repository, string branch, string agentPool, string convention, int[] variableGroups,
                IEnumerable<string> serviceConnections, string devOpsPath, bool whatIf, bool open, bool destroy,
                bool noSchedule, bool setManagedVariables, bool overwriteTriggers, CancellationToken cancellationToken,
                IReadOnlyList<DescriptorRequest> requests = null, List<PipelineGenerationResult> results = null,
                string repositoryRoot = null)
            {
                Assert.False(whatIf || destroy || overwriteTriggers);
                Assert.True(setManagedVariables);
                Assert.NotNull(repositoryRoot);
                Assert.StartsWith(repositoryRoot, path, StringComparison.Ordinal);
                Assert.False(Directory.Exists(Path.Combine(repositoryRoot, ".git")));
                Calls.Add((convention, branch, variableGroups, serviceConnections.ToArray(), requests.ToArray()));
                if (convention == FailConvention)
                {
                    return Task.FromResult(ExitCondition.Exception);
                }
                results.AddRange(requests.Select((r, i) =>
                    new PipelineGenerationResult(r.Path, 123 + i, convention, convention == "up" ? "unified" : "tests", "updated")));
                return Task.FromResult(ExitCondition.Success);
            }
        }
    }
}
