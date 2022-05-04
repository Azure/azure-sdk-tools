namespace Azure.Sdk.Tools.PipelineWitness
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using Azure.Sdk.Tools.PipelineWitness.Services;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using Azure.Storage.Queues;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.TeamFoundation.Build.WebApi;
    using Microsoft.TeamFoundation.TestManagement.WebApi;
    using Microsoft.VisualStudio.Services.TestResults.WebApi;

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
        private readonly QueueClient queueClient;
        private readonly IOptions<PipelineWitnessSettings> options;

        public BlobUploadProcessor(
            ILogger<BlobUploadProcessor> logger,
            BuildLogProvider logProvider,
            BlobServiceClient blobServiceClient,
            QueueServiceClient queueServiceClient,
            BuildHttpClient buildClient,
            TestResultsHttpClient testResultsClient,
            IOptions<PipelineWitnessSettings> options)
        {
            if (blobServiceClient == null)
            {
                throw new ArgumentNullException(nameof(blobServiceClient));
            }
            
            if (queueServiceClient == null)
            {
                throw new ArgumentNullException(nameof(queueServiceClient));
            }

            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.logProvider = logProvider ?? throw new ArgumentNullException(nameof(logProvider));
            this.buildClient = buildClient ?? throw new ArgumentNullException(nameof(buildClient));
            this.testResultsClient = testResultsClient ?? throw new ArgumentNullException(nameof(testResultsClient));
            this.buildsContainerClient = blobServiceClient.GetBlobContainerClient(BuildsContainerName);
            this.buildTimelineRecordsContainerClient = blobServiceClient.GetBlobContainerClient(BuildTimelineRecordsContainerName);
            this.buildLogLinesContainerClient = blobServiceClient.GetBlobContainerClient(BuildLogLinesContainerName);
            this.testRunsContainerClient = blobServiceClient.GetBlobContainerClient(TestRunsContainerNameTestRunsContainerName);
            this.queueClient = queueServiceClient.GetQueueClient(this.options.Value.BuildLogBundlesQueueName);
            this.queueClient.CreateIfNotExists();
        }

        public async Task UploadBuildBlobsAsync(string account, Guid projectId, int buildId)
        {
            var build = await GetBuildAsync(projectId, buildId);

            if (build == null)
            {
                this.logger.LogWarning("Unable to process run due to missing build. Project: {Project}, BuildId: {BuildId}", projectId, buildId);
                return;
            }

            var skipBuild = false;

            // Project name is used in blob paths and cannot be empty
            if (build.Project == null)
            {
                this.logger.LogWarning("Skipping build with null project property. Project: {Project}, BuildId: {BuildId}", projectId, buildId);
                skipBuild = true;
            }
            else if (string.IsNullOrWhiteSpace(build.Project.Name))
            {
                this.logger.LogWarning("Skipping build with null project property. Project: {Project}, BuildId: {BuildId}", projectId, buildId);
                skipBuild = true;
            }

            if (build.Deleted)
            {
                this.logger.LogInformation("Skipping deleted build. Project: {Project}, BuildId: {BuildId}", build.Project.Name, buildId);
                skipBuild = true;
            }

            if (build.StartTime == null)
            {
                this.logger.LogWarning("Skipping build with null start time. Project: {Project}, BuildId: {BuildId}", build.Project.Name, buildId);
                skipBuild = true;
            }

            // FinishTime is used in blob paths and cannot be null
            if (build.FinishTime == null)
            {
                this.logger.LogWarning("Skipping build with null finish time. Project: {Project}, BuildId: {BuildId}", build.Project.Name, buildId);
                skipBuild = true;
            }

            // QueueTime is used in blob paths and cannot be null
            if (build.QueueTime == null)
            {
                this.logger.LogWarning("Skipping build with null queue time. Project: {Project}, BuildId: {BuildId}", build.Project.Name, buildId);
                skipBuild = true;
            }

            if (build.Definition == null)
            {
                this.logger.LogWarning("Skipping build with null definition property. Project: {Project}, BuildId: {BuildId}", build.Project.Name, buildId);
                skipBuild = true;
            }

            if (skipBuild)
            {
                return;
            }
    
            await UploadBuildBlobAsync(account, build);

            await UploadTestRunBlobsAsync(account, build);

            var timeline = await this.buildClient.GetBuildTimelineAsync(projectId, buildId);

            if (timeline == null)
            {
                logger.LogWarning("No timeline available for build {Project}: {BuildId}", build.Project.Name, build.Id);
            }
            else
            {
                await UploadTimelineBlobAsync(account, build, timeline);
            }

            var logs = await buildClient.GetBuildLogsAsync(build.Project.Id, build.Id);

            if (logs == null || logs.Count == 0)
            {
                logger.LogWarning("No logs available for build {Project}: {BuildId}", build.Project.Name, build.Id);
                return;
            }

            var bundles = BuildLogBundles(account, build, timeline, logs);

            if (bundles.Count == 1)
            {
                await ProcessBuildLogBundleAsync(bundles[0]);
            }
            else
            {
                // If there's more than one bundle, we need to fan out the logs to multiple queue messages
                foreach (var bundle in bundles)
                {
                    await EnqueueBuildLogBundleAsync(bundle);
                }
            }            
        }

        public async Task ProcessBuildLogBundleAsync(BuildLogBundle buildLogBundle)
        {
            foreach (var log in buildLogBundle.TimelineLogs)
            {
                await UploadLogLinesBlobAsync(buildLogBundle, log);
            }
        }

        private List<BuildLogBundle> BuildLogBundles(string account, Build build, Timeline timeline, List<BuildLog> logs)
        {
            BuildLogBundle CreateBundle() => new BuildLogBundle
            {
                Account = account,
                BuildId = build.Id,
                ProjectId = build.Project.Id,
                ProjectName = build.Project.Name,
                QueueTime = build.QueueTime.Value,
                StartTime = build.StartTime.Value,
                FinishTime = build.FinishTime.Value,
                DefinitionId = build.Definition.Id,
                DefinitionName = build.Definition.Name,
                DefinitionPath = build.Definition.Path
            };

            BuildLogBundle currentBundle;
            var logBundles = new List<BuildLogBundle>();
            logBundles.Add(currentBundle = CreateBundle());

            var logsById = logs.ToDictionary(l => l.Id);

            foreach (var log in logs)
            {
                if(currentBundle.TimelineLogs.Count >= this.options.Value.BuildLogBundleSize)
                {
                    logBundles.Add(currentBundle = CreateBundle());
                }

                var logRecords = timeline.Records.Where(x => x.Log?.Id == log.Id).ToArray();

                if(logRecords.Length > 1)
                {
                    this.logger.LogWarning("Found multiple timeline records for build {BuildId}, log {LogId}", build.Id, log.Id);
                }

                var logRecord = logRecords.FirstOrDefault();

                // Job logs are typically just a duplication of their child task logs with the addition of extra start and end lines.
                // If we can, we skip the redundant lines.
                if (string.Equals(logRecord?.RecordType, "job", StringComparison.OrdinalIgnoreCase))
                {
                    // find all of the child task records
                    var childRecords = timeline.Records.Where(x => x.ParentId == logRecord.Id);
                    
                    // sum the line counts for all of the child task records
                    var childLineCount = childRecords
                        .Where(x => x.Log != null && logsById.ContainsKey(x.Log.Id))
                        .Sum(x => logsById[x.Log.Id].LineCount);
                    
                    // if the job's line count is the task line count + 2, then we can skip the job log
                    if (log.LineCount == childLineCount + 2)                    
                    {
                        this.logger.LogTrace("Skipping redundant logs for build {BuildId}, job {RecordId}, log {LogId}", build.Id, logRecord.Id, log.Id);
                        continue;
                    }
                }

                currentBundle.TimelineLogs.Add(new BuildLogInfo
                {
                    LogId = log.Id,
                    LineCount = log.LineCount,
                    LogCreatedOn = log.CreatedOn.Value,
                    RecordId = logRecord?.Id,
                    ParentRecordId = logRecord?.ParentId,
                    RecordType = logRecord?.RecordType
                });
            }

            return logBundles;
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
        
        private async Task UploadLogLinesBlobAsync(BuildLogBundle build, BuildLogInfo log)
        {
            try
            {
                // we don't use FinishTime in the logs blob path to prevent duplicating logs when processing retries.
                // i.e.  logs with a given buildid/logid are immutable and retries only add new logs.
                var blobPath = $"{build.ProjectName}/{build.QueueTime:yyyy/MM/dd}/{build.BuildId}-{log.LogId}.jsonl";
                var blobClient = this.buildLogLinesContainerClient.GetBlobClient(blobPath);

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing log for build {BuildId}, record {RecordId}, log {LogId}", build.BuildId, log.RecordId, log.LogId);
                    return;
                }

                this.logger.LogInformation("Processing log for build {BuildId}, record {RecordId}, log {LogId}", build.BuildId, log.RecordId, log.LogId);

                var lineNumber = 0;
                var characterCount = 0;

                // Over an open read stream and an open write stream, one line at a time, read, process, and write to
                // blob storage
                using (var logStream = await this.logProvider.GetLogStreamAsync(build.ProjectName, build.BuildId, log.LogId))
                using (var logReader = new StreamReader(logStream))
                using (var blobStream = await blobClient.OpenWriteAsync(overwrite: true, new BlobOpenWriteOptions()))
                using (var blobWriter = new StreamWriter(blobStream))
                {
                    var lastTimeStamp = log.LogCreatedOn;

                    while (true)
                    {
                        var line = await logReader.ReadLineAsync();
                        
                        if (line == null)
                        {
                            break;
                        }
                        
                        var isLastLine = logReader.EndOfStream;
                        lineNumber += 1;
                        characterCount += line.Length;

                        // log lines usually follow the format:
                        // 2022-03-30T21:38:38.7007903Z Downloading task: AzureKeyVault (1.200.0)
                        // Sometimes, there's no leading timestamp, so we'll use the last timestamp we saw.
                        var match = Regex.Match(line, @"^([^Z]{20,28}Z) (.*)$");

                        var timestamp = match.Success
                            ? DateTime.ParseExact(match.Groups[1].Value, TimeFormat, null,
                                System.Globalization.DateTimeStyles.AssumeUniversal).ToUniversalTime()
                            : lastTimeStamp;

                        lastTimeStamp = timestamp;
                        
                        var message = match.Success ? match.Groups[2].Value : line;

                        await blobWriter.WriteLineAsync(JsonConvert.SerializeObject(new
                        {
                            OrganizationName = build.Account,
                            ProjectId = build.ProjectId,
                            ProjectName = build.ProjectName,
                            BuildDefinitionId = build.DefinitionId,
                            BuildDefinitionPath = build.DefinitionPath,
                            BuildDefinitionName = build.DefinitionName,
                            BuildId = build.BuildId,
                            LogId = log.LogId,
                            LineNumber = lineNumber,
                            Length = message.Length,
                            Timestamp = timestamp.ToString(TimeFormat),
                            Message = message,
                            EtlIngestDate = DateTime.UtcNow.ToString(TimeFormat),
                        }, jsonSettings));
                    }
                }

                logger.LogInformation("Processed {CharacterCount} characters and {LineCount} lines for build {BuildId}, record {RecordId}, log {LogId}", characterCount, lineNumber, build.BuildId, log.RecordId, log.LogId);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                this.logger.LogInformation("Ignoring existing blob exception for build {BuildId}, record {RecordId}, log {LogId}", build.BuildId, log.RecordId, log.LogId);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error processing build {BuildId}, record {RecordId}, log {LogId}", build.BuildId, log.RecordId, log.LogId);
                throw;
            }
        }

        private async Task EnqueueBuildLogBundleAsync(BuildLogBundle bundle)
        {
            var message = JsonConvert.SerializeObject(bundle, jsonSettings);
            await this.queueClient.SendMessageAsync(message);
        }

        private async Task UploadTestRunBlobsAsync(string account, Build build)
        {
            try
            {
                var continuationToken = string.Empty;
                var buildIds = new[] { build.Id };
                
                var minLastUpdatedDate = build.QueueTime.Value.AddHours(-1);
                var maxLastUpdatedDate = build.FinishTime.Value.AddHours(1);

                var rangeStart = minLastUpdatedDate;

                while(rangeStart < maxLastUpdatedDate)
                {
                    // Ado limits test run queries to a 7 day range, so we'll chunk on 6 days.
                    var rangeEnd = rangeStart.AddDays(6);
                    if(rangeEnd > maxLastUpdatedDate)
                    {
                        rangeEnd = maxLastUpdatedDate;
                    }

                    do
                    {
                        var page = await testResultsClient.QueryTestRunsAsync2(
                            build.Project.Id,
                            rangeStart,
                            rangeEnd,
                            continuationToken: continuationToken,
                            buildIds: buildIds
                        );

                        foreach (var testRun in page)
                        {
                            await UploadTestRunBlobAsync(account, build, testRun);
                        }

                        continuationToken = page.ContinuationToken;
                    } while (!string.IsNullOrEmpty(continuationToken));

                    rangeStart = rangeEnd;
                }                
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

        private async Task<Build> GetBuildAsync(Guid projectId, int buildId)
        {
            Build build = null;

            try
            {
                build = await buildClient.GetBuildAsync(projectId, buildId);
            }
            catch (BuildNotFoundException)
            {
            }

            return build;
        }
    }
}

