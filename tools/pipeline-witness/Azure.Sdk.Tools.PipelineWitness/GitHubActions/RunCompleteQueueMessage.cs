namespace Azure.Sdk.Tools.PipelineWitness.GitHubActions;

public class RunCompleteQueueMessage
{
    public string Owner { get; set; }
    public string Repository { get; set; }
    public long RunId { get; set; }
}
