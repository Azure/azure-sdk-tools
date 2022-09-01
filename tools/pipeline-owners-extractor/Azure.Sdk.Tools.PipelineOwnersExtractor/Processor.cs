using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Azure.Sdk.Tools.CodeOwnersParser;
using Azure.Sdk.Tools.NotificationConfiguration;
using Azure.Sdk.Tools.NotificationConfiguration.Helpers;
using Azure.Sdk.Tools.NotificationConfiguration.Services;
using Azure.Sdk.Tools.PipelineOwnersExtractor.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.Build.WebApi;

using Newtonsoft.Json;

namespace Azure.Sdk.Tools.PipelineOwnersExtractor
{
    public class Processor
    {
        // Type 2 maps to a pipeline YAML file in the repository
        private const int PipelineYamlProcessType = 2;

        private readonly ILogger logger;
        private readonly GitHubToAADConverter githubToAadResolver;
        private readonly AzureDevOpsService devOpsService;
        private readonly GitHubService gitHubService;
        private readonly PipelineOwnerSettings settings;

        public Processor(
            ILogger<Processor> logger,
            GitHubService gitHubService,
            GitHubToAADConverter githubToAadResolver,
            AzureDevOpsService devOpsService,
            IOptions<PipelineOwnerSettings> options)
        {
            this.logger = logger;
            this.gitHubService = gitHubService;
            this.githubToAadResolver = githubToAadResolver;
            this.devOpsService = devOpsService;
            this.settings = options.Value;
        }

        public async Task ExecuteAsync(CancellationToken stoppingToken = default)
        {
            var projects = this.settings.Projects.Split(',');
            var pipelineResults = await Task.WhenAll(projects.Select(x => devOpsService.GetPipelinesAsync(x.Trim())));

            // flatten arrays of pipelines by project into an array of pipelines
            var pipelines = pipelineResults
                .SelectMany(x => x)
                .ToArray();

            var repositoryUrls = GetDistinctRepositoryUrls(pipelines);

            var codeOwnerEntriesByRepository = await GetCodeOwnerEntriesAsync(repositoryUrls);

            var pipelineOwners = await AssociateOwnersToPipelinesAsync(pipelines, codeOwnerEntriesByRepository);

            var outputContent = pipelineOwners.ToDictionary(x => x.Pipeline.Id, x => x.Owners);

            await File.WriteAllTextAsync(this.settings.Output, JsonConvert.SerializeObject(outputContent, Formatting.Indented), stoppingToken);
        }

        private async Task<List<(BuildDefinition Pipeline, List<string> Owners)>> AssociateOwnersToPipelinesAsync(
            IEnumerable<BuildDefinition> pipelines,
            Dictionary<string, List<CodeOwnerEntry>> codeOwnerEntriesByRepository)
        {
            var linkedGithubUsers = await githubToAadResolver.GetPeopleLinksAsync();

            var microsoftAliasMap = linkedGithubUsers.ToDictionary(x => x.GitHub.Login, x => x.Aad.UserPrincipalName, StringComparer.OrdinalIgnoreCase);

            var microsoftPipelineOwners = new List<(BuildDefinition Pipeline, List<string> Owners)>();
            
            var unrecognizedGitHubAliases = new HashSet<string>();

            foreach (var pipeline in pipelines)
            {
                if (pipeline.Process.Type != PipelineYamlProcessType || !(pipeline.Process is YamlProcess process))
                {
                    logger.LogInformation("Skipping non-yaml pipeline '{Pipeline}'", pipeline.Name);
                    continue;
                }

                if (pipeline.Repository.Type != "GitHub")
                {
                    logger.LogInformation("Skipping pipeline '{Pipeline}' with {Type} repository", pipeline.Name, pipeline.Repository.Type);
                    continue;
                }

                if (!codeOwnerEntriesByRepository.TryGetValue(SanitizeRepositoryUrl(pipeline.Repository.Url.AbsoluteUri), out var codeOwnerEntries))
                {
                    logger.LogInformation("Skipping pipeline '{Pipeline}' because its repo has no CODEOWNERS file", pipeline.Name);
                    continue;
                }

                logger.LogInformation("Processing pipeline '{Pipeline}'", pipeline.Name);

                logger.LogInformation("Searching CODEOWNERS for patch matching {Path}", process.YamlFilename);
                var codeOwnerEntry = CodeOwnersFile.FindOwnersForClosestMatch(codeOwnerEntries, process.YamlFilename);
                codeOwnerEntry.FilterOutNonUserAliases();

                logger.LogInformation("Matching Path = {Path}, Owner Count = {OwnerCount}", process.YamlFilename, codeOwnerEntry.Owners.Count);

                // Get set of team members in the CODEOWNERS file
                var githubOwners = codeOwnerEntry.Owners.ToArray();

                var microsoftOwners = new List<string>();

                foreach (var githubOwner in githubOwners)
                {
                    if (microsoftAliasMap.TryGetValue(githubOwner, out var microsoftOwner))
                    {
                        microsoftOwners.Add(microsoftOwner);
                    }
                    else
                    {
                        unrecognizedGitHubAliases.Add(githubOwner);
                    }
                }

                microsoftPipelineOwners.Add((pipeline, microsoftOwners));
            }

            var mappedCount = microsoftPipelineOwners.SelectMany(x => x.Owners).Distinct().Count();
            logger.LogInformation("{Mapped} unique pipeline owner aliases mapped to Microsoft users. {Unmapped} could not be mapped.", mappedCount, unrecognizedGitHubAliases.Count);

            return microsoftPipelineOwners;
        }

        private async Task<Dictionary<string, List<CodeOwnerEntry>>> GetCodeOwnerEntriesAsync(string[] repositoryUrls)
        {
            var tasks = repositoryUrls
                .Select(SanitizeRepositoryUrl)
                .Select(async url => (
                    RepositoryUrl: url,
                    CodeOwners: await this.gitHubService.GetCodeownersFile(new Uri(url))
                ));

            var taskResults = await Task.WhenAll(tasks);

            return taskResults.Where(x => x.CodeOwners != null)
                .ToDictionary(x => x.RepositoryUrl, x => x.CodeOwners, StringComparer.OrdinalIgnoreCase);
        }

        private static string SanitizeRepositoryUrl(string url) => Regex.Replace(url, @"\.git$", string.Empty);

        private static string[] GetDistinctRepositoryUrls(IEnumerable<BuildDefinition> pipelines)
        {
            return pipelines.Where(x => x.Repository?.Properties?.ContainsKey("manageUrl") == true)
                .Select(x => x.Repository.Properties["manageUrl"])
                .Distinct()
                .ToArray();
        }
    }
}
