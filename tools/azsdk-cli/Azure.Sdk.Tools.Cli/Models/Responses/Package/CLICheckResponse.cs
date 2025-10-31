using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package;

/// <summary>
/// Base class for CLI check responses with exit code and output.
/// </summary>
public class CLICheckResponse : PackageResponseBase
{
    // Map ExitCode to CliExitCode for JSON serialization
    [JsonPropertyName("exit_code")]
    public int CliExitCode => ExitCode;

    [JsonPropertyName("check_status_details")]
    public string CheckStatusDetails { get; set; }

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

    public CLICheckResponse(ProcessResult processResult)
    {
        ExitCode = processResult.ExitCode;
        CheckStatusDetails = processResult.Output;
    }

    protected override string Format()
    {
        StringBuilder output = new ();
        output.Append($"Check status: {CheckStatusDetails}");
        output.Append($"Language: {Language}");
        output.Append($"Package PackageName: {PackageName}");
        return output.ToString();
    }
}

/// <summary>
/// CLI check response for cookbook/documentation reference responses.
/// </summary>
public class CookbookCLICheckResponse : CLICheckResponse
{
    [JsonPropertyName("cookbook_reference")]
    public string CookbookReference { get; set; }

    public CookbookCLICheckResponse(int exitCode, string checkStatusDetails, string cookbookReference) : base(exitCode, checkStatusDetails)
    {
        CookbookReference = cookbookReference;
    }

    protected override string Format()
    {
        return CheckStatusDetails;
    }
}

