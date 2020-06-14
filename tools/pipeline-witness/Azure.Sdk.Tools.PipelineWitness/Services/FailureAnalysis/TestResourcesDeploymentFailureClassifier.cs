using Microsoft.TeamFoundation.Build.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
   public class TestResourcesDeploymentFailureClassifier : IFailureClassifier
    {
        public async Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = from r in context.Timeline.Records
                              where r.Name.StartsWith("Deploy test resources")
                              where r.Result == TaskResult.Failed
                              select r;

            if (failedTasks.Count() > 0)
            {
                foreach (var failedTask in failedTasks)
                {
                    context.AddFailure(failedTask, "Test Resource Failure");
                }
            }
        }
    }
}
