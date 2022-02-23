namespace Azure.Sdk.Tools.PipelineWitness
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using Azure.Sdk.Tools.PipelineWitness.Services;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;

    using Microsoft.Extensions.Logging;
    using Microsoft.TeamFoundation.Build.WebApi;
    using Microsoft.TeamFoundation.TestManagement.WebApi;
    using Microsoft.VisualStudio.Services.TestResults.WebApi;
    using Microsoft.WindowsAzure.Storage;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Serialization;

    public class BlobUploadProcessor
    {
        private const string BuildsContainerName = "builds";
        private const string BuildLogLinesContainerName = "buildloglines";
        private const string BuildTimelineRecordsContainerName = "buildtimelinerecords";
        private const string TestRunsContainerNameTestRunsContainerName = "testruns";

        private const string TimeFormat = @"yyyy-MM-dd\THH:mm:ss.fffffff\Z";
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = { new StringEnumConverter(new CamelCaseNamingStrategy()) },
            Formatting = Formatting.None,
        };

        private readonly ILogger<BlobUploadProcessor> logger;
        private readonly BuildLogProvider logProvider;
        private readonly TestResultsHttpClient testResultsClient;
        private readonly BuildHttpClient buildClient;
        private readonly BlobContainerClient buildLogLinesContainerClient;
        private readonly BlobContainerClient buildsContainerClient;
        private readonly BlobContainerClient buildTimelineRecordsContainerClient;
        private readonly BlobContainerClient testRunsContainerClient;

        public BlobUploadProcessor(ILogger<BlobUploadProcessor> logger, BuildLogProvider logProvider, BlobServiceClient blobServiceClient, BuildHttpClient buildClient, TestResultsHttpClient testResultsClient)
        {
            if (blobServiceClient == null)
            {
                throw new ArgumentNullException(nameof(blobServiceClient));
            }

            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.logProvider = logProvider ?? throw new ArgumentNullException(nameof(logProvider));
            this.buildClient = buildClient ?? throw new ArgumentNullException(nameof(buildClient));
            this.testResultsClient = testResultsClient ?? throw new ArgumentNullException(nameof(testResultsClient));
            this.buildsContainerClient = blobServiceClient.GetBlobContainerClient(BuildsContainerName);
            this.buildTimelineRecordsContainerClient = blobServiceClient.GetBlobContainerClient(BuildTimelineRecordsContainerName);
            this.buildLogLinesContainerClient = blobServiceClient.GetBlobContainerClient(BuildLogLinesContainerName);
            this.testRunsContainerClient = blobServiceClient.GetBlobContainerClient(TestRunsContainerNameTestRunsContainerName);
        }

        public async Task UploadBuildBlobsAsync(
            string account,
            Build build,
            Timeline timeline)
        {
            if (build == null)
            {
                throw new ArgumentNullException(nameof(build));
            }

            if (timeline == null)
            {
                throw new ArgumentNullException(nameof(timeline));
            }

            if (build.Project == null)
            {
                throw new ArgumentException("build.Project is null", nameof(build));
            }

            if (build.Definition == null)
            {
                throw new ArgumentException("build.Definition is null", nameof(build));
            }

            // QueueTime is used in blob paths and cannot be null
            if (build.QueueTime == null)
            {
                throw new ArgumentException("build.QueueTime is null", nameof(build));
            }

            // FinishTime is used in blob paths and cannot be null
            if (build.FinishTime == null)
            {
                throw new ArgumentException("build.FinishTime is null", nameof(build));
            }

            // Project name is used in blob paths and cannot be empty
            if (string.IsNullOrWhiteSpace(build.Project.Name))
            {
                throw new ArgumentException("build.Project.Name is null or whitespace", nameof(build));
            }

            await UploadBuildBlobAsync(account, build);

            await UploadTestRunBlobsAsync(account, build);

            await UploadTimelineBlobAsync(account, build, timeline);

            var logs = await buildClient.GetBuildLogsAsync(build.Project.Id, build.Id);

            foreach (var log in logs)
            {
                await UploadLogLinesBlobAsync(account, build, log);
            }
        }

        private async Task UploadBuildBlobAsync(string account, Build build)
        {
            try
            {
                var blobPath = $"{build.Project.Name}/{build.FinishTime:yyyy/MM/dd}/{build.Id}.jsonl";
                var blobClient = this.buildsContainerClient.GetBlobClient(blobPath);

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing build blob for build {BuildId}", build.Id);
                    return;
                }

                var content = JsonConvert.SerializeObject(new
                {
                    OrganizationName = account,
                    ProjectId = build.Project?.Id,
                    BuildId = build.Id,
                    DefinitionId = build.Definition?.Id,
                    RepositoryId = build.Repository?.Id,
                    BuildNumber = build.BuildNumber,
                    BuildNumberRevision = build.BuildNumberRevision,
                    DefinitionName = build.Definition?.Name,
                    DefinitionPath = build.Definition?.Path,
                    DefinitionProjectId = build.Definition?.Project?.Id,
                    DefinitionProjectName = build.Definition?.Project?.Name,
                    DefinitionProjectRevision = build.Definition?.Project?.Revision,
                    DefinitionProjectState = build.Definition?.Project?.State,
                    DefinitionRevision = build.Definition?.Revision,
                    DefinitionType = build.Definition?.Type,
                    QueueTime = build.QueueTime,
                    StartTime = build.StartTime,
                    FinishTime = build.FinishTime,
                    KeepForever = build.KeepForever,
                    LastChangedByDisplayName = build.LastChangedBy?.DisplayName,
                    LastChangedById = build.LastChangedBy?.Id,
                    LastChangedByIsContainer = build.LastChangedBy?.IsContainer,
                    LastChangedByUniqueName = build.LastChangedBy?.UniqueName,
                    LastChangedDate = build.LastChangedDate,
                    LogsId = build.Logs?.Id,
                    LogsType = build.Logs?.Type,
                    OrchestrationPlanId = build.OrchestrationPlan?.PlanId,
                    Parameters = build.Parameters,
                    PlanId = build.Plans?.FirstOrDefault()?.PlanId,
                    Priority = build.Priority,
                    ProjectName = build.Project?.Name,
                    ProjectRevision = build.Project?.Revision,
                    ProjectState = build.Project?.State,
                    QueueId = build.Queue?.Id,
                    QueueName = build.Queue?.Name,
                    QueuePoolId = build.Queue?.Pool?.Id,
                    QueuePoolName = build.Queue?.Pool?.Name,
                    Reason = build.Reason,
                    RepositoryCheckoutSubmodules = build.Repository?.CheckoutSubmodules,
                    RepositoryType = build.Repository?.Type,
                    RequestedByDisplayName = build.RequestedBy?.DisplayName,
                    RequestedById = build.RequestedBy?.Id,
                    RequestedByIsContainer = build.RequestedBy?.IsContainer,
                    RequestedByUniqueName = build.RequestedBy?.UniqueName,
                    RequestedForDisplayName = build.RequestedFor?.DisplayName,
                    RequestedForId = build.RequestedFor?.Id,
                    RequestedForIsContainer = build.RequestedFor?.IsContainer,
                    RequestedForUniqueName = build.RequestedFor?.UniqueName,
                    Result = build.Result,
                    RetainedByRelease = build.RetainedByRelease,
                    SourceBranch = build.SourceBranch,
                    SourceVersion = build.SourceVersion,
                    Status = build.Status,
                    Tags = build.Tags?.Any() == true ? JsonConvert.SerializeObject(build.Tags, jsonSettings) : null,
                    Uri = build.Uri,
                    ValidationResults = build.ValidationResults,
                    Data = JsonConvert.SerializeObject(build, jsonSettings),
                    EtlIngestDate = DateTime.UtcNow,
                }, jsonSettings);

                await blobClient.UploadAsync(new BinaryData(content));
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                this.logger.LogInformation("Ignoring exception from existing blob for build {BuildId}", build.Id);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error processing build blob for build {BuildId}", build.Id);
                throw;
            }
        }

        private async Task UploadTimelineBlobAsync(string account, Build build, Timeline timeline)
        {
            try
            {
                if (timeline.Records == null)
                {
                    this.logger.LogInformation("Skipping timeline with null Records property for build {BuildId}", build.Id);
                    return;
                }

                var blobPath = $"{build.Project.Name}/{build.FinishTime:yyyy/MM/dd}/{build.Id}-{timeline.ChangeId}.jsonl";
                var blobClient = this.buildTimelineRecordsContainerClient.GetBlobClient(blobPath);

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing timeline for build {BuildId}, change {ChangeId}", build.Id, timeline.ChangeId);
                    return;
                }

                var builder = new StringBuilder();
                foreach (var record in timeline.Records)
                {
                    builder.AppendLine(JsonConvert.SerializeObject(
                        new
                        {
                            OrganizationName = account,
                            ProjectId = build.Project?.Id,
                            ProjectName = build.Project?.Name,
                            BuildDefinitionId = build.Definition?.Id,
                            BuildDefinitionPath = build.Definition?.Path,
                            BuildDefinitionName = build.Definition?.Name,
                            BuildId = build.Id,
                            BuildTimelineId = timeline.Id,
                            RepositoryId = build.Repository?.Id,
                            RecordId = record.Id,
                            ParentId = record.ParentId,
                            ChangeId = timeline.ChangeId,
                            LastChangedBy = timeline.LastChangedBy,
                            LastChangedOn = timeline.LastChangedOn,
                            RecordChangeId = record.ChangeId,
                            DetailsChangeId = record.Details?.ChangeId,
                            DetailsId = record.Details?.Id,
                            WorkerName = record.WorkerName,
                            RecordName = record.Name,
                            Order = record.Order,
                            StartTime = record.StartTime,
                            FinishTime = record.FinishTime,
                            WarningCount = record.WarningCount,
                            ErrorCount = record.ErrorCount,
                            LogId = record.Log?.Id,
                            LogType = record.Log?.Type,
                            PercentComplete = record.PercentComplete,
                            Result = record.Result,
                            State = record.State,
                            TaskId = record.Task?.Id,
                            TaskName = record.Task?.Name,
                            TaskVersion = record.Task?.Version,
                            Type = record.RecordType,
                            Issues = record.Issues?.Any() == true
                                ? JsonConvert.SerializeObject(record.Issues, jsonSettings)
                                : null,
                            EtlIngestDate = DateTime.UtcNow,
                        }, jsonSettings));
                }

                await blobClient.UploadAsync(new BinaryData(builder.ToString()));
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                this.logger.LogInformation("Ignoring exception from existing timeline blob for build {BuildId}, change {ChangeId}", build.Id, timeline.ChangeId);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error processing timeline for build {BuildId}", build.Id);
                throw;
            }
        }

        private async Task UploadLogLinesBlobAsync(string account, Build build, BuildLog log)
        {
            try
            {
                // we don't use FinishTime in the logs blob path to prevent duplicating logs when processing retries.
                // i.e.  logs with a given buildid/logid are immutable and retries only add new logs.
                var blobPath = $"{build.Project.Name}/{build.QueueTime:yyyy/MM/dd}/{build.Id}-{log.Id}.jsonl";
                var blobClient = this.buildLogLinesContainerClient.GetBlobClient(blobPath);

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing log {LogId} for build {BuildId}", log.Id, build.Id);
                    return;
                }

                using var tempFile = new DisposableTempFile();

                await using (var messagesWriter = new StreamWriter(File.OpenWrite(tempFile.Path)))
                {
                    var logLines = await this.logProvider.GetLogLinesAsync(build, log.Id);
                    var lastTimeStamp = log.CreatedOn;

                    for (var lineNumber = 1; lineNumber <= logLines.Count; lineNumber++)
                    {
                        var line = logLines[lineNumber - 1];
                        var match = Regex.Match(line, @"^([^Z]{20,28}Z) (.*)$");
                        var timestamp = match.Success
                            ? DateTime.ParseExact(match.Groups[1].Value, TimeFormat, null,
                                System.Globalization.DateTimeStyles.AssumeUniversal).ToUniversalTime()
                            : lastTimeStamp;

                        var message = match.Success ? match.Groups[2].Value : line;

                        if (timestamp == null)
                        {
                            throw new Exception($"Error processing line {lineNumber}. No leading timestamp.");
                        }

                        await messagesWriter.WriteLineAsync(JsonConvert.SerializeObject(
                            new
                            {
                                OrganizationName = account,
                                ProjectId = build.Project?.Id,
                                ProjectName = build.Project?.Name,
                                BuildDefinitionId = build.Definition?.Id,
                                BuildDefinitionPath = build.Definition?.Path,
                                BuildDefinitionName = build.Definition?.Name,
                                BuildId = build.Id,
                                LogId = log.Id,
                                LineNumber = lineNumber,
                                Length = message.Length,
                                Timestamp = timestamp?.ToString(TimeFormat),
                                Message = message,
                                EtlIngestDate = DateTime.UtcNow.ToString(TimeFormat),
                            }, jsonSettings));

                        lastTimeStamp = timestamp;
                    }
                }

                await blobClient.UploadAsync(tempFile.Path);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                this.logger.LogInformation("Ignoring exception from existing blob for log {LogId} for build {BuildId}", log?.Id, build?.Id);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error processing log {LogId} for build {BuildId}", log?.Id, build?.Id);
                throw;
            }
        }

        private async Task UploadTestRunBlobsAsync(string account, Build build)
        {
            try
            {
                var continuationToken = string.Empty;
                var buildIds = new[] { build.Id };
                var minLastUpdatedDate = build.QueueTime.Value.AddHours(-1);
                var maxLastUpdatedDate = build.FinishTime.Value.AddHours(1);

                do
                {
                    var page = await testResultsClient.QueryTestRunsAsync2(
                        build.Project.Id,
                        minLastUpdatedDate,
                        maxLastUpdatedDate,
                        continuationToken: continuationToken,
                        buildIds: buildIds
                    );

                    foreach (var testRun in page)
                    {
                        await UploadTestRunBlobAsync(account, build, testRun);
                    }

                    continuationToken = page.ContinuationToken;
                } while (!string.IsNullOrEmpty(continuationToken));
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error processing test runs for build {BuildId}", build.Id);
                throw;
            }
        }

        private async Task UploadTestRunBlobAsync(string account, Build build, TestRun testRun)
        {
            try
            {
                var blobPath = $"{build.Project.Name}/{testRun.CompletedDate:yyyy/MM/dd}/{testRun.Id}.jsonl";
                var blobClient = this.testRunsContainerClient.GetBlobClient(blobPath);

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing test run blob for build {BuildId}, test run {RunId}", build.Id, testRun.Id);
                    return;
                }

                var stats = testRun.RunStatistics.ToDictionary(x => x.Outcome, x => x.Count);

                var content = JsonConvert.SerializeObject(new
                {
                    OrganizationName = account,
                    ProjectId = build.Project?.Id,
                    ProjectName = build.Project?.Name,
                    BuildDefinitionId = build.Definition?.Id,
                    BuildDefinitionPath = build.Definition?.Path,
                    BuildDefinitionName = build.Definition?.Name,
                    BuildId = build.Id,
                    TestRunId = testRun.Id,
                    Title = testRun.Name,
                    StartedDate = testRun.StartedDate,
                    CompletedDate = testRun.CompletedDate,
                    ResultDurationSeconds = 0,
                    RunDurationSeconds = (int)testRun.CompletedDate.Subtract(testRun.StartedDate).TotalSeconds,
                    BranchName = build.SourceBranch,
                    HasDetail = default(bool?),
                    IsAutomated = testRun.IsAutomated,
                    ResultAbortedCount = stats.TryGetValue("Aborted", out var value) ? value : 0,
                    ResultBlockedCount = stats.TryGetValue("Blocked", out value) ? value : 0,
                    ResultCount = testRun.TotalTests,
                    ResultErrorCount = stats.TryGetValue("Error", out value) ? value : 0,
                    ResultFailCount = stats.TryGetValue("Failed", out value) ? value : 0,
                    ResultInconclusiveCount = stats.TryGetValue("Inconclusive", out value) ? value : 0,
                    ResultNoneCount = stats.TryGetValue("None", out value) ? value : 0,
                    ResultNotApplicableCount = stats.TryGetValue("NotApplicable", out value) ? value : 0,
                    ResultNotExecutedCount = stats.TryGetValue("NotExecuted", out value) ? value : 0,
                    ResultNotImpactedCount = stats.TryGetValue("NotImpacted", out value) ? value : 0,
                    ResultPassCount = stats.TryGetValue("Passed", out value) ? value : 0,
                    ResultTimeoutCount = stats.TryGetValue("Timeout", out value) ? value : 0,
                    ResultWarningCount = stats.TryGetValue("Warning", out value) ? value : 0,
                    TestRunType = testRun.IsAutomated ? "Automated" : "Manual",
                    Workflow = !string.IsNullOrEmpty(testRun.Build?.Id) || testRun.BuildConfiguration != null ? "Build"
                        : testRun.Release?.Id > 0 ? "Release"
                        : "",
                    OrganizationId = default(string),
                    Data = default(string),
                    EtlIngestDate = DateTime.UtcNow,
                }, jsonSettings);

                await blobClient.UploadAsync(new BinaryData(content));
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                this.logger.LogInformation("Ignoring exception from existing blob for test run {RunId} for build {BuildId}", testRun.Id, build.Id);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error processing test run blob for build {BuildId}, test run {RunId}", build.Id, testRun.Id);
                throw;
            }
        }
    }
}
