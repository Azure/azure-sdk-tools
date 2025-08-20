
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
}
