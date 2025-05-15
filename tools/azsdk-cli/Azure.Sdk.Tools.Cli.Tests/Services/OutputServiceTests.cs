using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

internal class OutputServiceTests
{
    private readonly string summary = "a test summary";
    private readonly List<LogError> errors =
    [
        new LogError
        {
            File = "file1",
            Line = 1,
            Message = "message1"
        },
        new LogError
        {
            File = "file2",
            Line = 2,
            Message = "message2"
        }
    ];
    private readonly string suggestedFix = "a test suggested fix";

    [Test]
    public void TestTypedJsonOutput()
    {
        var json = @"{
  ""summary"": ""a test summary"",
  ""errors"": [
    {
      ""file"": ""file1"",
      ""line"": 1,
      ""message"": ""message1""
    },
    {
      ""file"": ""file2"",
      ""line"": 2,
      ""message"": ""message2""
    }
  ],
  ""suggested_fix"": ""a test suggested fix""
}";

        var output = new OutputService(OutputModes.Json);
        var formatted = output.ValidateAndFormat<LogAnalysisResponse>(json);

        Assert.That(formatted, Is.EqualTo(json));
    }

    [Test]
    public void TestPlainTextOutput()
    {
        var response = new LogAnalysisResponse
        {
            Summary = summary,
            Errors = errors,
            SuggestedFix = suggestedFix
        };

        var expectedStr = @"
### Summary:
a test summary

### Suggested Fix:
a test suggested fix

### Errors:
file1:1 - message1
file2:2 - message2
".TrimStart();

        var output = new OutputService(OutputModes.Plain);
        var formatted = output.Format(response);

        Assert.That(formatted, Is.EqualTo(expectedStr));
    }
}
