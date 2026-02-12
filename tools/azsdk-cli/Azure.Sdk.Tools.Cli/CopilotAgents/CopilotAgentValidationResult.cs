namespace Azure.Sdk.Tools.Cli.CopilotAgents;

/// <summary>
/// The result of validating a copilot agent's output, including whether it succeeded and, if not, the reason for failure.
/// </summary>
public class CopilotAgentValidationResult
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
