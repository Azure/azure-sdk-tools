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

namespace Azure.Sdk.Tools.GithubCodeownerSubscriber
{
    class Program
    {
        // Type 2 maps to a pipeline YAML file in the repository
        private const int PipelineYamlProcessType = 2;

        /// <summary>
        /// Synchronizes CODEOWNERS contacts to appropriate DevOps groups
        /// </summary>
        /// <param name="organization">Azure DevOps organization name</param>
        /// <param name="project">Azure DevOps project name</param>
        /// <param name="devOpsTokenVar">Personal Access Token environment variable name</param>
        /// <param name="aadAppIdVar">AAD App ID environment variable name (Kusto access)</param>
        /// <param name="aadAppSecretVar">AAD App Secret environment variable name (Kusto access)</param>
        /// <param name="aadTenantVar">AAD Tenant environment variable name (Kusto access)</param>
        /// <param name="kustoUrlVar">Kusto URL environment variable name</param>
        /// <param name="kustoDatabaseVar">Kusto DB environment variable name</param>
        /// <param name="kustoTableVar">Kusto Table environment variable name</param>
        /// <param name="pathPrefix">Azure DevOps path prefix (e.g. "\net")</param>
        /// <param name="dryRun">Do not persist changes</param>
        /// <returns></returns>
        public static async Task Main(
            string organization,
            string project,
            string devOpsTokenVar,
            string aadAppIdVar,
            string aadAppSecretVar,
            string aadTenantVar,
            string kustoUrlVar,
            string kustoDatabaseVar,
            string kustoTableVar,
            string pathPrefix = "",
            bool dryRun = false
            )
        {
#pragma warning disable CS0618 // Type or member is obsolete
            using (var loggerFactory = LoggerFactory.Create(builder => {
                builder.AddConsole(config => { config.IncludeScopes = true; }); 
            }))
#pragma warning restore CS0618 // Type or member is obsolete
            {
                var devOpsService = AzureDevOpsService.CreateAzureDevOpsService(
                    Environment.GetEnvironmentVariable(devOpsTokenVar),
                    $"https://dev.azure.com/{organization}/",
                    loggerFactory.CreateLogger<AzureDevOpsService>()
                );
                
                var gitHubServiceLogger = loggerFactory.CreateLogger<GitHubService>();
                var gitHubService = new GitHubService(gitHubServiceLogger);

                var githubNameResolver = new GitHubNameResolver(
                    Environment.GetEnvironmentVariable(aadAppIdVar),
                    Environment.GetEnvironmentVariable(aadAppSecretVar),
                    Environment.GetEnvironmentVariable(aadTenantVar),
                    Environment.GetEnvironmentVariable(kustoUrlVar),
                    Environment.GetEnvironmentVariable(kustoDatabaseVar),
                    Environment.GetEnvironmentVariable(kustoTableVar),
                    loggerFactory.CreateLogger<GitHubNameResolver>()
                );

                var logger = loggerFactory.CreateLogger<Program>();
                var pipelines = (await devOpsService.GetPipelinesAsync(project)).ToDictionary(p => p.Id);

                var pipelineGroupTasks = (await devOpsService.GetAllTeamsAsync(project))
                    .Where(team =>
                        YamlHelper.Deserialize<TeamMetadata>(team.Description, swallowExceptions: true)?.Purpose == TeamPurpose.SynchronizedNotificationTeam
                    ).Select(async team =>
                    {
                        BuildDefinition pipeline;
                        var pipelineId = YamlHelper.Deserialize<TeamMetadata>(team.Description).PipelineId;

                        if (!pipelines.ContainsKey(pipelineId))
                        {
                            pipeline = await devOpsService.GetPipelineAsync(project, pipelineId);
                        }
                        else
                        {
                            pipeline = pipelines[pipelineId];
                        }

                        if (pipeline == default)
                        {
                            logger.LogWarning($"Could not find pipeline with id {pipelineId} referenced by team {team.Name}.");
                        }

                        return new
                        {
                            Pipeline = pipeline,
                            Team = team
                        };
                    });

                var pipelineGroups = await Task.WhenAll(pipelineGroupTasks);
                var filteredGroups = pipelineGroups.Where(group => group.Pipeline != default && group.Pipeline.Path.StartsWith(pathPrefix));

                foreach (var group in filteredGroups)
                {
                    using (logger.BeginScope("Team Name = {0}", group.Team.Name))
                    {

                        if (group.Pipeline.Process.Type != PipelineYamlProcessType)
                        {
                            continue;
                        }

                        // Get contents of CODEOWNERS
                        logger.LogInformation("Fetching CODEOWNERS file");
                        var managementUrl = new Uri(group.Pipeline.Repository.Properties["manageUrl"]);
                        var codeownersContent = await gitHubService.GetCodeownersFile(managementUrl);

                        if (codeownersContent == default)
                        {
                            logger.LogInformation("CODEOWNERS file not found, skipping sync");
                            continue;
                        }

                        var process = group.Pipeline.Process as YamlProcess;

                        logger.LogInformation("Searching CODEOWNERS for matching path for {0}", process.YamlFilename);
                        var codeOwnerEntries = CodeOwnersFile.ParseContent(codeownersContent);
                        var contacts = codeOwnerEntries.FindLast(x => x.PathExpression.Trim('/').StartsWith(process.YamlFilename.Trim('/'))).Owners;

                        logger.LogInformation("Matching Contacts Path = {0}, NumContacts = {1}", process.YamlFilename, contacts.Count);

                        // Get set of team members in the CODEOWNERS file
                        var contactResolutionTasks = contacts
                            .Select(contact => githubNameResolver.GetInternalUserPrincipal(contact));
                        var codeownerPrincipals = await Task.WhenAll(contactResolutionTasks);

                        var codeownersDescriptorsTasks = codeownerPrincipals
                            .Where(userPrincipal => !string.IsNullOrEmpty(userPrincipal))
                            .Select(userPrincipal => devOpsService.GetDescriptorForPrincipal(userPrincipal));
                        var codeownersDescriptors = await Task.WhenAll(codeownersDescriptorsTasks);
                        var codeownersSet = new HashSet<string>(codeownersDescriptors);

                        // Get set of existing team members
                        var teamMembers = await devOpsService.GetMembersAsync(group.Team);
                        var teamContactTasks = teamMembers
                            .Select(async member => await devOpsService.GetUserFromId(new Guid(member.Identity.Id)));
                        var teamContacts = await Task.WhenAll(teamContactTasks);
                        var teamDescriptors = teamContacts.Select(contact => contact.SubjectDescriptor.ToString());
                        var teamSet = new HashSet<string>(teamDescriptors);
                        
                        // Synchronize contacts
                        var contactsToRemove = teamSet.Except(codeownersSet);
                        var contactsToAdd = codeownersSet.Except(teamSet);

                        var teamDescriptor = await devOpsService.GetDescriptorAsync(group.Team.Id);

                        foreach (var descriptor in contactsToRemove)
                        {
                            logger.LogInformation("Delete Contact TeamDescriptor = {0}, ContactDescriptor = {1}", teamDescriptor, descriptor);
                            if (!dryRun)
                            {
                                await devOpsService.RemoveMember(teamDescriptor, descriptor);
                            }
                        }

                        foreach (var descriptor in contactsToAdd)
                        {
                            logger.LogInformation("Add Contact TeamDescriptor = {0}, ContactDescriptor = {1}", teamDescriptor, descriptor);
                            if (!dryRun)
                            {
                                await devOpsService.AddToTeamAsync(teamDescriptor, descriptor);
                            }
                        }
                    }
                }
            }
        }
    }
}
