namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    using Microsoft.TeamFoundation.Build.WebApi;
    using System.Linq;
    using System.Threading.Tasks;

    public class DotnetPipelineTestFailureClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            if (context.Build.Definition.Name.StartsWith("net - "))
            {
                var failedTasks = context.Timeline.Records
                    .Where(r => r.Result == TaskResult.Failed && 
                                r.RecordType == "Task" && 
                                r.Name.StartsWith("Build & Test"));

                foreach (var failedTask in failedTasks)
                {
                    context.AddFailure(failedTask, "Test Failure");
                }
            }

            return Task.CompletedTask;
        }
    }
}
