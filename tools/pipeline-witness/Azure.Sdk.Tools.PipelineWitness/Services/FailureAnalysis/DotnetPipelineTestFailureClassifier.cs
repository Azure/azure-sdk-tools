using Microsoft.TeamFoundation.Build.WebApi;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class DotnetPipelineTestFailureClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            if (context.Build.Definition.Name.StartsWith("net - "))
            {
                var failedTasks = from r in context.Timeline.Records
                                  where r.Result == TaskResult.Failed
                                  where r.RecordType == "Task"
                                  where r.Name.StartsWith("Build & Test")
                                  select r;

                if (failedTasks.Count() > 0)
                {
                    foreach (var failedTask in failedTasks)
                    {
                        context.AddFailure(failedTask, "Test Failure");
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}
