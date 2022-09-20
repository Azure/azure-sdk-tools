using System.Diagnostics;

namespace Azure.Sdk.Tools.PipelineWitness
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using Azure.Sdk.Tools.PipelineWitness.Configuration;
    using Azure.Sdk.Tools.PipelineWitness.Services;
    using Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using Azure.Storage.Queues;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.TeamFoundation.Build.WebApi;
    using Microsoft.TeamFoundation.TestManagement.WebApi;
    using Microsoft.VisualStudio.Services.Common;
    using Microsoft.VisualStudio.Services.TestResults.WebApi;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Serialization;

    public class BlobUploadProcessor
    {
        private const string BuildsContainerName = "builds";
        private const string BuildLogLinesContainerName = "buildloglines";
        private const string BuildTimelineRecordsContainerName = "buildtimelinerecords";
        private const string BuildDefinitionsContainerName = "builddefinitions";
        private const string BuildFailuresContainerName = "buildfailures";
        private const string PipelineOwnersContainerName = "pipelineowners";
        private const string TestRunsContainerName = "testruns";

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
        private readonly BlobContainerClient buildDefinitionsContainerClient;
        private readonly BlobContainerClient buildFailuresContainerClient;
        private readonly BlobContainerClient pipelineOwnersContainerClient; 
        private readonly IOptions<PipelineWitnessSettings> options;
        private readonly Dictionary<string, int?> cachedDefinitionRevisions = new();
        private readonly IFailureAnalyzer failureAnalyzer;        

        public BlobUploadProcessor(
            ILogger<BlobUploadProcessor> logger,
            BuildLogProvider logProvider,
            BlobServiceClient blobServiceClient,
            BuildHttpClient buildClient,
            TestResultsHttpClient testResultsClient,
            IOptions<PipelineWitnessSettings> options,
            IFailureAnalyzer failureAnalyzer)
        {
            if (blobServiceClient == null)
            {
                throw new ArgumentNullException(nameof(blobServiceClient));
            }

            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.logProvider = logProvider ?? throw new ArgumentNullException(nameof(logProvider));
            this.buildClient = buildClient ?? throw new ArgumentNullException(nameof(buildClient));
            this.testResultsClient = testResultsClient ?? throw new ArgumentNullException(nameof(testResultsClient));
            this.buildsContainerClient = blobServiceClient.GetBlobContainerClient(BuildsContainerName);
            this.buildTimelineRecordsContainerClient = blobServiceClient.GetBlobContainerClient(BuildTimelineRecordsContainerName);
            this.buildLogLinesContainerClient = blobServiceClient.GetBlobContainerClient(BuildLogLinesContainerName);
            this.buildDefinitionsContainerClient = blobServiceClient.GetBlobContainerClient(BuildDefinitionsContainerName);
            this.buildFailuresContainerClient = blobServiceClient.GetBlobContainerClient(BuildFailuresContainerName);
            this.testRunsContainerClient = blobServiceClient.GetBlobContainerClient(TestRunsContainerName);
            this.buildDefinitionsContainerClient = blobServiceClient.GetBlobContainerClient(BuildDefinitionsContainerName);
            this.pipelineOwnersContainerClient = blobServiceClient.GetBlobContainerClient(PipelineOwnersContainerName);
            this.failureAnalyzer = failureAnalyzer;
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
                await UploadBuildFailureBlobAsync(account, build, timeline);
            }

            var logs = await buildClient.GetBuildLogsAsync(build.Project.Id, build.Id);

            if (logs == null || logs.Count == 0)
            {
                logger.LogWarning("No logs available for build {Project}: {BuildId}", build.Project.Name, build.Id);
                return;
            }

            var buildLogInfos = GetBuildLogInfos(account, build, timeline, logs);

            foreach (var log in buildLogInfos)
            {
                await UploadLogLinesBlobAsync(account, build, log);
            }

            if (build.Definition.Id == options.Value.PipelineOwnersDefinitionId)
            {
                await UploadPipelineOwnersBlobAsync(account, build, timeline); 
            }
        }

        private async Task UploadPipelineOwnersBlobAsync(string account, Build build, Timeline timeline)
        {
            try
            {
                var blobPath = $"{build.Project.Name}/{build.FinishTime:yyyy/MM/dd}/{build.Id}-{timeline.ChangeId}.jsonl";
                var blobClient = this.pipelineOwnersContainerClient.GetBlobClient(blobPath);

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing build failure blob for build {BuildId}", build.Id);
                    return;
                }

                var owners = await GetOwnersFromBuildArtifactAsync(build);

                if (owners == null)
                {
                    // no need to log anything here. GetOwnersFromBuildArtifactAsync logs a warning before returning null;
                    return;
                }

                this.logger.LogInformation("Creating owners blob for build {DefinitionId} change {ChangeId}", build.Id, timeline.ChangeId);

                var stringBuilder = new StringBuilder();

                foreach (var owner in owners)
                {
                    var contentLine = JsonConvert.SerializeObject(new
                    {
                        OrganizationName = account,
                        BuildDefinitionId = owner.Key,
                        Owners = owner.Value,
                        Timestamp = new DateTimeOffset(build.FinishTime.Value).ToUniversalTime(),
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
            var artifactName = this.options.Value.PipelineOwnersArtifactName;
            var filePath = this.options.Value.PipelineOwnersFilePath;

            try
            {
                await using var artifactStream = await this.buildClient.GetArtifactContentZipAsync(build.Project.Id, build.Id, artifactName);
                using var zip = new ZipArchive(artifactStream);

                var fileEntry = zip.GetEntry(filePath);

                if (fileEntry == null)
                {
                    this.logger.LogWarning("Artifact {ArtifactName} in build {BuildId} didn't contain the expected file {FilePath}", artifactName, build.Id, filePath);
                    return null;
                }

                await using var contentStream = fileEntry.Open();
                using var contentReader = new StreamReader(contentStream);
                var content = await contentReader.ReadToEndAsync();

                if (string.IsNullOrEmpty(content))
                {
                    this.logger.LogWarning("The file {filePath} in artifact {ArtifactName} in build {BuildId} contained no content", filePath, artifactName, build.Id);
                    return null;
                }

                var ownersDictionary = JsonConvert.DeserializeObject<Dictionary<int, string[]>>(content);

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

        private async Task UploadBuildFailureBlobAsync(string account, Build build, Timeline timeline)
        {
            try
            {
                var blobPath = $"{build.Project.Name}/{build.FinishTime:yyyy/MM/dd}/{build.Id}-{timeline.ChangeId}.jsonl";
                var blobClient = this.buildFailuresContainerClient.GetBlobClient(blobPath);

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing build failure blob for build {BuildId}", build.Id);
                    return;
                }

                var failures = await this.failureAnalyzer.AnalyzeFailureAsync(build, timeline);
                if (!failures.Any())
                {
                    return;
                }

                this.logger.LogInformation("Creating failure blob for build {DefinitionId} change {ChangeId}", build.Id, timeline.ChangeId);

                var stringBuilder = new StringBuilder();

                foreach (var failure in failures)
                {
                    var contentLine = JsonConvert.SerializeObject(new
                    {
                        OrganizationName = account,
                        ProjectId = build.Project.Id,
                        ProjectName = build.Project.Name,
                        BuildDefinitionId = build.Definition.Id,
                        BuildDefinitionName = build.Definition.Name,
                        BuildId = build.Id,
                        BuildFinishTime = build.FinishTime,
                        RecordFinishTime = failure.Record.FinishTime,
                        ChangeId = timeline.ChangeId,
                        RecordId = failure.Record.Id,
                        BuildTimelineId = timeline.Id,
                        ErrorClassification = failure.Classification,
                        EtlIngestDate = DateTimeOffset.UtcNow
                    }, jsonSettings);
                     stringBuilder.AppendLine(contentLine);
                }

                await blobClient.UploadAsync(new BinaryData(stringBuilder.ToString()));   
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                this.logger.LogInformation("Ignoring exception from existing failure blob for build {BuildId}", build.Id);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error processing build failure blob for build {BuildId}", build.Id);
                throw;
            }
        }

        public async Task UploadBuildDefinitionBlobsAsync(string account, string projectName)
        {
            var definitions = await buildClient.GetFullDefinitionsAsync2(project: projectName);

            foreach (var definition in definitions)
            {
                var cacheKey = $"{definition.Project.Id}:{definition.Id}";

                if (!this.cachedDefinitionRevisions.TryGetValue(cacheKey, out var cachedRevision) || cachedRevision != definition.Revision)
                {
                    await UploadBuildDefinitionBlobAsync(account, definition);
                }

                this.cachedDefinitionRevisions[cacheKey] = definition.Revision;
            }
        }
        
        private async Task UploadBuildDefinitionBlobAsync(string account, BuildDefinition definition)
        {
            var blobPath = $"{definition.Project.Name}/{definition.Id}-{definition.Revision}.jsonl";

            try
            {
                var blobClient = this.buildDefinitionsContainerClient.GetBlobClient(blobPath);

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing build definition blob for build {DefinitionId} project {Project}", definition.Id, definition.Project.Name);
                    return;
                }

                this.logger.LogInformation("Creating blob for build definition {DefinitionId} revision {Revision} project {Project}", definition.Id, definition.Revision, definition.Project.Name);

                var content = JsonConvert.SerializeObject(new
                {
                    OrganizationName = account,
                    ProjectId = definition.Project.Id,
                    BuildDefinitionId = definition.Id,
                    BuildDefinitionRevision = definition.Revision,
                    BuildDefinitionName = definition.Name,
                    Path = definition.Path,
                    RepositoryId = definition.Repository.Id,
                    RepositoryName = definition.Repository.Name,
                    AuthoredByDescriptor = definition.AuthoredBy.Descriptor.ToString(),
                    AuthoredByDisplayName = definition.AuthoredBy.DisplayName,
                    AuthoredById = definition.AuthoredBy.Id,
                    AuthoredByIsContainer = definition.AuthoredBy.IsContainer,
                    AuthoredByUniqueName = definition.AuthoredBy.UniqueName,
                    CreatedDate = definition.CreatedDate,
                    DefaultBranch = definition.Repository.DefaultBranch,
                    DraftOfId = default(string),
                    DraftOfName = default(string),
                    DraftOfProjectId = default(string),
                    DraftOfProjectName = default(string),
                    DraftOfProjectRevision = default(string),
                    DraftOfProjectState = default(string),
                    DraftOfProjectVisibility = default(string),
                    DraftOfQueueStatus = default(string),
                    DraftOfRevision = default(string),
                    DraftOfType = default(string),
                    DraftOfUri = default(string),
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
                    Uri = definition.Uri,
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
                    Data = definition,
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

        private List<BuildLogInfo> GetBuildLogInfos(string account, Build build, Timeline timeline, List<BuildLog> logs)
        {
            var logsById = logs.ToDictionary(l => l.Id);

            var buildLogInfos = new List<BuildLogInfo>();

            foreach (var log in logs)
            {
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

                buildLogInfos.Add(new BuildLogInfo
                {
                    LogId = log.Id,
                    LineCount = log.LineCount,
                    LogCreatedOn = log.CreatedOn.Value,
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
                long changeTime = ((DateTimeOffset)build.LastChangedDate).ToUnixTimeSeconds();
                var blobPath = $"{build.Project.Name}/{build.FinishTime:yyyy/MM/dd}/{build.Id}-{changeTime}.jsonl";
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

        private async Task UploadLogLinesBlobAsync(string account, Build build, BuildLogInfo log)
        {
            try
            {
                // we don't use FinishTime in the logs blob path to prevent duplicating logs when processing retries.
                // i.e.  logs with a given buildid/logid are immutable and retries only add new logs.
                var blobPath = $"{build.Project.Name}/{build.QueueTime:yyyy/MM/dd}/{build.Id}-{log.LogId}.jsonl";
                var blobClient = this.buildLogLinesContainerClient.GetBlobClient(blobPath);

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing log for build {BuildId}, record {RecordId}, log {LogId}", build.Id, log.RecordId, log.LogId);
                    return;
                }

                this.logger.LogInformation("Processing log for build {BuildId}, record {RecordId}, log {LogId}", build.Id, log.RecordId, log.LogId);

                var lineNumber = 0;
                var characterCount = 0;

                // Over an open read stream and an open write stream, one line at a time, read, process, and write to
                // blob storage
                using (var logStream = await this.logProvider.GetLogStreamAsync(build.Project.Name, build.Id, log.LogId))
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

                logger.LogInformation("Processed {CharacterCount} characters and {LineCount} lines for build {BuildId}, record {RecordId}, log {LogId}", characterCount, lineNumber, build.Id, log.RecordId, log.LogId);
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

