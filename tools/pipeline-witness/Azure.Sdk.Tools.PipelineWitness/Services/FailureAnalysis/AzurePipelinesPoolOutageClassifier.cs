using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class AzurePipelinesPoolOutageClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var jobs = context.Timeline.Records
                .Where(r => r.RecordType == "Job")
                .Where(r => r.Issues.Any(i => i.Message.Contains("abandoned due to an infrastructure failure")));

            foreach (var job in jobs)
            {
                context.AddFailure(job, "Azure Pipelines Pool Outage");
            }

            return Task.CompletedTask;
        }
    }
}
