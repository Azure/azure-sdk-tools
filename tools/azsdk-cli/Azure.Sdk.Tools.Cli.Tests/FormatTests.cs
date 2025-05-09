using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.TeamFoundation.TestManagement.WebApi;

namespace Azure.Sdk.Tools.Cli.Tests;

internal class FormatTests
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
    public void TestLogAnalysisJsonResponse()
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
  ""suggestedfix"": ""a test suggested fix""
}";

        var logger = new TestLogger<LogAnalysisResponse>();
        var response = new ResponseService(new JsonFormatter()).Respond(new LogAnalysisResponse
        {
            Summary = summary,
            Errors = errors,
            SuggestedFix = suggestedFix
        });

        Assert.That(response, Is.EqualTo(json));
    }

    [Test]
    public void TestLogAnalysisPlainTextResponse()
    {

        var response = new ResponseService(new PlainTextFormatter()).Respond(new LogAnalysisResponse
        {
            Summary = summary,
            Errors = errors,
            SuggestedFix = suggestedFix
        });

        var expectedStr = @"
### Summary:
a test summary

### Suggested Fix:
a test suggested fix

### Errors:
file1:1 - message1
file2:2 - message2
".TrimStart();

        Assert.That(response, Is.EqualTo(expectedStr));
    }
}
