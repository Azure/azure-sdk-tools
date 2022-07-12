namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    using Microsoft.TeamFoundation.Build.WebApi;
    using System.Linq;
    using System.Threading.Tasks;

    public class MavenBrokenPipeFailureClassifier : IFailureClassifier
    {
        private readonly BuildLogProvider buildLogProvider;
        
        public MavenBrokenPipeFailureClassifier(BuildLogProvider buildLogProvider)
        {
            this.buildLogProvider = buildLogProvider;
        }
        
        public async Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = context.Timeline.Records
                .Where(r => r.Result == TaskResult.Failed &&
                            r.RecordType == "Task" &&
                            r.Task?.Name == "Maven" &&
                            r.Log != null);

            foreach (var failedTask in failedTasks)
            {
                var lines = await buildLogProvider.GetLogLinesAsync(context.Build, failedTask.Log.Id);

                if (lines.Any(line => line.Contains("Connection reset") || line.Contains("Connection timed out") || line.Contains("504 Gateway Timeout")))
                {
                    context.AddFailure(failedTask, "Maven Broken Pipe");
                }
            }
        }
    }
}
