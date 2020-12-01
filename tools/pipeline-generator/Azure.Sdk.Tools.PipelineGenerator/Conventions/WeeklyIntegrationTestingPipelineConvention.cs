using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;

namespace PipelineGenerator.Conventions
{
    public class WeeklyIntegrationTestingPipelineConvention : IntegrationTestingPipelineConvention
    {
        public WeeklyIntegrationTestingPipelineConvention(ILogger logger, PipelineGenerationContext context) : base(logger, context)
        {
        }

        protected override string GetDefinitionName(SdkComponent component)
        {
            return $"{Context.Prefix} - {component.Name} - tests-weekly";
        }

        protected override Schedule CreateScheduleFromDefinition(BuildDefinition definition)
        {
            var bucket = definition.Id % TotalBuckets;
            var startHours = bucket / BucketsPerHour;
            var startMinutes = bucket % BucketsPerHour;

            var schedule = new Schedule
            {
                DaysToBuild = ScheduleDays.Saturday | ScheduleDays.Sunday,
                ScheduleOnlyWithChanges = true,
                StartHours = FirstSchedulingHour + startHours,
                StartMinutes = startMinutes * BucketSizeInMinutes,
                TimeZoneId = "Pacific Standard Time",
            };
            schedule.BranchFilters.Add("+master");

            return schedule;
        }
    }
}
