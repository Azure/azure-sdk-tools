namespace Azure.Sdk.Tools.PipelineWitness
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using Azure.Sdk.Tools.PipelineWitness.Services;
    using Azure.Storage.Blobs;

    using Microsoft.Extensions.Logging;
    using Microsoft.TeamFoundation.Build.WebApi;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public class BlobUploadProcessor
    {
        private const string BuildLogLinesContainerName = "buildloglines";

        private const string TimeFormat = @"yyyy-MM-dd\THH:mm:ss.fffffff\Z";
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
        };

        private readonly ILogger<BlobUploadProcessor> logger;
        private readonly BuildLogProvider logProvider;
        private readonly BlobContainerClient containerClient;
        private readonly BuildHttpClient buildClient;

        public BlobUploadProcessor(ILogger<BlobUploadProcessor> logger, BuildLogProvider logProvider, BlobServiceClient blobServiceClient, BuildHttpClient buildClient)
        {
            if (blobServiceClient == null)
            {
                throw new ArgumentNullException(nameof(blobServiceClient));
            }

            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.logProvider = logProvider ?? throw new ArgumentNullException(nameof(logProvider));
            this.buildClient = buildClient ?? throw new ArgumentNullException(nameof(buildClient));
            this.containerClient = blobServiceClient.GetBlobContainerClient(BuildLogLinesContainerName);
        }

        public async Task UploadLogBlobsAsync(Build build)
        {
            var logs = await buildClient.GetBuildLogsAsync(build.Project.Id, build.Id);

            foreach (var log in logs)
            {
                var blobPath = $"{build.Project.Name}/{build.QueueTime:yyyy/MM/dd}/{build.Id}-{log.Id}.jsonl";
                var blobClient = containerClient.GetBlobClient(blobPath);

                if (await blobClient.ExistsAsync())
                {
                    this.logger.LogInformation("Skipping existing log {LogId} for build {BuildId}", log.Id, build.Id);
                    continue;
                }

                await UploadLogBlobAsync(blobClient, build, log);
            }
        }

        private async Task UploadLogBlobAsync(BlobClient blobClient, Build build, BuildLog log)
        {
            var tempFile = Path.GetTempFileName();

            try
            {
                await using (var messagesWriter = new StreamWriter(File.OpenWrite(tempFile)))
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
                                OrganizationName = "azure-sdk",
                                ProjectId = build.Project.Id,
                                BuildId = build.Id,
                                BuildDefinitionId = build.Definition.Id,
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

                await blobClient.UploadAsync(tempFile);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error processing log {LogId} for build {BuildId}", log.Id, build.Id);
                throw;
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}
