using System;
using Microsoft.TeamFoundation.Build.WebApi;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class AzurePowerShellModuleInstallationFailureClassifier : IFailureClassifier
    {
        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            if (context.Build.Definition.Name.EndsWith("- tests", StringComparison.InvariantCulture))
            {
                var failedTasks = context.Timeline.Records
                    .Where(r => r.Result == TaskResult.Failed)
                    .Where(r => r.RecordType == "Task")
                    .Where(r => r.Name == "Install Azure PowerShell module");

                foreach (var failedTask in failedTasks)
                {
                    context.AddFailure(failedTask, "Azure PS Module");
                }
            }

            return Task.CompletedTask;
        }
    }
}
