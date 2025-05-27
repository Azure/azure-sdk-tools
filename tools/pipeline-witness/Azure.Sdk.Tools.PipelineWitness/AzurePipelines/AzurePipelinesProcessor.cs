using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Sdk.Tools.PipelineWitness.Utilities;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.TestResults.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Attachment = Microsoft.TeamFoundation.Build.WebApi.Attachment;
using Timeline = Microsoft.TeamFoundation.Build.WebApi.Timeline;

namespace Azure.Sdk.Tools.PipelineWitness.AzurePipelines
{
    [SuppressMessage("Style", "IDE0037:Use inferred member name", Justification = "Explicit member names are added to json export objects for clarity")]
    public class AzurePipelinesProcessor
    {
        private const string BuildsContainerName = "builds";
        private const string BuildLogLinesContainerName = "buildloglines";
        private const string BuildTimelineRecordsContainerName = "buildtimelinerecords";
        private const string BuildDefinitionsContainerName = "builddefinitions";
        private const string PipelineOwnersContainerName = "pipelineowners";
        private const string TestRunsContainerName = "testruns";
        private const string TestResultsContainerName = "testrunresults";
        private const int ApiBatchSize = 10000;

        private const string TimeFormat = @"yyyy-MM-dd\THH:mm:ss.fffffff\Z";

        private static readonly JsonSerializerSettings jsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = { new StringEnumConverter(new CamelCaseNamingStrategy()) },
            Formatting = Formatting.None,
        };

        private readonly ILogger<AzurePipelinesProcessor> logger;
        private readonly TestResultsHttpClient testResultsClient;
        private readonly BuildHttpClient buildClient;
        private readonly BlobContainerClient buildLogLinesContainerClient;
        private readonly BlobContainerClient buildsContainerClient;
        private readonly BlobContainerClient buildTimelineRecordsContainerClient;
        private readonly BlobContainerClient testRunsContainerClient;
        private readonly BlobContainerClient testResultsContainerClient;
        private readonly BlobContainerClient buildDefinitionsContainerClient;
        private readonly BlobContainerClient pipelineOwnersContainerClient;
        private readonly IOptions<PipelineWitnessSettings> options;
        private readonly Dictionary<string, int?> cachedDefinitionRevisions = [];

        public AzurePipelinesProcessor(
            ILogger<AzurePipelinesProcessor> logger,
            BlobServiceClient blobServiceClient,
            VssConnection vssConnection,
            IOptions<PipelineWitnessSettings> options)
        {
            ArgumentNullException.ThrowIfNull(blobServiceClient);

            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.buildsContainerClient = blobServiceClient.GetBlobContainerClient(BuildsContainerName);
            this.buildTimelineRecordsContainerClient = blobServiceClient.GetBlobContainerClient(BuildTimelineRecordsContainerName);
            this.buildLogLinesContainerClient = blobServiceClient.GetBlobContainerClient(BuildLogLinesContainerName);
            this.buildDefinitionsContainerClient = blobServiceClient.GetBlobContainerClient(BuildDefinitionsContainerName);
            this.testRunsContainerClient = blobServiceClient.GetBlobContainerClient(TestRunsContainerName);
            this.testResultsContainerClient = blobServiceClient.GetBlobContainerClient(TestResultsContainerName);
            this.buildDefinitionsContainerClient = blobServiceClient.GetBlobContainerClient(BuildDefinitionsContainerName);
            this.pipelineOwnersContainerClient = blobServiceClient.GetBlobContainerClient(PipelineOwnersContainerName);

            ArgumentNullException.ThrowIfNull(vssConnection);

            this.buildClient = vssConnection.GetClient<EnhancedBuildHttpClient>();
            this.testResultsClient = vssConnection.GetClient<TestResultsHttpClient>();
        }

        public async Task UploadBuildBlobsAsync(string account, Guid projectId, int buildId)
        {
            Build build = await GetBuildAsync(projectId, buildId);

            if (build == null)
            {
                this.logger.LogWarning("Unable to process run due to missing build. Project: {Project}, BuildId: {BuildId}", projectId, buildId);
                return;
            }

            bool skipBuild = false;

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
                this.logger.LogInformation("Skipping deleted build. Project: {Project}, BuildId: {BuildId}", build.Project?.Name, buildId);
                skipBuild = true;
            }

