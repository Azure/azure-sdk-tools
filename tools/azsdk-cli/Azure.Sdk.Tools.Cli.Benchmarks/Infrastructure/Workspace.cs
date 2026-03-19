// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Benchmarks.Models;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;

/// <summary>
/// Represents a workspace containing a cloned repository for benchmark execution.
/// Provides file and git operations for interacting with the repository.
/// </summary>
public class Workspace : IDisposable
{
    /// <summary>
    /// Gets the path to the workspace root directory.
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// Gets the path to the home repository within the workspace.
    /// </summary>
    public string RepoPath { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Workspace"/> class.
    /// </summary>
    /// <param name="rootPath">The path to the workspace root directory.</param>
    /// <param name="repoName">The name of the repository directory within the workspace.</param>
    public Workspace(string rootPath, string repoName)
    {
        RootPath = rootPath;
        RepoPath = Path.Combine(rootPath, repoName);
    }

    /// <summary>
    /// Gets the git diff of all uncommitted changes in the repository.
    /// </summary>
    /// <param name="contextLines">Number of context lines to include around each change (default: 3).</param>
    /// <param name="includeUntracked">Whether to include untracked (newly created) files in the diff (default: true).</param>
    /// <returns>The git diff output as a string.</returns>
    public async Task<string> GetGitDiffAsync(int contextLines = 3, bool includeUntracked = true)
    {
        if (includeUntracked)
        {
            // Stage intent-to-add for all untracked files so they appear in the diff.
            // This doesn't actually stage file contents, just makes git aware of them.
            await RunGitCommandAsync("add", "--intent-to-add", ".");
        }

        return await RunGitCommandAsync("diff", $"-U{contextLines}");
    }

    /// <summary>
    /// Runs a command in the repository directory.
    /// </summary>
    /// <param name="command">The command to run (e.g., "npm", "dotnet").</param>
    /// <param name="args">Arguments to pass to the command.</param>
    /// <returns>The standard output of the command.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the command exits with a non-zero exit code.</exception>
    public async Task<string> RunCommandAsync(string command, params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = RepoPath
        };

        // On Windows, wrap with cmd /c to resolve .cmd/.bat files (e.g., npm, tsp)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            startInfo.FileName = "cmd.exe";
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(command);
        }
        else
        {
            startInfo.FileName = command;
        }

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Command '{command} {string.Join(" ", args)}' failed with exit code {process.ExitCode}.\nStderr: {error}");
        }

        return output;
    }

    /// <summary>
    /// Runs a git command in the repository directory.
    /// </summary>
    private async Task<string> RunGitCommandAsync(params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = RepoPath
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return output;
    }

    /// <summary>
    /// Writes content to a file at the specified path relative to the repository root.
    /// Creates parent directories if they don't exist.
    /// </summary>
    /// <param name="relativePath">The file path relative to the repository root.</param>
    /// <param name="content">The content to write to the file.</param>
    public async Task WriteFileAsync(string relativePath, string content)
    {
        var fullPath = Path.Combine(RepoPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content);
    }

    /// <summary>
    /// Reads the content of a file at the specified path relative to the repository root.
    /// </summary>
    /// <param name="relativePath">The file path relative to the repository root.</param>
    /// <returns>The content of the file as a string.</returns>
    public async Task<string> ReadFileAsync(string relativePath)
    {
        var fullPath = Path.Combine(RepoPath, relativePath);
        return await File.ReadAllTextAsync(fullPath);
    }

    /// <summary>
    /// Writes the benchmark execution log to the workspace root directory.
    /// The log includes messages, tool calls, validation results, and other execution details.
    /// </summary>
    /// <param name="scenarioName">The name of the scenario that was executed.</param>
    /// <param name="messages">The messages from the Copilot SDK session.</param>
    /// <param name="toolCalls">The list of tool calls made during execution.</param>
    /// <param name="gitDiff">The git diff of changes made during execution.</param>
    /// <param name="duration">The duration of the execution.</param>
    /// <param name="passed">Whether the benchmark passed validation.</param>
    /// <param name="validation">The validation summary (null if no validators were run).</param>
    /// <param name="error">Optional error message if the benchmark failed.</param>
    public async Task WriteExecutionLogAsync(
        string scenarioName,
        IReadOnlyList<object> messages,
        IReadOnlyList<ToolCallRecord> toolCalls,
        string? gitDiff,
        TimeSpan duration,
        bool passed,
        ValidationSummary? validation = null,
        string? error = null)
    {
        var log = new
        {
            Scenario = scenarioName,
            Timestamp = DateTime.UtcNow.ToString("o"),
            Duration = duration.ToString(),
            Passed = passed,
            Error = error,
            Validation = validation,
            ToolCalls = toolCalls,
            Messages = messages,
            GitDiff = gitDiff
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(log, options);
        var logPath = Path.Combine(RootPath, "benchmark-log.json");
        await File.WriteAllTextAsync(logPath, json);
    }

    /// <summary>
    /// Copies a file or directory from source to the workspace.
    /// If the source is a directory, copies the entire directory recursively.
    /// </summary>
    /// <param name="sourcePath">The source file or directory path (absolute or relative to current directory).</param>
    /// <param name="targetRelativePath">The target path relative to the repository root.</param>
    public async Task CopyToWorkspaceAsync(string sourcePath, string targetRelativePath)
    {
        var targetPath = Path.Combine(RepoPath, targetRelativePath);
        
        if (Directory.Exists(sourcePath))
        {
            // Copy entire directory
            CopyDirectory(sourcePath, targetPath);
        }
        else if (File.Exists(sourcePath))
        {
            // Copy single file
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await Task.Run(() => File.Copy(sourcePath, targetPath, overwrite: true));
        }
        else
        {
            throw new FileNotFoundException($"Source path not found: {sourcePath}");
        }
    }

    /// <summary>
    /// Removes a file or directory from the workspace.
    /// If the path is a directory, removes the entire directory recursively.
    /// </summary>
    /// <param name="relativePath">The path relative to the repository root to remove.</param>
    public async Task RemoveFromWorkspace(string relativePath)
    {
        var targetPath = Path.Combine(RepoPath, relativePath);
        
        if (Directory.Exists(targetPath))
        {
            // Remove entire directory recursively
            await Task.Run(() => Directory.Delete(targetPath, recursive: true));
        }
        else if (File.Exists(targetPath))
        {
            // Remove single file
            await Task.Run(() => File.Delete(targetPath));
        }
        // If path doesn't exist, no-op (already removed)
    }

    /// <summary>
    /// Recursively copies a directory and all its contents.
    /// </summary>
    /// <param name="sourceDir">The source directory path.</param>
    /// <param name="targetDir">The target directory path.</param>
    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        // Create target directory
        Directory.CreateDirectory(targetDir);
        
        // Copy all files
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var targetFile = Path.Combine(targetDir, fileName);
            File.Copy(file, targetFile, overwrite: true);
        }
        
        // Copy all subdirectories recursively
        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(subDir);
            var targetSubDir = Path.Combine(targetDir, dirName);
            CopyDirectory(subDir, targetSubDir);
        }
    }

    /// <summary>
    /// Disposes of the workspace resources.
    /// Note: Cleanup of the workspace directory is handled by <see cref="WorkspaceManager"/> based on the configured cleanup policy.
    /// </summary>
    public void Dispose()
    {
        // Cleanup will be handled by WorkspaceManager based on CleanupPolicy
    }
}
