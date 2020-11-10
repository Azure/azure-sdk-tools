using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class CodeSigningFailureClassifier : IFailureClassifier
    {
        public async Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = from r in context.Timeline.Records
                              where r.Result == TaskResult.Failed
                              where r.RecordType == "Task"
                              where r.Task != null
                              where r.Task.Name == "EsrpCodeSigning"
                              where r.Log != null
                              select r;

            if (failedTasks.Count() > 0)
            {
                foreach (var failedTask in failedTasks)
                {
                    context.AddFailure(failedTask, "Code Signing");
                }
            }
        }
    }
}
