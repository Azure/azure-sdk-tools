namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    using Azure.Sdk.Tools.PipelineWitness.Entities.AzurePipelines;
    using Microsoft.TeamFoundation.Build.WebApi;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class FailureAnalyzer : IFailureAnalyzer
    {
        private readonly IFailureClassifier[] classifiers;

        public FailureAnalyzer(IEnumerable<IFailureClassifier> classifiers)
        {
            this.classifiers = classifiers.ToArray();
        }

        public async Task<IReadOnlyCollection<Failure>> AnalyzeFailureAsync(Build build, Timeline timeline)
        {
            var failures = new List<Failure>();

            var context = new FailureAnalyzerContext(build, timeline, failures);
            
            foreach (var classifier in classifiers)
            {
                await classifier.ClassifyAsync(context);
            }

            if (failures.Count == 0 && 
                build.Result != BuildResult.Succeeded && 
                build.Result != BuildResult.Canceled)
            {
                var failedStageRecords = timeline.Records
                    .Where(record => record.ParentId == null && record.Result == TaskResult.Failed);
                
                failures.AddRange(failedStageRecords.Select(record => new Failure(record, "Unknown")));
            }

            return failures;
        }
    }
}
