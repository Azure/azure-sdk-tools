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
        public DnsResolutionFailureClassifier(BuildLogProvider buildLogProvider)
        {
            this.buildLogProvider = buildLogProvider;
        }

        private readonly BuildLogProvider buildLogProvider;

        private bool IsDnsResolutionFailure(string line)
        {
            return line.Contains("EAI_AGAIN", StringComparison.OrdinalIgnoreCase)
                || line.Contains("getaddrinfo", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Temporary failure in name resolution", StringComparison.OrdinalIgnoreCase)
                || line.Contains("No such host is known", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Couldn't resolve host name", StringComparison.OrdinalIgnoreCase);
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
                var lines = await buildLogProvider.GetLogLinesAsync(context.Build, failedTask.Log.Id);

                if (lines.Any(line => IsDnsResolutionFailure(line)))
                {
                    context.AddFailure(failedTask, "DNS Resolution Failure");
                }
            }
        }
    }
}
