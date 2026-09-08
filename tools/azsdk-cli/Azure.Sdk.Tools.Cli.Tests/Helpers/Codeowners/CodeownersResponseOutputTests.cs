// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Codeowners;

/// <summary>
/// These commands are gates: they exit non-zero so a build fails, and they print a report so the
/// contributor can act on it. Signalling the failure through <c>ResponseError</c> gets the first
/// half and silently loses the second, because a Failed response prints only the error line. These
/// tests hold both halves together.
/// </summary>
[TestFixture]
public class CodeownersResponseOutputTests
{
    [Test]
    public void LintFailureStillPrintsTheReport()
    {
        var response = new CodeownersLintResponse
        {
            Fragments =
            [
                new FragmentLintResult(
                    "sdk/ai/owners.yaml",
                    [
                        new LintViolation
                        {
                            RuleId = "LNT-OWN-001",
                            Description = "owner @ghost is not a valid code owner",
                            Detail = "Ask them to join the azure-sdk-write team.",
                            SourceFile = "sdk/ai/owners.yaml:12",
                        }
                    ],
                    [])
            ]
        };

        var output = response.ToString();

        Assert.That(response.ExitCode, Is.EqualTo(1), "violations have to fail the build");
        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("LNT-OWN-001"));
            Assert.That(output, Does.Contain("@ghost"));
            Assert.That(output, Does.Contain("azure-sdk-write"), "the fix instruction is the point");
            Assert.That(output, Does.Contain("sdk/ai/owners.yaml:12"));
        });
    }

    [Test]
    public void CleanLintExitsZero()
    {
        var response = new CodeownersLintResponse
        {
            Fragments = [new FragmentLintResult("sdk/ai/owners.yaml", [], [])]
        };

        Assert.That(response.ExitCode, Is.EqualTo(0));
    }

    [Test]
    public void InvalidOwnerStillPrintsWhatToDoAboutIt()
    {
        var response = new ValidateOwnerResponse
        {
            Owner = "ghost",
            Valid = false,
            Violation = new LintViolation
            {
                RuleId = "LNT-OWN-001",
                Description = "ghost is not a member of azure-sdk-write",
                Detail = "Their Azure org membership may be private.",
            }
        };

        var output = response.ToString();

        Assert.That(response.ExitCode, Is.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("azure-sdk-write"));
            Assert.That(output, Does.Contain("private"), "the detail is the actionable half");
        });
    }

    [Test]
    public void ValidOwnerExitsZero()
    {
        var response = new ValidateOwnerResponse { Owner = "someone", Valid = true, Members = ["someone"] };

        Assert.That(response.ExitCode, Is.EqualTo(0));
        Assert.That(response.ToString(), Does.Contain("someone"));
    }

    [Test]
    public void CheckPackageFailureStillPrintsTheReport()
    {
        var response = new CheckPackageResponse
        {
            DirectoryPath = "sdk/ai/Azure.AI.Inference",
        };
        response.Issues.Add(new CheckPackageIssue
        {
            Code = CheckPackageIssue.Codes.InsufficientOwners,
            Message = "Found 1 source owner, 2 are required.",
            NextStep = "Add an owner to sdk/ai/owners.yaml.",
        });

        var output = response.ToString();

        Assert.That(response.ExitCode, Is.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("sdk/ai/Azure.AI.Inference"));
            Assert.That(output, Does.Contain("2 are required"));
        });
    }
}
