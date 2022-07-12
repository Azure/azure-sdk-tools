namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    using Microsoft.TeamFoundation.Build.WebApi;
    using System.Linq;
    using System.Threading.Tasks;

    public class JsSamplesExecutionFailureClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            if (context.Build.Definition.Name.StartsWith("js -"))
            {
                var failedTasks = context.Timeline.Records
                    .Where(r => r.Name == "Execute Samples" &&
                                r.Result == TaskResult.Failed);

                foreach (var failedTask in failedTasks)
                {
                    context.AddFailure(failedTask, "Sample Execution");
                }
            }
            
            return Task.CompletedTask;
        }
    }
}
