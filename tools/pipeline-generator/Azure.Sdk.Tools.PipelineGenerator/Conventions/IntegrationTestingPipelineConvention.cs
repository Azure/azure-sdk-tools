using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PipelineGenerator.Conventions
{
    public class IntegrationTestingPipelineConvention : PipelineConvention
    {
        /// <summary>
        /// Key in repository properties dictionary for reporting build status
        /// </summary>
        private const string ReportBuildStatusKey = "reportBuildStatus";

        /// <summary>
        /// Start hour (3AM)
        /// </summary>
        private const int StartHourOffset = 3;

        /// <summary>
        /// Number of buckets for hour hashing
        /// </summary>
        private const int HourBuckets = 3;


        public override string SearchPattern => "tests.yml";

        public IntegrationTestingPipelineConvention(ILogger logger, PipelineGenerationContext context) : base(logger, context)
        {
        }

        protected override string GetDefinitionName(SdkComponent component)
        {
            return $"{Context.Prefix} - {component.Name} - tests";
        }

        protected override Task<bool> ApplyConventionAsync(BuildDefinition definition, SdkComponent component)
        {
            var hasChanges = false;

            // Ensure Path
            if (definition.Path != this.Context.DevOpsPath)
            {
                definition.Path = this.Context.DevOpsPath;
                hasChanges = true;
            }

            // Ensure Schedule Trigger
            var scheduleTriggers = definition.Triggers.OfType<ScheduleTrigger>();

            if (scheduleTriggers == default || !scheduleTriggers.Any())
            {
                var schedule = new Schedule
                {
                    DaysToBuild = ScheduleDays.All,
                    ScheduleOnlyWithChanges = false,
                    StartHours = StartHourOffset + HashBucket(definition.Name),
                    StartMinutes = 0,
                    TimeZoneId = "Pacific Standard Time",
                };
                schedule.BranchFilters.Add("+master");

                definition.Triggers.Add(new ScheduleTrigger
                {
                    Schedules = new List<Schedule> { schedule }
                });

                hasChanges = true;
            }

            // Ensure PR trigger
            var prTriggers = definition.Triggers.OfType<PullRequestTrigger>();
            if (prTriggers == default || !prTriggers.Any())
            {
                var newTrigger = GetDefaultPrTrigger();
                definition.Triggers.Add(newTrigger);
                hasChanges = true;
            }
            else
            {
                foreach (var trigger in prTriggers)
                {
                    if (EnsurePrTriggerDefaults(trigger))
                    {
                        hasChanges = true;
                    }
                }
            }

            // Ensure Variable Group
            if(EnsureVariableGroups(definition))
            {
                hasChanges = true;
            }

            // Ensure "Report Build Status" is set
            if (EnsureReportBuildStatus(definition))
            {
                hasChanges = true;
            }

            return Task.FromResult(hasChanges);
        }

        private int HashBucket(string pipelineName)
        {
            return pipelineName.GetHashCode() % HourBuckets;
        }

        private PullRequestTrigger GetDefaultPrTrigger()
        {
            var newTrigger = new PullRequestTrigger
            {
                Forks = new Forks { AllowSecrets = true, Enabled = true },
                RequireCommentsForNonTeamMembersOnly = false,
                IsCommentRequiredForPullRequest = true,
            };
            newTrigger.BranchFilters.Add("+master");

            return newTrigger;
        }

        private bool EnsurePrTriggerDefaults(PullRequestTrigger target)
        {
            var hasChanges = false;

            if (!target.Forks.AllowSecrets)
            {
                target.Forks.AllowSecrets = true;
                hasChanges = true;
            }

            if (!target.Forks.Enabled)
            {
                target.Forks.Enabled = true;
                hasChanges = true;
            }

            if (target.RequireCommentsForNonTeamMembersOnly)
            {
                target.RequireCommentsForNonTeamMembersOnly = false;
                hasChanges = true;
            }

            if (!target.IsCommentRequiredForPullRequest)
            {
                target.IsCommentRequiredForPullRequest = true;
                hasChanges = true;
            }

            return hasChanges;
        }

        private bool EnsureVariableGroups(BuildDefinition definition)
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
    }
}
