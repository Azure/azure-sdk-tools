using System.Text.Json;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Azure.Sdk.Tools.PipelineWitness.GitHubActions;

public class RunCompleteQueue
{
    private readonly ILogger<RunCompleteQueue> logger;
    private readonly QueueClient queueClient;

    public RunCompleteQueue(ILogger<RunCompleteQueue> logger, QueueServiceClient queueServiceClient, IOptions<PipelineWitnessSettings> options)
    {
        this.logger = logger;
        this.queueClient = queueServiceClient.GetQueueClient(options.Value.GitHubActionRunsQueueName);
    }

    public async Task EnqueueMessageAsync(RunCompleteQueueMessage message)
    {
        SendReceipt response = await this.queueClient.SendMessageAsync(JsonSerializer.Serialize(message));
        this.logger.LogDebug("Message added to queue with id {MessageId}", response.MessageId);
    }
}
