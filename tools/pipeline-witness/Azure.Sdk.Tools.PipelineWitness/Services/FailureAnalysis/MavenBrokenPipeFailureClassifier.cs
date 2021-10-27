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
        public MavenBrokenPipeFailureClassifier(BuildLogProvider buildLogProvider)
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
                                where r.Task.Name == "Maven"
                                where r.Log != null
                                select r;

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