            if (build.StartTime == null)
            {
                this.logger.LogWarning("Skipping build with null start time. Project: {Project}, BuildId: {BuildId}", build.Project?.Name, buildId);
                skipBuild = true;
            }

            // FinishTime is used in blob paths and cannot be null
            if (build.FinishTime == null)
            {
                this.logger.LogWarning("Skipping build with null finish time. Project: {Project}, BuildId: {BuildId}", build.Project?.Name, buildId);
                skipBuild = true;
            }

            // QueueTime is used in blob paths and cannot be null
            if (build.QueueTime == null)
            {
                this.logger.LogWarning("Skipping build with null queue time. Project: {Project}, BuildId: {BuildId}", build.Project?.Name, buildId);
                skipBuild = true;
            }

            if (build.Definition == null)
            {
                this.logger.LogWarning("Skipping build with null definition property. Project: {Project}, BuildId: {BuildId}", build.Project?.Name, buildId);
                skipBuild = true;
            }

            if (skipBuild)
            {
                return;
            }

            await UploadTestRunBlobsAsync(account, build);

            Timeline timeline = await this.buildClient.GetBuildTimelineAsync(projectId, buildId);

            if (timeline == null)
            {
                this.logger.LogWarning("No timeline available for build {Project}: {BuildId}", build.Project.Name, build.Id);
            }
            else
            {
                await UploadTimelineBlobAsync(account, build, timeline);
            }

            List<BuildLog> logs = await this.buildClient.GetBuildLogsAsync(build.Project.Id, build.Id);

            if (logs == null || logs.Count == 0)
            {
                this.logger.LogWarning("No logs available for build {Project}: {BuildId}", build.Project.Name, build.Id);
                return;
            }

            List<BuildLogInfo> buildLogInfos = GetBuildLogInfos(build, timeline, logs);

            foreach (BuildLogInfo log in buildLogInfos)
            {
                await UploadLogLinesBlobAsync(account, build, log);
            }

            if (build.Definition.Id == this.options.Value.PipelineOwnersDefinitionId)
            {
                await UploadPipelineOwnersBlobAsync(account, build, timeline);
            }

