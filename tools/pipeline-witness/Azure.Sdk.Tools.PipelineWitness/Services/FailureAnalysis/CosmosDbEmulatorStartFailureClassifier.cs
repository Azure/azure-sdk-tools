using System.Linq;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class CosmosDbEmulatorStartFailureClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = context.Timeline.Records
                .Where(r => r.RecordType == "Task")
                .Where(r => r.Name == "Start Cosmos DB Emulator")
                .Where(r => r.Result == TaskResult.Failed);

            foreach (var failedTask in failedTasks)
            {
                context.AddFailure(failedTask, "Cosmos DB Emulator Failure");
            }

            return Task.CompletedTask;
        }
    }
}
