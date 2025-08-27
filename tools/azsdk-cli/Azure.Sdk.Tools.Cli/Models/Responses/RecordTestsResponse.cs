using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Base class for CLI check responses with exit code and output.
/// </summary>
public class RecordTestsResponse : Response
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("failed_test_name")]
    public string FailedTestName { get; set; }

    [JsonPropertyName("recommendation")]
    public string Recommendation { get; set; }

    public RecordTestsResponse(bool success, string failedTestName = null, string recommendation = null, string error = null)
    {
        Success = success;
        FailedTestName = failedTestName;

        if (recommendation is not null)
        {
            NextSteps = [$"Apply these changes, then run the tool again: {recommendation}"];
        }
    }

    public override string ToString()
    {
        if (Success)
        {
            return ToString("All tests were recorded successfully");
        }
        else
        {
            return ToString($"Test recording failed for test: {FailedTestName}. Recommendation: {Recommendation}");
        }
    }
}