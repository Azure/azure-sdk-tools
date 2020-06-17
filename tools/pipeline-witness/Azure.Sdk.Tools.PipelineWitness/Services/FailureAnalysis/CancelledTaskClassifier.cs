using Azure.Sdk.Tools.PipelineWitness.Entities.AzurePipelines;
using Microsoft.TeamFoundation.Build.WebApi;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class CancelledTaskClassifier : IFailureClassifier
    {
        public async Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var timedOutTestTasks = from r in context.Timeline.Records
                                    where r.RecordType == "Task"
                                    where r.Result == TaskResult.Canceled
                                    select r;

            if (timedOutTestTasks.Count() > 0)
            {
                foreach (var timedOutTestTask in timedOutTestTasks)
                {
                    context.AddFailure(timedOutTestTask, "Cancelled Task");
                }
            }
        }
    }
}
