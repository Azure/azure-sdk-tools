using Azure.Sdk.Tools.PipelineWitness.Entities.AzurePipelines;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class MavenBrokenPipeFailureClassifier : IFailureClassifier
    {
        public MavenBrokenPipeFailureClassifier(VssConnection vssConnection)
        {
            this.vssConnection = vssConnection;
            buildClient = vssConnection.GetClient<BuildHttpClient>();
        }

        private VssConnection vssConnection;
        private BuildHttpClient buildClient;

        public async Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = from r in context.Timeline.Records
                                where r.Result == TaskResult.Failed
                                where r.RecordType == "Task"
                                where r.Task != null
                                where r.Task.Name == "Maven"
                                where r.Log != null
                                select r;

            foreach (var failedTask in failedTasks)
            {
                var lines = await buildClient.GetBuildLogLinesAsync(
                    context.Build.Project.Id,
                    context.Build.Id,
                    failedTask.Log.Id
                    );

                if (lines.Any(line => line.Contains("Connection reset") || line.Contains("Connection timed out") || line.Contains("504 Gateway Timeout")))
                {
                    context.AddFailure(failedTask, "Maven Broken Pipe");
                }
            }
        }
    }
}
