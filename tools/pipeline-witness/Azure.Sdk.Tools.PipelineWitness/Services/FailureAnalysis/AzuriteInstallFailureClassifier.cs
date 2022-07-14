using Microsoft.TeamFoundation.Build.WebApi;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class AzuriteInstallFailureClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = context.Timeline.Records
                .Where(r => r.Result == TaskResult.Failed)
                .Where(r => r.RecordType == "Task")
                .Where(r => r.Name == "Install Azurite");

            foreach (var failedTask in failedTasks)
            {
                context.AddFailure(failedTask, "Azurite Install");
            }

            return Task.CompletedTask;
        }
    }
}
