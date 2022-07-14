using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class JavaScriptLiveTestFailureClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var definitionName = context.Build.Definition.Name;

            if (definitionName.StartsWith("js - ", StringComparison.InvariantCulture) &&
                definitionName.EndsWith(" - tests", StringComparison.InvariantCulture))
            {
                var failedTasks = context.Timeline.Records
                    .Where(r => r.RecordType == "Task")
                    .Where(r => r.Name == "Integration test libraries")
                    .Where(r => r.Result == TaskResult.Failed);

                foreach (var failedTask in failedTasks)
                {
                    context.AddFailure(failedTask, "Test Failure");
                }
            }

            return Task.CompletedTask;
        }
    }
}
