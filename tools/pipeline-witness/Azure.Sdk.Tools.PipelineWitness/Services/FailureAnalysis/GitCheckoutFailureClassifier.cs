using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class GitCheckoutFailureClassifier : IFailureClassifier
    {
        public async Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var tasks = from r in context.Timeline.Records
                       where r.RecordType == "Task"
                       where r.Issues.Any(i => i.Message.Contains("Git fetch failed with exit code: 128"))
                       select r;

            if (tasks.Count() > 0)
            {
                foreach (var task in tasks)
                {
                    context.AddFailure(task
                        , "Git Checkout");
                }
            }
        }
    }
}
