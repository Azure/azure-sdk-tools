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

namespace Azure.Sdk.Tools.NotificationConfiguration
{
    class NotificationConfigurator
    {
        private readonly AzureDevOpsService service;
        private readonly GitHubService gitHubService;
        private readonly ILogger<NotificationConfigurator> logger;

        private const int MaxTeamNameLength = 64;

        // A cache on the code owners github identity to owner descriptor.
        private readonly Dictionary<string, string> contactsCache = new Dictionary<string, string>();
        // A cache on the team member to member descriptor.
        private readonly Dictionary<string, string> teamMemberCache = new Dictionary<string, string>();

        public NotificationConfigurator(AzureDevOpsService service, GitHubService gitHubService, ILogger<NotificationConfigurator> logger)
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
            var teams = await service.GetAllTeamsAsync(projectName);

            foreach (var pipeline in pipelines)
            {
                using (logger.BeginScope("Evaluate Pipeline: Name = {0}, Path = {1}, Id = {2}", pipeline.Name, pipeline.Path, pipeline.Id))
                {
                    var parentTeam = await EnsureTeamExists(pipeline, TeamPurpose.ParentNotificationTeam, teams, gitHubToAADConverter, persistChanges);
                    var childTeam = await EnsureTeamExists(pipeline, TeamPurpose.SynchronizedNotificationTeam, teams, gitHubToAADConverter, persistChanges);

                    if (!persistChanges && (parentTeam == default || childTeam == default))
                    {
                        // Skip team nesting and notification work if
                        logger.LogInformation("Skipping Teams and Notifications because parent or child team does not exist");
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
                    logger.LogWarning($"Notification team name (length {fullTeamName.Length}) will be truncated to {teamName}");
                }
            }
            else if (purpose == TeamPurpose.SynchronizedNotificationTeam)
            {
                teamName += $"Code owners sync notifications";
            }

            bool updateMetadataAndName = false;
            var result = teams.FirstOrDefault(
                team =>
                {
                    // Swallowing exceptions because parse errors on
                    // free form text fields which might be non-yaml text
                    // are not exceptional
                    var metadata = YamlHelper.Deserialize<TeamMetadata>(team.Description, swallowExceptions: true);
                    bool metadataMatches = (metadata?.PipelineId == pipeline.Id && metadata?.Purpose == purpose);
                    bool nameMatches = (team.Name == teamName);

                    if (metadataMatches && nameMatches)
                    {
                        return true;
                    }

                    if (metadataMatches)
                    {
                        logger.LogInformation("Found team with matching pipeline id {0} but different name '{1}', expected '{2}'. Purpose = '{3}'", metadata?.PipelineId, team.Name, teamName, metadata?.Purpose);
                        updateMetadataAndName = true;
                        return true;
                    }

                    if (nameMatches)
                    {
                        logger.LogInformation("Found team with matching name {0} but different pipeline id {1}, expected {2}. Purpose = '{3}'", team.Name, metadata?.PipelineId, pipeline.Id, metadata?.Purpose);
                        updateMetadataAndName = true;
                        return true;
                    }

                    return false;
                });
            if (result == default)
            {
                logger.LogInformation("Team Not Found purpose = {0}", purpose);
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

                logger.LogInformation("Create Team for Pipeline PipelineId = {0} Purpose = {1} Name = '{2}'", pipeline.Id, purpose, teamName);
                if (persistChanges)
                {
                    result = await service.CreateTeamForProjectAsync(pipeline.Project.Id.ToString(), newTeam);
                    if (purpose == TeamPurpose.ParentNotificationTeam)
                    {
                        await EnsureScheduledBuildFailSubscriptionExists(pipeline, result, true);
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
                result.Description = YamlHelper.Serialize(teamMetadata);
                result.Name = teamName;

                logger.LogInformation("Update Team for Pipeline PipelineId = {0} Purpose = {1} Name = '{2}'", pipeline.Id, purpose, teamName);
                if (persistChanges)
                {
                    result = await service.UpdateTeamForProjectAsync(pipeline.Project.Id.ToString(), result);
                    if (purpose == TeamPurpose.ParentNotificationTeam)
                    {
                        await EnsureScheduledBuildFailSubscriptionExists(pipeline, result, true);
                    }
                }
            }

            if (purpose == TeamPurpose.SynchronizedNotificationTeam)
            {
                await SyncTeamWithCodeownersFile(pipeline, result, gitHubToAADConverter, persistChanges);
            }
            return result;
        }

        private async Task SyncTeamWithCodeownersFile(
            BuildDefinition buildDefinition,
            WebApiTeam team,
            GitHubToAADConverter gitHubToAADConverter,
            bool persistChanges)
        {
            using (logger.BeginScope("Team Name = {0}", team.Name))
            {
                List<string> contacts =
                    new Contacts(gitHubService, logger).GetFromBuildDefinitionRepoCodeowners(buildDefinition);
                if (contacts == null)
                {
                    // assert: the reason for why contacts is null has been already logged.
                    return;
                }

                // Get set of team members in the CODEOWNERS file
                var contactsDescriptors = new List<string>();
                foreach (string contact in contacts)
                {
                    if (!contactsCache.ContainsKey(contact))
                    {
                        // TODO: Better to have retry if no success on this call.
                        var userPrincipal = gitHubToAADConverter.GetUserPrincipalNameFromGithub(contact);
                        if (!string.IsNullOrEmpty(userPrincipal))
                        {
                            contactsCache[contact] = await service.GetDescriptorForPrincipal(userPrincipal);
                        }
                        else
                        {
                            logger.LogInformation(
                                "Cannot find the user principal for GitHub contact '{contact}'",
                                contact);
                            contactsCache[contact] = null;
                        }
                    }
                    contactsDescriptors.Add(contactsCache[contact]);
                }

                var contactsSet = new HashSet<string>(contactsDescriptors);
                // Get set of team members in the DevOps teams
                var teamMembers = await service.GetMembersAsync(team);
                var teamDescriptors = new List<String>();
                foreach (var member in teamMembers)
                {
                    if (!teamMemberCache.ContainsKey(member.Identity.Id))
                    {
                        var teamMemberDescriptor = (await service.GetUserFromId(new Guid(member.Identity.Id))).SubjectDescriptor.ToString();
                        teamMemberCache[member.Identity.Id] = teamMemberDescriptor;
                    }
                    teamDescriptors.Add(teamMemberCache[member.Identity.Id]);
                }
                var teamSet = new HashSet<string>(teamDescriptors);
                var contactsToRemove = teamSet.Except(contactsSet);
                var contactsToAdd = contactsSet.Except(teamSet);

                foreach (string descriptor in contactsToRemove)
                {
                    if (persistChanges && descriptor != null)
                    {
                        string teamDescriptor = await service.GetDescriptorAsync(team.Id);
                        logger.LogInformation("Delete Contact TeamDescriptor = {0}, ContactDescriptor = {1}", teamDescriptor, descriptor);
                        await service.RemoveMember(teamDescriptor, descriptor);
                    }
                }

                foreach (string descriptor in contactsToAdd)
                {
                    if (persistChanges && descriptor != null)
                    {
                        string teamDescriptor = await service.GetDescriptorAsync(team.Id);
                        logger.LogInformation("Add Contact TeamDescriptor = {0}, ContactDescriptor = {1}", teamDescriptor, descriptor);
                        await service.AddToTeamAsync(teamDescriptor, descriptor);
                    }
                }
            }
        }

        private async Task<IEnumerable<BuildDefinition>> GetPipelinesAsync(string projectName, string projectPath, PipelineSelectionStrategy strategy)
        {
            var definitions = await service.GetPipelinesAsync(projectName, projectPath);

            switch (strategy)
            {
                case PipelineSelectionStrategy.All:
                    return definitions;
                case PipelineSelectionStrategy.Scheduled:
                default:
                    return definitions.Where(
                        def => def.Triggers.Any(
                            trigger => trigger.TriggerType == DefinitionTriggerType.Schedule));
            }
        }

        private async Task EnsureSynchronizedNotificationTeamIsChild(WebApiTeam parent, WebApiTeam child, bool persistChanges)
        {
            var parentDescriptor = await service.GetDescriptorAsync(parent.Id);
            var childDescriptor = await service.GetDescriptorAsync(child.Id);

            var isInTeam = await service.CheckMembershipAsync(parentDescriptor, childDescriptor);

            logger.LogInformation("Child In Parent ParentId = {0}, ChildId = {1}, IsInTeam = {2}", parent.Id, child.Id, isInTeam);

            if (!isInTeam)
            {
                logger.LogInformation("Adding Child Team");

                if (persistChanges)
                {
                    await service.AddToTeamAsync(parentDescriptor, childDescriptor);
                }
            }
        }

        private async Task EnsureScheduledBuildFailSubscriptionExists(BuildDefinition pipeline, WebApiTeam team, bool persistChanges)
        {
            const string BuildFailureNotificationTag = "#AutomaticBuildFailureNotification";
            var subscriptions = await service.GetSubscriptionsAsync(team.Id);

            var subscription = subscriptions.FirstOrDefault(sub => sub.Description.Contains(BuildFailureNotificationTag));

            logger.LogInformation("Team Is Subscribed TeamName = {0} PipelineId = {1}", team.Name, pipeline.Id);

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
                    Description = $"A build fails {BuildFailureNotificationTag}",
                    Filter = filter,
                    Scope = new SubscriptionScope { Type = "none", Id = pipeline.Project.Id },
                    Subscriber = identity,
                };

                logger.LogInformation("Creating Subscription PipelineId = {0}, TeamId = {1}", pipeline.Id, team.Id);
                if (persistChanges)
                {
                    subscription = await service.CreateSubscriptionAsync(newSubscription);
                }
            }
            else
            {
                var filter = subscription.Filter as ExpressionFilter;
                if (filter == null)
                {
                    logger.LogWarning("Subscription expression is not correct for of team {0}", team.Name);
                    return;
                }

                var definitionClause = filter.FilterModel.Clauses.FirstOrDefault(c => c.FieldName == "Definition name");

                if (definitionClause == null)
                {
                    logger.LogWarning("Subscription doesn't have correct expression filters for of team {0}", team.Name);
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
                        logger.LogInformation("Updating Subscription expression for team {0} with correct definition name {1}", team.Name, definitionName);
                        subscription = await service.UpdatedSubscriptionAsync(updateParameters, subscription.Id.ToString());
                    }
                }
            }
        }
    }
}
