using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PipelineGenerator.Conventions
{
    public abstract class PipelineConvention
    {
        public PipelineConvention(ILogger logger, PipelineGenerationContext context)
        {
            Logger = logger;
            Context = context;
        }

        private const string ReportBuildStatusKey = "reportBuildStatus";
        private Dictionary<string, BuildDefinitionReference> pipelineReferences;

        protected ILogger Logger { get; }
        protected PipelineGenerationContext Context { get; }

        public abstract string SearchPattern { get; }

        protected abstract string GetDefinitionName(SdkComponent component);

        public async Task<BuildDefinition> DeleteDefinitionAsync(SdkComponent component, CancellationToken cancellationToken)
        {
            var definitionName = GetDefinitionName(component);

            Logger.LogDebug("Checking to see if definition '{0}' exists prior to deleting.", definitionName);
            var definition = await GetExistingDefinitionAsync(definitionName, cancellationToken);

            if (definition != null)
            {
                Logger.LogDebug("Found definition called '{0}' at '{1}'.", definitionName, definition.GetWebUrl());

                if (!Context.WhatIf)
                {
                    Logger.LogWarning("Deleting definition '{0}'.", definitionName);
                    var projectReference = await Context.GetProjectReferenceAsync(cancellationToken);
                    var buildClient = await Context.GetBuildHttpClientAsync(cancellationToken);
                    await buildClient.DeleteDefinitionAsync(
                        project: projectReference.Id,
                        definitionId: definition.Id,
                        cancellationToken: cancellationToken
                        );
                }
                else
                {
                    Logger.LogWarning("Skipping deleting definition '{0}' (--whatif).", definitionName);
                }

                return definition;
            }
            else
            {
                Logger.LogDebug("No definition called '{0}' existed.", definitionName);
                return null;
            }
        }

        public async Task<BuildDefinition> CreateOrUpdateDefinitionAsync(SdkComponent component, CancellationToken cancellationToken)
        {
            var definitionName = GetDefinitionName(component);

            Logger.LogDebug("Checking to see if definition '{0}' exists prior to create/update.", definitionName);
            var definition = await GetExistingDefinitionAsync(definitionName, cancellationToken);

            if (definition == null)
            {
                Logger.LogDebug("Definition '{0}' was not found.", definitionName);
                definition = await CreateDefinitionAsync(definitionName, component, cancellationToken);
            }

            Logger.LogDebug("Applying convention to '{0}' definition.", definitionName);
            var hasChanges = await ApplyConventionAsync(definition, component);

            if (hasChanges)
            {
                if (!Context.WhatIf)
                {
                    Logger.LogInformation("Convention had changes, updating '{0}' definition.", definitionName);
                    var buildClient = await Context.GetBuildHttpClientAsync(cancellationToken);
                    definition.Comment = "Updated by pipeline generation tool";
                    definition = await buildClient.UpdateDefinitionAsync(
                        definition: definition,
                        cancellationToken: cancellationToken
                        );
                }
                else
                {
                    Logger.LogWarning("Skipping update to definition '{0}' (--whatif).", definitionName);
                }
            }
            else
            {
                Logger.LogDebug("No changes for definition '{0}'.", definitionName);
            }

            return definition;
        }

        private async Task<BuildDefinition> GetExistingDefinitionAsync(string definitionName, CancellationToken cancellationToken)
        {
            Logger.LogDebug("Attempting to get existing definition '{0}'.", definitionName);
            var projectReference = await Context.GetProjectReferenceAsync(cancellationToken);
            var sourceRepository = await Context.GetSourceRepositoryAsync(cancellationToken);
            var buildClient = await Context.GetBuildHttpClientAsync(cancellationToken);

            if (pipelineReferences == default)
            {
                var definitionReferences = await buildClient.GetDefinitionsAsync(
                    project: projectReference.Id,
                    path: Context.DevOpsPath
                    );

                pipelineReferences = new Dictionary<string, BuildDefinitionReference>();
                foreach (var definition in definitionReferences)
                {
                    if (pipelineReferences.ContainsKey(definition.Name))
                    {
                        Logger.LogDebug($"Found more then one definition with name {definition.Name}, picking the first one {pipelineReferences[definition.Name].Id} and not {definition.Id}");
                    }
                    else
                    {
                        pipelineReferences.Add(definition.Name, definition);
                    }
                }
                Logger.LogDebug($"Cached {definitionReferences.Count} pipelines.");
            }

            BuildDefinitionReference definitionReference = null;
            pipelineReferences.TryGetValue(definitionName, out definitionReference);

            if (definitionReference != null)
            {
                Logger.LogDebug("Existing definition '{0}' found at '{1}'.", definitionName, definitionReference.GetWebUrl());
                return await buildClient.GetDefinitionAsync(
                        project: projectReference.Id,
                        definitionId: definitionReference.Id,
                        cancellationToken: cancellationToken
                        );
            }
            else
            {
                Logger.LogDebug("No definition named '{0}' was found.", definitionName);
                return null;
            }
        }

        private async Task<BuildDefinition> CreateDefinitionAsync(string definitionName, SdkComponent component, CancellationToken cancellationToken)
        {

            var sourceRepository = await Context.GetSourceRepositoryAsync(cancellationToken);

            var buildRepository = new BuildRepository()
            {
                DefaultBranch = Context.Branch,
                Id = sourceRepository.Id,
                Name = sourceRepository.FullName,
                Type = "GitHub",
                Url = new Uri(sourceRepository.Properties["cloneUrl"]),
            };

            buildRepository.Properties.AddRangeIfRangeNotNull(sourceRepository.Properties);

            var projectReference = await Context.GetProjectReferenceAsync(cancellationToken);
            var agentPoolQueue = await Context.GetAgentPoolQueue(cancellationToken);
            var normalizedRelativeYamlPath = component.RelativeYamlPath.Replace("\\", "/");

            var definition = new BuildDefinition()
            {
                Name = definitionName,
                Project = projectReference,
                Path = Context.DevOpsPath,
                Repository = buildRepository,
                Process = new YamlProcess()
                {
                    YamlFilename = normalizedRelativeYamlPath
                },
                Queue = agentPoolQueue
            };

            if (!Context.WhatIf)
            {
                Logger.LogDebug("Creating definition named '{0}'.", definitionName);

                var buildClient = await Context.GetBuildHttpClientAsync(cancellationToken);
                definition = await buildClient.CreateDefinitionAsync(
                    definition: definition,
                    cancellationToken: cancellationToken
                    );

                Logger.LogInformation("Created definition '{0}' at: {1}", definitionName, definition.GetWebUrl());
            }
            else
            {
                Logger.LogWarning("Skipping creating definition '{0}' (--whatif).", definitionName);
            }

            return definition;
        }

        protected bool EnsureVariableGroups(BuildDefinition definition)
        {
            var hasChanges = false;

            var definitionVariableGroupSet = definition.VariableGroups
                .Select(group => group.Id)
                .ToHashSet();

            var parameterGroupSet = this.Context.VariableGroups.ToHashSet();

            var idsToAdd = parameterGroupSet.Except(definitionVariableGroupSet);
            if (idsToAdd.Any())
            {
                hasChanges = true;
            }
            var groupsToAdd = idsToAdd.Select(id => new VariableGroup { Id = id });

            definition.VariableGroups.AddRange(groupsToAdd);

            return hasChanges;
        }

        private bool EnsureReportBuildStatus(BuildDefinition definition)
        {
            var hasChanges = false;

            if (definition.Repository.Properties.TryGetValue(ReportBuildStatusKey, out var reportBuildStatusString))
            {
                if (!bool.TryParse(reportBuildStatusString, out var reportBuildStatusValue) || !reportBuildStatusValue)
                {
                    definition.Repository.Properties[ReportBuildStatusKey] = "true";
                    hasChanges = true;
                }
            }
            else
            {
                definition.Repository.Properties.Add(ReportBuildStatusKey, "true");
                hasChanges = true;
            }

            return hasChanges;
        }

        protected const int FirstSchedulingHour = 0;
        protected const int LastSchedulingHour = 24;
        protected const int TotalHours = LastSchedulingHour - FirstSchedulingHour;
        protected const int TotalMinutes = TotalHours * 60;
        protected const int BucketSizeInMinutes = 15;
        protected const int TotalBuckets = TotalMinutes / BucketSizeInMinutes;
        protected const int BucketsPerHour = 60 / BucketSizeInMinutes;

        protected virtual Schedule CreateScheduleFromDefinition(BuildDefinition definition)
        {
            var bucket = definition.Id % TotalBuckets;
            var startHours = bucket / BucketsPerHour;
            var startMinutes = bucket % BucketsPerHour;

            var schedule = new Schedule
            {
                DaysToBuild = (ScheduleDays)31, // Schedule M-F
                ScheduleOnlyWithChanges = true,
                StartHours = FirstSchedulingHour + startHours,
                StartMinutes = startMinutes * BucketSizeInMinutes,
                TimeZoneId = "Pacific Standard Time",
            };
            schedule.BranchFilters.Add($"+{Context.Branch}");

            return schedule;
        }

        protected virtual Task<bool> ApplyConventionAsync(BuildDefinition definition, SdkComponent component)
        {
            bool hasChanges = false;

            if (EnsureVariableGroups(definition))
            {
                hasChanges = true;
            }

            if (EnsureReportBuildStatus(definition))
            {
                hasChanges = true;
            }

            if (definition.Path != this.Context.DevOpsPath)
            {
                definition.Path = this.Context.DevOpsPath;
                hasChanges = true;
            }

            if (definition.Repository.Properties.TryGetValue(ReportBuildStatusKey, out var reportBuildStatusString))
            {
                if (!bool.TryParse(reportBuildStatusString, out var reportBuildStatusValue) || !reportBuildStatusValue)
                {
                    definition.Repository.Properties[ReportBuildStatusKey] = "true";
                    hasChanges = true;
                }
            }
            else
            {
                definition.Repository.Properties.Add(ReportBuildStatusKey, "true");
                hasChanges = true;
            }

            return Task.FromResult(hasChanges);
        }

        protected bool EnsureDefautPullRequestTrigger(BuildDefinition definition, bool overrideYaml = true, bool securePipeline = true)
        {
            bool hasChanges = false;
            var prTriggers = definition.Triggers.OfType<PullRequestTrigger>();
            if (prTriggers == default || !prTriggers.Any())
            {
                var newTrigger = new PullRequestTrigger();

                if (overrideYaml)
                {
                    newTrigger.SettingsSourceType = 1; // Override what is in the yaml file and use what is in the pipeline definition
                    newTrigger.BranchFilters.Add("+*");
                }
                else
                {
                    newTrigger.SettingsSourceType = 2; // Pull settings from yaml
                }

                newTrigger.Forks = new Forks
                {
                    AllowSecrets = securePipeline,
                    Enabled = true
                };

                newTrigger.RequireCommentsForNonTeamMembersOnly = false;
                newTrigger.IsCommentRequiredForPullRequest = securePipeline;

                definition.Triggers.Add(newTrigger);
                hasChanges = true;
            }
            else
            {
                foreach (var trigger in prTriggers)
                {
                    if (overrideYaml)
                    {
                        if (trigger.SettingsSourceType != 1 ||
                            trigger.BranchFilters.Contains("+*"))
                        {
                            // Override what is in the yaml file and use what is in the pipeline definition
                            trigger.SettingsSourceType = 1;
                            trigger.BranchFilters.Add("+*");
                        }
                    }
                    else
                    {
                        if (trigger.SettingsSourceType != 2)
                        {
                            // Pull settings from yaml
                            trigger.SettingsSourceType = 2;
                            hasChanges = true;
                        }

                    }
                    if (trigger.RequireCommentsForNonTeamMembersOnly != false ||
                       trigger.Forks.AllowSecrets != securePipeline ||
                       trigger.Forks.Enabled != true ||
                       trigger.IsCommentRequiredForPullRequest != securePipeline
                       )
                    {
                        trigger.Forks.AllowSecrets = securePipeline;
                        trigger.Forks.Enabled = true;
                        trigger.RequireCommentsForNonTeamMembersOnly = false;
                        trigger.IsCommentRequiredForPullRequest = securePipeline;
   
                        hasChanges = true;
                    }
                }
            }
            return hasChanges;
        }

        protected bool EnsureDefaultScheduledTrigger(BuildDefinition definition)
        {
            bool hasChanges = false;
            var scheduleTriggers = definition.Triggers.OfType<ScheduleTrigger>();

            // Only add the schedule trigger if one doesn't exist. 
            if (scheduleTriggers == default || !scheduleTriggers.Any())
            {
                var computedSchedule = CreateScheduleFromDefinition(definition);

                definition.Triggers.Add(new ScheduleTrigger
                {
                    Schedules = new List<Schedule> { computedSchedule }
                });

                hasChanges = true;
            }
            return hasChanges;
        }

        protected bool EnsureDefaultCITrigger(BuildDefinition definition)
        {
            bool hasChanges = false;
            var ciTrigger = definition.Triggers.OfType<ContinuousIntegrationTrigger>().SingleOrDefault();
            if (ciTrigger == null)
            {
                definition.Triggers.Add(new ContinuousIntegrationTrigger()
                {
                    SettingsSourceType = 2 // Get CI trigger data from yaml file
                });
                hasChanges = true;
            }
            else
            {
                if (ciTrigger.SettingsSourceType != 2)
                {
                    ciTrigger.SettingsSourceType = 2;
                    hasChanges = true;
                }
            }
            return hasChanges;
        }
    }
}

