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
    public class AzureArtifactsServiceUnavailableClassifier : IFailureClassifier
    {
        public AzureArtifactsServiceUnavailableClassifier(VssConnection vssConnection)
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
                                where r.Name == "Publish to Java Dev Feed"
                                where r.Log != null
                                select r;

            foreach (var failedTask in failedTasks)
            {
                var lines = await buildClient.GetBuildLogLinesAsync(
                    context.Build.Project.Id,
                    context.Build.Id,
                    failedTask.Log.Id
                    );

                if (lines.Any(line => line.Contains("Transfer failed for https://pkgs.dev.azure.com") && line.Contains("503 Service Unavailable")))
                {
                    context.AddFailure(failedTask, "Azure Artifacts Service Unavailable");
                }
            }
        }
    }
}
