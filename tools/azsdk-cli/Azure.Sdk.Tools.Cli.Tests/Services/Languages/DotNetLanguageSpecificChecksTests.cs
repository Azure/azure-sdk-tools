using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture]
internal class DotNetLanguageSpecificChecksTests
{
    private Mock<IProcessHelper> _processHelperMock = null!;
    private Mock<IGitHelper> _gitHelperMock = null!;
    private DotNetLanguageSpecificChecks _languageChecks = null!;
    private string _packagePath = null!;
    private string _repoRoot = null!;
    private const string RequiredDotNetVersion = "9.0.102";

    [SetUp]
    public void SetUp()
    {
        _processHelperMock = new Mock<IProcessHelper>();
        _gitHelperMock = new Mock<IGitHelper>();

        _languageChecks = new DotNetLanguageSpecificChecks(
            _processHelperMock.Object,
            _gitHelperMock.Object,
            NullLogger<DotNetLanguageSpecificChecks>.Instance);

        _repoRoot = Path.Combine(Path.GetTempPath(), "azure-sdk-for-net");
        _packagePath = Path.Combine(_repoRoot, "sdk", "storage", "Azure.Storage.Blobs");
    }

    private void SetupSuccessfulDotNetVersionCheck()
    {
        var versionOutput = $"9.0.100 [C:\\Program Files\\dotnet\\sdk]\n{RequiredDotNetVersion} [C:\\Program Files\\dotnet\\sdk]";
        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout(versionOutput);

        _processHelperMock
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(p => 
                    p.Command == "cmd.exe" && 
                    p.Args.Contains("/C") && 
                    p.Args.Contains("dotnet") && 
                    p.Args.Contains("--list-sdks")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);
    }

    private void SetupGitRepoDiscovery()
    {
        _gitHelperMock
            .Setup(x => x.DiscoverRepoRoot(It.IsAny<string>()))
            .Returns(_repoRoot);
    }

