using common.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Notifications.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using NotificationConfiguration.Enums;
using NotificationConfiguration.Models;
using NotificationConfiguration.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NotificationConfiguration
{
    class NotificationConfigurator
    {
        private readonly AzureDevOpsService service;
        private readonly ILogger<NotificationConfigurator> logger;

        private const int MaxTeamNameLength = 64;

        public NotificationConfigurator(AzureDevOpsService service, ILogger<NotificationConfigurator> logger)
        {
            this.service = service;
            this.logger = logger;
        }

        public async Task ConfigureNotifications(
            string projectName, 
            string projectPath, 
            bool persistChanges = true, 
            PipelineSelectionStrategy strategy = PipelineSelectionStrategy.Scheduled)
        {
            var pipelines = await GetPipelinesAsync(projectName, projectPath, strategy);
            var teams = await service.GetAllTeamsAsync(projectName);

            foreach (var pipeline in pipelines)
            {
                // If the pipeline name length is max or greater there is no
                // room to add differentiators like "-- Sync Notifications"
                // and this will result in team name collisions.
                if (pipeline.Name.Length >= MaxTeamNameLength)
                {
                    throw new Exception($"Pipeline Name outside of character limit: Max = {MaxTeamNameLength}, Actual = {pipeline.Name.Length}, Name = {pipeline.Name}");
                }

                using (logger.BeginScope("Evaluate Pipeline Name = {0}, Path = {1}, Id = {2}", pipeline.Name, pipeline.Path, pipeline.Id))
                {
                    var parentTeam = await EnsureTeamExists(pipeline, "Notifications", TeamPurpose.ParentNotificationTeam, teams, persistChanges);
                    var childTeam = await EnsureTeamExists(pipeline, "Sync Notifications", TeamPurpose.SynchronizedNotificationTeam, teams, persistChanges);

                    if (!persistChanges && (parentTeam == default || childTeam == default))
                    {
                        // Skip team nesting and notification work if 
                        logger.LogInformation("Skipping Teams and Notifications because parent or child team does not exist");
                        continue;
                    }

                    await EnsureSynchronizedNotificationTeamIsChild(parentTeam, childTeam, persistChanges);
                    await EnsureScheduledBuildFailSubscriptionExists(pipeline, parentTeam, persistChanges);

                    // Associate 
                }
            }
        }

        private async Task<WebApiTeam> EnsureTeamExists(
            BuildDefinition pipeline,
            string suffix,
            TeamPurpose purpose,
            IEnumerable<WebApiTeam> teams, 
            bool persistChanges)
        {
            // Ensure team name fits within maximum 64 character limit
            // https://docs.microsoft.com/en-us/azure/devops/organizations/settings/naming-restrictions?view=azure-devops#teams
            string teamName = StringHelper.MaxLength($"{pipeline.Name} -- {suffix}", MaxTeamNameLength);
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
                logger.LogInformation("Team Not Found Suffix = {0}", suffix);
                var teamMetadata = new TeamMetadata
                {
                    PipelineId = pipeline.Id,
                    Purpose = purpose,
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
                }
            }

            return result;
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
