namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    using Microsoft.TeamFoundation.Build.WebApi;
    using System.Linq;
    using System.Threading.Tasks;

    public class DownloadSecretsFailureClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = context.Timeline.Records
                .Where(r => r.RecordType == "Task" && 
                            r.Result == TaskResult.Failed && 
                            r.Name.Contains("Download secrets"));

            foreach (var failedTask in failedTasks)
            {
                context.AddFailure(failedTask, "Secrets Failure");
            }
            
            return Task.CompletedTask;
        }
    }
}
