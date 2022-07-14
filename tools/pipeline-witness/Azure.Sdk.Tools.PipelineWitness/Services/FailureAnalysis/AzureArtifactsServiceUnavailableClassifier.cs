using Microsoft.TeamFoundation.Build.WebApi;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class AzureArtifactsServiceUnavailableClassifier : IFailureClassifier
    {
        public AzureArtifactsServiceUnavailableClassifier(BuildLogProvider buildLogProvider)
        {
            _buildLogProvider = buildLogProvider;
        }

        private readonly BuildLogProvider _buildLogProvider;

        public async Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = context.Timeline.Records
                .Where(r => r.Result == TaskResult.Failed)
                .Where(r => r.RecordType == "Task")
                .Where(r => r.Task != null)
                .Where(r => r.Name == "Publish to Java Dev Feed")
                .Where(r => r.Log != null);

            foreach (var failedTask in failedTasks)
            {
                var lines = await _buildLogProvider.GetLogLinesAsync(context.Build, failedTask.Log.Id);

                if (lines.Any(line => line.Contains("Transfer failed for https://pkgs.dev.azure.com") && line.Contains("503 Service Unavailable")))
                {
                    context.AddFailure(failedTask, "Azure Artifacts Service Unavailable");
                }
            }
        }
    }
}
