using Azure.Sdk.Tools.PipelineWitness.Entities.AzurePipelines;
using Microsoft.TeamFoundation.Build.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class AzurePowerShellModuleInstallationFailureClassifier : IFailureClassifier
    {
        public async Task ClassifyAsync(FailureAnalyzerContext context)
        {
            if (context.Build.Definition.Name.EndsWith("- tests"))
            {
                var failedTasks = from r in context.Timeline.Records
                                  where r.Result == TaskResult.Failed
                                  where r.RecordType == "Task"
                                  where r.Name == "Install Azure PowerShell module"
                                  select r;

                if (failedTasks.Count() > 0)
                {
                    foreach (var failedTask in failedTasks)
                    {
                        context.AddFailure(failedTask, "Azure PS Module");
                    }
                }
            }
        }
    }
}
