using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Newtonsoft.Json;
using PipelineGenerator.CommandParserOptions;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace PipelineGenerator
{
    public class PostMergeOperations : IPostMergeOperations
    {
        private readonly Program _generator;
        private readonly PipelineGenerationContext _context;
        private readonly ILogger _logger;

        public PostMergeOperations(Program generator, ILogger logger, string repositoryName)
        {
            _generator = generator;
            _logger = logger;
            var language = repositoryName.Replace("azure-sdk-for-", "", StringComparison.Ordinal);
            _context = new PipelineGenerationContext(logger, PostMergeConstants.Organization, PostMergeConstants.Project,
                "Azure", $"Azure/{repositoryName}", "refs/heads/main", "Hosted", Array.Empty<int>(),
                $"\\{language}", language, false, false, true, false) { Strict = true };
        }

        public async Task ValidateLockAsync(CancellationToken cancellationToken)
        {
            var environments = await GetJsonAsync(
                $"distributedtask/environments?name={PostMergeConstants.LockEnvironment}&api-version=7.1", cancellationToken);
            var values = environments["value"].AsArray();
            if (values.Count != 1)
            {
                throw new InvalidOperationException("The post-merge preparation environment must be provisioned first.");
            }
            var id = values[0]["id"].GetValue<int>();
            var checks = await GetJsonAsync(
                $"pipelines/checks/configurations?resourceType=environment&resourceId={id}&api-version=7.1-preview.1", cancellationToken);
            ValidateLockConfiguration(checks);
        }

        public static void ValidateLockConfiguration(JsonNode checks)
        {
            var enabledLocks = checks["value"].AsArray().Count(check =>
                check["type"]?["id"]?.GetValue<string>() == PostMergeConstants.ExclusiveLockCheckTypeId &&
                check["isDisabled"]?.GetValue<bool>() != true);
            if (enabledLocks != 1)
            {
                throw new InvalidOperationException("The preparation environment must have an enabled Exclusive lock check; refusing unlocked mutations.");
            }
        }

        public async Task<IReadOnlyList<PipelineGenerationResult>> PrepareAsync(PreparePostMergeOptions options,
            DescriptorRequest[] descriptors, string branch, CancellationToken cancellationToken)
        {
            var definitions = new List<PipelineGenerationResult>();
            var profile = PostMergeResourceProfile.For(_context.Prefix);
            var internalGroups = profile.InternalVariableGroups;
            var testGroups = profile.TestVariableGroups;
            var testConnections = profile.TestServiceConnections;
            var internalConnections = profile.InternalServiceConnections;
            var index = 0;
            foreach (var directory in PostMergeRequest.GetDirectories(descriptors))
            {
                var scanPath = Path.Combine(options.SdkDirectory, directory);
                var targets = descriptors.Where(d => d.Path.StartsWith(directory + "/", StringComparison.Ordinal)).ToArray();
                definitions.AddRange(await GenerateAsync("up", targets,
                    internalGroups.Concat(testGroups).Distinct().ToArray(),
                    internalConnections.Concat(testConnections).Distinct().ToArray()));
                if (testGroups.Length > 0)
                {
                    var scanner = new SdkComponentScanner(Microsoft.Extensions.Logging.Abstractions.NullLogger<SdkComponentScanner>.Instance);
                    var tests = scanner.Scan(new DirectoryInfo(scanPath), "tests.yml", options.SdkDirectory)
                        .Where(c => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(c.RelativeYamlPath),
                            @"\Atests(?:\.\w+)?\.yml\z"))
                        .Select(c => new DescriptorRequest { Path = c.RelativeYamlPath.Replace('\\', '/') }).ToArray();
                    if (tests.Length > 0)
                    {
                        await GenerateAsync("tests", tests, testGroups, testConnections);
                    }
                }

                async Task<PipelineGenerationResult[]> GenerateAsync(string convention, DescriptorRequest[] requests,
                    int[] groups, string[] connections)
                {
                    var requestPath = Path.Combine(options.ResultsDirectory, $"descriptor-request-{index}.json");
                    var outputPath = Path.Combine(options.ResultsDirectory, $"definitions-{index++}.json");
                    File.WriteAllText(requestPath, JsonSerializer.Serialize(requests));
                    var result = await _generator.RunAsync(PostMergeConstants.Organization, PostMergeConstants.Project,
                        _context.Prefix, scanPath, "Azure", _context.Repository, $"refs/heads/{branch}", "Hosted",
                        convention, groups, connections, _context.DevOpsPath, false, false, false, false, true, false,
                        cancellationToken, requestPath, outputPath, options.SdkDirectory);
                    if (result != ExitCondition.Success)
                    {
                        throw new InvalidOperationException($"Pipeline generation/authorization failed for '{directory}' ({convention}): {result}.");
                    }
                    return JsonSerializer.Deserialize<PipelineGenerationResult[]>(File.ReadAllText(outputPath));
                }
            }
            return definitions;
        }

        public async Task<PostMergeJournal> ReadJournalAsync(int definitionId, string sha, CancellationToken cancellationToken)
        {
            var client = await _context.GetBuildHttpClientAsync(cancellationToken);
            var properties = await client.GetDefinitionPropertiesAsync(PostMergeConstants.Project, definitionId,
                filter: new[] { PostMergeConstants.JournalProperty(sha) }, cancellationToken: cancellationToken);
            if (!properties.TryGetValue(PostMergeConstants.JournalProperty(sha), out var value))
            {
                return null;
            }
            var journal = JsonSerializer.Deserialize<PostMergeJournal>((string)value);
            if (journal == null || journal.Status is not ("pending" or "submitted") ||
                (journal.RunId.HasValue && journal.RunId <= 0))
            {
                throw new InvalidOperationException($"Invalid durable journal for definition {definitionId}; reconcile before replay.");
            }
            return journal;
        }

        public async Task WriteJournalAsync(int definitionId, string sha, PostMergeJournal journal, CancellationToken cancellationToken)
        {
            var client = await _context.GetBuildHttpClientAsync(cancellationToken);
            var project = await _context.GetProjectReferenceAsync(cancellationToken);
            var patch = new JsonPatchDocument
            {
                new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/" + PostMergeConstants.JournalProperty(sha),
                    Value = JsonSerializer.Serialize(journal)
                }
            };
            await client.UpdateDefinitionPropertiesAsync(patch, project.Id, definitionId, cancellationToken: cancellationToken);
        }

        public async Task<Build> FindBuildAsync(int definitionId, string repository, string sha, string branch, CancellationToken cancellationToken)
        {
            var client = await _context.GetBuildHttpClientAsync(cancellationToken);
            return await FindBuildAsync(client, definitionId, repository, sha, branch, cancellationToken);
        }

        public static async Task<Build> FindBuildAsync(BuildHttpClient client, int definitionId,
            string repository, string sha, string branch, CancellationToken cancellationToken)
        {
            string continuation = null;
            Build match = null;
            do
            {
                var page = await client.GetBuildsAsync2(PostMergeConstants.Project, definitions: new[] { definitionId },
                    branchName: $"refs/heads/{branch}", reasonFilter: BuildReason.IndividualCI,
                    queryOrder: BuildQueryOrder.QueueTimeDescending, top: 100, continuationToken: continuation,
                    cancellationToken: cancellationToken);
                foreach (var build in page.Where(b => PostMergePreparation.Matches(b, definitionId, repository, sha, branch)))
                {
                    if (match != null)
                    {
                        throw new InvalidOperationException($"Multiple matching CI runs already exist for definition {definitionId} at {sha}; reconcile manually.");
                    }
                    match = build;
                }
                continuation = page.ContinuationToken;
            } while (!string.IsNullOrEmpty(continuation));
            return match;
        }

        public async Task<Build> GetBuildAsync(int runId, CancellationToken cancellationToken)
        {
            var client = await _context.GetBuildHttpClientAsync(cancellationToken);
            return await client.GetBuildAsync(PostMergeConstants.Project, runId, cancellationToken: cancellationToken);
        }

        public async Task<Build> QueueBuildAsync(int definitionId, string repository, string sha, string branch, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Queueing definition {DefinitionId} at {Sha}", definitionId, sha);
            // Use the context's raw HttpClient: unlike a retry-enabled SDK handler, this sends exactly one POST attempt.
            var client = await _context.GetRawHttpClient(cancellationToken);
            return await QueueBuildAsync(client, definitionId, repository, sha, branch, cancellationToken);
        }

        public static async Task<Build> QueueBuildAsync(HttpClient client, int definitionId,
            string repository, string sha, string branch, CancellationToken cancellationToken)
        {
            var body = new
            {
                definition = new { id = definitionId },
                sourceBranch = $"refs/heads/{branch}",
                sourceVersion = sha,
                reason = "individualCI",
                properties = new Dictionary<string, string> { [PostMergeConstants.PropertyName] = $"{repository}/{sha}/{definitionId}" }
            };
            using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(
                PostMergeConstants.ApiBaseUrl + "build/builds?api-version=7.1", content, cancellationToken);
            response.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<Build>(await response.Content.ReadAsStringAsync(cancellationToken));
        }

        private async Task<JsonNode> GetJsonAsync(string path, CancellationToken cancellationToken)
        {
            var client = await _context.GetRawHttpClient(cancellationToken);
            using var response = await client.GetAsync(PostMergeConstants.ApiBaseUrl + path, cancellationToken);
            response.EnsureSuccessStatusCode();
            return JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        }

    }
}
