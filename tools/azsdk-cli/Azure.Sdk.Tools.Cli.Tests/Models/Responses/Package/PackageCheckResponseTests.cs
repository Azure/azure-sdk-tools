using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture]
public class PackageCheckResponseTests
{
    [Test]
    public void TestConstructorMultipleResponses_Validation()
    {
        var notUsed = new ProcessResult();

        var ex = Assert.Throws<ArgumentException>(() => new PackageCheckResponse("", Models.SdkLanguage.Go, [notUsed]));
        Assert.That(ex.ParamName, Is.EqualTo("packageName"));

        // purposefully allowing a null
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        ex = Assert.Throws<ArgumentNullException>(() => new PackageCheckResponse(null, Models.SdkLanguage.Go, [notUsed]));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        Assert.That(ex.ParamName, Is.EqualTo("packageName"));

        ex = Assert.Throws<ArgumentException>(() => new PackageCheckResponse("aztemplate", Models.SdkLanguage.Unknown, [notUsed]));
        Assert.That(ex.ParamName, Is.EqualTo("language"));

        ex = Assert.Throws<ArgumentException>(() => new PackageCheckResponse("aztemplate", Models.SdkLanguage.Go, []));
        Assert.That(ex.ParamName, Is.EqualTo("processResults"));

        // purposefully allowing a null
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
        ex = Assert.Throws<ArgumentNullException>(() => new PackageCheckResponse("aztemplate", Models.SdkLanguage.Go, (IEnumerable<ProcessResult>)null));
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        Assert.That(ex.ParamName, Is.EqualTo("processResults"));
    }

    [Test]
    public void TestConstructorSingleResponse_Validation()
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        ArgumentException ex = Assert.Throws<ArgumentNullException>(() => new PackageCheckResponse(null, Models.SdkLanguage.Go, 0, "", ""));
        Assert.That(ex.ParamName, Is.EqualTo("packageName"));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

        ex = Assert.Throws<ArgumentException>(() => new PackageCheckResponse("", Models.SdkLanguage.Go, 0, "", ""));
        Assert.That(ex.ParamName, Is.EqualTo("packageName"));

        ex = Assert.Throws<ArgumentException>(() => new PackageCheckResponse("aztemplate", Models.SdkLanguage.Unknown, 0, "", ""));
        Assert.That(ex.ParamName, Is.EqualTo("language"));
    }

    [Test]
    public void TestConstructWithMultipleResponses()
    {
        var resp = new PackageCheckResponse("github.com/Azure/azure-sdk-for-go/sdk/template/aztemplate", Models.SdkLanguage.Go, [
            CreateProcessResult(1001, "output", "error")
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(resp.CheckStatusDetails, Is.EqualTo(
                // NOTE: the "output\nerror\n" is just how the ProcessResult class handles stdout and stderr being added, it's not part of the formatting in PackageCheckResponse.
                $"output{Environment.NewLine}error{Environment.NewLine}" + Environment.NewLine));
            Assert.That(resp.ExitCode, Is.EqualTo(1001));
            Assert.That(resp.PackageName, Is.EqualTo("github.com/Azure/azure-sdk-for-go/sdk/template/aztemplate"));
            Assert.That(resp.Language, Is.EqualTo(Models.SdkLanguage.Go));
        });

        resp = new PackageCheckResponse("github.com/Azure/azure-sdk-for-go/sdk/template/aztemplate", Models.SdkLanguage.Go, [
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
            Assert.That(resp.PackageName, Is.EqualTo("github.com/Azure/azure-sdk-for-go/sdk/template/aztemplate"));
            Assert.That(resp.Language, Is.EqualTo(Models.SdkLanguage.Go));
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
