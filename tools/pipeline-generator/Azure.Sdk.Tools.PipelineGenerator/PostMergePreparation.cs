using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;
using PipelineGenerator.CommandParserOptions;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace PipelineGenerator
{
    public class PostMergePreparation
    {
        private readonly IPostMergeOperations _operations;

        public PostMergePreparation(IPostMergeOperations operations)
        {
            _operations = operations;
        }

        public async Task RunAsync(PreparePostMergeOptions options, CancellationToken cancellationToken)
        {
            var requests = PostMergeRequest.Validate(options);
            PostMergeRequest.ValidateTrustedEnvironment(Environment.GetEnvironmentVariable);
            Directory.CreateDirectory(options.ResultsDirectory);
            var repository = $"Azure/{options.RepositoryName}";
            var runs = new List<ReleaseRunResult>();
            WriteResults(options, repository, runs);

            PreparedSdkDirectory.Validate(options, requests);
            await _operations.ValidateLockAsync(cancellationToken);
            // The trusted checkout step only admits the currently supported default branch, main.
            const string branch = "main";
            var definitions = await _operations.PrepareAsync(options, requests, branch, cancellationToken);
            if (!definitions.Select(d => d.DescriptorPath).OrderBy(p => p, StringComparer.Ordinal)
                    .SequenceEqual(requests.Select(r => r.Path).OrderBy(p => p, StringComparer.Ordinal)) ||
                definitions.Select(d => d.DefinitionId).Distinct().Count() != requests.Length ||
                definitions.Any(d => d.DefinitionId <= 0 || d.Convention != "up" || d.Category != "unified"))
            {
                throw new InvalidOperationException("Generator results do not map one-to-one to the requested unified release descriptors.");
            }

            foreach (var definition in definitions)
            {
                var id = definition.DefinitionId;
                var sha = options.MergeCommitSha;
                // Build history is the source of truth: a run created by normal CI or an earlier attempt
                // (even one whose queue response was lost) is recognized here instead of queued again.
                var build = await _operations.FindBuildAsync(id, repository, sha, branch, cancellationToken);
                if (build == null)
                {
                    // Never automatically retry this POST within an attempt.
                    build = await _operations.QueueBuildAsync(id, repository, sha, branch, cancellationToken);
                    File.WriteAllText(Path.Combine(options.ResultsDirectory, $"queue-response-{id}.json"),
                        JsonConvert.SerializeObject(build, Formatting.Indented));
                }
                if (!HasWebLink(build))
                {
                    if (build?.Id is not > 0)
                    {
                        throw new InvalidOperationException("Azure DevOps did not return a run ID; submission outcome is uncertain.");
                    }
                    build = await _operations.GetBuildAsync(build.Id, cancellationToken);
                }
                var result = GetRunResult(build, definition, repository, sha, branch);
                runs.Add(result);
                WriteResults(options, repository, runs);
            }
        }

        private static bool HasWebLink(Build build) =>
            build?.Links?.Links.TryGetValue("web", out var link) == true && link is ReferenceLink { Href: not null };

        public static bool Matches(Build build, int definitionId, string repository, string sha, string branch) =>
            build?.Definition?.Id == definitionId && build.SourceVersion == sha &&
            build.SourceBranch == $"refs/heads/{branch}" && build.Reason == BuildReason.IndividualCI &&
            string.Equals(build.Repository?.Name, repository, StringComparison.OrdinalIgnoreCase);

        public static ReleaseRunResult GetRunResult(Build build, PipelineGenerationResult definition,
            string repository, string sha, string branch)
        {
            if (build?.Id is not > 0 || !Matches(build, definition.DefinitionId, repository, sha, branch))
            {
                throw new InvalidOperationException("Returned run does not match the requested definition/repository/commit/CI reason.");
            }
            if (!HasWebLink(build) || !Uri.TryCreate(((ReferenceLink)build.Links.Links["web"]).Href, UriKind.Absolute, out var url) ||
                url.Scheme != "https" || url.UserInfo.Length != 0 ||
                (url.Host != "dev.azure.com" && url.Host != "azure-sdk.visualstudio.com"))
            {
                throw new InvalidOperationException($"Run {build.Id} did not return a supported web link.");
            }
            return new ReleaseRunResult(definition.DescriptorPath, definition.DefinitionId, build.Id, url.AbsoluteUri);
        }

        private static void WriteResults(PreparePostMergeOptions options, string repository, List<ReleaseRunResult> runs)
        {
            var result = new { schemaVersion = 1, repository, sourceVersion = options.MergeCommitSha, releaseRuns = runs };
            File.WriteAllText(Path.Combine(options.ResultsDirectory, "release-runs.json"),
                JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
