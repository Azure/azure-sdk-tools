namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    using Microsoft.TeamFoundation.Build.WebApi;
    using System.Linq;
    using System.Threading.Tasks;

    public class AzureArtifactsServiceUnavailableClassifier : IFailureClassifier
    {
        public AzureArtifactsServiceUnavailableClassifier(BuildLogProvider buildLogProvider)
        {
            this.buildLogProvider = buildLogProvider;
        }

        private readonly BuildLogProvider buildLogProvider;

        public async Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = context.Timeline.Records
                .Where(r => r.Result == TaskResult.Failed && 
                            r.RecordType == "Task" && 
                            r.Task != null &&
                            r.Name == "Publish to Java Dev Feed" && 
                            r.Log != null);

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
