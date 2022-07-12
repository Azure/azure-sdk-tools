namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    using Microsoft.TeamFoundation.Build.WebApi;
    using System.Linq;
    using System.Threading.Tasks;

    public class AzurePowerShellModuleInstallationFailureClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            if (context.Build.Definition.Name.EndsWith("- tests"))
            {
                var failedTasks = context.Timeline.Records
                    .Where(r => r.Result == TaskResult.Failed && 
                                r.RecordType == "Task" &&
                                r.Name == "Install Azure PowerShell module");

                foreach (var failedTask in failedTasks)
                {
                    context.AddFailure(failedTask, "Azure PS Module");
                }
            }
            
            return Task.CompletedTask;
        }
    }
}
