using System.Linq;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class CodeSigningFailureClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = context.Timeline.Records
                .Where(r => r.Result == TaskResult.Failed)
                .Where(r => r.RecordType == "Task")
                .Where(r => r.Task?.Name == "EsrpCodeSigning")
                .Where(r => r.Log != null);

            foreach (var failedTask in failedTasks)
            {
                context.AddFailure(failedTask, "Code Signing");
            }

            return Task.CompletedTask;
        }
    }
}
