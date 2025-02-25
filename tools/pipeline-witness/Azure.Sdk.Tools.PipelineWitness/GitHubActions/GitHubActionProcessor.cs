using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Net;
using Octokit;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System.Text;
using System.Threading;
using Azure.Sdk.Tools.PipelineWitness.Utilities;

namespace Azure.Sdk.Tools.PipelineWitness.GitHubActions
{
    public partial class GitHubActionProcessor
    {
        private const string RunsContainerName = "githubactionsruns";
        private const string JobsContainerName = "githubactionsjobs";
        private const string StepsContainerName = "githubactionssteps";
        private const string LogsContainerName = "githubactionslogs";
        private const string TimeFormat = @"yyyy-MM-dd\THH:mm:ss.fffffff\Z";

        [GeneratedRegex(@"^(?:(?<folder>.*)\/)?(?<index>\d+)_(?<name>[^\/]+)\.txt$")]
        private static partial Regex LogFilePathRegex();

        private static readonly JsonSerializerSettings jsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = { new StringEnumConverter(new CamelCaseNamingStrategy()) },
            Formatting = Formatting.None,
        };

        private readonly ILogger<GitHubActionProcessor> logger;
        private readonly GitHubClientFactory clientFactory;
        private readonly BlobContainerClient runsContainerClient;
        private readonly BlobContainerClient jobsContainerClient;
        private readonly BlobContainerClient stepsContainerClient;
        private readonly BlobContainerClient logsContainerClient;

        public GitHubActionProcessor(ILogger<GitHubActionProcessor> logger, BlobServiceClient blobServiceClient, GitHubClientFactory clientFactory)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(blobServiceClient);
            ArgumentNullException.ThrowIfNull(clientFactory);

