using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Azure.Cosmos;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using Azure.Sdk.Tools.PipelineWitness.Entities.AzurePipelines;
using Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis;
using Azure.Sdk.Tools.PipelineWitness.Services;

namespace Azure.Sdk.Tools.PipelineWitness
{


    public class RunProcessor
    {
        public RunProcessor(IFailureAnalyzer failureAnalyzer, ILogger<RunProcessor> logger, BuildLogProvider logProvider, CosmosClient cosmosClient, BlobContainerClient containerClient, VssConnection vssConnection)
        {
            this.failureAnalyzer = failureAnalyzer;
            this.logger = logger;
            this.logProvider = logProvider;
            this.cosmosClient = cosmosClient;
            this.containerClient = containerClient;
            this.vssConnection = vssConnection;
        }

        private static readonly JsonSerializerSettings jsonSettings = new () { ContractResolver = new CamelCasePropertyNamesContractResolver(), Formatting = Formatting.None };
        private readonly IFailureAnalyzer failureAnalyzer;
        private readonly ILogger<RunProcessor> logger;
        private readonly BuildLogProvider logProvider;
        private readonly CosmosClient cosmosClient;
        private readonly BlobContainerClient containerClient;
        private readonly VssConnection vssConnection;

        private bool IsValidAzureDevOpsUri(Uri uri)
        {
            // We want to throw if we start getting messages from other Azure DevOps
            // organizations since currently Pipeline Witness is designed to only work
            // against our instance.
            return uri.Host == "dev.azure.com" && uri.PathAndQuery.StartsWith("/azure-sdk/");
        }

        public async Task ProcessRunAsync(Uri runUri)
        {
            if (!IsValidAzureDevOpsUri(runUri))
            {
                throw new ArgumentOutOfRangeException("Run URI does not point to the Azure SDK instance of Azure DevOps");
            }

            var runUriPath = runUri.AbsolutePath;
            var runUriPathSegments = runUri.PathAndQuery.Split("/");
            var projectGuid = Guid.Parse(runUriPathSegments[2]);
            var pipelineId = int.Parse(runUriPathSegments[5]);
            var runId = int.Parse(runUriPathSegments[7]);

            var buildClient = vssConnection.GetClient<BuildHttpClient>();
            var projectClient = vssConnection.GetClient<ProjectHttpClient>();

            var project = await projectClient.GetProject(projectGuid.ToString());
            var build = await buildClient.GetBuildAsync(projectGuid, runId);
            var pipeline = await buildClient.GetDefinitionAsync(project.Id, build.Definition.Id);
            var timeline = await buildClient.GetBuildTimelineAsync(projectGuid, runId);

            double agentDurationInSeconds = 0;
            double queueDurationInSeconds = 0;

            if (timeline != null)
            {
                agentDurationInSeconds = (from record in timeline.Records
                                            where record.RecordType == "Task"
                                            where (record.Name == "Initialize job" || record.Name == "Finalize Job") // Love consistency in capitalization!
                                            group record by record.ParentId into job
                                            let jobStartTime = job.Min(jobRecord => jobRecord.StartTime)
                                            let jobFinishTime = job.Max(jobRecord => jobRecord.FinishTime)
                                            let agentDuration = jobFinishTime - jobStartTime
                                            select agentDuration.Value.TotalSeconds).Sum();

                queueDurationInSeconds = (from taskRecord in timeline.Records
                                            where taskRecord.RecordType == "Task"
                                            where taskRecord.Name == "Initialize job"
                                            join jobRecord in timeline.Records on taskRecord.ParentId equals jobRecord.Id
                                            let queueDuration = taskRecord.StartTime - jobRecord.StartTime
                                            select queueDuration.Value.TotalSeconds).Sum();
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

            await UploadLogsBlobAsync(build, timeline);
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

        private async Task UploadLogsBlobAsync(Build build, Timeline timeline)
        {
            if (timeline.Records == null)
            {
                return;
            }

            const string timeFormat = @"yyyy-MM-dd\THH:mm:ss.fffffff\Z";

            var hasErrors = false;

            await using var messagesWriter = new StringWriter();
            foreach(var record in timeline.Records)
            {
                var logLines = await this.logProvider.GetTimelineRecordLogsAsync(build, record);

                var lastTimeStamp = record.StartTime;

                for(var lineNumber = 1; lineNumber <= logLines.Count; lineNumber++)
                {
                    var line = logLines[lineNumber - 1];
                    var match = Regex.Match(line, @"^([^Z]{20,28}Z) (.*)$");
                    var timestamp = match.Success
                        ? DateTime.ParseExact(match.Groups[1].Value, timeFormat, null, System.Globalization.DateTimeStyles.AssumeUniversal).ToUniversalTime()
                        : lastTimeStamp;
                        
                    var message = match.Success ? match.Groups[2].Value : line;

                    if (timestamp == null)
                    {
                        messagesWriter.WriteLine("ERROR: NO LEADING TIMESTAMP");
                        hasErrors = true;
                    }

                    await messagesWriter.WriteLineAsync(JsonConvert.SerializeObject(new
                    {
                        OrganizationName = "azure-sdk", 
                        ProjectId = build.Project.Id, 
                        BuildId = build.Id, 
                        BuildDefinitionId = build.Definition.Id, 
                        LogId = record.Log.Id, 
                        LineNumber = lineNumber, 
                        Length = message.Length,
                        Timestamp = timestamp?.ToString(timeFormat),
                        Message = message,
                        EtlIngestDate = DateTime.UtcNow.ToString(timeFormat),
                    }, jsonSettings));
                    
                    lastTimeStamp = timestamp;
                }
            }

            var blobPathPrefix = $"{build.QueueTime:yyyy/MM/dd}/{build.Project.Name}-{build.Id}";
            var blobClient = containerClient.GetBlobClient($"/success/{blobPathPrefix}.jsonl");

            if (hasErrors)
            {
                var attempt = 1;

                do
                {
                    blobClient = containerClient.GetBlobClient($"/error/{blobPathPrefix}-{attempt++}.jsonl");
                }
                while (await blobClient.ExistsAsync());
            }
            
            await blobClient.UploadAsync(new BinaryData(messagesWriter.ToString()));
        }
    }
}
