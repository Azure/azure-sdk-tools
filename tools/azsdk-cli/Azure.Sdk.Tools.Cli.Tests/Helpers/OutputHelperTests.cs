using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

internal class OutputHelperTests
{
    private readonly List<LogEntry> errors =
    [
        new LogEntry
        {
            File = "file1",
            Line = 1,
            Message = "message1"
        },
        new LogEntry
        {
            File = "file2",
            Line = 2,
            Message = "message2"
        }
    ];

    [Test]
    public void TestTypedJsonOutput()
    {
        var json = @"{
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
  ""operation_status"": ""Succeeded""
}";

        var output = new OutputHelper(OutputHelper.OutputModes.Json);
        var formatted = output.ValidateAndFormat<LogAnalysisResponse>(json);

        // Normalize line endings before comparison. The repo .gitattributes enforces LF in source
        // files, so verbatim string literals contain \n, but production code uses Environment.NewLine
        // which is \r\n on Windows.
        Assert.That(formatted.ReplaceLineEndings("\n"), Is.EqualTo(json.ReplaceLineEndings("\n")));
    }

    [Test]
    public void TestPlainTextOutput()
    {
        var response = new LogAnalysisResponse
        {
            Errors = errors,
        };

        var expectedStr = @"
### Errors:
--> file1:1
message1

--> file2:2
message2
".TrimStart();

        var output = new OutputHelper(OutputHelper.OutputModes.Plain);
        var formatted = output.Format(response);

        // Normalize line endings before comparison. See comment in TestTypedJsonOutput.
        Assert.That(formatted.ReplaceLineEndings("\n"), Is.EqualTo(expectedStr.ReplaceLineEndings("\n")));
    }

    [Test]
    public void CheckPackageResponse_UsesDerivedResponseErrorWhenReferencedAsBase()
    {
        CommandResponse response = new CheckPackageResponse
        {
            DirectoryPath = "sdk/test/Azure.Test",
        };

        ((CheckPackageResponse)response).Issues.Add(new CheckPackageIssue
        {
            Code = "insufficient_owners",
            Message = "single issue message",
            NextStep = $"/owners add owners {CheckPackageHelper.CurrentGitHubUserPlaceholder} to package Azure.Test",
        });

        Assert.Multiple(() =>
        {
            Assert.That(response.ResponseError, Is.EqualTo("single issue message"));
            Assert.That(response.OperationStatus, Is.EqualTo(Status.Failed));
            Assert.That(response.ExitCode, Is.EqualTo(1));
        });
    }

    [Test]
    public void CheckPackageResponse_AggregatesMultipleIssuesIntoSummary()
    {
        var response = new CheckPackageResponse
        {
            DirectoryPath = "sdk/test/Azure.Test",
        };

        response.Issues.Add(new CheckPackageIssue
        {
            Code = "insufficient_owners",
            Message = "first issue",
            NextStep = "first prompt",
        });
        response.Issues.Add(new CheckPackageIssue
        {
            Code = "missing_pr_label",
            Message = "second issue",
            NextStep = "second prompt",
        });

        Assert.That(response.ResponseError, Is.EqualTo("check-package found 2 issue(s) for path 'sdk/test/Azure.Test'."));
    }

    [Test]
    public void CheckPackageResponse_DoesNotSerializeOwnerPromptUser()
    {
        var response = new CheckPackageResponse
        {
            DirectoryPath = "sdk/test/Azure.Test",
            PackageName = "Azure.Test",
        };

        var output = new OutputHelper(OutputHelper.OutputModes.Json);
        var formatted = output.Format(response);

        Assert.That(formatted, Does.Not.Contain("owner_prompt_user"));
    }

    [Test]
    public void CheckPackageResponse_UsesCodeownersSupportChannelInJson()
    {
        var response = new CheckPackageResponse
        {
            DirectoryPath = "sdk/test/Azure.Test",
        };

        response.Issues.Add(new CheckPackageIssue
        {
            Code = "insufficient_owners",
            Message = "single issue message",
            NextStep = "prompt",
        });

        var output = new OutputHelper(OutputHelper.OutputModes.Json);
        var formatted = output.Format(response);
        using var document = JsonDocument.Parse(formatted);

        Assert.That(
            document.RootElement.GetProperty("support_channel").GetString(),
            Is.EqualTo("aka.ms/azsdk/codeowners"));
    }

    [Test]
    public void DefaultCommandResponse_UsesDefaultSupportChannelInJson()
    {
        var response = new DefaultCommandResponse
        {
            ResponseError = "command failed",
        };

        var output = new OutputHelper(OutputHelper.OutputModes.Json);
        var formatted = output.Format(response);
        using var document = JsonDocument.Parse(formatted);

        Assert.That(
            document.RootElement.GetProperty("support_channel").GetString(),
            Is.EqualTo(CommandResponse.SupportChannelMessage));
    }
}
