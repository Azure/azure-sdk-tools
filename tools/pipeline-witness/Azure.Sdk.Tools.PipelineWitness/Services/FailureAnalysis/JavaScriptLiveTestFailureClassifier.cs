namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.TeamFoundation.Build.WebApi;

    public class JavaScriptLiveTestFailureClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var definitionName = context.Build.Definition.Name;
            
            if (definitionName.StartsWith("js - ") && definitionName.EndsWith(" - tests"))
            {
                var failedTasks = context.Timeline.Records
                    .Where(r => r.RecordType == "Task" &&
                                r.Name == "Integration test libraries" &&
                                r.Result == TaskResult.Failed);

                foreach (var failedTask in failedTasks)
                {
                    context.AddFailure(failedTask, "Test Failure");
                }
            }

            return Task.CompletedTask;
        }
    }
}
