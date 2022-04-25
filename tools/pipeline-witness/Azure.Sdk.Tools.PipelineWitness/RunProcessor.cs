using System;
using System.Linq;
using System.Threading.Tasks;

using Azure.Cosmos;

using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

using Azure.Sdk.Tools.PipelineWitness.Entities.AzurePipelines;
using Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis;

namespace Azure.Sdk.Tools.PipelineWitness
{
    public class RunProcessor
    {
        public RunProcessor(
            IFailureAnalyzer failureAnalyzer,
            ILogger<RunProcessor> logger,
            CosmosClient cosmosClient,
            VssConnection vssConnection,
            BlobUploadProcessor blobUploadProcessor)
        {
            this.failureAnalyzer = failureAnalyzer ?? throw new ArgumentNullException(nameof(failureAnalyzer));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.blobUploadProcessor = blobUploadProcessor ?? throw new ArgumentNullException(nameof(blobUploadProcessor));
            this.cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
            this.vssConnection = vssConnection ?? throw new ArgumentNullException(nameof(vssConnection));
        }

        private const string Account = "azure-sdk";
        private readonly IFailureAnalyzer failureAnalyzer;
        private readonly ILogger<RunProcessor> logger;
        private readonly CosmosClient cosmosClient;
        private readonly VssConnection vssConnection;
        private readonly BlobUploadProcessor blobUploadProcessor;

        private bool IsValidAzureDevOpsUri(Uri uri)
        {
            // We want to throw if we start getting messages from other Azure DevOps
            // organizations since currently Pipeline Witness is designed to only work
            // against our instance.
            return uri.Host == "dev.azure.com" && uri.PathAndQuery.StartsWith($"/{Account}/");
        }

        public async Task ProcessRunAsync(Uri runUri)
        {
            if (!IsValidAzureDevOpsUri(runUri))
            {
                throw new ArgumentOutOfRangeException(
                    "Run URI does not point to the Azure SDK instance of Azure DevOps");
            }

            var runUriPath = runUri.AbsolutePath;
            var runUriPathSegments = runUri.PathAndQuery.Split("/");
            var projectGuid = Guid.Parse(runUriPathSegments[2]);
            var pipelineId = int.Parse(runUriPathSegments[5]);
            var runId = int.Parse(runUriPathSegments[7]);

            var buildClient = vssConnection.GetClient<BuildHttpClient>();
            var projectClient = vssConnection.GetClient<ProjectHttpClient>();

            var project = await projectClient.GetProject(projectGuid.ToString());

            // Avoid unhandled BuildNotFoundException by folding it into the following null check.
            Build build = null;

            try
            {
                build = await buildClient.GetBuildAsync(projectGuid, runId);
            }
            catch (BuildNotFoundException)
            {
            }

            if (build == null)
            {
                this.logger.LogWarning("Unable to process run due to missing build. Project: {Project}, BuildId: {BuildId}", project.Name, runId);
                return;
            }

            if (build.Deleted)
            {
                this.logger.LogInformation("Skipping deleted build. Project: {Project}, BuildId: {BuildId}", project.Name, runId);
                return;
            }

            if (build.StartTime == null || build.FinishTime == null)
            {
                this.logger.LogWarning("Skipping build with null start or finish time. Project: {Project}, BuildId: {BuildId}", project.Name, runId);
                return;
            }

            var pipeline = await buildClient.GetDefinitionAsync(project.Id, build.Definition.Id);
            var timeline = await buildClient.GetBuildTimelineAsync(projectGuid, runId);

            double agentDurationInSeconds = 0;
            double queueDurationInSeconds = 0;

            if (timeline != null)
            {
                agentDurationInSeconds = (from record in timeline.Records
                    where record.RecordType == "Task"
                    where (record.Name == "Initialize job" ||
                        record.Name == "Finalize Job") // Love consistency in capitalization!
                    group record by record.ParentId
                    into job
                    let jobStartTime = job.Min(jobRecord => jobRecord.StartTime)
                    let jobFinishTime = job.Max(jobRecord => jobRecord.FinishTime)
                    let agentDuration = jobFinishTime - jobStartTime
                    select agentDuration?.TotalSeconds ?? 0).Sum();

                queueDurationInSeconds = (from taskRecord in timeline.Records
                    where taskRecord.RecordType == "Task"
                    where taskRecord.Name == "Initialize job"
                    join jobRecord in timeline.Records on taskRecord.ParentId equals jobRecord.Id
                    let queueDuration = taskRecord.StartTime - jobRecord.StartTime
                    select queueDuration?.TotalSeconds ?? 0).Sum();
            }

            var run = new Run()
            {
                RunId = build.Id,
                RunName = build.BuildNumber,
                RunUrl = new Uri(((ReferenceLink)build.Links.Links["web"]).Href),
                PipelineId = pipeline.Id,
                PipelineName = pipeline.Name,
                PipelineUrl = new Uri(((ReferenceLink)pipeline.Links.Links["web"]).Href),
                ProjectId = build.Project.Id,
                ProjectName = build.Project.Name,
                ProjectUrl = new Uri(((ReferenceLink)project.Links.Links["web"]).Href),
                RepositoryId = build.Repository.Id,
                Reason = build.Reason switch
                {
                    BuildReason.Manual => RunReason.Manual,
                    BuildReason.IndividualCI => RunReason.ContinuousIntegration,
                    BuildReason.Schedule => RunReason.Scheduled,
                    BuildReason.PullRequest => RunReason.PullRequest,
                    _ => RunReason.Other
                },
                State = build.Status switch
                {
                    BuildStatus.None => RunStatus.None,
                    BuildStatus.Completed => RunStatus.Completed,
                    BuildStatus.Cancelling => RunStatus.Cancelling,
                    BuildStatus.InProgress => RunStatus.InProgress,
                    BuildStatus.NotStarted => RunStatus.NotStarted,
                    BuildStatus.Postponed => RunStatus.Postponed,
                    _ => RunStatus.None
                },
                Result = build.Result switch
                {
                    BuildResult.Canceled => RunResult.Cancelled,
                    BuildResult.Failed => RunResult.Failed,
                    BuildResult.None => RunResult.None,
                    BuildResult.PartiallySucceeded => RunResult.PartiallySucceded,
                    BuildResult.Succeeded => RunResult.Succeeded,
                    _ => RunResult.None
                },
                GitReference = build.SourceBranch,
                GitCommitSha = build.SourceVersion,
                StartTime = build.StartTime.Value,
                FinishTime = build.FinishTime.Value,
                AgentDurationInSeconds = agentDurationInSeconds,
                QueueDurationInSeconds = queueDurationInSeconds,
                Failures = await GetFailureClassificationsAsync(build, timeline)
            };

            var container = await GetItemContainerAsync("azure-pipelines-runs");
            await container.UpsertItemAsync(run);
        }

        public async Task<Failure[]> GetFailureClassificationsAsync(Build build, Timeline timeline)
        {
            // If there is no timeline, we can't analyze anything!
            if (timeline == null) return new Failure[0];

            var failures = await failureAnalyzer.AnalyzeFailureAsync(build, timeline);

            return failures.ToArray();
        }

        private async Task<CosmosContainer> GetItemContainerAsync(string containerName)
        {
            var database = cosmosClient.GetDatabase("records");
            var container = cosmosClient.GetContainer(database.Id, containerName);
            return container;
        }
    }
}
