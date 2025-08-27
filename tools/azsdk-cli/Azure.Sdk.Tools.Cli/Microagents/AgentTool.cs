
using System.Text.Json;

namespace Azure.Sdk.Tools.Cli.Microagents;

/// <summary>
/// Strongly-typed implementation of IAgentTool providing serialization
/// and deserialization support
/// </summary>
/// <typeparam name="TInput">Input schema type</typeparam>
/// <typeparam name="TOutput">Output schema type</typeparam>
public abstract class AgentTool<TInput, TOutput> : IAgentTool
{
    public string InputSchema { get; } = ToolHelpers.GetJsonSchemaRepresentation(typeof(TInput));

    public abstract string Name { get; init; }
    public abstract string Description { get; init; }

    public async Task<string> Invoke(string input, CancellationToken ct = default)
    {
        var deserialized = JsonSerializer.Deserialize<TInput>(input);
        var result = await this.Invoke(deserialized, ct);
        return JsonSerializer.Serialize(result);
    }

    public abstract Task<TOutput> Invoke(TInput input, CancellationToken ct);

    /// <summary>
    /// Creates an AgentTool that wraps a function to invoke, rather than requiring a class.
    /// </summary>
    /// <param name="name">The name of the tool</param>
    /// <param name="description">The description of the tool</param>
    /// <param name="invokeHandler">The function to invoke when this tool is used.</param>
    /// <returns></returns>
    public static AgentTool<TInput, TOutput> FromFunc(string name, string description, Func<TInput, CancellationToken, Task<TOutput>> invokeHandler)
    {
        return new FuncAgentTool<TInput, TOutput>(name, description, invokeHandler);
    }

    /// <summary>
    /// An AgentTool that wraps a function to invoke, rather than requiring a class.
    /// </summary>
    /// <typeparam name="TInput">Input schema type</typeparam>
    /// <typeparam name="TOutput">Output schema type</typeparam>
    /// <param name="name">The name of the tool</param>
    /// <param name="description">The description of the tool</param>
    /// <param name="invokeHandler">The function to invoke when this tool is used.</param>
    private class FuncAgentTool<ToolInputT, ToolOutputT>(string name, string description, Func<ToolInputT, CancellationToken, Task<ToolOutputT>> invokeHandler) : AgentTool<ToolInputT, ToolOutputT>
    {
        private readonly Func<ToolInputT, CancellationToken, Task<ToolOutputT>> invoke = invokeHandler;

        public override string Name { get; init; } = name;
        public override string Description { get; init; } = description;

        public override Task<ToolOutputT> Invoke(ToolInputT input, CancellationToken ct)
        {
            return invoke(input, ct);
        }
    }
}
