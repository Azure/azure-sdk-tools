using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.NotificationConfiguration;
using Azure.Sdk.Tools.NotificationConfiguration.Helpers;
using Azure.Sdk.Tools.NotificationConfiguration.Services;
using Azure.Sdk.Tools.PipelineOwnersExtractor.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.Build.WebApi;
using Models.OpenSourcePortal;
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
            string[] projects = this.settings.Projects.Split(',');
            IEnumerable<BuildDefinition>[] pipelineResults = await Task.WhenAll(
                projects.Select(x => devOpsService.GetPipelinesAsync(x.Trim())));

            // flatten arrays of pipelines by project into an array of pipelines
            BuildDefinition[] pipelines = pipelineResults
                .SelectMany(x => x)
                .ToArray();

            string[] repositoryUrls = GetDistinctRepositoryUrls(pipelines);

            Dictionary<string, List<CodeownersEntry>> codeownersEntriesByRepository =
                await GetCodeownersEntriesAsync(repositoryUrls);

            List<(BuildDefinition Pipeline, List<string> Owners)> pipelineOwners =
                await AssociateOwnersToPipelinesAsync(pipelines, codeownersEntriesByRepository);

            Dictionary<int, List<string>> outputContent =
                pipelineOwners.ToDictionary(x => x.Pipeline.Id, x => x.Owners);

            await File.WriteAllTextAsync(
                this.settings.Output,
                JsonConvert.SerializeObject(outputContent, Formatting.Indented),
                stoppingToken);
        }

        private async Task<List<(BuildDefinition Pipeline, List<string> Owners)>> AssociateOwnersToPipelinesAsync(
            IEnumerable<BuildDefinition> pipelines,
            Dictionary<string, List<CodeownersEntry>> codeownersEntriesByRepository)
        {
            UserLink[] linkedGithubUsers = await githubToAadResolver.GetPeopleLinksAsync();

            Dictionary<string, string> microsoftAliasMap = linkedGithubUsers.ToDictionary(
                x => x.GitHub.Login,
                x => x.Aad.UserPrincipalName,
                StringComparer.OrdinalIgnoreCase);

            List<(BuildDefinition Pipeline, List<string> Owners)> microsoftPipelineOwners =
                new List<(BuildDefinition Pipeline, List<string> Owners)>();
            
            HashSet<string> unrecognizedGitHubAliases = new HashSet<string>();

            foreach (BuildDefinition pipeline in pipelines)
            {
                if (pipeline.Process.Type != PipelineYamlProcessType || !(pipeline.Process is YamlProcess process))
                {
                    logger.LogInformation("Skipping non-yaml pipeline '{Pipeline}'", pipeline.Name);
                    continue;
                }

                if (pipeline.Repository.Type != "GitHub")
                {
                    logger.LogInformation(
                        "Skipping pipeline '{Pipeline}' with {Type} repository",
                        pipeline.Name,
                        pipeline.Repository.Type);
                    continue;
                }

                if (!codeownersEntriesByRepository.TryGetValue(
                        SanitizeRepositoryUrl(pipeline.Repository.Url.AbsoluteUri),
                        out List<CodeownersEntry> codeownersEntries))
                {
                    logger.LogInformation(
                        "Skipping pipeline '{Pipeline}' because its repo has no CODEOWNERS file",
                        pipeline.Name);
                    continue;
                }

                logger.LogInformation("Processing pipeline '{Pipeline}'", pipeline.Name);

                string buildDefPath = process.YamlFilename;
                logger.LogInformation("Searching CODEOWNERS for patch matching {Path}", buildDefPath);
                CodeownersEntry codeownersEntry =
                    CodeownersParser.GetMatchingCodeownersEntry(buildDefPath, codeownersEntries);
                codeownersEntry.ExcludeNonUserAliases();

                logger.LogInformation(
                    "Matching Path = {Path}, Owner Count = {OwnerCount}",
                    buildDefPath,
                    codeownersEntry.SourceOwners.Count);

                // Get set of team members in the CODEOWNERS file
                string[] githubOwners = codeownersEntry.SourceOwners.ToArray();

                List<string> microsoftOwners = new List<string>();

                foreach (string githubOwner in githubOwners)
                {
                    if (microsoftAliasMap.TryGetValue(githubOwner, out string microsoftOwner))
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

            int mappedCount = microsoftPipelineOwners.SelectMany(x => x.Owners).Distinct().Count();
            logger.LogInformation(
                "{Mapped} unique pipeline owner aliases mapped to Microsoft users. {Unmapped} could not be mapped.",
                mappedCount,
                unrecognizedGitHubAliases.Count);

            return microsoftPipelineOwners;
        }

        private async Task<Dictionary<string, List<CodeownersEntry>>> GetCodeownersEntriesAsync(string[] repositoryUrls)
        {
            IEnumerable<Task<(string RepositoryUrl, List<CodeownersEntry> Codeowners)>> tasks = repositoryUrls
                .Select(SanitizeRepositoryUrl)
                .Select(url => Task.FromResult((
                    RepositoryUrl: url,
                    Codeowners: this.gitHubService.GetCodeownersFileEntries(new Uri(url))
                )));

            (string RepositoryUrl, List<CodeownersEntry> Codeowners)[] taskResults = await Task.WhenAll(tasks);

            return taskResults.Where(x => x.Codeowners != null)
                .ToDictionary(x => x.RepositoryUrl, x => x.Codeowners, StringComparer.OrdinalIgnoreCase);
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
