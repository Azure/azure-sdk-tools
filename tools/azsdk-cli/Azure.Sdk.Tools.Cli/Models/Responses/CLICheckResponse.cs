using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Base class for CLI check responses with exit code and output.
/// </summary>
public class CLICheckResponse: Response
{
    [JsonPropertyName("exit_code")]
    public int ExitCode { get; set;}
    
    [JsonPropertyName("check_status_details")]
    public string CheckStatusDetails { get; set;}

    public CLICheckResponse() { }

    public CLICheckResponse(int exitCode, string checkStatusDetails, string error = null)
    {
        ExitCode = exitCode;
        CheckStatusDetails = checkStatusDetails;
        if (!string.IsNullOrEmpty(error))
        {
            ResponseError = error;
        }
    }

    /// <summary>
    /// Creates a CLICheckResponse from a process result.
    /// </summary>
    /// <param name="processResult">The process result to convert</param>
    /// <returns>CLI check response</returns>
    public static CLICheckResponse CreateResponseFromProcessResult(ProcessResult processResult)
    {
        return new CLICheckResponse(processResult.ExitCode, processResult.Output);
    }

    public override string ToString()
    {
        return ToString(CheckStatusDetails);
    }
}

/// <summary>
/// CLI check response for cookbook/documentation reference responses.
/// </summary>
public class CookbookCLICheckResponse : CLICheckResponse
{
    [JsonPropertyName("cookbook_reference")]
    public string CookbookReference { get; set;}

    public CookbookCLICheckResponse(int exitCode, string checkStatusDetails, string cookbookReference) : base(exitCode, checkStatusDetails)
    {
        CookbookReference = cookbookReference;
    }

    public override string ToString()
    {
        return ToString(CheckStatusDetails);
    }
}

