using Azure.Sdk.Tools.PipelineWitness.Entities.AzurePipelines;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class FailureAnalyzerContext
    {
        public FailureAnalyzerContext(Build build, Timeline timeline, IList<Failure> failures)
        {
            Build = build;
            Timeline = timeline;
            this.failures = failures;
        }

        public Build Build { get; private set; }
        public Timeline Timeline { get; private set; }

        private IList<Failure> failures;

        private string GetScope(TimelineRecord record)
        {
            var timelineStack = new Stack<TimelineRecord>();

            var current = record;
            while (true)
            {
                timelineStack.Push(current);

                var parent = Timeline.Records.Where(r => r.Id == current.ParentId).SingleOrDefault();
                if (parent == null)
                {
                    break;
                }
                else
                {
                    current = parent;
                }
            }

            var scopeBuilder = new StringBuilder();
            timelineStack.ForEach(r => scopeBuilder.Append($"/{r.RecordType}:{r.Name}"));

            var scope = scopeBuilder.ToString();
            return scope;
        }

        public void AddFailure(TimelineRecord record, string classification)
        {
            var failure = new Failure(record, classification);
            failures.Add(failure);
        }
    }
}
