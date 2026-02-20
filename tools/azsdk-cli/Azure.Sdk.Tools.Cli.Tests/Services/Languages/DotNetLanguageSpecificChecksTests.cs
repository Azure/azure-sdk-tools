using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture]
internal class DotNetLanguageSpecificChecksTests
{
    private Mock<IProcessHelper> _processHelperMock = null!;
    private Mock<IGitHelper> _gitHelperMock = null!;
    private Mock<IPowershellHelper> _powerShellHelperMock = null!;
    private Mock<ICommonValidationHelpers> _commonValidationHelperMock = null!;
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

        _languageChecks = new DotnetLanguageService(
            _processHelperMock.Object,
            _powerShellHelperMock.Object,
            _gitHelperMock.Object,
            NullLogger<DotnetLanguageService>.Instance,
            _commonValidationHelperMock.Object,
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
                return processResult;
            });

        var result = await _languageChecks.CheckGeneratedCode(_packagePath, ct: CancellationToken.None);

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
                It.Is<ProcessOptions>(p => IsDotNetListSdksCommand(p)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        var result = await _languageChecks.CheckAotCompat(_packagePath, ct: CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.CheckStatusDetails, Does.Contain("dotnet --list-sdks failed"));
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

        var result = await _languageChecks.RunAllTests(tempDir.DirectoryPath, CancellationToken.None);

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

        var result = await _languageChecks.RunAllTests(tempDir.DirectoryPath, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.TestRunOutput, Does.Contain("Tests passed!"));
        });
        
        _processHelperMock.Verify(x => x.Run(
            It.Is<ProcessOptions>(p => IsDotNetTestCommand(p, tempDir.DirectoryPath)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
