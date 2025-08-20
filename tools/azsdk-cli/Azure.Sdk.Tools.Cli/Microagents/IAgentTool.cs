namespace Azure.Sdk.Tools.Cli.Microagents;

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
    /// String representing a JSON schema of the input to the tool
    /// </summary>
    string InputSchema { get; }

    /// <summary>
    /// Invoke the tool with the specified input as a JSON string
    /// </summary>
    /// <param name="input">The input</param>
    /// <returns>Tool call result as a JSON string</returns>
    Task<string> Invoke(string input, CancellationToken ct = default);
}
