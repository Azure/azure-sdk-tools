using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Sdk.Tools.PipelineWitness.GitHubActions;
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
        private readonly RunCompleteQueue queue;
        private readonly ILogger<GitHubEventsController> logger;
        private readonly PipelineWitnessSettings settings;

        public GitHubEventsController(ILogger<GitHubEventsController> logger, RunCompleteQueue queue, IOptions<PipelineWitnessSettings> options)
        {
            this.logger = logger;
            this.settings = options.Value;
            this.queue = queue;
        }

        // POST api/githubevents
        [HttpPost]
        public async Task<IActionResult> PostAsync()
        {
            var eventName = Request.Headers["X-GitHub-Event"].FirstOrDefault();
            switch (eventName)
            {
                case "workflow_run":
                    return await ProcessWorkflowRunEventAsync();
                default:
                    this.logger.LogWarning("Received GitHub event {EventName} which is not supported", eventName);
                    return Ok();
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
                string owner = eventMessage.GetProperty("repository").GetProperty("owner").GetProperty("login").GetString();
                string repository = eventMessage.GetProperty("repository").GetProperty("name").GetString();
                long runId = eventMessage.GetProperty("workflow_run").GetProperty("id").GetInt64();

                if (this.settings.GitHubRepositories.Contains($"{owner}/{repository}", StringComparer.InvariantCultureIgnoreCase))
                {
                    this.logger.LogInformation("Enqueuing GitHubRunCompleteMessage for {Owner}/{Repository} run {RunId}", owner, repository, runId);
                    
                    var queueMessage = new RunCompleteQueueMessage
                    {
                        Owner = owner,
                        Repository = repository,
                        RunId = runId,
                    };
                    
                    await this.queue.EnqueueMessageAsync(queueMessage);
                }
                else
                {
                    this.logger.LogInformation("Skipping message for unknown repostory {Owner}/{Repository}", owner, repository);
                }
            }

            return Ok();
        }
    }
}
