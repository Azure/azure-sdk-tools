namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.TeamFoundation.Build.WebApi;

    public class CosmosDbEmulatorStartFailureClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = context.Timeline.Records
                .Where(r => r.RecordType == "Task" &&
                            r.Name == "Start Cosmos DB Emulator" &&
                            r.Result == TaskResult.Failed);

            foreach (var failedTask in failedTasks)
            {
                context.AddFailure(failedTask, "Cosmos DB Emulator Failure");
            }
            
            return Task.CompletedTask;
        }
    }
}
