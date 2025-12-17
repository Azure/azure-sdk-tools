using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture]
public class PackageCheckResponseTests
{
    [Test]
    public void TestInitWithMultiple()
    {
        var resp = new PackageCheckResponse([
            CreateProcessResult(1001, "output", "error")
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(resp.CheckStatusDetails, Is.EqualTo(
                // NOTE: the "output\nerror\n" is just how the ProcessResult class handles stdout and stderr being added, it's not part of the formatting in PackageCheckResponse.
                $"output{Environment.NewLine}error{Environment.NewLine}" + Environment.NewLine));
            Assert.That(resp.ExitCode, Is.EqualTo(1001));
        });

        resp = new PackageCheckResponse([
            // NOTE: the "output\nerror\n" is just how the ProcessResult class handles stdout and stderr being added, it's not part of the formatting in PackageCheckResponse.
            CreateProcessResult(0, "first-output", "first-error"),
            CreateProcessResult(1001, "last-output", "last-error")
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(resp.CheckStatusDetails, Is.EqualTo(
                $"first-output{Environment.NewLine}first-error{Environment.NewLine}" + Environment.NewLine +
                $"last-output{Environment.NewLine}last-error{Environment.NewLine}" + Environment.NewLine));

            // last exit code wins.
            Assert.That(resp.ExitCode, Is.EqualTo(1001));
        });
    }

    private static ProcessResult CreateProcessResult(int exitCode, string stdout, string stderr)
    {
        var pr = new ProcessResult
        {
            ExitCode = exitCode
        };

        pr.AppendStdout(stdout);
        pr.AppendStderr(stderr);

        return pr;
    }
}
