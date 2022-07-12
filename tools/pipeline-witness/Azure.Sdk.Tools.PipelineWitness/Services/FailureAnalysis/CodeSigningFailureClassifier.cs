namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.TeamFoundation.Build.WebApi;

    public class CodeSigningFailureClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = context.Timeline.Records
                .Where(r => r.Result == TaskResult.Failed &&
                            r.RecordType == "Task" &&
                            r.Task?.Name == "EsrpCodeSigning" &&
                            r.Log != null);

            foreach (var failedTask in failedTasks)
            {
                context.AddFailure(failedTask, "Code Signing");
            }

            return Task.CompletedTask;
        }
    }
}
