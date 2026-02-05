using System.Diagnostics;

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
    /// <returns>The git diff output as a string.</returns>
    public async Task<string> GetGitDiffAsync(int contextLines = 3)
    {
        return await RunGitCommandAsync("diff", $"-U{contextLines}");
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
    /// Disposes of the workspace resources.
    /// Note: Cleanup of the workspace directory is handled by <see cref="WorkspaceManager"/> based on the configured cleanup policy.
    /// </summary>
    public void Dispose()
    {
        // Cleanup will be handled by WorkspaceManager based on CleanupPolicy
    }
}
