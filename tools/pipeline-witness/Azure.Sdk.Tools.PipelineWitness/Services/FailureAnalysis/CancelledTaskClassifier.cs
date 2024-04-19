using Microsoft.TeamFoundation.Build.WebApi;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class CancelledTaskClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var timedOutTestTasks = from r in context.Timeline.Records
                                    where r.RecordType == "Task"
                                    where r.Result == TaskResult.Canceled
                                    select r;

            if (timedOutTestTasks.Count() > 0)
            {
                foreach (var timedOutTestTask in timedOutTestTasks)
                {
                    context.AddFailure(timedOutTestTask, "Cancelled Task");
                }
            }

            return Task.CompletedTask;
        }
    }
}
