using Microsoft.TeamFoundation.Build.WebApi;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class MavenBrokenPipeFailureClassifier : IFailureClassifier
    {
        private readonly BuildLogProvider _buildLogProvider;

        public MavenBrokenPipeFailureClassifier(BuildLogProvider buildLogProvider)
        {
            _buildLogProvider = buildLogProvider;
        }

        public async Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = context.Timeline.Records
                .Where(r => r.Result == TaskResult.Failed)
                .Where(r => r.RecordType == "Task")
                .Where(r => r.Task?.Name == "Maven")
                .Where(r => r.Log != null);

            foreach (var failedTask in failedTasks)
            {
                var lines = await _buildLogProvider.GetLogLinesAsync(context.Build, failedTask.Log.Id);

                if (lines.Any(line => line.Contains("Connection reset") || line.Contains("Connection timed out") || line.Contains("504 Gateway Timeout")))
                {
                    context.AddFailure(failedTask, "Maven Broken Pipe");
                }
            }
        }
    }
}
