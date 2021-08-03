using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public class CacheFailureClassifier : IFailureClassifier
    {
        private class FailureClassifier
        {
            public readonly string MessageContains;
            public readonly string FailureName;

            public FailureClassifier(string messageContains, string failureName)
            {
                this.MessageContains = messageContains;
                this.FailureName = failureName;
            }

            public bool IsFailure(string message)
            {
                return message.StartsWith(MessageContains);
            }
        }

        private static readonly FailureClassifier[] failureClassifiers = new FailureClassifier[]
        {
            new FailureClassifier("Chunks are not arriving in order or sizes are not matched up", "Cache Chunk Ordering" ),
            new FailureClassifier("The task has timed out", "Cache Task Timeout" ),
            new FailureClassifier("Service Unavailable", "Cache Service Unavailable"),
            new FailureClassifier("The HTTP request timed out after", "Cache Service HTTP Timeout"),
            new FailureClassifier("Access to the path", "Cache Cannot Access Path"),
        };

        public CacheFailureClassifier(VssConnection vssConnection)
        {
            this.vssConnection = vssConnection;
            buildClient = vssConnection.GetClient<BuildHttpClient>();
        }

        private VssConnection vssConnection;
        private BuildHttpClient buildClient;

        public async Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = from r in context.Timeline.Records
                                where r.Result == TaskResult.Failed
                                where r.RecordType == "Task"
                                where r.Task != null
                                where r.Task.Name == "Cache"
                                where r.Log != null
                                select r;

            var classificationFound = false;
            foreach (var failedTask in failedTasks)
            {
                foreach (var classifier in failureClassifiers) {
                    if (failedTask.Issues.Any(i => classifier.IsFailure(i.Message)))
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
        }
    }
}
