using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PipelineGenerator.Conventions
{
    public class UnifiedPipelineConvention : PipelineConvention
    {
        public UnifiedPipelineConvention(ILogger logger, PipelineGenerationContext context) : base(logger, context)
        {
        }

        protected override string GetDefinitionName(SdkComponent component)
        {
            return $"{Context.Prefix} - {component.Name}";
        }

        public override string SearchPattern => "ci.yml";

        protected override async Task<bool> ApplyConventionAsync(BuildDefinition definition, SdkComponent component)
        {
            // NOTE: Not happy with this code at all, I'm going to look for a reasonable
            // API that can do equality comparisons (without having to do all the checks myself).

            var hasChanges = await base.ApplyConventionAsync(definition, component);

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

            var ciTrigger = definition.Triggers.OfType<ContinuousIntegrationTrigger>().SingleOrDefault();

            if (ciTrigger == null)
            {
                definition.Triggers.Add(new ContinuousIntegrationTrigger()
                {
                    SettingsSourceType = 2 // HACK: This is editor invisible, but this is required to inherit branch filters from YAML file.
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

            var prTrigger = definition.Triggers.OfType<PullRequestTrigger>().SingleOrDefault();

            if (prTrigger == null)
            {
                definition.Triggers.Add(new PullRequestTrigger()
                {
                    SettingsSourceType = 1, // HACK: See above.
                    IsCommentRequiredForPullRequest = true,
                    BranchFilters = new List<string>()
                    {
                        $"+{Context.Branch}"
                    },
                    Forks = new Forks()
                    {
                        AllowSecrets = true,
                        Enabled = true
                    }
                });
                hasChanges = true;
            }
            else
            {
                if (prTrigger.SettingsSourceType != 2 ||
                    prTrigger.IsCommentRequiredForPullRequest != true ||
                    !prTrigger.BranchFilters.All(bf => bf == $"+{Context.Branch}") ||
                    prTrigger.Forks.AllowSecrets != true ||
                    prTrigger.Forks.Enabled != true)
                {
                    prTrigger.SettingsSourceType = 2;
                    prTrigger.IsCommentRequiredForPullRequest = true;
                    prTrigger.BranchFilters = new List<string>()
                    {
                        $"+{Context.Branch}"
                    };
                    prTrigger.Forks.AllowSecrets = true;
                    prTrigger.Forks.Enabled = true;
                    hasChanges = true;
                }
            }

            return hasChanges;
        }
    }
}
