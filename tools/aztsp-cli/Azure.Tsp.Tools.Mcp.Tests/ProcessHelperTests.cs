using Azure.Tsp.Tools.Mcp.Helpers;

namespace Azure.Tsp.Tools.Mcp.Tests;

[TestFixture]
public class ProcessHelperTests
{
    [Test]
    public void RunProcess_WithSuccessfulCommand_ReturnsExpectedOutput()
    {
        // Arrange
        string command, expectedOutput;
        string[] args;

        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            command = "cmd.exe";
            args = ["/C", "echo", "Hello World"];
            expectedOutput = "Hello World";
        }
        else
        {
            command = "echo";
            args = ["Hello World"];
            expectedOutput = "Hello World";
        }

        string workingDirectory = Environment.CurrentDirectory;

        // Act
        var result = ProcessHelper.RunProcess(command, args, workingDirectory);

        // Assert
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.Output.Trim(), Does.Contain(expectedOutput));
    }

    [Test]
    public void RunProcess_WithFailingCommand_ReturnsNonZeroExitCode()
    {
        // Arrange
        string command, workingDirectory;
        string[] args;

        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            command = "cmd.exe";
            args = ["/C", "exit", "1"];
        }
        else
        {
            command = "sh";
            args = ["-c", "exit 1"];
        }

        workingDirectory = Environment.CurrentDirectory;

        // Act
        var result = ProcessHelper.RunProcess(command, args, workingDirectory);

        // Assert
        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Output, Does.Contain("Process failed."));
    }

    [Test]
    public void RunProcess_WithNonExistentCommand_ReturnsFailure()
    {
        // Arrange
        string command = "nonexistentcommand12345";
        string[] args = ["--version"];
        string workingDirectory = Environment.CurrentDirectory;

        // Act & Assert
        // This should either throw an exception or return a failure result
        // depending on the OS behavior
        var exception = Assert.Throws<System.ComponentModel.Win32Exception>(
            () => ProcessHelper.RunProcess(command, args, workingDirectory));

        Assert.That(exception, Is.Not.Null);
    }

    [Test]
    public void RunProcess_WithEmptyArgs_ExecutesCommand()
    {
        // Arrange
        string command, expectedPattern;
        string[] args;

        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            command = "cmd.exe";
            args = ["/C", "echo", "test"];
            expectedPattern = "test";
        }
        else
        {
            command = "pwd";
            args = [];
            expectedPattern = "/"; // Should contain a path separator
        }

        string workingDirectory = Environment.CurrentDirectory;

        // Act
        var result = ProcessHelper.RunProcess(command, args, workingDirectory);

        // Assert
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.Output, Does.Contain(expectedPattern));
    }

    [Test]
    public void RunProcess_WithDifferentWorkingDirectory_UsesCorrectWorkingDir()
    {
        // Arrange
        string tempDir = Path.GetTempPath();
        string command, expectedOutput;
        string[] args;

        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            command = "cmd.exe";
            args = ["/C", "cd"];
            expectedOutput = tempDir.TrimEnd('\\'); // Remove trailing backslash for comparison
        }
        else
        {
            command = "pwd";
            args = [];
            expectedOutput = tempDir.TrimEnd('/'); // Remove trailing slash for comparison
        }

        // Act
        var result = ProcessHelper.RunProcess(command, args, tempDir);

        // Assert
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.Output.Trim(), Does.Contain(expectedOutput));
    }
}
