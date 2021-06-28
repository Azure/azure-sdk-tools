using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using NotificationConfiguration;
using NotificationConfiguration.Enums;
using NotificationConfiguration.Helpers;
using NotificationConfiguration.Models;
using NotificationConfiguration.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GitHubCodeownerSubscriber
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

                var pipelineGroupTasks = (await devOpsService.GetAllTeamsAsync(project))
                    .Where(team =>
                        YamlHelper.Deserialize<TeamMetadata>(team.Description, swallowExceptions: true)?.Purpose == TeamPurpose.SynchronizedNotificationTeam
                    ).Select(async team =>
                    {
                        var pipelineId = YamlHelper.Deserialize<TeamMetadata>(team.Description).PipelineId;
                        return new
                        {
                            Pipeline = await devOpsService.GetPipelineAsync(project, pipelineId),
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

                        // Find matching contacts for the YAML file's path
                        var parser = new CodeOwnersParser(codeownersContent);
                        var matchPath = PathWithoutFilename(process.YamlFilename);
                        var searchPath = $"/{matchPath}/";
                        logger.LogInformation("Searching CODEOWNERS for matching path Path = {0}", searchPath);
                        var contacts = parser.GetContactsForPath(searchPath);

                        logger.LogInformation("Matching Contacts Path = {0}, NumContacts = {1}", searchPath, contacts.Count);

                        // Get set of team members in the CODEOWNERS file
                        var contactResolutionTasks = contacts
                            .Where(contact => contact.StartsWith("@"))
                            .Select(contact => githubNameResolver.GetInternalUserPrincipal(contact.Substring(1)));
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

        /// <summary>
        /// Returns a path that excludes the file name
        /// </summary>
        /// <param name="path">Path, must have forward slash separators ("/")</param>
        /// <returns></returns>
        private static string PathWithoutFilename(string path)
        {
            var splitPath = path
                .Split("/", options: StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            return string.Join("/", splitPath.Take(splitPath.Count - 1));
        }
    }
}
