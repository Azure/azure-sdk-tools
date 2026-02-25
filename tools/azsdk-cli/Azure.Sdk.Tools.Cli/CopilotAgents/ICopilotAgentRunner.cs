namespace Azure.Sdk.Tools.Cli.CopilotAgents;

/// <summary>
/// Orchestrates the execution of a <see cref="CopilotAgent{TResult}"/> to completion.
/// </summary>
public interface ICopilotAgentRunner
{
    /// <summary>
    /// Runs the agent until it produces a valid result or exceeds the maximum iterations.
    /// </summary>
    /// <typeparam name="TResult">The type of result the agent produces.</typeparam>
    /// <param name="agent">The agent definition including instructions, tools, and constraints.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The validated result from the agent.</returns>
    Task<TResult> RunAsync<TResult>(CopilotAgent<TResult> agent, CancellationToken ct = default)
        where TResult : notnull;
}
