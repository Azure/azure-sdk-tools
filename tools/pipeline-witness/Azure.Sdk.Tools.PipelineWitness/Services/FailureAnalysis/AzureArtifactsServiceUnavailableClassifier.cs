using Microsoft.TeamFoundation.Build.WebApi;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class AzureArtifactsServiceUnavailableClassifier : IFailureClassifier
    {
        public AzureArtifactsServiceUnavailableClassifier(BuildLogProvider buildLogProvider)
        {
            this.buildLogProvider = buildLogProvider;
        }

        private readonly BuildLogProvider buildLogProvider;

        public async Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = from r in context.Timeline.Records
                                where r.Result == TaskResult.Failed
                                where r.RecordType == "Task"
                                where r.Task != null
                                where r.Name == "Publish to Java Dev Feed"
                                where r.Log != null
                                select r;

            foreach (var failedTask in failedTasks)
            {
                var lines = await this.buildLogProvider.GetLogLinesAsync(context.Build, failedTask.Log.Id);

                if (lines.Any(line => line.Contains("Transfer failed for https://pkgs.dev.azure.com") && line.Contains("503 Service Unavailable")))
                {
                    context.AddFailure(failedTask, "Azure Artifacts Service Unavailable");
                }
            }
        }
    }
}
