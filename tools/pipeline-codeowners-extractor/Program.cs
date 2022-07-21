using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Azure.Sdk.Tools.NotificationConfiguration.Enums;
using Azure.Sdk.Tools.NotificationConfiguration.Helpers;
using Azure.Sdk.Tools.NotificationConfiguration.Models;
using Azure.Sdk.Tools.NotificationConfiguration.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeOwnersParser;
using Azure.Identity;
using System.IO;
using Newtonsoft.Json;

namespace Azure.Sdk.Tools.PipelineCodeownerExtractor
{
    class Program
    {
        // Type 2 maps to a pipeline YAML file in the repository
        private const int PipelineYamlProcessType = 2;
        private static readonly Dictionary<string, string> githubPrincipalNameCache = new Dictionary<string, string>();
        private static ILogger logger;
        private static GitHubToAADConverter githubToAadResolver;
        private static GitHubService gitHubService;

        /// <summary>
        /// Synchronizes CODEOWNERS contacts to appropriate DevOps groups
        /// </summary>
        /// <param name="organization">Azure DevOps organization name</param>
        /// <param name="projects">Azure DevOps project name</param>
        /// <param name="devOpsTokenVar">Personal Access Token environment variable name</param>
        /// <param name="aadAppIdVar">AAD App ID environment variable name (OpensourceAPI access)</param>
        /// <param name="aadAppSecretVar">AAD App Secret environment variable name (OpensourceAPI access)</param>
        /// <param name="aadTenantVar">AAD Tenant environment variable name (OpensourceAPI access)</param>
        /// <param name="pathPrefix">Azure DevOps path prefix (e.g. "\net")</param>
        /// <param name="output">Output file path</param>
        /// <returns></returns>
        public static async Task Main(
            string organization,
            string[] projects,
            string aadAppIdVar = "",
            string aadAppSecretVar = "",
            string aadTenantVar = "",
            string devOpsTokenVar = "",
            string pathPrefix = "",
            string output = "pipeline-owners.json"
            )
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole(options => options.SingleLine = true));

            var devOpsService = AzureDevOpsService.CreateAzureDevOpsService(
                Environment.GetEnvironmentVariable(devOpsTokenVar),
                $"https://dev.azure.com/{organization}/",
                loggerFactory.CreateLogger<AzureDevOpsService>()
            );

            gitHubService = new GitHubService(loggerFactory.CreateLogger<GitHubService>());

            var credential = new ClientSecretCredential(
                Environment.GetEnvironmentVariable(aadTenantVar),
                Environment.GetEnvironmentVariable(aadAppIdVar),
                Environment.GetEnvironmentVariable(aadAppSecretVar));

            githubToAadResolver = new GitHubToAADConverter(
                credential,
                loggerFactory.CreateLogger<GitHubToAADConverter>()
            );

            logger = loggerFactory.CreateLogger(string.Empty);

            var pipelineResults = await Task.WhenAll(projects.Select(x => devOpsService.GetPipelinesAsync(x)));

            var pipelines = pipelineResults.SelectMany(x => x);

            var filteredPipelines = pipelines
               .Where(pipeline => pipeline.Path.StartsWith(pathPrefix))
               .ToArray();

            var repositoryUrls = GetDistinctRepositoryUrls(filteredPipelines);

            var codeOwnerEntriesByRepository = await GetCodeOwnerEntriesAsync(repositoryUrls);

            var pipelineOwners = await AssociateOwnersToPipelinesAsync(filteredPipelines, codeOwnerEntriesByRepository);

            var outputContent = pipelineOwners.ToDictionary(x => x.Pipeline.Id, x => x.Owners);

            File.WriteAllText(output, JsonConvert.SerializeObject(outputContent, Formatting.Indented));
        }

        private static async Task<(BuildDefinition Pipeline, string[] Owners)[]> AssociateOwnersToPipelinesAsync(BuildDefinition[] filteredPipelines, Dictionary<string, List<CodeOwnerEntry>> codeOwnerEntriesByRepository)
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
                var codeownerPrincipals = codeOwnerEntry.Owners.ToArray();

                githubPipelineOwners.Add((pipeline, codeownerPrincipals));
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

        private static async Task<Dictionary<string, List<CodeOwnerEntry>>> GetCodeOwnerEntriesAsync(string[] repositoryUrls)
        {
            var tasks = repositoryUrls.Select(async url => new
            {
                RepositoryUrl = url,
                CodeOwners = await gitHubService.GetCodeownersFile(new Uri(url))
            });

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
