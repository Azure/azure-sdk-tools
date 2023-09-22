using Azure.Sdk.Tools.PipelineWitness.Entities.AzurePipelines;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class FailureAnalyzer : IFailureAnalyzer
    {
        public FailureAnalyzer(IEnumerable<IFailureClassifier> classifiers)
        {
            this.classifiers = classifiers.ToArray();
        }

        private IFailureClassifier[] classifiers;

        public async Task<IEnumerable<Failure>> AnalyzeFailureAsync(Build build, Timeline timeline)
        {
            var failures = new List<Failure>();

            var context = new FailureAnalyzerContext(build, timeline, failures);
            foreach (var classifier in classifiers)
            {
                await classifier.ClassifyAsync(context);
            }

            if (failures.Count == 0)
            {
                if (build.Result != BuildResult.Succeeded && 
                    build.Result != BuildResult.Canceled)
                {
                    foreach (var record in timeline.Records.Where(x => x.ParentId.HasValue == false))
                    {
                        if (record.Result == TaskResult.Failed)
                        {
                            failures.Add(new Failure(record, "Unknown"));
                        }
                    }
                }
            }

            return failures;
        }
    }
}
