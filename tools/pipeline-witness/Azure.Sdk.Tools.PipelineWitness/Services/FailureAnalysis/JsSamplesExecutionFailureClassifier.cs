using System;
using Microsoft.TeamFoundation.Build.WebApi;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class JsSamplesExecutionFailureClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            if (context.Build.Definition.Name.StartsWith("js -", StringComparison.InvariantCulture))
            {
                var failedTasks = context.Timeline.Records
                    .Where(r => r.Name == "Execute Samples")
                    .Where(r => r.Result == TaskResult.Failed);

                foreach (var failedTask in failedTasks)
                {
                    context.AddFailure(failedTask, "Sample Execution");
                }
            }

            return Task.CompletedTask;
        }
    }
}
