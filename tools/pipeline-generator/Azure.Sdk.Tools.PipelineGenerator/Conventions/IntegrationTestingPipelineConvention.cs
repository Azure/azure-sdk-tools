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
        public override string SearchPattern => "tests.yml";

        public IntegrationTestingPipelineConvention(ILogger logger, PipelineGenerationContext context) : base(logger, context)
        {
        }

        protected override string GetDefinitionName(SdkComponent component)
        {
            return $"{Context.Prefix} - {component.Name} - tests";
        }

        protected override async Task<bool> ApplyConventionAsync(BuildDefinition definition, SdkComponent component)
        {
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

            return hasChanges;
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
    }
}
