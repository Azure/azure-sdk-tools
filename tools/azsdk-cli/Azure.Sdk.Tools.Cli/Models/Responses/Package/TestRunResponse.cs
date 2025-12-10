using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package;

/// <summary>
/// Response payload for package test runs with exit code and details.
/// </summary>
public class TestRunResponse : PackageResponseBase
{
    [JsonPropertyName("test_run_output")]
    public string? TestRunOutput { get; set; }

    public TestRunResponse(int exitCode, string? testRunOutput, string? error = null)
    {
        ExitCode = exitCode;
        TestRunOutput = testRunOutput;
        if (!string.IsNullOrEmpty(error))
        {
            ResponseError = error;
        }
    }

    public TestRunResponse(ProcessResult processResult)
    {
        ExitCode = processResult.ExitCode;
        TestRunOutput = processResult.Output;

        if (ExitCode != 0)
        {
            ResponseError = "Test run failed with a non-zero exit code";
        }
    }

    protected override string Format()
    {
        StringBuilder output = new();
        if (!string.IsNullOrEmpty(TestRunOutput))
        {
            output.Append(TestRunOutput);
        }
        else if (!string.IsNullOrEmpty(ResponseError))
        {
            output.Append(ResponseError);
        }
        else
        {
            output.Append("Test run executed.");
        }

        return output.ToString();
    }
}
