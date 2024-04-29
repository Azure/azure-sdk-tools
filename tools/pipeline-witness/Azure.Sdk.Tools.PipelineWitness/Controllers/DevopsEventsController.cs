using System.Text.Json;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Azure.Sdk.Tools.PipelineWitness.Controllers
{
    [Route("api/devopsevents")]
    [ApiController]
    public class DevopsEventsController : ControllerBase
    {
        private readonly QueueClient queueClient;
        private readonly ILogger<DevopsEventsController> logger;

        public DevopsEventsController(ILogger<DevopsEventsController> logger, QueueServiceClient queueServiceClient, IOptions<PipelineWitnessSettings> options)
        {
            this.queueClient = queueServiceClient.GetQueueClient(options.Value.BuildCompleteQueueName);
            this.logger = logger;
        }

        // POST api/devopsevents
        [HttpPost]
        public async Task PostAsync([FromBody] JsonDocument value)
        {
            if (value == null) {
                throw new BadHttpRequestException("Missing payload", 400);
            }

            this.logger.LogInformation("Message received in DevopsEventsController.PostAsync");
            string message = value.RootElement.GetRawText();

            if (value.RootElement.TryGetProperty("resource", out var resource)
                && resource.TryGetProperty("url", out var url)
                && url.GetString().StartsWith("https://dev.azure.com/azure-sdk/"))
            {
                SendReceipt response = await this.queueClient.SendMessageAsync(message);
                this.logger.LogInformation("Message added to queue with id {MessageId}", response.MessageId);
            }
            else
            {
                this.logger.LogError("Message content invalid: {Content}", message);
                throw new BadHttpRequestException("Invalid payload", 400);
            }
        }
    }
}
