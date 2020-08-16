using Microsoft.TeamFoundation.Build.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class JsDevFeedPublishingFailureClassifier : IFailureClassifier
    {
        public async Task ClassifyAsync(FailureAnalyzerContext context)
        {
            if (context.Build.Definition.Name.StartsWith("js -"))
            {
                var failedJobs = from r in context.Timeline.Records
                                 where r.Name == "Publish package to daily feed"
                                 where r.RecordType == "Job"
                                 where r.Result == TaskResult.Failed
                                 select r;

                if (failedJobs.Count() > 0)
                {
                    foreach (var failedJob in failedJobs)
                    {
                        context.AddFailure(failedJob, "Publish Failure");
                    }
                }
            }
        }
    }
}
