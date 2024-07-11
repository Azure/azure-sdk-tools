using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Sdk.Tools.PipelineWitness.GitHubActions;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Azure.Sdk.Tools.PipelineWitness.Controllers
{
    [Route("api/githubevents")]
    [ApiController]
    public class GitHubEventsController : ControllerBase
    {
        private readonly QueueClient queueClient;
        private readonly ILogger<GitHubEventsController> logger;
        private readonly PipelineWitnessSettings settings;

        public GitHubEventsController(ILogger<GitHubEventsController> logger, QueueServiceClient queueServiceClient, IOptions<PipelineWitnessSettings> options)
        {
            this.logger = logger;
            this.settings = options.Value;
            this.queueClient = queueServiceClient.GetQueueClient(this.settings.GitHubActionRunsQueueName);
        }

        // POST api/githubevents
        [HttpPost]
        public async Task<IActionResult> PostAsync()
        {
            var eventName = Request.Headers["X-GitHub-Event"].FirstOrDefault();
            switch (eventName)
            {
                case "ping":
                    return Ok();
                case "workflow_run":
                    return await ProcessWorkflowRunEventAsync();
                default:
                    this.logger.LogWarning("Received GitHub event {EventName} which is not supported", eventName);
                    return BadRequest();
            }
        }

        private static bool VerifySignature(string text, string key, string signature)
        {
            Encoding encoding = Encoding.UTF8;

            byte[] textBytes = encoding.GetBytes(text);
            byte[] keyBytes = encoding.GetBytes(key);

            using HMACSHA256 hasher = new(keyBytes);
            byte[] hashBytes = hasher.ComputeHash(textBytes);

            var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            var expectedSignature = $"sha256={hash}";
            return signature == expectedSignature;
        }

        private async Task<IActionResult> ProcessWorkflowRunEventAsync()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();

            if (!VerifySignature(body, this.settings.GitHubWebhookSecret, signature))
            {
                this.logger.LogWarning("Received GitHub event {Event} with invalid signature", "workflow_run");
                return Unauthorized();
            }

            var eventMessage = JsonDocument.Parse(body).RootElement;

            string action = eventMessage.GetProperty("action").GetString();

            this.logger.LogInformation("Received GitHub event {Event}.{Action}", "workflow_run", action);

            if (action == "completed")
            {
                var queueMessage = new GitHubRunCompleteMessage
                {
                    Owner = eventMessage.GetProperty("repository").GetProperty("owner").GetProperty("login").GetString(),
                    Repository = eventMessage.GetProperty("repository").GetProperty("name").GetString(),
                    RunId = eventMessage.GetProperty("workflow_run").GetProperty("id").GetInt64(),
                };

                this.logger.LogInformation("Enqueuing GitHubRunCompleteMessage for {Owner}/{Repository} run {RunId}", queueMessage.Owner, queueMessage.Repository, queueMessage.RunId);

                await this.queueClient.SendMessageAsync(JsonSerializer.Serialize(queueMessage));
            }

            return Ok();
        }
    }
}
