using System;
using Microsoft.TeamFoundation.Build.WebApi;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class JsDevFeedPublishingFailureClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            if (context.Build.Definition.Name.StartsWith("js -", StringComparison.InvariantCulture))
            {
                var failedJobs = context.Timeline.Records
                    .Where(r => r.Name == "Publish package to daily feed")
                    .Where(r => r.RecordType == "Job")
                    .Where(r => r.Result == TaskResult.Failed);

                foreach (var failedJob in failedJobs)
                {
                    context.AddFailure(failedJob, "Publish Failure");
                }
            }

            return Task.CompletedTask;
        }
    }
}
