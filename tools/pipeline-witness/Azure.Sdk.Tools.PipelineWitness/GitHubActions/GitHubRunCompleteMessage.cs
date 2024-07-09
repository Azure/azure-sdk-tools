namespace Azure.Sdk.Tools.PipelineWitness.GitHubActions;

internal class GitHubRunCompleteMessage
{
    public string Owner { get; set; }
    public string Repository { get; set; }
    public long RunId { get; set; }
}
