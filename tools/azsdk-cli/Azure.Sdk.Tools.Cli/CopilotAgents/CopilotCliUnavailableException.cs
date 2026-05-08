namespace Azure.Sdk.Tools.Cli.CopilotAgents;

/// <summary>
/// Thrown when the GitHub Copilot CLI is not installed, not found, or not authenticated.
/// </summary>
public class CopilotCliUnavailableException : InvalidOperationException
{
    public CopilotCliUnavailableException(string message) : base(message) { }
    public CopilotCliUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}
