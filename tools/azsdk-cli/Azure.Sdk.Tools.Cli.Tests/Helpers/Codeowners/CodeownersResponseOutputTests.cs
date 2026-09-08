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

    /// <summary>
    /// Pins the report template. <c>Does.Contain</c> assertions pass under any ordering, so the
    /// shape a contributor reads — ownership above violations, summary last — has to be asserted by
    /// position or it is not actually covered.
    /// </summary>
    [Test]
    public void LintReportFollowsTheTemplate()
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
                            SourceFile = "sdk/ai/owners.yaml:12",
                        }
                    ],
                    [new DirectoryOwners("sdk/ai", ["alice"], ["OpenAI"], ".")])
            ]
        };

        var lines = Strip(response.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(lines[0], Is.EqualTo("--- sdk/ai/owners.yaml ---"));
            Assert.That(lines[1], Is.EqualTo("  sdk/ai: alice"), "ownership comes before violations");
            Assert.That(lines[2], Is.EqualTo("  [LNT-OWN-001] owner @ghost is not a valid code owner"));
            Assert.That(lines[3], Is.EqualTo("    At: sdk/ai/owners.yaml:12"));
            Assert.That(lines[5], Is.EqualTo("=== Lint Report ==="));
            Assert.That(lines[6], Is.EqualTo("Fragments checked: 1"));
            Assert.That(lines[7], Is.EqualTo("Total violations: 1"));
            Assert.That(lines[^1], Is.EqualTo(
                "See https://aka.ms/azsdk/codeowners to learn how to fix these violations"));
        });
    }

    /// <summary>Only violations are red; the ownership report and the summary are not.</summary>
    [Test]
    public void OnlyViolationLinesAreRed()
    {
        var response = new CodeownersLintResponse
        {
            Fragments =
            [
                new FragmentLintResult(
                    "sdk/ai/owners.yaml",
                    [new LintViolation { RuleId = "LNT-OWN-001", Description = "bad owner" }],
                    [new DirectoryOwners("sdk/ai", ["alice"], [], ".")])
            ]
        };

        var lines = response.ToString().Split(Environment.NewLine);

        Assert.Multiple(() =>
        {
            Assert.That(lines.Where(l => l.Contains("LNT-OWN-001", StringComparison.Ordinal)).All(IsRed), Is.True);
            Assert.That(lines.Where(l => l.Contains("sdk/ai: alice", StringComparison.Ordinal)).Any(IsRed), Is.False);
            Assert.That(lines.Where(l => l.Contains("Total violations", StringComparison.Ordinal)).Any(IsRed), Is.False);
        });
    }

    [Test]
    public void CleanLintOmitsTheFixLink()
    {
        var response = new CodeownersLintResponse
        {
            Fragments = [new FragmentLintResult("sdk/ai/owners.yaml", [], [])]
        };

        var lines = Strip(response.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(lines[^1], Is.EqualTo("Total violations: 0"));
            Assert.That(response.ToString(), Does.Not.Contain("aka.ms/azsdk/codeowners"));
        });
    }

    /// <summary>
    /// The spec documents NO_COLOR support, so it is asserted here. Marked non-parallelizable
    /// because it mutates process-wide state.
    /// </summary>
    [Test]
    [NonParallelizable]
    public void NoColorSuppressesTheEscapes()
    {
        var original = Environment.GetEnvironmentVariable("NO_COLOR");
        try
        {
            Environment.SetEnvironmentVariable("NO_COLOR", "1");

            var response = new CodeownersLintResponse
            {
                Fragments =
                [
                    new FragmentLintResult(
                        "sdk/ai/owners.yaml",
                        [new LintViolation { RuleId = "LNT-OWN-001", Description = "bad owner" }],
                        [])
                ]
            };

            Assert.That(response.ToString().IndexOf('\u001b'), Is.EqualTo(-1),
                "NO_COLOR must suppress every escape");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NO_COLOR", original);
        }
    }

    /// <summary>
    /// Ordinal on purpose. NUnit's substring constraints compare culture-sensitively, and ICU treats
    /// ESC as an ignorable character, so <c>Does.Contain("\u001b")</c> matches a string that holds no
    /// escape at all. Every color assertion here has to bypass that.
    /// </summary>
    private static bool IsRed(string line) => line.Contains("\u001b[31m", StringComparison.Ordinal);

    /// <summary>Removes the color escapes so assertions can name the text a reader sees.</summary>
    private static string[] Strip(string output) =>
        [.. output.Replace("\u001b[31m", "").Replace("\u001b[0m", "").Split(Environment.NewLine)];

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
