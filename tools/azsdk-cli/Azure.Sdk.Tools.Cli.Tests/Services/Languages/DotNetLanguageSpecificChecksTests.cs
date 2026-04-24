using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Azure.Sdk.Tools.Cli.CopilotAgents;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture]
internal class DotNetLanguageSpecificChecksTests
{
    private Mock<IProcessHelper> _processHelperMock = null!;
    private Mock<IGitHelper> _gitHelperMock = null!;
    private Mock<IPowershellHelper> _powerShellHelperMock = null!;
    private Mock<ICommonValidationHelpers> _commonValidationHelperMock = null!;
    private Mock<IPackageInfoHelper> _packageInfoHelperMock = null!;
    private DotnetLanguageService _languageChecks = null!;
    private string _packagePath = null!;
    private string _repoRoot = null!;
    private const string RequiredDotNetVersion = "9.0.102";

    [SetUp]
    public void SetUp()
    {
        _processHelperMock = new Mock<IProcessHelper>();
        _gitHelperMock = new Mock<IGitHelper>();
        _gitHelperMock.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net");
        _powerShellHelperMock = new Mock<IPowershellHelper>();
        _commonValidationHelperMock = new Mock<ICommonValidationHelpers>();
        _packageInfoHelperMock = new Mock<IPackageInfoHelper>();

        _languageChecks = new DotnetLanguageService(
            _processHelperMock.Object,
            _powerShellHelperMock.Object,
            Mock.Of<ICopilotAgentRunner>(),
            _gitHelperMock.Object,
            NullLogger<DotnetLanguageService>.Instance,
            _commonValidationHelperMock.Object,
            _packageInfoHelperMock.Object,
            Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(),
            Mock.Of<IChangelogHelper>());

        _repoRoot = Path.Combine(Path.GetTempPath(), "azure-sdk-for-net");
        _packagePath = Path.Combine(_repoRoot, "sdk", "healthdataaiservices", "Azure.Health.Deidentification");
    }

    private void SetupSuccessfulDotNetVersionCheck()
    {
        var versionOutput = $"9.0.102 [C:\\Program Files\\dotnet\\sdk]\n{RequiredDotNetVersion} [C:\\Program Files\\dotnet\\sdk]";
        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout(versionOutput);

        _processHelperMock
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(p => IsDotNetListSdksCommand(p)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);
    }

    private void SetupGitRepoDiscovery()
    {
        _gitHelperMock
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_repoRoot);

        // Dynamically compute relative path from sdk/ for any package path
        _packageInfoHelperMock
            .Setup(p => p.ParsePackagePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((path, _) =>
            {
                var sdkRoot = Path.Combine(_repoRoot, "sdk");
                var relativePath = Path.GetRelativePath(sdkRoot, path);
                return Task.FromResult((_repoRoot, relativePath, path));
            });
    }

