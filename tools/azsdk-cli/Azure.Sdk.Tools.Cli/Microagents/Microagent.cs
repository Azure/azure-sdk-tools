namespace Azure.Sdk.Tools.Cli.Microagents;

/// <summary>
/// The result of a microagent run, including whether it succeeded and, if not, the reason for failure.
/// </summary>
public class MicroagentValidationResult
{
    /// <summary>
    /// Whether the result is valid.
    /// </summary>
    public required bool Success { get; set; }

    /// <summary>
    /// The reason for failure if Success is false. This can be a string or a more complex object that can be serialized to JSON.
    /// Only populated when Success is false; otherwise, it is null.
    /// </summary>
    public object? Reason { get; set; }
}

/// <summary>
/// A definition of a microagent, including a system prompt, a list of tools, and a model.
/// 
/// A microagent is a lightweight, focused agent designed to perform a specific task or set of tasks within a larger system.
/// It operates an agent loop (LLM <-> tool call) until the LLM determines that its task is complete, at which point
/// it calls the special sentinel "exit" tool with the result.
/// </summary>
/// <typeparam name="TResult">A type representing the schema of the result of the agent's execution</typeparam>
public class Microagent<TResult> where TResult : notnull
{
    /// <summary>
    /// The instructions provided to the agent.
    /// </summary>
    public required string Instructions { get; init; }

    /// <summary>
    /// An optional callback that is called when the microagent calls the 'exit' tool, allowing for validation to check whether the agent has in fact succeeded in its task.
    /// If the validation returns a successful result, the run is deemed to have succeeded, and the validated result is returned to the caller.
    /// If the validation returns a failed result, the run continues, and the agent is prompted to try again with the optionally provided reason for failure.
    /// 
    /// If this callback is not provided, no validation is performed.
    /// </summary>
    public Func<TResult, Task<MicroagentValidationResult>>? ValidateResult { get; init; }

    /// <summary>
    /// A list of tools that are made available to the agent. For a simple LLM call without tools, this can be left empty.
    /// </summary>
    public IEnumerable<IAgentTool> Tools { get; init; } = [];

    /// <summary>
    /// The model that this microagent is designed for.
    /// </summary>
    public string Model { get; init; } = "gpt-4.1";

    /// <summary>
    /// The maximum number of tool calls in the agent loop. If this is exceeded, the microagent run is deemed to have failed.
    /// </summary>
    public int MaxToolCalls { get; init; } = 100;
}
