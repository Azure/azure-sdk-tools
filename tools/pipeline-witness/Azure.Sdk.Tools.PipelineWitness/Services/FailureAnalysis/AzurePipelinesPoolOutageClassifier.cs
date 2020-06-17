using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class AzurePipelinesPoolOutageClassifier : IFailureClassifier
    {
        public async Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var jobs = from r in context.Timeline.Records
                       where r.RecordType == "Job"
                       where r.Issues.Any(i => i.Message.Contains("abandoned due to an infrastructure failure"))
                       select r;

            if (jobs.Count() > 0)
            {
                foreach (var job in jobs)
                {
                    context.AddFailure(job, "Azure Pipelines Pool Outage");
                }
            }
        }
    }
}
