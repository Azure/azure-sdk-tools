using System;
using Microsoft.TeamFoundation.Build.WebApi;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class TestResourcesDeploymentFailureClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = context.Timeline.Records
                .Where(r => r.Name.StartsWith("Deploy test resources", StringComparison.InvariantCulture))
                .Where(r => r.Result == TaskResult.Failed);

            foreach (var failedTask in failedTasks)
            {
                context.AddFailure(failedTask, "Test Resource Failure");
            }

            return Task.CompletedTask;
        }
    }
}
