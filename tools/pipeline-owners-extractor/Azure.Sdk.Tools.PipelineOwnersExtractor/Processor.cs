using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Azure.Sdk.Tools.CodeOwnersParser;
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
            var pipelineResults = await Task.WhenAll(this.settings.Projects.Select(x => devOpsService.GetPipelinesAsync(x)));

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

        private async Task<(BuildDefinition Pipeline, string[] Owners)[]> AssociateOwnersToPipelinesAsync(
            BuildDefinition[] filteredPipelines,
            Dictionary<string, List<CodeOwnerEntry>> codeOwnerEntriesByRepository)
        {
            var githubPipelineOwners = new List<(BuildDefinition Pipeline, string[] Owners)>();

            foreach (var pipeline in filteredPipelines)
            {
                logger.LogInformation("Pipeline Name = {0}", pipeline.Name);

                if (pipeline.Process.Type != PipelineYamlProcessType || !(pipeline.Process is YamlProcess process))
                {
                    continue;
                }

                if (!pipeline.Repository.Properties.TryGetValue("manageUrl", out var managementUrl))
                {
                    logger.LogInformation("ManagementURL not found, skipping sync");
                    continue;
                }

                if (!codeOwnerEntriesByRepository.TryGetValue(managementUrl, out var codeOwnerEntries))
                {
                    logger.LogInformation("CODEOWNERS file not found, skipping sync");
                    continue;
                }

                logger.LogInformation("Searching CODEOWNERS for matching path for {0}", process.YamlFilename);
                var codeOwnerEntry = CodeOwnersFile.FindOwnersForClosestMatch(codeOwnerEntries, process.YamlFilename);
                codeOwnerEntry.FilterOutNonUserAliases();

                logger.LogInformation("Matching Contacts Path = {0}, NumContacts = {1}", process.YamlFilename, codeOwnerEntry.Owners.Count);

                // Get set of team members in the CODEOWNERS file
                var codeOwnerPrincipals = codeOwnerEntry.Owners.ToArray();

                githubPipelineOwners.Add((pipeline, codeOwnerPrincipals));
            }

            var distinctGithubAliases = githubPipelineOwners.SelectMany(x => x.Owners).Distinct().ToArray();

            var microsoftAliasMap = await distinctGithubAliases
                .Select(async githubAlias => new
                {
                    Github = githubAlias,
                    Microsoft = await githubToAadResolver.GetUserPrincipalNameFromGithubAsync(githubAlias),
                })
                .LimitConcurrencyAsync(10)
                .ContinueWith(x => x.Result.ToDictionary(x => x.Github, x => x.Microsoft));

            var microsoftPipelineOwners = githubPipelineOwners
                .Select(x => (
                    x.Pipeline,
                    x.Owners
                        .Select(o => microsoftAliasMap[o])
                        .Where(o => o != null)
                        .ToArray()))
                .ToArray();

            return microsoftPipelineOwners;
        }

        private async Task<Dictionary<string, List<CodeOwnerEntry>>> GetCodeOwnerEntriesAsync(string[] repositoryUrls)
        {
            var tasks = repositoryUrls.Select(async url => (
                RepositoryUrl: url,
                CodeOwners: await this.gitHubService.GetCodeOwnersFile(new Uri(url))
            ));

            var taskResults = await Task.WhenAll(tasks);

            return taskResults.Where(x => x.CodeOwners != null).ToDictionary(x => x.RepositoryUrl, x => x.CodeOwners);
        }

        private static string[] GetDistinctRepositoryUrls(IEnumerable<BuildDefinition> pipelines)
        {
            return pipelines.Where(x => x.Repository?.Properties?.ContainsKey("manageUrl") == true)
                .Select(x => x.Repository.Properties["manageUrl"])
                .Distinct()
                .ToArray();
        }
    }
}
