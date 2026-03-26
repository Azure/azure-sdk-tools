using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.CopilotAgents;

/// <summary>
/// A definition of a copilot agent, including a system prompt, a list of tools, and a model.
/// 
/// A copilot agent is designed to perform a specific task or set of tasks.
/// It operates an agent loop (LLM <-> tool call) until the LLM determines that its task is complete, at which point
/// it returns the result.
/// </summary>
/// <typeparam name="TResult">A type representing the schema of the result of the agent's execution.</typeparam>
public class CopilotAgent<TResult> where TResult : notnull
{
    /// <summary>
    /// The instructions (system prompt) provided to the agent.
    /// </summary>
    public required string Instructions { get; init; }

    /// <summary>
    /// A list of tools that are made available to the agent. For a simple LLM call without tools, this can be left empty.
    /// </summary>
    public IEnumerable<AIFunction> Tools { get; init; } = [];

    /// <summary>
    /// The model that this agent will use. Defaults to "claude-sonnet-4.5".
    /// </summary>
    public string Model { get; init; } = "claude-sonnet-4.5";

    /// <summary>
    /// The maximum number of iterations in the agent loop. If this is exceeded, the agent run is deemed to have failed.
    /// </summary>
    public int MaxIterations { get; init; } = 100;

    /// <summary>
    /// The maximum time to wait for the session to become idle after sending a message.
    /// </summary>
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// An optional callback that is called when the agent completes, allowing for validation to check whether the agent has in fact succeeded in its task.
    /// If the validation returns a successful result, the run is deemed to have succeeded, and the validated result is returned to the caller.
    /// If the validation returns a failed result, the run continues, and the agent is prompted to try again with the optionally provided reason for failure.
    /// 
    /// If this callback is not provided, no validation is performed.
    /// </summary>
    public Func<TResult, Task<CopilotAgentValidationResult>>? ValidateResult { get; init; }
}
