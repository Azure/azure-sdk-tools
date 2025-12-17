using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package;

/// <summary>
/// Base class for CLI check responses with exit code and output.
/// </summary>
public class PackageCheckResponse : PackageResponseBase
{
    // Map ExitCode to CliExitCode for JSON serialization
    [JsonPropertyName("exit_code")]
    public int CliExitCode => ExitCode;

    [JsonPropertyName("check_status_details")]
    public string CheckStatusDetails { get; set; }

    public PackageCheckResponse() { }

    public PackageCheckResponse(int exitCode, string checkStatusDetails, string error = null)
    {
        ExitCode = exitCode;
        CheckStatusDetails = checkStatusDetails;
        if (!string.IsNullOrEmpty(error))
        {
            ResponseError = error;
        }
    }

    /// <summary>
    /// Combines multiple <see cref="ProcessResult"/> instances into a single response.
    /// </summary>
    /// <param name="processResults">Results to combine.
    /// <see cref="ExitCode"/> is set to the last result's exit code
    /// <see cref="CheckStatusDetails"/> is set to the concatenated output from all results.
    /// </param>
    public PackageCheckResponse(IEnumerable<ProcessResult> processResults)
    {
        var output = new StringBuilder();

        foreach (var pr in processResults)
        {
            ExitCode = pr.ExitCode;

            output.AppendLine(pr.Output);
            output.AppendLine();
        }

        CheckStatusDetails = output.ToString();
    }

    public PackageCheckResponse(ProcessResult processResult)
    {
        ExitCode = processResult.ExitCode;
        CheckStatusDetails = processResult.Output;
    }

    protected override string Format()
    {
        StringBuilder output = new();
        output.Append($"Check status: {CheckStatusDetails}");
        output.Append($"Language: {Language}");
        output.Append($"Package PackageName: {PackageName}");
        return output.ToString();
    }
}

/// <summary>
/// Package check response for cookbook/documentation reference responses.
/// </summary>
public class CookbookPackageCheckResponse : PackageCheckResponse
{
    [JsonPropertyName("cookbook_reference")]
    public string CookbookReference { get; set; }

    public CookbookPackageCheckResponse(int exitCode, string checkStatusDetails, string cookbookReference) : base(exitCode, checkStatusDetails)
    {
        CookbookReference = cookbookReference;
    }

    protected override string Format()
    {
        return CheckStatusDetails;
    }
}

