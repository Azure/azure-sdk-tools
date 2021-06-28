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
    public class CacheChunkOrderingFailureClassifier : IFailureClassifier
    {
        public CacheChunkOrderingFailureClassifier(VssConnection vssConnection)
        {
            this.vssConnection = vssConnection;
            buildClient = vssConnection.GetClient<BuildHttpClient>();
        }

        private VssConnection vssConnection;
        private BuildHttpClient buildClient;

        private bool IsChunkOrderingError(string message)
        {
            return message.StartsWith("Chunks are not arriving in order or sizes are not matched up");
        }

        public async Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = from r in context.Timeline.Records
                                where r.Result == TaskResult.Failed
                                where r.RecordType == "Task"
                                where r.Task != null
                                where r.Task.Name == "Cache"
                                where r.Log != null
                                select r;

            foreach (var failedTask in failedTasks.Where(t => t.Issues.Any(i => IsChunkOrderingError(i.Message))))
            {
                context.AddFailure(failedTask, "Cache Chunk Ordering");
            }
        }
    }
}