            // We upload the build blob last. This allows us to use the existence of the blob as a signal that build processing is complete.
            await UploadBuildBlobAsync(account, build);
        }

        public async Task<string[]> GetBuildBlobNamesAsync(string projectName, DateTimeOffset minTime, DateTimeOffset maxTime, CancellationToken cancellationToken)
        {
            DateTimeOffset minDay = minTime.ToUniversalTime().Date;
            DateTimeOffset maxDay = maxTime.ToUniversalTime().Date;

            DateTimeOffset[] days = Enumerable.Range(0, (int)(maxDay - minDay).TotalDays + 1)
                .Select(offset => minDay.AddDays(offset))
                .ToArray();

            List<string> blobNames = [];

            foreach (DateTimeOffset day in days)
            {
                string blobPrefix = $"{projectName}/{day:yyyy/MM/dd}/";

                await foreach (BlobItem blob in this.buildsContainerClient.GetBlobsAsync(prefix: blobPrefix, cancellationToken: cancellationToken))
                {
                    blobNames.Add(blob.Name);
                }
            }

            return [.. blobNames];
        }

        public string GetBuildBlobName(Build build)
        {
            long changeTime = ((DateTimeOffset)build.LastChangedDate).ToUnixTimeSeconds();
            string blobName = $"{build.Project.Name}/{build.FinishTime:yyyy/MM/dd}/{build.Id}-{changeTime}.jsonl".ToLower();

            return blobName;
        }

        private async Task UploadPipelineOwnersBlobAsync(string account, Build build, Timeline timeline)
        {
            try
            {
                string blobPath = $"{build.Project.Name}/{build.FinishTime:yyyy/MM/dd}/{build.Id}-{timeline.ChangeId}.jsonl".ToLower();
                BlobClient blobClient = this.pipelineOwnersContainerClient.GetBlobClient(blobPath);

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing build failure blob for build {BuildId}", build.Id);
                    return;
                }

                Dictionary<int, string[]> owners = await GetOwnersFromBuildArtifactAsync(build);

                if (owners == null)
                {
                    // no need to log anything here. GetOwnersFromBuildArtifactAsync logs a warning before returning null;
                    return;
                }

                this.logger.LogInformation("Creating owners blob for build {DefinitionId} change {ChangeId}", build.Id, timeline.ChangeId);

                StringBuilder stringBuilder = new();

                foreach (KeyValuePair<int, string[]> owner in owners)
                {
                    string contentLine = JsonConvert.SerializeObject(new
                    {
                        OrganizationName = account,
                        BuildDefinitionId = owner.Key,
                        Owners = owner.Value,
                        Timestamp = new DateTimeOffset(build.FinishTime!.Value).ToUniversalTime(),
                        EtlIngestDate = DateTimeOffset.UtcNow
                    }, jsonSettings);

                    stringBuilder.AppendLine(contentLine);
                }

                await blobClient.UploadAsync(new BinaryData(stringBuilder.ToString()));
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                this.logger.LogInformation("Ignoring exception from existing owners blob for build {BuildId}", build.Id);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error processing owners artifact from build {BuildId}", build.Id);
                throw;
            }
        }

        private async Task<Dictionary<int, string[]>> GetOwnersFromBuildArtifactAsync(Build build)
        {
            string artifactName = this.options.Value.PipelineOwnersArtifactName;
            string filePath = this.options.Value.PipelineOwnersFilePath;

            try
            {
                await using Stream artifactStream = await this.buildClient.GetArtifactContentZipAsync(build.Project.Id, build.Id, artifactName);
                using ZipArchive zip = new(artifactStream);

                ZipArchiveEntry fileEntry = zip.GetEntry(filePath);

                if (fileEntry == null)
                {
                    this.logger.LogWarning("Artifact {ArtifactName} in build {BuildId} didn't contain the expected file {FilePath}", artifactName, build.Id, filePath);
                    return null;
                }

                await using Stream contentStream = fileEntry.Open();
                using StreamReader contentReader = new(contentStream);
                string content = await contentReader.ReadToEndAsync();

                if (string.IsNullOrEmpty(content))
                {
                    this.logger.LogWarning("The file {filePath} in artifact {ArtifactName} in build {BuildId} contained no content", filePath, artifactName, build.Id);
                    return null;
                }

                Dictionary<int, string[]> ownersDictionary = JsonConvert.DeserializeObject<Dictionary<int, string[]>>(content);

                if (ownersDictionary == null)
                {
                    this.logger.LogWarning("The file {filePath} in artifact {ArtifactName} in build {BuildId} contained a null json object", filePath, artifactName, build.Id);
                }

                return ownersDictionary;
            }
            catch (ArtifactNotFoundException ex)
            {
                this.logger.LogWarning(ex, "Build {BuildId} did not contain the expected artifact {ArtifactName}", build.Id, artifactName);
            }
            catch (InvalidDataException ex)
            {
                this.logger.LogWarning(ex, "Unable to read ZIP contents from artifact {ArtifactName} in build {BuildId}", artifactName, build.Id);

                // rethrow the exception so the queue message will be retried.
                throw;
            }
            catch (JsonSerializationException ex)
            {
                this.logger.LogWarning(ex, "Problem deserializing JSON from artifact {ArtifactName} in build {BuildId}", artifactName, build.Id);
            }

            return null;
        }

        public async Task UploadBuildDefinitionBlobsAsync(string account, string projectName)
        {
            IPagedList<BuildDefinition> definitions = await this.buildClient.GetFullDefinitionsAsync2(project: projectName);

            foreach (BuildDefinition definition in definitions)
            {
                string cacheKey = $"{definition.Project.Id}:{definition.Id}";

                if (!this.cachedDefinitionRevisions.TryGetValue(cacheKey, out int? cachedRevision) || cachedRevision != definition.Revision)
                {
                    await UploadBuildDefinitionBlobAsync(account, definition);
                }

                this.cachedDefinitionRevisions[cacheKey] = definition.Revision;
            }
        }

        private async Task UploadBuildDefinitionBlobAsync(string account, BuildDefinition definition)
        {
            string blobPath = $"{definition.Project.Name}/{definition.Id}-{definition.Revision}.jsonl".ToLower();

            try
            {
                BlobClient blobClient = this.buildDefinitionsContainerClient.GetBlobClient(blobPath);

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing build definition blob for build {DefinitionId} project {Project}", definition.Id, definition.Project.Name);
                    return;
                }

                this.logger.LogInformation("Creating blob for build definition {DefinitionId} revision {Revision} project {Project}", definition.Id, definition.Revision, definition.Project.Name);

                string content = JsonConvert.SerializeObject(new
                {
                    OrganizationName = account,
                    ProjectId = definition.Project.Id,
                    BuildDefinitionId = definition.Id,
                    BuildDefinitionRevision = definition.Revision,
                    BuildDefinitionName = definition.Name,
                    Path = definition.Path,
                    RepositoryId = definition.Repository.Id,
                    RepositoryName = definition.Repository.Name,
                    CreatedDate = definition.CreatedDate,
                    DefaultBranch = definition.Repository.DefaultBranch,
                    ProjectName = definition.Project.Name,
                    ProjectRevision = definition.Project.Revision,
                    ProjectState = definition.Project.State,
                    Quality = definition.DefinitionQuality,
                    QueueId = definition.Queue?.Id,
                    QueueName = definition.Queue?.Name,
                    QueuePoolId = definition.Queue?.Pool?.Id,
                    QueuePoolIsHosted = definition.Queue?.Pool?.IsHosted,
                    QueuePoolName = definition.Queue?.Pool?.Name,
                    QueueStatus = definition.QueueStatus,
                    Type = definition.Type,
                    Url = $"https://dev.azure.com/{account}/{definition.Project.Name}/_build?definitionId={definition.Id}",
                    BadgeEnabled = definition.BadgeEnabled,
                    BuildNumberFormat = definition.BuildNumberFormat,
                    Comment = definition.Comment,
                    JobAuthorizationScope = definition.JobAuthorizationScope,
                    JobCancelTimeoutInMinutes = definition.JobCancelTimeoutInMinutes,
                    JobTimeoutInMinutes = definition.JobTimeoutInMinutes,
                    ProcessType = definition.Process.Type,
                    ProcessYamlFilename = definition.Process is YamlProcess yamlprocess ? yamlprocess.YamlFilename : null,
                    RepositoryCheckoutSubmodules = definition.Repository.CheckoutSubmodules,
                    RepositoryClean = definition.Repository.Clean,
                    RepositoryDefaultBranch = definition.Repository.DefaultBranch,
                    RepositoryRootFolder = definition.Repository.RootFolder,
                    RepositoryType = definition.Repository.Type,
                    RepositoryUrl = definition.Repository.Url,
                    Options = definition.Options,
                    Variables = definition.Variables,
                    Tags = definition.Tags,
                    Triggers = definition.Triggers,
                    EtlIngestDate = DateTimeOffset.UtcNow
                }, jsonSettings);

                await blobClient.UploadAsync(new BinaryData(content));
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                this.logger.LogInformation("Ignoring exception from existing blob for build definition {DefinitionId}", definition.Id);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error processing blob for build definition {DefinitionId}", definition.Id);
                throw;
            }
        }

        private List<BuildLogInfo> GetBuildLogInfos(Build build, Timeline timeline, List<BuildLog> logs)
        {
            Dictionary<int, BuildLog> logsById = logs.ToDictionary(l => l.Id);

            List<BuildLogInfo> buildLogInfos = [];

            foreach (BuildLog log in logs)
            {
                TimelineRecord[] logRecords = timeline.Records.Where(x => x.Log?.Id == log.Id).ToArray();

                if (logRecords.Length > 1)
                {
                    this.logger.LogWarning("Found multiple timeline records for build {BuildId}, log {LogId}", build.Id, log.Id);
                }

                TimelineRecord logRecord = logRecords.FirstOrDefault();

                // Job logs are typically just a duplication of their child task logs with the addition of extra start and end lines.
                // If we can, we skip the redundant lines.
                if (string.Equals(logRecord?.RecordType, "job", StringComparison.OrdinalIgnoreCase))
                {
                    // find all of the child task records
                    IEnumerable<TimelineRecord> childRecords = timeline.Records.Where(x => x.ParentId == logRecord.Id);

                    // sum the line counts for all of the child task records
                    long childLineCount = childRecords
                        .Where(x => x.Log != null && logsById.ContainsKey(x.Log.Id))
                        .Sum(x => logsById[x.Log.Id].LineCount);

                    // if the job's line count is the task line count + 2, then we can skip the job log
                    if (log.LineCount == childLineCount + 2)
                    {
                        this.logger.LogTrace("Skipping redundant logs for build {BuildId}, job {RecordId}, log {LogId}", build.Id, logRecord.Id, log.Id);
                        continue;
                    }
                }

                buildLogInfos.Add(new BuildLogInfo
                {
                    LogId = log.Id,
                    LineCount = log.LineCount,
                    LogCreatedOn = log.CreatedOn,
                    RecordId = logRecord?.Id,
                    ParentRecordId = logRecord?.ParentId,
                    RecordType = logRecord?.RecordType
                });
            }

            return buildLogInfos;
        }

        private async Task UploadBuildBlobAsync(string account, Build build)
        {
            try
            {
                BlobClient blobClient = this.buildsContainerClient.GetBlobClient(GetBuildBlobName(build));

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing build blob for build {BuildId}", build.Id);
                    return;
                }

                string content = JsonConvert.SerializeObject(new
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
                    Result = build.Result,
                    RetainedByRelease = build.RetainedByRelease,
                    SourceBranch = build.SourceBranch,
                    SourceVersion = build.SourceVersion,
                    Status = build.Status,
                    Tags = build.Tags?.Count > 0 ? JsonConvert.SerializeObject(build.Tags, jsonSettings) : null,
                    Url = $"https://dev.azure.com/{account}/{build.Project!.Name}/_build/results?buildId={build.Id}",
                    ValidationResults = build.ValidationResults,
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

                string blobPath = $"{build.Project.Name}/{build.FinishTime:yyyy/MM/dd}/{build.Id}-{timeline.ChangeId}.jsonl".ToLower();
                BlobClient blobClient = this.buildTimelineRecordsContainerClient.GetBlobClient(blobPath);

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing timeline for build {BuildId}, change {ChangeId}", build.Id, timeline.ChangeId);
                    return;
                }

                StringBuilder builder = new();
                foreach (TimelineRecord record in timeline.Records)
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
                            Issues = record.Issues?.Count > 0
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

        private async Task UploadLogLinesBlobAsync(string account, Build build, BuildLogInfo log)
        {
            try
            {
                // we don't use FinishTime in the logs blob path to prevent duplicating logs when processing retries.
                // i.e.  logs with a given buildid/logid are immutable and retries only add new logs.
                string blobPath = $"{build.Project.Name}/{build.QueueTime:yyyy/MM/dd}/{build.Id}-{log.LogId}.jsonl".ToLower();
                BlobClient blobClient = this.buildLogLinesContainerClient.GetBlobClient(blobPath);

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing log for build {BuildId}, record {RecordId}, log {LogId}", build.Id, log.RecordId, log.LogId);
                    return;
                }

                this.logger.LogInformation("Processing log for build {BuildId}, record {RecordId}, log {LogId}", build.Id, log.RecordId, log.LogId);

                int lineNumber = 0;
                int characterCount = 0;

                // Over an open read stream and an open write stream, one line at a time, read, process, and write to
                // blob storage
                using (Stream logStream = await this.buildClient.GetBuildLogAsync(build.Project.Name, build.Id, log.LogId))
                using (StreamReader logReader = new(logStream))
                using (Stream blobStream = await blobClient.OpenWriteAsync(overwrite: true, new BlobOpenWriteOptions()))
                using (StreamWriter blobWriter = new(blobStream))
                {
                    DateTimeOffset lastTimeStamp = log.LogCreatedOn ?? build.StartTime!.Value;

                    while (true)
                    {
                        string line = await logReader.ReadLineAsync();

                        if (line == null)
                        {
                            break;
                        }

                        lineNumber += 1;
                        characterCount += line.Length;

                        var (timestamp, message) = StringUtilities.ParseLogLine(line, lastTimeStamp);

                        lastTimeStamp = timestamp;

                        await blobWriter.WriteLineAsync(JsonConvert.SerializeObject(new
                        {
                            OrganizationName = account,
                            ProjectId = build.Project.Id,
                            ProjectName = build.Project.Name,
                            BuildDefinitionId = build.Definition.Id,
                            BuildDefinitionPath = build.Definition.Path,
                            BuildDefinitionName = build.Definition.Name,
                            BuildId = build.Id,
                            LogId = log.LogId,
                            LineNumber = lineNumber,
                            Length = message.Length,
                            Timestamp = timestamp.ToString(TimeFormat),
                            Message = message,
                            EtlIngestDate = DateTime.UtcNow.ToString(TimeFormat),
                        }, jsonSettings));
                    }
                }

                this.logger.LogInformation("Processed {CharacterCount} characters and {LineCount} lines for build {BuildId}, record {RecordId}, log {LogId}", characterCount, lineNumber, build.Id, log.RecordId, log.LogId);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                this.logger.LogInformation("Ignoring existing blob exception for build {BuildId}, record {RecordId}, log {LogId}", build.Id, log.RecordId, log.LogId);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error processing build {BuildId}, record {RecordId}, log {LogId}", build.Id, log.RecordId, log.LogId);
                throw;
            }
        }

        private async Task UploadTestRunBlobsAsync(string account, Build build)
        {
            try
            {
                string continuationToken = string.Empty;

                DateTime minLastUpdatedDate = build.QueueTime!.Value.AddHours(-1);
                DateTime maxLastUpdatedDate = build.FinishTime!.Value.AddHours(1);

                DateTime rangeStart = minLastUpdatedDate;

                while (rangeStart < maxLastUpdatedDate)
                {
                    // Ado limits test run queries to a 7 day range, so we'll chunk on 6 days.
                    DateTime rangeEnd = rangeStart.AddDays(6);
                    if (rangeEnd > maxLastUpdatedDate)
                    {
                        rangeEnd = maxLastUpdatedDate;
                    }

                    do
                    {
                        IPagedList<TestRun> page = await this.testResultsClient.QueryTestRunsAsync2(
                            build.Project.Id,
                            rangeStart,
                            rangeEnd,
                            continuationToken: continuationToken,
                            buildIds: [ build.Id ]
                        );

                        foreach (TestRun testRun in page)
                        {
                            await UploadTestRunBlobAsync(account, build, testRun);
                            await UploadTestRunResultBlobAsync(account, build, testRun);
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
                string blobPath = $"{build.Project.Name}/{testRun.CompletedDate:yyyy/MM/dd}/{testRun.Id}.jsonl".ToLower();
                BlobClient blobClient = this.testRunsContainerClient.GetBlobClient(blobPath);

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing test run blob for build {BuildId}, test run {RunId}", build.Id, testRun.Id);
                    return;
                }

                Dictionary<string, int> stats = testRun.RunStatistics.ToDictionary(x => x.Outcome, x => x.Count);

                string content = JsonConvert.SerializeObject(new
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
                    ResultAbortedCount = stats.TryGetValue("Aborted", out int value) ? value : 0,
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

        public static int CalculateBatches(int startingNumber, int batchSize = ApiBatchSize)
        {
            if (startingNumber == 0)
            {
                return 0;
            }
            if (startingNumber <= batchSize)
            {
                return 1;
            }
            else
            {
                return (int)Math.Ceiling((double)(startingNumber) / batchSize);
            }
        }

        private async Task UploadTestRunResultBlobAsync(string account, Build build, TestRun testRun)
        {
            try
            {
                string blobPath = $"{build.Project.Name}/{testRun.CompletedDate:yyyy/MM/dd}/{testRun.Id}.jsonl".ToLower();
                BlobClient blobClient = this.testResultsContainerClient.GetBlobClient(blobPath);

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing test results blob for build {BuildId}, test run {RunId}", build.Id, testRun.Id);
                    return;
                }

                StringBuilder builder = new();
                int batchCount = AzurePipelinesProcessor.CalculateBatches(testRun.TotalTests, batchSize: ApiBatchSize);

                for (int batchMultiplier = 0; batchMultiplier < batchCount; batchMultiplier++)
                {
                    List<TestCaseResult> data = await this.testResultsClient.GetTestResultsAsync(build.Project!.Id, testRun.Id, top: ApiBatchSize, skip: batchMultiplier * ApiBatchSize);

                    foreach (TestCaseResult record in data)
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
                                TestRunId = testRun.Id,
                                TestCaseId = record.Id,
                                TestCaseReferenceId = record.TestCaseReferenceId,
                                TestCaseTitle = record.TestCaseTitle,
                                Outcome = record.Outcome,
                                Priority = record.Priority,
                                AutomatedTestName = record.AutomatedTestName,
                                AutomatedTestStorageName = record.AutomatedTestStorage,
                                FailingSince = record.FailingSince,
                                FailureType = record.FailureType,
                                StartedDate = record.StartedDate,
                                CompletedDate = record.CompletedDate,
                                EtlIngestDate = DateTime.UtcNow
                            }, jsonSettings)
                        );
                    }
                }

                await blobClient.UploadAsync(new BinaryData(builder.ToString()));
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error processing test results for build {BuildId}", build.Id);
                throw;
            }
        }

        private async Task<Build> GetBuildAsync(Guid projectId, int buildId)
        {
            Build build = null;

            try
            {
                build = await this.buildClient.GetBuildAsync(projectId, buildId);
            }
            catch (BuildNotFoundException)
            {
            }

            return build;
        }

        internal async Task AddAdditionalBuildTagsAsync(string account, Guid projectId, int buildId)
        {
            this.logger.LogInformation("Processing build {BuildId} for AdditionalTags attachments", buildId);

            const string attachmentType = "AdditionalTags";

            HashSet<string> additionalTags = [];

            List<Attachment> attachments = await this.buildClient.GetAttachmentsAsync(projectId, buildId, attachmentType);

            IEnumerable<string> attachmentLinks = attachments
                .Select(x => x.Links?.Links?.TryGetValue("self", out var value) == true ? value : null)
                .OfType<ReferenceLink>()
                .Select(x => x.Href)
                .Where(x => !string.IsNullOrEmpty(x));

            foreach (string attachmentLink in attachmentLinks)
            {
                Match match = Regex.Match(attachmentLink, @"https://dev.azure.com/[\w-]+/[\w-]+/_apis/build/builds/\d+/(?<timelineId>[\w-]+)/(?<recordId>[\w-]+)/attachments/[\w\.]+/(?<name>.+)");

                if (!match.Success ||
                    !Guid.TryParse(match.Groups["timelineId"].Value, out Guid timelineId) ||
                    !Guid.TryParse(match.Groups["recordId"].Value, out Guid recordId))
                {
                    // retries won't help here, so we log and continue
                    this.logger.LogWarning("Unable to parse attachment link {AttachmentLink}", attachmentLink);
                    continue;
                }

                string name = match.Groups["name"].Value;

                this.logger.LogInformation("Downloading AdditionalTags attachment {TimelineId}/{RecordId}/{Name} for build {BuildId}", timelineId, recordId, name, buildId);

                using Stream contentStream = await this.buildClient.GetAttachmentAsync(
                    projectId,
                    buildId,
                    timelineId,
                    recordId,
                    attachmentType,
                    match.Groups["name"].Value);

                using StreamReader reader = new (contentStream);
                var content = reader.ReadToEnd();

                try
                {
                    string[] tags = JsonConvert.DeserializeObject<string[]>(content);
                    additionalTags.UnionWith(tags);
                }
                catch(Exception ex)
                {
                    // retries won't help here, so we log and continue
                    this.logger.LogError(ex, "Error parsing AdditionalTags attachment {TimelineId}/{RecordId}/{Name} for build {BuildId}", timelineId, recordId, name, buildId);
                    continue;
                }
            }

            if (additionalTags.Count != 0)
            {
                this.logger.LogInformation("Adding tags {Tags} to build {BuildId}", JsonConvert.SerializeObject(additionalTags), buildId);
                await this.buildClient.AddBuildTagsAsync(additionalTags, projectId, buildId);
            }
        }
    }
}

