using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
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

        public async Task<IReadOnlyList<PipelineGenerationResult>> PrepareAsync(PreparePostMergeOptions options,
            DescriptorRequest[] descriptors, string branch, CancellationToken cancellationToken)
        {
            var definitions = new List<PipelineGenerationResult>();
            var allDefinitions = new List<PipelineGenerationResult>();
            var profile = PostMergeResourceProfile.For(_context.Prefix);
            var internalGroups = profile.InternalVariableGroups;
            var testGroups = profile.TestVariableGroups;
            var testConnections = profile.TestServiceConnections;
            var internalConnections = profile.InternalServiceConnections;
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

                async Task<List<PipelineGenerationResult>> GenerateAsync(string convention, DescriptorRequest[] requests,
                    int[] groups, string[] connections)
                {
                    var results = new List<PipelineGenerationResult>();
                    var result = await _generator.RunAsync(PostMergeConstants.Organization, PostMergeConstants.Project,
                        _context.Prefix, scanPath, "Azure", _context.Repository, $"refs/heads/{branch}", "Hosted",
                        convention, groups, connections, _context.DevOpsPath, false, false, false, false, true, false,
                        cancellationToken, requests, results, options.SdkDirectory);
                    if (result != ExitCondition.Success)
                    {
                        throw new InvalidOperationException($"Pipeline generation/authorization failed for '{directory}' ({convention}): {result}.");
                    }
                    // Diagnostics only: every prepared definition (release and test) so far, rewritten after each step.
                    allDefinitions.AddRange(results);
                    File.WriteAllText(Path.Combine(options.ResultsDirectory, "definitions.json"),
                        JsonSerializer.Serialize(allDefinitions, new JsonSerializerOptions { WriteIndented = true }));
                    return results;
                }
            }
            return definitions;
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
    }
}
