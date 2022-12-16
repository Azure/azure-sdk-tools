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
        public abstract string PipelineNameSuffix { get; }
        public abstract string PipelineCategory { get; }

        public string GetDefinitionName(SdkComponent component)
        {
            var baseName = component.Variant == null
                            ? $"{Context.Prefix} - {component.Name}"
                            : $"{Context.Prefix} - {component.Name} - {component.Variant}";
            return baseName + PipelineNameSuffix;
        }

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

            if (hasChanges || Context.OverwriteTriggers)
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
            var serviceEndpoint = await Context.GetServiceEndpointAsync(cancellationToken);

            var repository = Context.Repository;

            var buildRepository = new BuildRepository
            {
                DefaultBranch = Context.Branch,
                Id = repository,
                Name = repository,
                Type = "GitHub",
                Url = new Uri($"https://github.com/{repository}.git"),
                Properties = { ["connectedServiceId"] = serviceEndpoint.Id.ToString() }
            };

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

        protected bool EnsureManagedVariables(BuildDefinition definition, SdkComponent component)
        {
            var hasChanges = false;

            var managedVariables = new Dictionary<string, string>
            {
                { "meta.platform", this.Context.Prefix },
                { "meta.component", component.Name },
                { "meta.variant", component.Variant },
                { "meta.category", this.PipelineCategory },
                { "meta.autoGenerated", "true" },
            };

            foreach (var (key, value) in managedVariables)
            {
                if (string.IsNullOrEmpty(value))
                {
                    if (definition.Variables.ContainsKey(key))
                    {
                        Logger.LogInformation("Removing managed variable {Name}", key);
                        definition.Variables.Remove(key);
                        hasChanges = true;
                    }

                    // else: Nothing to do if an empty variable doesn't already exist.
                    continue;
                }

                if (definition.Variables.TryGetValue(key, out var existingVariable))
                {
                    if (existingVariable.Value == value && !existingVariable.AllowOverride && !existingVariable.IsSecret)
                    {
                        // nothing to do if an existing variable matches the new value and options
                        continue;
                    }

                    Logger.LogInformation("Overwriting managed variable {Name} from '{OriginalValue}' to '{NewValue}', not secret, not overridable", key, existingVariable.Value, value);
                }

                definition.Variables[key] = new BuildDefinitionVariable { Value = value, IsSecret = false, AllowOverride = false };
                hasChanges = true;
            }

            return hasChanges;
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

            if (Context.SetManagedVariables && EnsureManagedVariables(definition, component))
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

        protected bool EnsureDefaultPullRequestTrigger(BuildDefinition definition, bool overrideYaml = true, bool securePipeline = true)
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
                        // Override what is in the yaml file and use what is in the pipeline definition
                        if (trigger.SettingsSourceType != 1)
                        {
                            trigger.SettingsSourceType = 1;
                            hasChanges = true;
                        }

                        // If any branch filters exist then overwrite them to the most generous filter.
                        // The filter should support all branches because PR triggers with a yaml override
                        // like this are expected to be manually invoked by `/azp run` comments, and these PRs
                        // may be targeting development branches.
                        if (!trigger.BranchFilters.SequenceEqual(new List<string>{"+*"}))
                        {
                            var filters = trigger.BranchFilters.Select(f => $"'{f}'");
                            Logger.LogInformation($"Overwriting branch filters ({String.Join(", ", filters)}) for PR trigger with '+*'");
                            trigger.BranchFilters.Clear();
                            trigger.BranchFilters.Add("+*");
                            hasChanges = true;
                        }
                    }
                    else if (trigger.SettingsSourceType != 2)
                    {
                        // Pull settings from yaml
                        trigger.SettingsSourceType = 2;
                        hasChanges = true;
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
            if (scheduleTriggers == default || !scheduleTriggers.Any() || Context.OverwriteTriggers)
            {
                var computedSchedule = CreateScheduleFromDefinition(definition);

                definition.Triggers.RemoveAll(e => e is ScheduleTrigger);
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
            if (ciTrigger == null || Context.OverwriteTriggers)
            {
                definition.Triggers.RemoveAll(e => e is ContinuousIntegrationTrigger);
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

