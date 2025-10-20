namespace Azure.Sdk.Tools.Cli.Microagents;

public interface IMicroagentHostService
{
    Task<TResult> RunAgentToCompletion<TResult>(Microagent<TResult> agentDefinition, CancellationToken ct = default) where TResult : notnull;
}
