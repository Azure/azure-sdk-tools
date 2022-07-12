namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    using Microsoft.TeamFoundation.Build.WebApi;
    using System.Linq;
    using System.Threading.Tasks;
    
    public class AzuriteInstallFailureClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = context.Timeline.Records
                .Where(r => r.Result == TaskResult.Failed &&
                            r.RecordType == "Task" &&
                            r.Name == "Install Azurite");

            foreach (var failedTask in failedTasks)
            {
                context.AddFailure(failedTask, "Azurite Install");
            }
            
            return Task.CompletedTask;
        }
    }
}