    [Test]
    public async Task TestCheckGeneratedCodeAsyncDotNetNotInstalledReturnsError()
    {
        var processResult = new ProcessResult { ExitCode = 1 };
        processResult.AppendStderr("dotnet command not found");

        _processHelperMock
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(p => IsDotNetListSdksCommand(p)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        var result = await _languageChecks.CheckGeneratedCode(_packagePath, ct: CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.CheckStatusDetails, Does.Contain("dotnet --list-sdks failed"));
            Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty, "NextSteps should be populated on failure");
            Assert.That(result.NextSteps, Has.Some.Contain("Install the .NET SDK"));
        });
    }

    [Test]
    public async Task TestCheckGeneratedCodeAsyncDotNetVersionTooLowReturnsError()
    {
        var versionOutput = "6.0.427 [C:\\Program Files\\dotnet\\sdk]\n8.0.404 [C:\\Program Files\\dotnet\\sdk]";
        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout(versionOutput);

        _processHelperMock
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(p => IsDotNetListSdksCommand(p)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        var result = await _languageChecks.CheckGeneratedCode(_packagePath, ct: CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain(".NET SDK version 8.0.404 is below minimum requirement of 9.0.102"));
            Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty, "NextSteps should be populated on version failure");
            Assert.That(result.NextSteps, Has.Some.Contain("Update the .NET SDK"));
        });
    }

    [Test]
    public async Task TestCheckGeneratedCodeAsyncInvalidPackagePathReturnsError()
    {
        SetupSuccessfulDotNetVersionCheck();
        var invalidPath = "/tmp/not-in-sdk-folder";

        var result = await _languageChecks.CheckGeneratedCode(invalidPath, ct: CancellationToken.None);

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

        var scriptPath = Path.Combine(_repoRoot, "eng", "scripts", "CodeChecks.ps1");
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        File.WriteAllText(scriptPath, "# Mock PowerShell script");

        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout("All checks passed successfully!");

        _powerShellHelperMock
            .Setup(x => x.Run(
                It.Is<PowershellOptions>(p => p.ScriptPath != null &&
                    p.ScriptPath.Contains("CodeChecks.ps1")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        try
        {
            var result = await _languageChecks.CheckGeneratedCode(_packagePath, ct: CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0));
                Assert.That(result.CheckStatusDetails, Does.Contain("All checks passed successfully"));
            });
        }
        finally
        {
            try { File.Delete(scriptPath); Directory.Delete(Path.GetDirectoryName(scriptPath)!, true); } catch { }
        }
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
        SetupGitRepoDiscovery();

        var scriptPath = Path.Combine(_repoRoot, "eng", "scripts", "CodeChecks.ps1");
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        File.WriteAllText(scriptPath, "# Mock PowerShell script");

        _powerShellHelperMock
            .Setup(x => x.Run(
                It.Is<PowershellOptions>(p => p.ScriptPath != null &&
                    p.ScriptPath.Contains("CodeChecks.ps1")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var processResult = new ProcessResult { ExitCode = 1 };
                processResult.AppendStdout(errorMessage);
                processResult.AppendStdout($"error : {errorMessage}");
                return processResult;
            });

        var result = await _languageChecks.CheckGeneratedCode(_packagePath, ct: CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.CheckStatusDetails, Does.Contain(expectedDetail));
            Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty, "NextSteps should be populated on failure");
            Assert.That(result.NextSteps, Has.Some.Contain(errorMessage));
        });
    }

    [Test]
    public async Task TestCheckAotCompatAsyncDotNetNotInstalledReturnsError()
    {
        var processResult = new ProcessResult { ExitCode = 1 };
        processResult.AppendStderr("dotnet command not found");

        _processHelperMock
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(p => IsDotNetListSdksCommand(p)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        var result = await _languageChecks.CheckAotCompat(_packagePath, ct: CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.CheckStatusDetails, Does.Contain("dotnet --list-sdks failed"));
            Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty, "NextSteps should be populated on failure");
            Assert.That(result.NextSteps, Has.Some.Contain("Install the .NET SDK"));
        });
    }

    [Test]
    public async Task TestCheckAotCompatAsyncDotNetVersionTooLowReturnsError()
    {
        var versionOutput = "6.0.427 [C:\\Program Files\\dotnet\\sdk]\n8.0.404 [C:\\Program Files\\dotnet\\sdk]";
        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout(versionOutput);

        _processHelperMock
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(p => IsDotNetListSdksCommand(p)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        var result = await _languageChecks.CheckAotCompat(_packagePath, ct: CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain(".NET SDK version 8.0.404 is below minimum requirement of 9.0.102"));
            Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty, "NextSteps should be populated on version failure");
            Assert.That(result.NextSteps, Has.Some.Contain("Update the .NET SDK"));
        });
    }

    [Test]
    public async Task TestCheckAotCompatAsyncInvalidPackagePathReturnsError()
    {
        SetupSuccessfulDotNetVersionCheck();
        var invalidPath = "/tmp/not-in-sdk-folder";

        var result = await _languageChecks.CheckAotCompat(invalidPath, ct: CancellationToken.None);

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

        var scriptPath = Path.Combine(_repoRoot, "eng", "scripts", "compatibility", "Check-AOT-Compatibility.ps1");
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        File.WriteAllText(scriptPath, "# Mock PowerShell script");

        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout("AOT compatibility check passed!");

        _powerShellHelperMock
            .Setup(x => x.Run(
                It.Is<PowershellOptions>(p => p.ScriptPath != null &&
                    p.ScriptPath.Contains("Check-AOT-Compatibility.ps1")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        try
        {
            var result = await _languageChecks.CheckAotCompat(_packagePath, ct: CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0));
                Assert.That(result.CheckStatusDetails, Does.Contain("AOT compatibility check passed"));
            });
        }
        finally
        {
            try { File.Delete(scriptPath); Directory.Delete(Path.GetDirectoryName(scriptPath)!, true); } catch { }
        }
    }

    [Test]
    public async Task TestCheckAotCompatAsyncAotWarningsReturnsError()
    {
        SetupSuccessfulDotNetVersionCheck();
        SetupGitRepoDiscovery();

        var scriptPath = Path.Combine(_repoRoot, "eng", "scripts", "compatibility", "Check-AOT-Compatibility.ps1");
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        File.WriteAllText(scriptPath, "# Mock PowerShell script");

        var errorMessage = "ILLink : Trim analysis warning IL2026: Azure.Storage.Blobs.BlobClient.Upload: " +
            "Using member 'System.Reflection.Assembly.GetTypes()' which has 'RequiresUnreferencedCodeAttribute' " +
            "can break functionality when trimming application code.";

        var processResult = new ProcessResult { ExitCode = 1 };
        processResult.AppendStdout(errorMessage);

        _powerShellHelperMock
            .Setup(x => x.Run(
                It.Is<PowershellOptions>(p => p.ScriptPath != null &&
                    p.ScriptPath.Contains("Check-AOT-Compatibility.ps1")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        try
        {
            var result = await _languageChecks.CheckAotCompat(_packagePath, ct: CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.CheckStatusDetails, Does.Contain("Trim analysis warning"));
                Assert.That(result.CheckStatusDetails, Does.Contain("RequiresUnreferencedCodeAttribute"));
                Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty, "NextSteps should be populated on AOT failure");
                Assert.That(result.NextSteps, Has.Some.Contain("AOT"));
            });
        }
        finally
        {
            try { File.Delete(scriptPath); Directory.Delete(Path.GetDirectoryName(scriptPath)!, true); } catch { }
        }
    }

    [Test]
    public async Task TestCheckAotCompatAsyncWithOptOutReturnsSkipped()
    {
        SetupSuccessfulDotNetVersionCheck();
        SetupGitRepoDiscovery();

        var testPackagePath = Path.Combine(_repoRoot, "sdk", "testservice", "Azure.TestService");
        Directory.CreateDirectory(testPackagePath);

        var csprojPath = Path.Combine(testPackagePath, "Azure.TestService.csproj");
        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <AotCompatOptOut>true</AotCompatOptOut>
  </PropertyGroup>
</Project>";
        File.WriteAllText(csprojPath, csprojContent);

        try
        {
            var result = await _languageChecks.CheckAotCompat(testPackagePath, ct: CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0));
                Assert.That(result.CheckStatusDetails, Does.Contain("AOT compatibility check skipped"));
                Assert.That(result.CheckStatusDetails, Does.Contain("AotCompatOptOut is set to true"));
            });
        }
        finally
        {
            try { Directory.Delete(testPackagePath, true); } catch { }
        }
    }

    [Test]
    public async Task TestCheckAotCompatAsyncWithoutOptOutRunsCheck()
    {
        SetupSuccessfulDotNetVersionCheck();
        SetupGitRepoDiscovery();

        var testPackagePath = Path.Combine(_repoRoot, "sdk", "testservice", "Azure.TestService");
        Directory.CreateDirectory(testPackagePath);

        var csprojPath = Path.Combine(testPackagePath, "Azure.TestService.csproj");
        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
  </PropertyGroup>
</Project>";
        File.WriteAllText(csprojPath, csprojContent);

        var scriptPath = Path.Combine(_repoRoot, "eng", "scripts", "compatibility", "Check-AOT-Compatibility.ps1");
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        File.WriteAllText(scriptPath, "# Mock PowerShell script");

        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout("AOT compatibility check passed!");

        _powerShellHelperMock
            .Setup(x => x.Run(
                It.Is<PowershellOptions>(p => p.ScriptPath != null &&
                    p.ScriptPath.Contains("Check-AOT-Compatibility.ps1")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        try
        {
            var result = await _languageChecks.CheckAotCompat(testPackagePath, ct: CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0));
                Assert.That(result.CheckStatusDetails, Does.Contain("AOT compatibility check passed"));
                Assert.That(result.CheckStatusDetails, Does.Not.Contain("skipped"));
            });
        }
        finally
        {
            try
            {
                Directory.Delete(testPackagePath, true);
                File.Delete(scriptPath);
                Directory.Delete(Path.GetDirectoryName(scriptPath)!, true);
            }
            catch { }
        }
    }

    [Test]
    public async Task TestCheckAotCompatAsyncOptOutCaseInsensitive()
    {
        SetupSuccessfulDotNetVersionCheck();
        SetupGitRepoDiscovery();

        var testPackagePath = Path.Combine(_repoRoot, "sdk", "testservice", "Azure.TestService");
        Directory.CreateDirectory(testPackagePath);

        var csprojPath = Path.Combine(testPackagePath, "Azure.TestService.csproj");
        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <AOTCOMPATOPTOUT>TRUE</AOTCOMPATOPTOUT>
  </PropertyGroup>
</Project>";
        File.WriteAllText(csprojPath, csprojContent);

        try
        {
            var result = await _languageChecks.CheckAotCompat(testPackagePath, ct: CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0));
                Assert.That(result.CheckStatusDetails, Does.Contain("AOT compatibility check skipped"));
                Assert.That(result.CheckStatusDetails, Does.Contain("AotCompatOptOut is set to true"));
            });
        }
        finally
        {
            try { Directory.Delete(testPackagePath, true); } catch { }
        }
    }

    [Test]
    public async Task TestCheckAotCompatAsyncNoCsprojFileRunsCheck()
    {
        SetupSuccessfulDotNetVersionCheck();
        SetupGitRepoDiscovery();

        var testPackagePath = Path.Combine(_repoRoot, "sdk", "testservice", "Azure.TestService");
        Directory.CreateDirectory(testPackagePath);

        var scriptPath = Path.Combine(_repoRoot, "eng", "scripts", "compatibility", "Check-AOT-Compatibility.ps1");
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        File.WriteAllText(scriptPath, "# Mock PowerShell script");

        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout("AOT compatibility check passed!");

        _powerShellHelperMock
            .Setup(x => x.Run(
                It.Is<PowershellOptions>(p => p.ScriptPath != null &&
                    p.ScriptPath.Contains("Check-AOT-Compatibility.ps1")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        try
        {
            var result = await _languageChecks.CheckAotCompat(testPackagePath, ct: CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0));
                Assert.That(result.CheckStatusDetails, Does.Contain("AOT compatibility check passed"));
                Assert.That(result.CheckStatusDetails, Does.Not.Contain("skipped"));
            });

            _powerShellHelperMock.Verify(x => x.Run(
                It.Is<PowershellOptions>(p => p.ScriptPath != null &&
                    p.ScriptPath.Contains("Check-AOT-Compatibility.ps1")),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            try
            {
                Directory.Delete(testPackagePath, true);
                File.Delete(scriptPath);
                Directory.Delete(Path.GetDirectoryName(scriptPath)!, true);
            }
            catch { }
        }
    }

    #region Helper Methods for Cross-Platform Command Validation

    /// <summary>
    /// Checks if the ProcessOptions represents a dotnet --list-sdks command.
    /// Handles both Unix (dotnet --list-sdks) and Windows (cmd.exe /C dotnet --list-sdks) patterns.
    /// </summary>
    private static bool IsDotNetListSdksCommand(ProcessOptions options) =>
        (options.Command == "dotnet" && options.Args.Contains("--list-sdks")) ||
        (options.Command == "cmd.exe" && options.Args.Contains("dotnet") && options.Args.Contains("--list-sdks"));

    /// <summary>
    /// Checks if the ProcessOptions represents a dotnet test command with the specified working directory.
    /// Handles both Unix (dotnet test) and Windows (cmd.exe /C dotnet test) patterns.
    /// </summary>
    private static bool IsDotNetTestCommand(ProcessOptions options, string expectedWorkingDirectory) =>
        ((options.Command == "dotnet" && options.Args.Contains("test")) ||
         (options.Command == "cmd.exe" && options.Args.Contains("dotnet") && options.Args.Contains("test"))) &&
        options.WorkingDirectory == expectedWorkingDirectory;

    /// <summary>
    /// Checks if the ProcessOptions represents a test-proxy push command.
    /// Handles both Unix (test-proxy push) and Windows (cmd.exe /C test-proxy push) patterns.
    /// </summary>
    private static bool IsTestProxyPushCommand(ProcessOptions options) =>
        (options.Command == "test-proxy" && options.Args.Contains("push")) ||
        (options.Command == "cmd.exe" && options.Args.Contains("test-proxy") && options.Args.Contains("push"));

    #endregion

    #region HasCustomizations Tests

    [Test]
    public void HasCustomizations_ReturnsPath_WhenPartialClassExistsOutsideGeneratedFolder()
    {
        using var tempDir = TempDirectory.Create("dotnet-customization-test");
        var srcDir = Path.Combine(tempDir.DirectoryPath, "src");
        Directory.CreateDirectory(srcDir);

        // Create a partial class file outside Generated folder
        File.WriteAllText(Path.Combine(srcDir, "CustomClient.cs"), @"
namespace Azure.Test;
public partial class TestClient
{
    public void CustomMethod() { }
}");

        var result = _languageChecks.HasCustomizations(tempDir.DirectoryPath);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo(srcDir));
    }

    [Test]
    public void HasCustomizations_ReturnsNull_WhenNoPartialClassesExist()
    {
        using var tempDir = TempDirectory.Create("dotnet-no-customization-test");
        var srcDir = Path.Combine(tempDir.DirectoryPath, "src");
        Directory.CreateDirectory(srcDir);

        // Create a regular class file (not partial)
        File.WriteAllText(Path.Combine(srcDir, "RegularClass.cs"), @"
namespace Azure.Test;
public class RegularClass
{
    public void Method() { }
}");

        var result = _languageChecks.HasCustomizations(tempDir.DirectoryPath);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void HasCustomizations_ReturnsNull_WhenPartialClassOnlyInGeneratedFolder()
    {
        using var tempDir = TempDirectory.Create("dotnet-generated-only-test");
        var srcDir = Path.Combine(tempDir.DirectoryPath, "src");
        var generatedDir = Path.Combine(srcDir, "Generated");
        Directory.CreateDirectory(generatedDir);

        // Create a partial class inside Generated folder
        File.WriteAllText(Path.Combine(generatedDir, "GeneratedClient.cs"), @"
namespace Azure.Test;
public partial class TestClient
{
    public void GeneratedMethod() { }
}");

        var result = _languageChecks.HasCustomizations(tempDir.DirectoryPath);

        Assert.That(result, Is.Null);
    }

    #endregion

    #region RunAllTests Tests

    [Test]
    public async Task RunAllTests_UsesTestsDirectory_WhenTestsDirectoryExists()
    {
        using var tempDir = TempDirectory.Create("dotnet-test-directory-test");
        var testsDir = Path.Combine(tempDir.DirectoryPath, "tests");
        Directory.CreateDirectory(testsDir);

        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout("Tests passed!");

        _processHelperMock
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(p => IsDotNetTestCommand(p, testsDir)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        var result = await _languageChecks.RunAllTests(tempDir.DirectoryPath, ct: CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.TestRunOutput, Does.Contain("Tests passed!"));
        });

        _processHelperMock.Verify(x => x.Run(
            It.Is<ProcessOptions>(p => IsDotNetTestCommand(p, testsDir)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunAllTests_UsesPackageDirectory_WhenTestsDirectoryDoesNotExist()
    {
        using var tempDir = TempDirectory.Create("dotnet-no-test-directory-test");

        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout("Tests passed!");

        _processHelperMock
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(p => IsDotNetTestCommand(p, tempDir.DirectoryPath)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        var result = await _languageChecks.RunAllTests(tempDir.DirectoryPath, ct: CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.TestRunOutput, Does.Contain("Tests passed!"));
        });

        _processHelperMock.Verify(x => x.Run(
            It.Is<ProcessOptions>(p => IsDotNetTestCommand(p, tempDir.DirectoryPath)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [TestCase(TestMode.Playback, "playback")]
    [TestCase(TestMode.Record, "record")]
    [TestCase(TestMode.Live, "live")]
    public async Task RunAllTests_WhenFrameworkUnknown_SetsBothTestModeEnvironmentVariables(TestMode testMode, string expectedEnvValue)
    {
        // No .csproj files in the temp directory, so framework detection returns Unknown
        // and both AZURE_TEST_MODE and CLIENTMODEL_TEST_MODE should be set.
        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout("Tests passed!");

        ProcessOptions? capturedOptions = null;
        _processHelperMock
            .Setup(p => p.Run(It.Is<ProcessOptions>(o => o.Args.Contains("test")), It.IsAny<CancellationToken>()))
            .Callback<ProcessOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(processResult);

        var result = await _languageChecks.RunAllTests(_packagePath, testMode, ct: CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(capturedOptions, Is.Not.Null);
            Assert.That(capturedOptions!.EnvironmentVariables, Is.Not.Null);
            Assert.That(capturedOptions.EnvironmentVariables!["AZURE_TEST_MODE"], Is.EqualTo(expectedEnvValue));
            Assert.That(capturedOptions.EnvironmentVariables!["CLIENTMODEL_TEST_MODE"], Is.EqualTo(expectedEnvValue));
        });
    }

    [Test]
    public async Task RunAllTests_PassesThroughLiveTestEnvironmentVariables()
    {
        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout("Tests passed!");

        ProcessOptions? capturedOptions = null;
        _processHelperMock
            .Setup(p => p.Run(It.Is<ProcessOptions>(o => o.Args.Contains("test")), It.IsAny<CancellationToken>()))
            .Callback<ProcessOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(processResult);

        var envVars = new Dictionary<string, string>
        {
            ["AZURE_SUBSCRIPTION_ID"] = "sub-123",
            ["AZURE_RESOURCE_GROUP"] = "rg-test",
        };

        var result = await _languageChecks.RunAllTests(_packagePath, TestMode.Live, envVars, ct: CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(capturedOptions, Is.Not.Null);
            Assert.That(capturedOptions!.EnvironmentVariables!["AZURE_SUBSCRIPTION_ID"], Is.EqualTo("sub-123"));
            Assert.That(capturedOptions.EnvironmentVariables["AZURE_RESOURCE_GROUP"], Is.EqualTo("rg-test"));
            Assert.That(capturedOptions.EnvironmentVariables["AZURE_TEST_MODE"], Is.EqualTo("live"));
            Assert.That(capturedOptions.EnvironmentVariables["CLIENTMODEL_TEST_MODE"], Is.EqualTo("live"));
        });
    }

    [Test]
    public async Task RunAllTests_UsesDefaultTimeoutForPlayback()
    {
        var processResult = new ProcessResult { ExitCode = 0 };

        ProcessOptions? capturedOptions = null;
        _processHelperMock
            .Setup(p => p.Run(It.Is<ProcessOptions>(o => o.Args.Contains("test")), It.IsAny<CancellationToken>()))
            .Callback<ProcessOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(processResult);

        await _languageChecks.RunAllTests(_packagePath, TestMode.Playback, ct: CancellationToken.None);

        Assert.That(capturedOptions!.Timeout, Is.EqualTo(ProcessOptions.DEFAULT_PROCESS_TIMEOUT));
    }

    [Test]
    [TestCase(TestMode.Record)]
    [TestCase(TestMode.Live)]
    public async Task RunAllTests_UsesLongerTimeoutForLiveAndRecordModes(TestMode testMode)
    {
        var processResult = new ProcessResult { ExitCode = 0 };

        ProcessOptions? capturedOptions = null;
        _processHelperMock
            .Setup(p => p.Run(It.Is<ProcessOptions>(o => o.Args.Contains("test")), It.IsAny<CancellationToken>()))
            .Callback<ProcessOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(processResult);

        await _languageChecks.RunAllTests(_packagePath, testMode, ct: CancellationToken.None);

        Assert.That(capturedOptions!.Timeout, Is.GreaterThan(ProcessOptions.DEFAULT_PROCESS_TIMEOUT));
    }

    [Test]
    public async Task RunAllTests_PushesAssetsAfterSuccessfulRecordMode()
    {
        using var tempDir = TempDirectory.Create("dotnet-asset-push-test");
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "assets.json"), "{}");

        var testResult = new ProcessResult { ExitCode = 0 };
        testResult.AppendStdout("Tests passed!");

        var pushResult = new ProcessResult { ExitCode = 0 };
        pushResult.AppendStdout("Assets pushed!");

        _processHelperMock
            .Setup(p => p.Run(It.Is<ProcessOptions>(o => o.Args.Contains("test")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testResult);

        _processHelperMock
            .Setup(p => p.Run(It.Is<ProcessOptions>(o => o.Args.Contains("push")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pushResult);

        var result = await _languageChecks.RunAllTests(tempDir.DirectoryPath, TestMode.Record, ct: CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        _processHelperMock.Verify(p => p.Run(It.Is<ProcessOptions>(o => o.Args.Contains("test")), It.IsAny<CancellationToken>()), Times.Once);
        _processHelperMock.Verify(p => p.Run(
            It.Is<ProcessOptions>(o => IsTestProxyPushCommand(o)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunAllTests_DoesNotPushAssetsInPlaybackMode()
    {
        using var tempDir = TempDirectory.Create("dotnet-no-push-playback-test");
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "assets.json"), "{}");

        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout("Tests passed!");

        _processHelperMock
            .Setup(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        await _languageChecks.RunAllTests(tempDir.DirectoryPath, TestMode.Playback, ct: CancellationToken.None);

        _processHelperMock.Verify(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunAllTests_DoesNotPushAssetsWhenTestsFail()
    {
        using var tempDir = TempDirectory.Create("dotnet-no-push-fail-test");
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "assets.json"), "{}");

        var processResult = new ProcessResult { ExitCode = 1 };
        processResult.AppendStderr("Tests failed!");

        _processHelperMock
            .Setup(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        var result = await _languageChecks.RunAllTests(tempDir.DirectoryPath, TestMode.Record, ct: CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        _processHelperMock.Verify(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunAllTests_DoesNotPushAssetsWhenNoAssetsJson()
    {
        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout("Tests passed!");

        _processHelperMock
            .Setup(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        var result = await _languageChecks.RunAllTests(_packagePath, TestMode.Record, ct: CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        _processHelperMock.Verify(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunAllTests_WhenFrameworkUnknown_DefaultModeIsPlayback()
    {
        // No .csproj files in the temp directory, so framework detection returns Unknown
        // and both AZURE_TEST_MODE and CLIENTMODEL_TEST_MODE should be set to playback.
        var processResult = new ProcessResult { ExitCode = 0 };

        ProcessOptions? capturedOptions = null;
        _processHelperMock
            .Setup(p => p.Run(It.Is<ProcessOptions>(o => o.Args.Contains("test")), It.IsAny<CancellationToken>()))
            .Callback<ProcessOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(processResult);

        // Call without specifying testMode - should default to Playback
        await _languageChecks.RunAllTests(_packagePath, ct: CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(capturedOptions!.EnvironmentVariables!["AZURE_TEST_MODE"], Is.EqualTo("playback"));
            Assert.That(capturedOptions.EnvironmentVariables["CLIENTMODEL_TEST_MODE"], Is.EqualTo("playback"));
        });
    }

    #endregion

    #region CheckSpelling Tests

    [Test]
    public async Task CheckSpelling_DelegatesToCommonValidationHelpers()
    {
        var expectedSuccess = new PackageCheckResponse(0, "No spelling errors found");

        string? capturedPackagePath = null;
        _commonValidationHelperMock
            .Setup(c => c.CheckSpelling(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, bool, CancellationToken>((pkgPath, _, _) =>
            {
                capturedPackagePath = pkgPath;
            })
            .ReturnsAsync(expectedSuccess);

        var response = await _languageChecks.CheckSpelling(_packagePath, false, CancellationToken.None);

        Assert.That(response.ExitCode, Is.EqualTo(0));
        Assert.That(capturedPackagePath, Is.EqualTo(_packagePath));

        _commonValidationHelperMock.Verify(
            c => c.CheckSpelling(_packagePath, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task CheckSpelling_WithFixEnabled_DelegatesToCommonValidationHelpers()
    {
        _commonValidationHelperMock
            .Setup(c => c.CheckSpelling(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageCheckResponse(0, "Spelling issues fixed"));

        var response = await _languageChecks.CheckSpelling(_packagePath, true, CancellationToken.None);

        Assert.That(response.ExitCode, Is.EqualTo(0));

        _commonValidationHelperMock.Verify(
            c => c.CheckSpelling(_packagePath, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region RunAllTests with Framework Detection Tests

    [Test]
    public async Task RunAllTests_SetsOnlyAzureTestMode_WhenAzureCoreTestFrameworkDetected()
    {
        using var tempDir = TempDirectory.Create("dotnet-runall-azcore");
        var testsDir = Path.Combine(tempDir.DirectoryPath, "tests");
        Directory.CreateDirectory(testsDir);

        File.WriteAllText(Path.Combine(testsDir, "MyPackage.Tests.csproj"), @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""$(AzureCoreTestFramework)"" />
  </ItemGroup>
</Project>");

        var processResult = new ProcessResult { ExitCode = 0 };
        ProcessOptions? capturedOptions = null;
        _processHelperMock
            .Setup(p => p.Run(It.Is<ProcessOptions>(o => o.Args.Contains("test")), It.IsAny<CancellationToken>()))
            .Callback<ProcessOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(processResult);

        await _languageChecks.RunAllTests(tempDir.DirectoryPath, TestMode.Record, ct: CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(capturedOptions!.EnvironmentVariables, Does.ContainKey("AZURE_TEST_MODE"));
            Assert.That(capturedOptions.EnvironmentVariables!["AZURE_TEST_MODE"], Is.EqualTo("record"));
            Assert.That(capturedOptions.EnvironmentVariables.ContainsKey("CLIENTMODEL_TEST_MODE"), Is.False);
        });
    }

    [Test]
    public async Task RunAllTests_SetsOnlyClientModelTestMode_WhenClientModelTestFrameworkDetected()
    {
        using var tempDir = TempDirectory.Create("dotnet-runall-clientmodel");
        var testsDir = Path.Combine(tempDir.DirectoryPath, "tests");
        Directory.CreateDirectory(testsDir);

        File.WriteAllText(Path.Combine(testsDir, "MyPackage.Tests.csproj"), @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Microsoft.ClientModel.TestFramework"" Version=""1.0.0"" />
  </ItemGroup>
</Project>");

        var processResult = new ProcessResult { ExitCode = 0 };
        ProcessOptions? capturedOptions = null;
        _processHelperMock
            .Setup(p => p.Run(It.Is<ProcessOptions>(o => o.Args.Contains("test")), It.IsAny<CancellationToken>()))
            .Callback<ProcessOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(processResult);

        await _languageChecks.RunAllTests(tempDir.DirectoryPath, TestMode.Live, ct: CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(capturedOptions!.EnvironmentVariables, Does.ContainKey("CLIENTMODEL_TEST_MODE"));
            Assert.That(capturedOptions.EnvironmentVariables!["CLIENTMODEL_TEST_MODE"], Is.EqualTo("live"));
            Assert.That(capturedOptions.EnvironmentVariables.ContainsKey("AZURE_TEST_MODE"), Is.False);
        });
    }

    [Test]
    public async Task RunAllTests_SetsBothEnvVars_WhenFrameworkUnknown()
    {
        // _packagePath doesn't have a real .csproj, so framework is Unknown
        var processResult = new ProcessResult { ExitCode = 0 };
        ProcessOptions? capturedOptions = null;
        _processHelperMock
            .Setup(p => p.Run(It.Is<ProcessOptions>(o => o.Args.Contains("test")), It.IsAny<CancellationToken>()))
            .Callback<ProcessOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(processResult);

        await _languageChecks.RunAllTests(_packagePath, TestMode.Playback, ct: CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(capturedOptions!.EnvironmentVariables, Does.ContainKey("AZURE_TEST_MODE"));
            Assert.That(capturedOptions.EnvironmentVariables, Does.ContainKey("CLIENTMODEL_TEST_MODE"));
            Assert.That(capturedOptions.EnvironmentVariables!["AZURE_TEST_MODE"], Is.EqualTo("playback"));
            Assert.That(capturedOptions.EnvironmentVariables["CLIENTMODEL_TEST_MODE"], Is.EqualTo("playback"));
        });
    }

    #endregion
}
