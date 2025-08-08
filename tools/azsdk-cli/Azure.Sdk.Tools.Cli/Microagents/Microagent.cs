namespace Azure.Sdk.Tools.Cli.Microagents;

/// <summary>
/// A definition of a microagent, including a system prompt, a list of tools, and a model.
/// 
/// A microagent is a lightweight, focused agent designed to perform a specific task or set of tasks within a larger system.
/// It operates an agent loop (LLM <-> tool call) until the LLM determines that its task is complete, at which point
/// it calls the special sentinel "exit" tool with the result.
/// </summary>
/// <typeparam name="TResult">A type representing the schema of the result of the agent's execution</typeparam>
/// <param name="SystemPrompt">The system prompt provided to the agent.</param>
/// <param name="Tools">A list of tools that are made available to the agent. For a simple LLM call without tools, this can be left empty.</param>
/// <param name="Model">The model that this microagent is designed for.</param>
/// <param name="MaxIterations">The maximum number of iterations in the agent loop. If this is exceeded, the microagent run is deemed to have failed.</param>
public record Microagent<TResult>(
    string SystemPrompt,
    IEnumerable<IAgentTool> Tools = null,
    string Model = "gpt-4.1",
    int MaxIterations = 1_000
);
