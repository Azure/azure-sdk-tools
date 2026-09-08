// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using Azure.Sdk.Tools.Cli.Tools.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Config;

/// <summary>
/// Covers the tool's own handling of an unusable request. The helper throws on one, which is right
/// for a programmatic caller but useless at a command line, so the tool has to turn it into
/// something a person can act on.
/// </summary>
internal class CodeownersToolTests
{
    /// <summary>Fails the test if the tool reaches it, so these cases prove the request stopped earlier.</summary>
    private static ICheckPackageHelper UnreachedHelper()
    {
        var helper = new Mock<ICheckPackageHelper>(MockBehavior.Strict);
        helper
            .Setup(h => h.CheckPackage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Throws(new AssertionException("The request should not have reached the check-package helper."));

        return helper.Object;
    }

    private static CodeownersTool Tool() => new(
        NullLogger<CodeownersTool>.Instance,
        loggerFactory: null,
        codeownersGenerateHelper: null!,
        checkPackageHelper: UnreachedHelper(),
        codeownersLintHelper: null!,
        ownerValidator: null!,
        gitHelper: null!,
        devOpsService: null!);

    [TestCase("sdk/*/Azure.Wildcard", "must not contain '*'")]
    [TestCase("   ", "Directory path is required")]
    public async Task CheckPackage_UnusableDirectory_ReportsWhatToFixInsteadOfThrowing(
        string directoryPath, string expected)
    {
        var response = (CheckPackageResponse)await Tool().CheckPackage(directoryPath, "/repo");

        Assert.Multiple(() =>
        {
            Assert.That(response.Issues.Select(i => i.Code),
                Is.EqualTo(new[] { CheckPackageIssue.Codes.InvalidDirectoryPath }));
            Assert.That(response.Issues[0].Message, Does.Contain(expected));
            Assert.That(response.Issues[0].NextStep, Does.Contain("concrete package directory path"));
            Assert.That(response.Issues[0].CurrentValues, Is.EqualTo(new[] { directoryPath }));
            Assert.That(response.ExitCode, Is.Not.EqualTo(0));
        });
    }
}
