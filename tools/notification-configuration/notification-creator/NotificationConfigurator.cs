using common.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Notifications.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Azure.Sdk.Tools.NotificationConfiguration.Enums;
using Azure.Sdk.Tools.NotificationConfiguration.Models;
using Azure.Sdk.Tools.NotificationConfiguration.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Sdk.Tools.NotificationConfiguration.Helpers;
using System;
using Azure.Sdk.Tools.CodeOwnersParser;
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.NotificationConfiguration
{
    class NotificationConfigurator
    {
        private readonly AzureDevOpsService service;
        private readonly GitHubService gitHubService;
        private readonly ILogger<NotificationConfigurator> logger;

        private const int MaxTeamNameLength = 64;
        // Type 2 maps to a pipeline YAML file in the repository
        private const int PipelineYamlProcessType = 2;
        // A cache on the code owners github identity to owner descriptor.
        private readonly Dictionary<string, string> codeownersCache = new Dictionary<string, string>();
        // A cache on the team member to member descriptor.
        private readonly Dictionary<string, string> teamMemberCache = new Dictionary<string, string>();

        public NotificationConfigurator(
            AzureDevOpsService service,
            GitHubService gitHubService,
            ILogger<NotificationConfigurator> logger)
        {
            this.service = service;
            this.gitHubService = gitHubService;
            this.logger = logger;
        }

        public async Task ConfigureNotifications(
            string projectName,
            string projectPath,
            GitHubToAADConverter gitHubToAADConverter,
            bool persistChanges = true,
            PipelineSelectionStrategy strategy = PipelineSelectionStrategy.Scheduled)
        {
            var pipelines = await GetPipelinesAsync(projectName, projectPath, strategy);
            var teams = (await service.GetAllTeamsAsync(projectName)).ToList();

            foreach (var pipeline in pipelines)
            {
                using (logger.BeginScope(
                           "Evaluate Pipeline: Name = {pipelineName}, Path = {pipelinePath}, Id = {pipelineId}",
                           pipeline.Name, pipeline.Path, pipeline.Id))
                {
                    var parentTeam = await EnsureTeamExists(pipeline,
                        TeamPurpose.ParentNotificationTeam, teams, gitHubToAADConverter,
                        persistChanges);
                    var childTeam = await EnsureTeamExists(pipeline,
                        TeamPurpose.SynchronizedNotificationTeam, teams, gitHubToAADConverter,
                        persistChanges);

                    if (!persistChanges && (parentTeam == default || childTeam == default))
                    {
                        // Skip team nesting and notification work
                        logger.LogInformation(
                            "Skipping Teams and Notifications because parent or child team does not exist");
                        continue;
                    }
                    await EnsureSynchronizedNotificationTeamIsChild(parentTeam, childTeam, persistChanges);
                }
            }
        }

        private async Task<WebApiTeam> EnsureTeamExists(
            BuildDefinition pipeline,
            TeamPurpose purpose,
            IEnumerable<WebApiTeam> teams,
            GitHubToAADConverter gitHubToAADConverter,
            bool persistChanges)
        {
            string teamName = $"{pipeline.Id} ";

            if (purpose == TeamPurpose.ParentNotificationTeam)
            {
                // Ensure team name fits within maximum 64 character limit
                // https://docs.microsoft.com/en-us/azure/devops/organizations/settings/naming-restrictions?view=azure-devops#teams
                string fullTeamName = teamName + $"{pipeline.Name}";
                teamName = StringHelper.MaxLength(fullTeamName, MaxTeamNameLength);
                if (fullTeamName.Length > teamName.Length)
                {
                    logger.LogWarning(
                        "Notification team name (length {fullTeamNameLength}) will be truncated to {teamName}",
                        fullTeamName.Length, teamName);
                }
            }
            else if (purpose == TeamPurpose.SynchronizedNotificationTeam)
            {
                teamName += "Code owners sync notifications";
            }
            else
            {
                logger.LogWarning(
                    "Unsupported team purpose. TeamName: {teamName}, Purpose: {purpose}", teamName,
                    purpose);
            }

            bool updateMetadataAndName = false;
            WebApiTeam webApiTeam = teams.FirstOrDefault(
                team => TeamMatchesPipelineMetadata(team, pipeline, purpose, teamName, out updateMetadataAndName));

            if (webApiTeam == default)
            {
                logger.LogInformation("Team Not Found purpose = {purpose}", purpose);
                var teamMetadata = new TeamMetadata
                {
                    PipelineId = pipeline.Id,
                    Purpose = purpose,
                    PipelineName = pipeline.Name,
                };
                var newTeam = new WebApiTeam
                {
                    Description = YamlHelper.Serialize(teamMetadata),
                    Name = teamName
                };

                logger.LogInformation(
                    "Create Team for Pipeline PipelineId = {pipelineId} Name = '{teamName}' Purpose = {purpose}",
                    pipeline.Id, teamName, purpose);
                if (persistChanges)
                {
                    webApiTeam = await service.CreateTeamForProjectAsync(pipeline.Project.Id.ToString(), newTeam);
                    if (purpose == TeamPurpose.ParentNotificationTeam)
                    {
                        await EnsureScheduledBuildFailSubscriptionExists(pipeline, webApiTeam, true);
                    }
                }
            }
            else if (updateMetadataAndName)
            {
                var teamMetadata = new TeamMetadata
                {
                    PipelineId = pipeline.Id,
                    Purpose = purpose,
                };
                webApiTeam.Description = YamlHelper.Serialize(teamMetadata);
                webApiTeam.Name = teamName;

                logger.LogInformation(
                    "Update Team for Pipeline PipelineId = {pipelineId} Name = '{teamName}' Purpose = {purpose}",
                    pipeline.Id, teamName, purpose);
                if (persistChanges)
                {
                    webApiTeam = await service.UpdateTeamForProjectAsync(pipeline.Project.Id.ToString(), webApiTeam);
                    if (purpose == TeamPurpose.ParentNotificationTeam)
                    {
                        await EnsureScheduledBuildFailSubscriptionExists(pipeline, webApiTeam, persistChanges: true);
                    }
                }
            }
            else
            {
                // Found the team, and the metadata and team name match. Hence, nothing to do.
            }

            if (purpose == TeamPurpose.SynchronizedNotificationTeam)
            {
                await SyncTeamWithCodeownersFile(pipeline, webApiTeam, gitHubToAADConverter, persistChanges);
            }
            return webApiTeam;
        }

        private bool TeamMatchesPipelineMetadata(
            WebApiTeam team,
            BuildDefinition pipeline,
            TeamPurpose purpose,
            string teamName,
            out bool updateMetadataAndName)
        {
            updateMetadataAndName = false;

            // "swallowExceptions" is set to "true" because parse errors on
            // free-form text fields which might be non-yaml text are not exceptional.
            var metadata = YamlHelper.Deserialize<TeamMetadata>(team.Description, swallowExceptions: true);
            bool metadataMatches = (metadata?.PipelineId == pipeline.Id && metadata?.Purpose == purpose);
            bool nameMatches = (team.Name == teamName);

            if (metadataMatches && nameMatches)
            {
                return true;
            }

            if (metadataMatches)
            {
                logger.LogInformation(
                    "Found team with matching pipeline id {pipelineId} but different name " +
                    "'{teamName}', expected '{expectedTeamName}'. Purpose = '{purpose}'",
                    metadata.PipelineId, team.Name, teamName, metadata.Purpose);
                updateMetadataAndName = true;
                return true;
            }

            if (nameMatches)
            {
                logger.LogInformation(
                    "Found team with matching name {teamName} but different pipeline id " +
                    "{pipelineId}, expected {expectedPipelineId}. Purpose = '{purpose}'",
                    team.Name, metadata?.PipelineId, pipeline.Id, metadata?.Purpose);
                updateMetadataAndName = true;
                return true;
            }

            return false;
        }

        private async Task SyncTeamWithCodeownersFile(
            BuildDefinition pipeline,
            WebApiTeam team,
            GitHubToAADConverter gitHubToAADConverter,
            bool persistChanges)
        {
            using (logger.BeginScope("Team Name = {teamName}", team.Name))
            {
                if (pipeline.Process.Type != PipelineYamlProcessType)
                {
                    return;
                }

                // Get contents of CODEOWNERS
                logger.LogInformation("Fetching CODEOWNERS file");
                Uri repoUrl = pipeline.Repository.Url;

                if (repoUrl != null)
                {
                    repoUrl = new Uri(Regex.Replace(repoUrl.ToString(), @"\.git$", String.Empty));
                }
                else
                {
                    logger.LogError("No repository url returned from pipeline. Repo id: {repoId}",
                        pipeline.Repository.Id);
                    return;
                }
                List<CodeownersEntry> codeownersEntries = await gitHubService.GetCodeownersFile(repoUrl);

                if (codeownersEntries == default)
                {
                    logger.LogInformation("CODEOWNERS file not found, skipping sync");
                    return;
                }
                var process = pipeline.Process as YamlProcess;

                logger.LogInformation("Searching CODEOWNERS for matching path for {yamlPath}", process?.YamlFilename);

                CodeownersEntry codeownersEntry =
                    CodeownersFile.GetMatchingCodeownersEntry(process?.YamlFilename,
                        codeownersEntries);
                codeownersEntry.ExcludeNonUserAliases();

                logger.LogInformation(
                    "Matching Contacts Path = {yamlPath}, NumContacts = {ownersCount}",
                    process?.YamlFilename, codeownersEntry.Owners.Count);

                // Get set of team members in the CODEOWNERS file
                var codeownersDescriptors = new List<String>();
                foreach (var contact in codeownersEntry.Owners)
                {
                    await AddContactToCodeownersDescriptors(gitHubToAADConverter, contact, codeownersDescriptors);
                }

                var codeownersSet = new HashSet<string>(codeownersDescriptors);
                // Get set of team members in the DevOps teams
                var teamMembers = await service.GetMembersAsync(team);
                var teamDescriptors = new List<String>();
                foreach (TeamMember teamMember in teamMembers)
                {
                    await AddTeamMemberToTeamDescriptors(teamMember, teamDescriptors);
                }
                var teamSet = new HashSet<string>(teamDescriptors);
                var contactsToRemove = teamSet.Except(codeownersSet);
                var contactsToAdd = codeownersSet.Except(teamSet);

                foreach (string contactToRemove in contactsToRemove)
                {
                    await RemoveContactIfApplicable(team, persistChanges, contactToRemove);
                }

                foreach (string contactToAdd in contactsToAdd)
                {
                    await AddContactIfApplicable(team, persistChanges, contactToAdd);
                }
            }
        }

        private async Task AddContactIfApplicable(WebApiTeam team, bool persistChanges, string descriptor)
        {
            if (persistChanges && descriptor != null)
            {
                var teamDescriptor = await service.GetDescriptorAsync(team.Id);
                logger.LogInformation(
                    "Add Contact TeamDescriptor = {teamDescriptor}, ContactDescriptor = {contactDescriptor}",
                    teamDescriptor, descriptor);
                await service.AddToTeamAsync(teamDescriptor, descriptor);
            }
        }

        private async Task RemoveContactIfApplicable(
            WebApiTeam team,
            bool persistChanges,
            string descriptor)
        {
            if (persistChanges && descriptor != null)
            {
                var teamDescriptor = await service.GetDescriptorAsync(team.Id);
                logger.LogInformation(
                    "Remove Contact TeamDescriptor = {teamDescriptor}, ContactDescriptor = {contactDescriptor}",
                    teamDescriptor, descriptor);
                await service.RemoveMember(teamDescriptor, descriptor);
            }
        }

        private async Task AddTeamMemberToTeamDescriptors(TeamMember member, List<string> teamDescriptors)
        {
            if (!teamMemberCache.ContainsKey(member.Identity.Id))
            {
                var userIdentityFromId = await service.GetUserFromId(new Guid(member.Identity.Id));
                var teamMemberDescriptor = userIdentityFromId.SubjectDescriptor.ToString();
                teamMemberCache[member.Identity.Id] = teamMemberDescriptor;
            }

            teamDescriptors.Add(teamMemberCache[member.Identity.Id]);
        }

        private async Task AddContactToCodeownersDescriptors(
            GitHubToAADConverter gitHubToAADConverter,
            string contact,
            List<string> codeownersDescriptors)
        {
            if (!codeownersCache.ContainsKey(contact))
            {
                // TODO: Better to have retry if no success on this call.
                // TODO: use async overload
                var userPrincipal = gitHubToAADConverter.GetUserPrincipalNameFromGithub(contact);
                if (!string.IsNullOrEmpty(userPrincipal))
                {
                    codeownersCache[contact] = await service.GetDescriptorForPrincipal(userPrincipal);
                }
                else
                {
                    logger.LogInformation(
                        "Cannot find the user principal for github contact '{contact}'",
                        contact);
                    codeownersCache[contact] = null;
                }
            }

            codeownersDescriptors.Add(codeownersCache[contact]);
        }

        private async Task<IEnumerable<BuildDefinition>> GetPipelinesAsync(
            string projectName,
            string projectPath,
            PipelineSelectionStrategy strategy)
        {
            var definitions = await service.GetPipelinesAsync(projectName, projectPath);

            return strategy switch
            {
                PipelineSelectionStrategy.All => definitions,
                _ => definitions.Where(
                    def => def.Triggers.Any(
                        trigger => trigger.TriggerType == DefinitionTriggerType.Schedule)),
            };
        }

        private async Task EnsureSynchronizedNotificationTeamIsChild(
            WebApiTeam parent,
            WebApiTeam child,
            bool persistChanges)
        {
            var parentDescriptor = await service.GetDescriptorAsync(parent.Id);
            var childDescriptor = await service.GetDescriptorAsync(child.Id);

            var isInTeam = await service.CheckMembershipAsync(parentDescriptor, childDescriptor);

            logger.LogInformation(
                "Child In Parent ParentId = {parentId}, ChildId = {childId}, IsInTeam = {isInTeam}",
                parent.Id, child.Id, isInTeam);

            if (!isInTeam)
            {
                logger.LogInformation("Adding Child Team");

                if (persistChanges)
                {
                    await service.AddToTeamAsync(parentDescriptor, childDescriptor);
                }
            }
        }

        private async Task EnsureScheduledBuildFailSubscriptionExists(
            BuildDefinition pipeline,
            WebApiTeam team,
            bool persistChanges)
        {
            const string buildFailureNotificationTag = "#AutomaticBuildFailureNotification";
            var subscriptions = await service.GetSubscriptionsAsync(team.Id);

            var subscription = subscriptions.FirstOrDefault(sub
                => sub.Description.Contains(buildFailureNotificationTag));

            logger.LogInformation(
                "Team Is Subscribed TeamName = {teamName} PipelineId = {pipelineId}", team.Name,
                pipeline.Id);

            string definitionName = $"\\{pipeline.Project.Name}\\{pipeline.Name}";
            if (subscription == default)
            {
                var filterModel = new ExpressionFilterModel
                {
                    Clauses = new ExpressionFilterClause[]
                    {
                        new ExpressionFilterClause { Index = 1, LogicalOperator = "", FieldName = "Status", Operator = "=", Value = "Failed" },
                        new ExpressionFilterClause { Index = 2, LogicalOperator = "And", FieldName = "Definition name", Operator = "=", Value = definitionName },
                        new ExpressionFilterClause { Index = 3, LogicalOperator = "And", FieldName = "Build reason", Operator = "=", Value = "Scheduled" }
                    }
                };
                var filter = new ExpressionFilter("ms.vss-build.build-completed-event", filterModel);

                var identity = new IdentityRef
                {
                    Id = team.Id.ToString(),
                    Url = team.IdentityUrl
                };

                var newSubscription = new NotificationSubscriptionCreateParameters
                {
                    Channel = new UserSubscriptionChannel { UseCustomAddress = false },
                    Description = $"A build fails {buildFailureNotificationTag}",
                    Filter = filter,
                    Scope = new SubscriptionScope { Type = "none", Id = pipeline.Project.Id },
                    Subscriber = identity,
                };

                logger.LogInformation(
                    "Creating Subscription PipelineId = {pipelineId}, TeamId = {teamId}",
                    pipeline.Id, team.Id);
                if (persistChanges)
                {
                    subscription = await service.CreateSubscriptionAsync(newSubscription);
                }
            }
            else
            {
                if (!(subscription.Filter is ExpressionFilter filter))
                {
                    logger.LogWarning("Subscription expression is not correct for of team {teamName}", team.Name);
                    return;
                }

                var definitionClause = filter.FilterModel.Clauses.FirstOrDefault(c => c.FieldName == "Definition name");

                if (definitionClause == null)
                {
                    logger.LogWarning(
                        "Subscription doesn't have correct expression filters for of team {teamName}",
                        team.Name);
                    return;
                }

                if (definitionClause.Value != definitionName)
                {
                    definitionClause.Value = definitionName;

                    if (persistChanges)
                    {
                        var updateParameters = new NotificationSubscriptionUpdateParameters()
                        {
                            Channel = subscription.Channel,
                            Description = subscription.Description,
                            Filter = subscription.Filter,
                            Scope = subscription.Scope,
                        };
                        logger.LogInformation(
                            "Updating Subscription expression for team {teamName} " +
                            "with correct definition name {definitionName}",
                            team.Name, definitionName);
                        subscription = await service.UpdatedSubscriptionAsync(updateParameters, subscription.Id);
                    }
                }
            }
        }
    }
}
