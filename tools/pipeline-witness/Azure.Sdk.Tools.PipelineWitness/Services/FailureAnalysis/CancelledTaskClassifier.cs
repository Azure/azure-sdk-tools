using Microsoft.TeamFoundation.Build.WebApi;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class CancelledTaskClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var timedOutTestTasks = context.Timeline.Records
                .Where(r => r.RecordType == "Task")
                .Where(r => r.Result == TaskResult.Canceled);

            foreach (var timedOutTestTask in timedOutTestTasks)
            {
                context.AddFailure(timedOutTestTask, "Cancelled Task");
            }

            return Task.CompletedTask;
        }
    }
}
