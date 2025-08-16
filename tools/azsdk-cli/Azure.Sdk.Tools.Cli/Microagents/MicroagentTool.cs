
using System.Text.Json;

namespace Azure.Sdk.Tools.Cli.Microagents
{
    public interface IAgentTool
    {
        /// <summary>
        /// Name of the tool
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Agent-friendly description of the tool
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Type repesenting a JSON schema of the input to the tool
        /// </summary>
        Type? InputSchema { get; }

        /// <summary>
        /// Type representing a JSON schema of the output of the tool
        /// </summary>
        Type? OutputSchema { get; }

        /// <summary>
        /// Invoke the tool with the specified input as a JSON string
        /// </summary>
        /// <param name="input">The input</param>
        /// <returns>Tool call result as a JSON string</returns>
        Task<string> InvokeAsync(string input, CancellationToken ct = default);
    }

    /// <summary>
    /// Strongly-typed implementation of IAgentTool providing serialization
    /// and deserialization support
    /// </summary>
    /// <typeparam name="TInput">Input schema type</typeparam>
    /// <typeparam name="TOutput">Output schema type</typeparam>
    public abstract class AgentTool<TInput, TOutput> : IAgentTool
    {
        public Type InputSchema { get; } = typeof(TInput);
        public Type OutputSchema { get; } = typeof(TOutput);
        public abstract string Name { get; init; }
        public abstract string Description { get; init; }

        public async Task<string> InvokeAsync(string input, CancellationToken ct = default)
        {
            var deserialized = JsonSerializer.Deserialize<TInput>(input);
            var result = await this.InvokeAsync(deserialized, ct);
            return JsonSerializer.Serialize(result);
        }

        public abstract Task<TOutput> InvokeAsync(TInput input, CancellationToken ct);
    }
}