    [Test]
    public async Task TestPackCodeAsyncDotNetNotInstalledReturnsError()
    {
        var processResult = new ProcessResult { ExitCode = 1 };
        processResult.AppendStderr("dotnet command not found");

        _processHelperMock
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(p => 
                    p.Command == "cmd.exe" && 
                    p.Args.Contains("/C") && 
                    p.Args.Contains("dotnet") && 
                    p.Args.Contains("--list-sdks")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        var result = await _languageChecks.PackCodeAsync(_packagePath, CancellationToken.None);
        
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.CheckStatusDetails, Does.Contain("dotnet --list-sdks failed"));
        });

        _processHelperMock.Verify(
            x => x.Run(
                It.Is<ProcessOptions>(p => p.Args.Contains("pack")),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task TestPackCodeAsyncOldDotNetVersionReturnsError()
    {
        var versionOutput = "8.0.100 [C:\\Program Files\\dotnet\\sdk]\n8.0.200 [C:\\Program Files\\dotnet\\sdk]";
        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout(versionOutput);

        _processHelperMock
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(p =>
                    p.Command == "cmd.exe" &&
                    p.Args.Contains("/C") &&
                    p.Args.Contains("dotnet") &&
                    p.Args.Contains("--list-sdks")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        var result = await _languageChecks.PackCodeAsync(_packagePath, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("below minimum requirement"));
            Assert.That(result.ResponseError, Does.Contain(RequiredDotNetVersion));
        });
    }

    [Test]
    public async Task TestPackCodeAsyncInvalidPackagePathReturnsError()
    {
        SetupSuccessfulDotNetVersionCheck();
        var invalidPath = "/tmp/not-in-sdk-folder";

        var result = await _languageChecks.PackCodeAsync(invalidPath, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("Failed to determine service directory"));
        });
    }

    [Test]
    public async Task TestPackCodeAsyncSuccessReturnsSuccess()
    {
        SetupSuccessfulDotNetVersionCheck();

        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout(
            "Microsoft (R) Build Engine version 17.10.0+1\n" +
            "Build started 1/9/2025 10:30:00 AM.\n" +
            "  Determining projects to restore...\n" +
            "  All projects are up-to-date for restore.\n" +
            "  Azure.Storage.Blobs -> bin/Release/net8.0/Azure.Storage.Blobs.dll\n" +
            "  Successfully created package 'bin/Release/Azure.Storage.Blobs.1.0.0.nupkg'.\n" +
            "Build succeeded.\n" +
            "    0 Warning(s)\n" +
            "    0 Error(s)"
        );

        _processHelperMock
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(p =>
                    p.Command == "cmd.exe" &&
                    p.Args.Contains("/C") &&
                    p.Args.Contains("dotnet") &&
                    p.Args.Contains("pack") &&
                    p.Args.Any(a => a.Contains("service.proj"))),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        SetupGitRepoDiscovery();

        var result = await _languageChecks.PackCodeAsync(_packagePath, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.CheckStatusDetails, Does.Contain("Build succeeded"));
        });

        _processHelperMock.Verify(
            x => x.Run(
                It.Is<ProcessOptions>(p =>
                    p.Command == "cmd.exe" &&
                    p.Args.Contains("/C") &&
                    p.Args.Contains("dotnet") &&
                    p.Args.Contains("pack") &&
                    p.Args.Any(a => a.Contains("service.proj")) &&
                    p.Args.Contains("-warnaserror") &&
                    p.Args.Contains("/p:ValidateRunApiCompat=true") &&
                    p.Args.Contains("/p:SDKType=client") &&
                    p.Args.Contains("/p:ServiceDirectory=storage") &&
                    p.Args.Contains("/p:IncludeTests=false") &&
                    p.Args.Contains("/p:PublicSign=false") &&
                    p.Args.Contains("/p:Configuration=Release") &&
                    p.Args.Contains("/p:IncludePerf=false") &&
                    p.Args.Contains("/p:IncludeStress=false") &&
                    p.Args.Contains("/p:IncludeIntegrationTests=false") &&
                    p.WorkingDirectory == _repoRoot &&
                    p.Timeout == TimeSpan.FromMinutes(10)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task TestPackCodeAsyncBuildFailureReturnsError()
    {
        SetupSuccessfulDotNetVersionCheck();

        _processHelperMock
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(p =>
                    p.Command == "cmd.exe" &&
                    p.Args.Contains("/C") &&
                    p.Args.Contains("dotnet") &&
                    p.Args.Contains("pack")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var processResult = new ProcessResult { ExitCode = 1 };
                processResult.AppendStderr("error CS0246: The type or namespace name 'Azure' could not be found");
                return processResult;
            });

        SetupGitRepoDiscovery();

        var result = await _languageChecks.PackCodeAsync(_packagePath, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.CheckStatusDetails, Does.Contain("could not be found"));
        });
    }

    [Test]
    public async Task TestCheckGeneratedCodeAsyncDotNetNotInstalledReturnsError()
    {
        var processResult = new ProcessResult { ExitCode = 1 };
        processResult.AppendStderr("dotnet command not found");

        _processHelperMock
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(p => 
                    p.Command == "cmd.exe" && 
                    p.Args.Contains("/C") && 
                    p.Args.Contains("dotnet") && 
                    p.Args.Contains("--list-sdks")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        var result = await _languageChecks.CheckGeneratedCodeAsync(_packagePath, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.CheckStatusDetails, Does.Contain("dotnet --list-sdks failed"));
        });
    }

    [Test]
    public async Task TestCheckGeneratedCodeAsyncInvalidPackagePathReturnsError()
    {
        SetupSuccessfulDotNetVersionCheck();
        var invalidPath = "/tmp/not-in-sdk-folder";

        var result = await _languageChecks.CheckGeneratedCodeAsync(invalidPath, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("Failed to determine service directory"));
        });
    }

    [Test]
    public async Task TestCheckGeneratedCodeAsyncSuccessReturnsSuccess()
    {
        SetupSuccessfulDotNetVersionCheck();
        SetupGitRepoDiscovery();

        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout("All checks passed successfully!");

        _processHelperMock
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(p => 
                    p.Command == "pwsh" && 
                    p.Args.Any(a => a.Contains("CodeChecks.ps1"))),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        var result = await _languageChecks.CheckGeneratedCodeAsync(_packagePath, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.CheckStatusDetails, Does.Contain("All checks passed successfully"));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    [Test]
    public async Task TestCheckGeneratedCodeAsyncSpellCheckFailureReturnsError(bool isGeneratedCode)
    {

        var errorMessage = isGeneratedCode ?
            "Generated code does not match committed code. Please regenerate and commit." :
            "Spell check failed: 'BlobClinet' is misspelled. Did you mean 'BlobClient'?";

        var expectedDetail = isGeneratedCode ?
            "Generated code does not match" :
            "Spell check failed";

        SetupSuccessfulDotNetVersionCheck();

        _processHelperMock
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(p =>
                    p.Command == "pwsh" &&
                    p.Args.Any(a => a.Contains("CodeChecks.ps1"))),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var processResult = new ProcessResult { ExitCode = 1 };
                processResult.AppendStderr(errorMessage);
                return processResult;
            });

        SetupGitRepoDiscovery();

        var result = await _languageChecks.CheckGeneratedCodeAsync(_packagePath, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.CheckStatusDetails, Does.Contain(expectedDetail));
        });
    }

    [Test]
    public async Task TestCheckAotCompatAsyncDotNetNotInstalledReturnsError()
    {
        var processResult = new ProcessResult { ExitCode = 1 };
        processResult.AppendStderr("dotnet command not found");

        _processHelperMock
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(p => 
                    p.Command == "cmd.exe" && 
                    p.Args.Contains("/C") && 
                    p.Args.Contains("dotnet") && 
                    p.Args.Contains("--list-sdks")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        var result = await _languageChecks.CheckAotCompatAsync(_packagePath, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.CheckStatusDetails, Does.Contain("dotnet --list-sdks failed"));
        });
    }

    [Test]
    public async Task TestCheckAotCompatAsyncInvalidPackagePathReturnsError()
    {
        SetupSuccessfulDotNetVersionCheck();
        var invalidPath = "/tmp/not-in-sdk-folder";

        var result = await _languageChecks.CheckAotCompatAsync(invalidPath, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("Failed to determine service directory or package name"));
        });
    }

    [Test]
    public async Task TestCheckAotCompatAsyncSuccessReturnsSuccess()
    {
        SetupSuccessfulDotNetVersionCheck();
        SetupGitRepoDiscovery();

        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout("AOT compatibility check passed!");

        _processHelperMock
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(p => 
                    p.Command == "pwsh" && 
                    p.Args.Any(a => a.Contains("Check-AOT-Compatibility.ps1"))),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        var result = await _languageChecks.CheckAotCompatAsync(_packagePath, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.CheckStatusDetails, Does.Contain("AOT compatibility check passed"));
        });
    }

    [Test]
    public async Task TestCheckAotCompatAsyncAotWarningsReturnsError()
    {
        SetupSuccessfulDotNetVersionCheck();
        SetupGitRepoDiscovery();

        var errorMessage = "ILLink : Trim analysis warning IL2026: Azure.Storage.Blobs.BlobClient.Upload: " +
            "Using member 'System.Reflection.Assembly.GetTypes()' which has 'RequiresUnreferencedCodeAttribute' " +
            "can break functionality when trimming application code.";
        
        var processResult = new ProcessResult { ExitCode = 1 };
        processResult.AppendStderr(errorMessage);
        
        _processHelperMock
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(p => 
                    p.Command == "pwsh" && 
                    p.Args.Any(a => a.Contains("Check-AOT-Compatibility.ps1"))),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        var result = await _languageChecks.CheckAotCompatAsync(_packagePath, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.CheckStatusDetails, Does.Contain("Trim analysis warning"));
            Assert.That(result.CheckStatusDetails, Does.Contain("RequiresUnreferencedCodeAttribute"));
        });
    }
}