            this.logger = logger;
            this.logsContainerClient = blobServiceClient.GetBlobContainerClient(LogsContainerName);
            this.runsContainerClient = blobServiceClient.GetBlobContainerClient(RunsContainerName);
            this.jobsContainerClient = blobServiceClient.GetBlobContainerClient(JobsContainerName);
            this.stepsContainerClient = blobServiceClient.GetBlobContainerClient(StepsContainerName);
            this.clientFactory = clientFactory;
        }

        public async Task ProcessAsync(string owner, string repository, long runId)
        {
            IGitHubClient client = await this.clientFactory.CreateGitHubClientAsync(owner, repository);
            WorkflowRun run = await GetWorkflowRunAsync(client, owner, repository, runId);
            await ProcessWorkflowRunAsync(client, run);

            for (long attempt = 1; attempt < run.RunAttempt; attempt++)
            {
                WorkflowRun runAttempt = await client.Actions.Workflows.Runs.GetAttempt(owner, repository, runId, attempt);
                await ProcessWorkflowRunAsync(client, runAttempt);
            }
        }

        public async Task<string[]> GetRunBlobNamesAsync(string repository, DateTimeOffset minTime, DateTimeOffset maxTime, CancellationToken cancellationToken)
        {
            DateTimeOffset minDay = minTime.ToUniversalTime().Date;
            DateTimeOffset maxDay = maxTime.ToUniversalTime().Date;

            DateTimeOffset[] days = Enumerable.Range(0, (int)(maxDay - minDay).TotalDays + 1)
                .Select(offset => minDay.AddDays(offset))
                .ToArray();

            List<string> blobNames = [];

            foreach (DateTimeOffset day in days)
            {
                string blobPrefix = $"{repository}/{day:yyyy/MM/dd}/".ToLower();

                AsyncPageable<BlobItem> blobs = this.runsContainerClient.GetBlobsAsync(prefix: blobPrefix, cancellationToken: cancellationToken);
                
                await foreach (BlobItem blob in blobs)
                {
                    blobNames.Add(blob.Name);
                }
            }

            return blobNames.ToArray();
        }

        public string GetRunBlobName(WorkflowRun run)
        {
            string repository = run.Repository.FullName;
            long runId = run.Id;
            long attempt = run.RunAttempt;
            DateTimeOffset runStartedAt = run.RunStartedAt;

            string blobName = $"{repository}/{runStartedAt:yyyy/MM/dd}/{runId}-{attempt}.jsonl".ToLower();
            return blobName;
        }

        private async Task ProcessWorkflowRunAsync(IGitHubClient client, WorkflowRun run)
        {
            Workflow workflow = await GetWorkflowAsync(client, run);
            List<WorkflowJob> jobs = await GetJobsAsync(client, run);

            await UploadJobsBlobAsync(workflow, run, jobs);
            await UploadStepsBlobAsync(workflow, run, jobs);
            await UploadLogsBlobAsync(client, workflow, run, jobs);

            // We upload the run blob last. This allows us to use the existence of the blob as a signal that run processing is complete.
            await UploadRunBlobAsync(workflow, run);
        }

        private async Task UploadRunBlobAsync(Workflow workflow, WorkflowRun run)
        {
            string repository = run.Repository.FullName;
            long workflowId = workflow.Id;
            string workflowName = workflow.Name;
            long runId = run.Id;
            string runName = run.Name;
            long attempt = run.RunAttempt;

            try
            {
                // even though runid/attempt is unique, we still add a date component to the path for easier browsing
                // multiple attempts have the same runStartedAt, so the different attempt blobs will be in the same folder
                string blobPath = GetRunBlobName(run);
                BlobClient blobClient = this.runsContainerClient.GetBlobClient(blobPath);

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing workflow jobs for repository {Repository}, workflow {Workflow}, run {RunId}, attempt {Attempt}", repository, workflowName, runId, attempt);
                    return;
                }

                this.logger.LogInformation("Processing workflow jobs for repository {Repository}, workflow {Workflow}, run {RunId}, attempt {Attempt}", repository, workflowName, runId, attempt);

                string content = JsonConvert.SerializeObject(
                    new
                    {
                        Repository = repository,
                        WorkflowId = workflowId,
                        WorkflowName = workflowName,
                        RunId = runId,
                        RunName = runName,
                        run.RunNumber,
                        run.HeadBranch,
                        run.HeadSha,
                        RunAttenpt = attempt,
                        run.Event,
                        Status = run.Status.StringValue,
                        Conclusion = run.Conclusion?.StringValue,
                        run.CheckSuiteId,
                        run.DisplayTitle,
                        run.Path,
                        RunStartedAt = run.RunStartedAt.ToString(TimeFormat),
                        CreatedAt = run.CreatedAt.ToString(TimeFormat),
                        UpdatedAt = run.UpdatedAt.ToString(TimeFormat),
                        run.NodeId,
                        run.CheckSuiteNodeId,
                        HeadRepository = run.HeadRepository?.FullName,
                        run.Url,
                        run.HtmlUrl,
                        EtlIngestDate = DateTime.UtcNow.ToString(TimeFormat),
                    }, jsonSettings);

                await blobClient.UploadAsync(new BinaryData(content));
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                this.logger.LogInformation("Ignoring existing blob exception for repository {Repository}, workflow {Workflow}, run {RunId}, attempt {Attempt}", repository, workflowName, runId, attempt);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error processing repository {Repository}, workflow {Workflow}, run {RunId}, attempt {Attempt}", repository, workflowName, runId, attempt);
                throw;
            }
        }

        private async Task UploadJobsBlobAsync(Workflow workflow, WorkflowRun run, List<WorkflowJob> jobs)
        {
            string repository = run.Repository.FullName;
            long workflowId = workflow.Id;
            string workflowName = workflow.Name;
            long runId = run.Id;
            string runName = run.Name;
            long attempt = run.RunAttempt;

            try
            {
                string blobPath = $"{repository}/{run.RunStartedAt:yyyy/MM/dd}/{runId}-{attempt}.jsonl".ToLower();
                BlobClient blobClient = this.jobsContainerClient.GetBlobClient(blobPath);

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing workflow jobs for repository {Repository}, workflow {Workflow}, run {RunId}, attempt {Attempt}", repository, workflowName, runId, attempt);
                    return;
                }

                this.logger.LogInformation("Processing workflow jobs for repository {Repository}, workflow {Workflow}, run {RunId}, attempt {Attempt}", repository, workflowName, runId, attempt);

                StringBuilder builder = new();

                foreach (var job in jobs)
                {
                    builder.AppendLine(JsonConvert.SerializeObject(
                        new
                        {
                            Repository = repository,
                            WorkflowId = workflowId,
                            WorkflowName = workflowName,
                            RunId = runId,
                            RunName = runName,
                            RunAttempt = attempt,
                            JobId = job.Id,
                            JobName = job.Name,
                            Status = job.Status.StringValue,
                            Conclusion = job.Conclusion?.StringValue,
                            CreatedAt = job.CreatedAt?.ToString(TimeFormat),
                            StartedAt = job.StartedAt.ToString(TimeFormat),
                            CompletedAt = job.CompletedAt?.ToString(TimeFormat),
                            job.NodeId,
                            job.HeadSha,
                            job.Labels,
                            job.RunnerId,
                            job.RunnerName,
                            job.RunnerGroupId,
                            job.RunnerGroupName,
                            job.HtmlUrl,
                            EtlIngestDate = DateTime.UtcNow.ToString(TimeFormat),
                        }, jsonSettings));
                }

                await blobClient.UploadAsync(new BinaryData(builder.ToString()));
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                this.logger.LogInformation("Ignoring existing blob exception for repository {Repository}, workflow {Workflow}, run {RunId}, attempt {Attempt}", repository, workflowName, runId, attempt);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error processing repository {Repository}, workflow {Workflow}, run {RunId}, attempt {Attempt}", repository, workflowName, runId, attempt);
                throw;
            }
        }

        private async Task UploadStepsBlobAsync(Workflow workflow, WorkflowRun run, List<WorkflowJob> jobs)
        {
            string repository = run.Repository.FullName;
            long workflowId = workflow.Id;
            string workflowName = workflow.Name;
            long runId = run.Id;
            string runName = run.Name;
            long attempt = run.RunAttempt;

            try
            {
                // logs with a given runId/attempt are immutable and retries add new attempts.
                // even though runid/attempt is unique, we still add a date component to the path for easier browsing
                // multiple attempts have the same runStartedAt, so the different attempt blobs will be in the same folder
                string blobPath = $"{repository}/{run.RunStartedAt:yyyy/MM/dd}/{runId}-{attempt}.jsonl".ToLower();
                BlobClient blobClient = this.stepsContainerClient.GetBlobClient(blobPath);

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing workflow steps for repository {Repository}, workflow {Workflow}, run {RunId}, attempt {Attempt}", repository, workflowName, runId, attempt);
                    return;
                }

                this.logger.LogInformation("Processing workflow steps for repository {Repository}, workflow {Workflow}, run {RunId}, attempt {Attempt}", repository, workflowName, runId, attempt);

                StringBuilder builder = new();

                foreach (var job in jobs)
                {
                    foreach (var step in job.Steps)
                    {
                        builder.AppendLine(JsonConvert.SerializeObject(
                            new
                            {
                                Repository = repository,
                                WorkflowId = workflowId,
                                WorkflowName = workflowName,
                                RunId = runId,
                                RunName = runName,
                                RunAttempt = attempt,
                                JobId = job.Id,
                                JobName = job.Name,
                                StepNumber = step.Number,
                                StepName = step.Name,
                                Status = step.Status.StringValue,
                                Conclusion = step.Conclusion?.StringValue,
                                StartedAt = step.StartedAt?.ToString(TimeFormat),
                                CompletedAt = step.CompletedAt?.ToString(TimeFormat),
                                EtlIngestDate = DateTime.UtcNow.ToString(TimeFormat),
                            }, jsonSettings));
                    }
                }

                await blobClient.UploadAsync(new BinaryData(builder.ToString()));
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                this.logger.LogInformation("Ignoring existing blob exception for repository {Repository}, workflow {Workflow}, run {RunId}, attempt {Attempt}", repository, runName, runId, attempt);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error processing repository {Repository}, workflow {Workflow}, run {RunId}, attempt {Attempt}", repository, runName, runId, attempt);
                throw;
            }
        }

        private async Task UploadLogsBlobAsync(IGitHubClient client, Workflow workflow, WorkflowRun run, List<WorkflowJob> jobs)
        {
            string repository = run.Repository.FullName;
            long workflowId = workflow.Id;
            string workflowName = workflow.Name;
            long runId = run.Id;
            string runName = run.Name;
            long attempt = run.RunAttempt;

            try
            {
                // logs with a given runId/attempt are immutable and retries add new attempts.
                // even though runid/attempt is unique, we still add a date component to the path for easier browsing
                // multiple attempts have the same runStartedAt, so the different attempt blobs will be in the same folder
                string blobPath = $"{repository}/{run.RunStartedAt:yyyy/MM/dd}/{runId}-{attempt}.jsonl".ToLower();
                BlobClient blobClient = this.logsContainerClient.GetBlobClient(blobPath);

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing log for repository {Repository}, workflow {Workflow}, run {RunId}, attempt {Attempt}", repository, runName, runId, attempt);
                    return;
                }

                this.logger.LogInformation("Processing log for repository {Repository}, workflow {Workflow}, run {RunId}, attempt {Attempt}", repository, runName, runId, attempt);

                using ZipArchive archive = await GetLogsAsync(client, run);

                var logEntries = archive.Entries
                    .Select(x => new
                    {
                        Entry = x,
                        NameRegex = LogFilePathRegex().Match(x.FullName),
                    })
                    .Where(x => x.NameRegex.Success)
                    .Select(x => new
                    {
                        ParentName = x.NameRegex.Groups["folder"].Value,
                        Index = x.NameRegex.Groups["index"].Value,
                        RecordName = x.NameRegex.Groups["name"].Value,
                        x.Entry
                    })
                    .ToDictionary(x => string.IsNullOrEmpty(x.ParentName) ? x.RecordName : $"{x.ParentName}/{x.Index}", x => x.Entry);

                await using Stream blobStream = await blobClient.OpenWriteAsync(overwrite: true, new BlobOpenWriteOptions());
                await using StreamWriter blobWriter = new(blobStream);

                long characterCount = 0;
                int lineCount = 0;

                foreach (var job in jobs)
                {
                    // Retries may not run all jobs and skipped jobs will not have logs
                    // The jobs still appear in the API response, but their runnerName is empty
                    bool isRetrySkipped = string.IsNullOrEmpty(job.RunnerName) && attempt > 1;

                    if (!logEntries.TryGetValue(job.Name, out ZipArchiveEntry jobEntry))
                    {
                        if (!isRetrySkipped)
                        {
                            // All jobs in the first attempt or with runner names should have logs
                            this.logger.LogWarning("Missing log entry for job {JobName}", job.Name);
                        }

                        continue;
                    }

                    IList<LogLine> logLines = ReadLogLines(jobEntry, step: 0, job.StartedAt);

                    IList<LogLine> stepLines = job.Steps
                        .Where(x => x.Conclusion != WorkflowJobConclusion.Skipped)
                        .OrderBy(x => x.Number)
                        .SelectMany(step => ReadLogLines(logEntries[$"{job.Name}/{step.Number}"], step.Number, step.StartedAt ?? job.StartedAt))
                        .ToArray();

                    UpdateStepLines(logLines, stepLines);


                    foreach (LogLine logLine in logLines)
                    {
                        characterCount += logLine.Message.Length;
                        lineCount += 1;

                        await blobWriter.WriteLineAsync(JsonConvert.SerializeObject(new
                        {
                            Repository = repository,
                            WorkflowId = workflowId,
                            WorkflowName = workflowName,
                            RunId = runId,
                            RunAttempt = attempt,
                            JobId = job.Id,
                            StepNumber = logLine.Step,
                            LineNumber = logLine.Number,
                            logLine.Message.Length,
                            Timestamp = logLine.Timestamp.ToString(TimeFormat),
                            logLine.Message,
                            EtlIngestDate = DateTime.UtcNow.ToString(TimeFormat),
                        }, jsonSettings));
                    }
                }

                this.logger.LogInformation("Processed {CharacterCount} characters and {LineCount} lines for repository {Repository}, workflow {Workflow}, run {RunId}, attempt {Attempt}", characterCount, lineCount, repository, runName, runId, attempt);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                this.logger.LogInformation("Ignoring existing blob exception for repository {Repository}, workflow {Workflow}, run {RunId}, attempt {Attempt}", repository, runName, runId, attempt);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error processing repository {Repository}, workflow {Workflow}, run {RunId}, attempt {Attempt}", repository, runName, runId, attempt);
                throw;
            }
        }

        private static bool UpdateStepLines(IList<LogLine> jobLines, IList<LogLine> stepLines)
        {
            // For each line in the step, remove the corresponding line from the job
            if (stepLines.Count == 0)
            {
                return true;
            }

            // seek to the first line in the job that is after the first line in the step
            for (int jobIndex = 0; jobIndex < jobLines.Count - stepLines.Count + 1; jobIndex++)
            {
                var isMatch = true;

                for (var stepIndex = 0; isMatch && stepIndex < stepLines.Count; stepIndex++)
                {
                    var stepLine = stepLines[stepIndex];
                    var jobLine = jobLines[jobIndex + stepIndex];

                    if (jobLine.Message != stepLine.Message)
                    {
                        isMatch = false;
                    }
                }

                if (isMatch)
                {
                    // Replace the step number and timestamp with the values from the step log
                    for (var stepIndex = 0; stepIndex < stepLines.Count; stepIndex++)
                    {
                        var stepLine = stepLines[stepIndex];
                        var jobLine = jobLines[jobIndex + stepIndex];

                        jobLine.Step = stepLine.Step;
                        jobLine.Number = stepLine.Number;
                        jobLine.Timestamp = stepLine.Timestamp;
                    }
                    return true;
                }
            }

            return false;
        }

        private static List<LogLine> ReadLogLines(ZipArchiveEntry entry, int step, DateTimeOffset logStartTime)
        {
            var result = new List<LogLine>();

            using var logReader = new StreamReader(entry.Open());
            DateTimeOffset lastTimestamp = logStartTime;

            for (int lineNumber = 1; !logReader.EndOfStream; lineNumber++)
            {
                string line = logReader.ReadLine();

                var (timestamp, message) = StringUtilities.ParseLogLine(line, lastTimestamp);

                lastTimestamp = timestamp;

                result.Add(new LogLine
                {
                    Step = step,
                    Number = lineNumber,
                    Timestamp = timestamp,
                    Message = message
                });
            }

            return result;
        }

        private static async Task<WorkflowRun> GetWorkflowRunAsync(IGitHubClient client, string owner, string repository, long runId)
        {
            WorkflowRun workflowRun = await client.Actions.Workflows.Runs.Get(owner, repository, runId);
            return workflowRun;
        }

        private static async Task<Workflow> GetWorkflowAsync(IGitHubClient client, WorkflowRun run)
        {
            Workflow workflow = await client.Actions.Workflows.Get(run.Repository.Owner.Login, run.Repository.Name, run.WorkflowId);
            return workflow;
        }

        private static async Task<List<WorkflowJob>> GetJobsAsync(IGitHubClient client, WorkflowRun run)
        {
            List<WorkflowJob> jobs = [];
            for (int pageNumber = 1; ; pageNumber++)
            {
                ApiOptions options = new()
                {
                    PageSize = 100,
                    PageCount = 1,
                    StartPage = pageNumber
                };

                WorkflowJobsResponse jobsResponse = await client.Actions.Workflows.Jobs.List(run.Repository.Owner.Login, run.Repository.Name, run.Id, (int)run.RunAttempt, options);

                IReadOnlyList<WorkflowJob> pageJobs = jobsResponse.Jobs;
                if (pageJobs.Count == 0)
                {
                    break;
                }

                jobs.AddRange(pageJobs);
            }

            return jobs;
        }

        private static async Task<ZipArchive> GetLogsAsync(IGitHubClient client, WorkflowRun run)
        {
            var logBytes = await client.Actions.Workflows.Runs.GetAttemptLogs(run.Repository.Owner.Login, run.Repository.Name, run.Id, run.RunAttempt);
            return new ZipArchive(new MemoryStream(logBytes), ZipArchiveMode.Read, false);
        }

        private class LogLine
        {
            public int Step;
            public int Number;
            public DateTimeOffset Timestamp;
            public string Message;
        };
    }
}
