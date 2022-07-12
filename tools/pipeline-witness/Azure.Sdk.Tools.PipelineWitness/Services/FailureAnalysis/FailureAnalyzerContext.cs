namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    using Azure.Sdk.Tools.PipelineWitness.Entities.AzurePipelines;
    using Microsoft.TeamFoundation.Build.WebApi;
    using System.Collections.Generic;

    public class FailureAnalyzerContext
    {
        private readonly IList<Failure> failures;

        public FailureAnalyzerContext(Build build, Timeline timeline, IList<Failure> failures)
        {
            Build = build;
            Timeline = timeline;
            this.failures = failures;
        }

        public Build Build { get; }
        public Timeline Timeline { get; }

        public void AddFailure(TimelineRecord record, string classification)
        {
            var failure = new Failure(record, classification);
            failures.Add(failure);
        }
    }
}
