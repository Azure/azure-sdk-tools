using Azure.Sdk.Tools.PipelineWitness.Entities.AzurePipelines;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class FailureAnalyzer : IFailureAnalyzer
    {
        public FailureAnalyzer(IFailureClassifier[] classifiers)
        {
            this.classifiers = classifiers;
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
                failures.Add(new Failure("Global", "Unknown"));
            }

            return failures;
        }
    }
}
