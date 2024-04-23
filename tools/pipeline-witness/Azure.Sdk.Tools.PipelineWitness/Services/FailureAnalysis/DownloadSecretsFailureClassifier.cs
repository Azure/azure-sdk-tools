using Microsoft.TeamFoundation.Build.WebApi;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class DownloadSecretsFailureClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = from r in context.Timeline.Records
                              where r.RecordType == "Task"
                              where r.Result == TaskResult.Failed
                              where r.Name.Contains("Download secrets")
                              select r;

            if (failedTasks.Count() > 0)
            {
                foreach (var failedTask in failedTasks)
                {
                    context.AddFailure(failedTask, "Secrets Failure");
                }
            }

            return Task.CompletedTask;
        }
    }
}
