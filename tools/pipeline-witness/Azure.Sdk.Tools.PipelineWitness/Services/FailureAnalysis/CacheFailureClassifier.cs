using System;
using Microsoft.TeamFoundation.Build.WebApi;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class CacheFailureClassifier : IFailureClassifier
    {
        private static readonly (string MessagePrefix, string FailureName)[] s_failureClassifiers =
        {
            ("Chunks are not arriving in order or sizes are not matched up", "Cache Chunk Ordering" ),
            ("The task has timed out", "Cache Task Timeout" ),
            ("Service Unavailable", "Cache Service Unavailable"),
            ("The HTTP request timed out after", "Cache Service HTTP Timeout"),
            ("Access to the path", "Cache Cannot Access Path"),
        };

        public Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = context.Timeline.Records
                .Where(r => r.Result == TaskResult.Failed)
                .Where(r => r.RecordType == "Task")
                .Where(r => r.Task?.Name == "Cache")
                .Where(r => r.Log != null);

            var classificationFound = false;

            foreach (var failedTask in failedTasks)
            {
                foreach (var classifier in s_failureClassifiers)
                {
                    if (failedTask.Issues.Any(i => i.Message.StartsWith(classifier.MessagePrefix, StringComparison.InvariantCulture)))
                    {
                        context.AddFailure(failedTask, classifier.FailureName);
                        classificationFound = true;
                    }
                }

                if (!classificationFound)
                {
                    context.AddFailure(failedTask, "Cache Failure Other");
                }
            }

            return Task.CompletedTask;
        }
    }
}
