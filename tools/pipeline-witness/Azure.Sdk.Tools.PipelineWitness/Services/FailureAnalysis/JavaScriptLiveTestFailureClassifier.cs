using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class JavaScriptLiveTestFailureClassifier : IFailureClassifier
    {
        public async Task ClassifyAsync(FailureAnalyzerContext context)
        {
            if (context.Build.Definition.Name.StartsWith("js - ") && context.Build.Definition.Name.EndsWith(" - tests"))
            {
                var failedTasks = from r in context.Timeline.Records
                                  where r.RecordType == "Task"
                                  where r.Name == "Integration test libraries"
                                  where r.Result == TaskResult.Failed
                                  select r;

                if (failedTasks.Count() > 0)
                {
                    foreach (var failedTask in failedTasks)
                    {
                        context.AddFailure(failedTask, "Test Failure");
                    }
                }
            }
        }
    }
}
