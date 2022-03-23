using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;

namespace PipelineGenerator.Conventions
{
    public class WeeklyIntegrationTestingPipelineConvention : IntegrationTestingPipelineConvention
    {
        public WeeklyIntegrationTestingPipelineConvention(ILogger logger, PipelineGenerationContext context) : base(logger, context)
        {
        }

        public override string PipelineNameSuffix => " - tests-weekly";
        public override string PipelineCategory => "tests-weekly";

        protected override Schedule CreateScheduleFromDefinition(BuildDefinition definition)
        {
            var bucket = definition.Id % TotalBuckets;
            var startHours = bucket / BucketsPerHour;
            var startMinutes = bucket % BucketsPerHour;
            var daysToBuild = new ScheduleDays[]{ ScheduleDays.Saturday, ScheduleDays.Sunday };
            var dayBucket = definition.Id % daysToBuild.Length;

            var schedule = new Schedule
            {
                DaysToBuild = daysToBuild[dayBucket],
                ScheduleOnlyWithChanges = true,
                StartHours = FirstSchedulingHour + startHours,
                StartMinutes = startMinutes * BucketSizeInMinutes,
                TimeZoneId = "Pacific Standard Time",
            };
            schedule.BranchFilters.Add($"+{Context.Branch}");

            return schedule;
        }
    }
}
