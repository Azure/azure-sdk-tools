using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class GitCheckoutFailureClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var tasks = context.Timeline.Records
                .Where(r => r.RecordType == "Task")
                .Where(r => r.Issues.Any(i => i.Message.Contains("Git fetch failed with exit code: 128")));

            foreach (var task in tasks)
            {
                context.AddFailure(task, "Git Checkout");
            }

            return Task.CompletedTask;
        }
    }
}
