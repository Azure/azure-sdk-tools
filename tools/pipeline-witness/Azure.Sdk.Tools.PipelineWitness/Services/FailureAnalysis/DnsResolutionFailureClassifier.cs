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
    public class DnsResolutionFailureClassifier : IFailureClassifier
    {
        public DnsResolutionFailureClassifier(VssConnection vssConnection)
        {
            this.vssConnection = vssConnection;
            buildClient = vssConnection.GetClient<BuildHttpClient>();
        }

        private VssConnection vssConnection;
        private BuildHttpClient buildClient;

        private bool IsDnsResolutionFailure(string line)
        {
            return (line.Contains("EAI_AGAIN") && line.Contains("getaddrinfo"))
                || line.Contains("getaddrinfo EAI_AGAIN")
                || line.Contains("Temporary failure in name resolution")
                || line.Contains("No such host is known.");
        }

        public async Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = from r in context.Timeline.Records
                                where r.Result == TaskResult.Failed
                                where r.RecordType == "Task"
                                where r.Log != null
                                select r;

            foreach (var failedTask in failedTasks)
            {
                var lines = await buildClient.GetBuildLogLinesAsync(
                    context.Build.Project.Id,
                    context.Build.Id,
                    failedTask.Log.Id
                    );

                if (lines.Any(line => IsDnsResolutionFailure(line)))
                {
                    context.AddFailure(failedTask, "DNS Resolution Failure");
                }
            }
        }
    }
}
