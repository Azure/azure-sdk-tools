namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    using Microsoft.TeamFoundation.Build.WebApi;
    using System.Linq;
    using System.Threading.Tasks;

    public class JsDevFeedPublishingFailureClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            if (context.Build.Definition.Name.StartsWith("js -"))
            {
                var failedJobs = context.Timeline.Records
                    .Where(r => r.Name == "Publish package to daily feed" && 
                                r.RecordType == "Job" &&
                                r.Result == TaskResult.Failed);

                foreach (var failedJob in failedJobs)
                {
                    context.AddFailure(failedJob, "Publish Failure");
                }
            }

            return Task.CompletedTask;
        }
    }
}
