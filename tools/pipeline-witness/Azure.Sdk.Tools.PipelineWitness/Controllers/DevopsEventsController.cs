using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.AzurePipelines;
using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Azure.Sdk.Tools.PipelineWitness.Controllers
{
    [Route("api/devopsevents")]
    [ApiController]
    public partial class DevopsEventsController : ControllerBase
    {
        [GeneratedRegex(@"^https://dev.azure.com/(?<account>[\w-]+)/(?<project>[0-9a-fA-F-]+)/_apis/build/Builds/(?<build>\d+)$")]
        private static partial Regex BuildUriRegex();

        private readonly ILogger<DevopsEventsController> logger;
        private readonly BuildCompleteQueue buildCompleteQueue;

        public DevopsEventsController(ILogger<DevopsEventsController> logger, BuildCompleteQueue buildCompleteQueue, IOptions<PipelineWitnessSettings> options)
        {
            this.buildCompleteQueue = buildCompleteQueue;
            this.logger = logger;
        }

        // POST api/devopsevents
        [HttpPost]
        public async Task PostAsync([FromBody] JsonDocument value)
        {
            this.logger.LogInformation("Message received in DevopsEventsController.PostAsync");

            if (!TryConvertMessage(value, out BuildCompleteQueueMessage message) || message.Account != "azure-sdk")
            {
                string messageText = value.RootElement.GetRawText();
                this.logger.LogError("Message content invalid: {Content}", messageText);
                throw new BadHttpRequestException("Invalid payload", 400);
            }

            await this.buildCompleteQueue.EnqueueMessageAsync(message);
        }

        public static bool TryConvertMessage(JsonDocument value, out BuildCompleteQueueMessage message)
        {
            string buildUrl = value.RootElement.TryGetProperty("resource", out JsonElement resource)
                    && resource.TryGetProperty("url", out JsonElement resourceUrl)
                    && resourceUrl.ValueKind == JsonValueKind.String
                ? resourceUrl.GetString()
                : null;

            if (buildUrl == null)
            {
                message = null;
                return false;
            }

            Match match = BuildUriRegex().Match(buildUrl);

            if (!match.Success)
            {
                message = null;
                return false;
            }

            string account = match.Groups["account"].Value;
            string projectIdString = match.Groups["project"].Value;
            string buildIdString = match.Groups["build"].Value;

            if (string.IsNullOrEmpty(account) || !Guid.TryParse(projectIdString, out Guid projectId) || !int.TryParse(buildIdString, out int buildId))
            {
                message = null;
                return false;
            }

            message = new BuildCompleteQueueMessage
            {
                Account = account,
                ProjectId = projectId,
                BuildId = buildId
            };

            return true;
        }
    }
}
