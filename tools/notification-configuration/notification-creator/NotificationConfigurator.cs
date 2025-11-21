using common.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Notifications.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
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
        // A cache on team subscriptions to avoid redundant API calls
        private readonly Dictionary<Guid, IEnumerable<NotificationSubscription>> subscriptionsCache = new Dictionary<Guid, IEnumerable<NotificationSubscription>>();

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

            Console.WriteLine($"Found {pipelines.Count()} pipelines to process.");
            foreach (var pipeline in pipelines)
            {
                using (logger.BeginScope("Evaluate Pipeline: Name = {0}, Path = {1}, Id = {2}", pipeline.Name, pipeline.Path, pipeline.Id))
                {
                    await EnsureTeamExists(pipeline, teams, gitHubToAADConverter, persistChanges);
                }
            }
        }

        private async Task EnsureTeamExists(
            BuildDefinition pipeline,
            IEnumerable<WebApiTeam> teams,
            GitHubToAADConverter gitHubToAADConverter,
            bool persistChanges)
        {
            string teamName = $"{pipeline.Id} {pipeline.Name}";
            string teamDescription = $"Automatically generated team from CODEOWNERS to enable notifications for pipeline {pipeline.Id}: {pipeline.Name}";

            // Ensure team name fits within maximum 64 character limit
            // https://docs.microsoft.com/en-us/azure/devops/organizations/settings/naming-restrictions?view=azure-devops#teams
            if (teamName.Length > MaxTeamNameLength) {
                logger.LogWarning($"Notification team name (length {teamName.Length}) will be truncated to {teamName}");
            }
            teamName = StringHelper.MaxLength(teamName, MaxTeamNameLength);


            var result = teams.FirstOrDefault(team => team.Name == teamName);

            if (result == default)
            {
                logger.LogInformation($"Create Team for Pipeline PipelineId = {pipeline.Id} Name = '{teamName}'");
                var newTeam = new WebApiTeam
                {
                    Name = teamName,
                    Description = teamDescription
                };
                if (persistChanges)
                {
                    result = await service.CreateTeamForProjectAsync(pipeline.Project.Id.ToString(), newTeam);
                }
            }
            else
            {
                if (result.Description != teamDescription)
                {
                    logger.LogInformation($"Updating Team for Pipeline PipelineId = {pipeline.Id} Name = '{teamName}'");
                    result.Description = teamDescription;
                    if (persistChanges)
                    {
                        result = await service.UpdateTeamForProjectAsync(pipeline.Project.Id.ToString(), result);
                    }
                }
            }
            if (result != default)
            {
                await EnsureScheduledBuildFailSubscriptionExists(pipeline, result, persistChanges);
                await SyncTeamWithCodeownersFile(pipeline, result, gitHubToAADConverter, persistChanges);
            }
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

                // A. Contacts Processing: Apply Distinct() and filter null/empty contacts early
                var distinctContacts = contacts
                    .Where(contact => !string.IsNullOrEmpty(contact))
                    .Distinct()
                    .ToList();

                // Resolve contact descriptors with caching
                var contactsDescriptors = await ResolveContactDescriptorsAsync(distinctContacts, gitHubToAADConverter);

                // C. Set Delta Optimization: Filter out null descriptors before forming HashSet
                var contactsSet = new HashSet<string>(contactsDescriptors.Where(d => d != null));
                
                // Get set of team members in the DevOps teams
                var teamMembers = await service.GetMembersAsync(team);
                
                // B. Team Member Descriptor Retrieval: Try new batch method first
                var teamDescriptors = await GetTeamMemberDescriptorsAsync(team, teamMembers);
                
                // C. Set Delta Optimization: Filter out null descriptors
                var teamSet = new HashSet<string>(teamDescriptors.Where(d => d != null));
                
                // C. Set Delta Optimization: Early equality check - skip if sets are equal
                if (contactsSet.SetEquals(teamSet))
                {
                    logger.LogInformation("Team membership is already synchronized. No changes needed.");
                    return;
                }
                
                var contactsToRemove = teamSet.Except(contactsSet).ToList();
                var contactsToAdd = contactsSet.Except(teamSet).ToList();
                
                // Only get team descriptor if there are changes to make
                string teamDescriptor = "";
                if (contactsToRemove.Any() || contactsToAdd.Any())
                {
                    teamDescriptor = await service.GetDescriptorAsync(team.Id);
                }

                // E. Logging Adjustments: Aggregate information
                if (contactsToRemove.Any())
                {
                    logger.LogInformation("Removing {count} contact(s) from team", contactsToRemove.Count);
                }
                
                foreach (string descriptor in contactsToRemove)
                {
                    // F. Dry-Run Behavior: Skip external operations when not persisting
                    if (persistChanges)
                    {
                        logger.LogInformation("Delete Contact TeamDescriptor = {0}, ContactDescriptor = {1}", teamDescriptor, descriptor);
                        await service.RemoveMember(teamDescriptor, descriptor);
                    }
                    else
                    {
                        logger.LogInformation("Would delete Contact TeamDescriptor = {0}, ContactDescriptor = {1}", teamDescriptor, descriptor);
                    }
                }

                // E. Logging Adjustments: Aggregate information
                if (contactsToAdd.Any())
                {
                    logger.LogInformation("Adding {count} contact(s) to team", contactsToAdd.Count);
                }
                
                foreach (string descriptor in contactsToAdd)
                {
                    // F. Dry-Run Behavior: Skip external operations when not persisting
                    if (persistChanges)
                    {
                        logger.LogInformation("Add Contact TeamDescriptor = {0}, ContactDescriptor = {1}", teamDescriptor, descriptor);
                        await service.AddToTeamAsync(teamDescriptor, descriptor);
                    }
                    else
                    {
                        logger.LogInformation("Would add Contact TeamDescriptor = {0}, ContactDescriptor = {1}", teamDescriptor, descriptor);
                    }
                }
            }
        }

        /// <summary>
        /// Resolves contact GitHub handles to descriptors with caching
        /// </summary>
        private async Task<List<string>> ResolveContactDescriptorsAsync(
            List<string> contacts,
            GitHubToAADConverter gitHubToAADConverter)
        {
            var contactsDescriptors = new List<string>();
            
            foreach (string contact in contacts)
            {
                if (!contactsCache.ContainsKey(contact))
                {
                    // TODO: Batch method for future optimization: GetUserPrincipalNamesFromGithubAsync(IEnumerable<string> githubUsernames)
                    var userPrincipal = await gitHubToAADConverter.GetUserPrincipalNameFromGithubAsync(contact);
                    if (!string.IsNullOrEmpty(userPrincipal))
                    {
                        // TODO: Batch method for future optimization: GetDescriptorsForPrincipalsAsync(IEnumerable<string> principals)
                        contactsCache[contact] = await service.GetDescriptorForPrincipal(userPrincipal);
                    }
                    else
                    {
                        logger.LogInformation(
                            "Cannot find the user principal for GitHub contact '{contact}'",
                            contact);
                        // Cache null results to avoid re-requesting
                        contactsCache[contact] = null;
                    }
                }
                contactsDescriptors.Add(contactsCache[contact]);
            }
            
            return contactsDescriptors;
        }

        /// <summary>
        /// Gets team member descriptors, trying batch method first, falling back to individual calls
        /// </summary>
        private async Task<List<string>> GetTeamMemberDescriptorsAsync(WebApiTeam team, List<TeamMember> teamMembers)
        {
            var teamDescriptors = new List<string>();
            
            // B. Team Member Descriptor Retrieval: Try new batch method first
            var batchDescriptors = await service.GetMemberDescriptorsAsync(team);
            
            if (batchDescriptors != null && batchDescriptors.Any())
            {
                // Use batch results
                teamDescriptors.AddRange(batchDescriptors);
            }
            else
            {
                // Fallback to individual calls with caching
                foreach (var member in teamMembers)
                {
                    if (!teamMemberCache.ContainsKey(member.Identity.Id))
                    {
                        var teamMemberDescriptor = (await service.GetUserFromId(new Guid(member.Identity.Id))).SubjectDescriptor.ToString();
                        teamMemberCache[member.Identity.Id] = teamMemberDescriptor;
                    }
                    teamDescriptors.Add(teamMemberCache[member.Identity.Id]);
                }
            }
            
            return teamDescriptors;
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

        private async Task EnsureScheduledBuildFailSubscriptionExists(BuildDefinition pipeline, WebApiTeam team, bool persistChanges)
        {
            const string BuildFailureNotificationTag = "#AutomaticBuildFailureNotification";
            
            // D. Subscription Retrieval Optimization: Use cache to avoid redundant calls
            if (!subscriptionsCache.ContainsKey(team.Id))
            {
                subscriptionsCache[team.Id] = await service.GetSubscriptionsAsync(team.Id);
            }
            var subscriptions = subscriptionsCache[team.Id];

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
                
                // F. Dry-Run Behavior: Skip external operations when not persisting
                if (persistChanges)
                {
                    subscription = await service.CreateSubscriptionAsync(newSubscription);
                    // Invalidate cache after creating new subscription
                    subscriptionsCache.Remove(team.Id);
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

                    // F. Dry-Run Behavior: Skip external operations when not persisting
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
                        // Invalidate cache after updating subscription
                        subscriptionsCache.Remove(team.Id);
                    }
                }
            }
        }
    }
}
