using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Helper class for check operations.
/// </summary>
public static class ChecksHelper
{
    /// <summary>
    /// Creates a CLICheckResponse from a process result.
    /// </summary>
    /// <param name="processResult">The process result to convert</param>
    /// <returns>CLI check response</returns>
    public static CLICheckResponse CreateResponseFromProcessResult(ProcessResult processResult)
    {
        return new CLICheckResponse(processResult.ExitCode, processResult.Output);
    }
}