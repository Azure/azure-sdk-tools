// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Microagents;
using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.CopilotAgents.Tools;

/// <summary>
/// Factory methods for creating file-related AIFunction tools for copilot agents.
/// </summary>
public static class FileTools
{
    /// <summary>
    /// Creates a ReadFile tool that reads file contents from a base directory.
    /// </summary>
    /// <param name="baseDir">The base directory for relative path resolution.</param>
    /// <param name="description">Optional custom description for the tool.</param>
    /// <returns>An AIFunction that reads files.</returns>
    public static AIFunction CreateReadFileTool(
        string baseDir,
        string description = "Read the contents of a file")
    {
        return AIFunctionFactory.Create(
            async ([Description("Relative path of the file to read")] string filePath) =>
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    throw new ArgumentException("Input path cannot be null or empty.", nameof(filePath));
                }
                if (!ToolHelpers.TryGetSafeFullPath(baseDir, filePath, out var path))
                {
                    throw new ArgumentException("The provided path is invalid or outside the allowed base directory.");
                }
                if (!File.Exists(path))
                {
                    throw new ArgumentException($"{path} does not exist");
                }
                return await File.ReadAllTextAsync(path);
            },
            "ReadFile",
            description);
    }

    /// <summary>
    /// Creates a WriteFile tool that writes content to files in a base directory.
    /// </summary>
    /// <param name="baseDir">The base directory for relative path resolution.</param>
    /// <param name="description">Optional custom description for the tool.</param>
    /// <returns>An AIFunction that writes files.</returns>
    public static AIFunction CreateWriteFileTool(
        string baseDir,
        string description = "Write content to a file")
    {
        return AIFunctionFactory.Create(
            async ([Description("Relative path of the file to write")] string filePath,
                   [Description("Content to write to the file")] string content) =>
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    throw new ArgumentException("Input path cannot be null or empty.", nameof(filePath));
                }
                if (!ToolHelpers.TryGetSafeFullPath(baseDir, filePath, out var path))
                {
                    throw new ArgumentException("The provided path is invalid or outside the allowed base directory.");
                }
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                await File.WriteAllTextAsync(path, content);
                return $"Successfully wrote to {filePath}";
            },
            "WriteFile",
            description);
    }

    /// <summary>
    /// Creates a ListFiles tool that lists files in a directory.
    /// </summary>
    /// <param name="baseDir">The base directory for relative path resolution.</param>
    /// <param name="description">Optional custom description for the tool.</param>
    /// <returns>An AIFunction that lists files.</returns>
    public static AIFunction CreateListFilesTool(
        string baseDir,
        string description = "List files and directories in a directory")
    {
        return AIFunctionFactory.Create(
            ([Description("Relative path of the directory to list (use '.' for root)")] string directoryPath,
             [Description("Whether to list files recursively (default: false)")] bool recursive = false,
             [Description("Optional glob pattern to filter results (e.g., '*.cs', '*.json')")] string? filter = null) =>
            {
                if (string.IsNullOrEmpty(directoryPath))
                {
                    directoryPath = ".";
                }
                if (!ToolHelpers.TryGetSafeFullPath(baseDir, directoryPath, out var path))
                {
                    throw new ArgumentException("The provided path is invalid or outside the allowed base directory.");
                }
                if (!Directory.Exists(path))
                {
                    throw new ArgumentException($"{path} does not exist");
                }

                var searchPattern = string.IsNullOrWhiteSpace(filter) ? "*" : filter;
                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                // Use GetFileSystemEntries to return both files and directories (parity with Microagents)
                var entries = Directory.GetFileSystemEntries(path, searchPattern, searchOption)
                    .Select(f => Path.GetRelativePath(baseDir, f))
                    .ToArray();

                return entries;
            },
            "ListFiles",
            description);
    }

    /// <summary>
    /// Creates a GrepSearch tool that searches for patterns in files within a base directory.
    /// </summary>
    /// <param name="baseDir">The base directory for relative path resolution.</param>
    /// <param name="description">Optional custom description for the tool.</param>
    /// <returns>An AIFunction that searches files for patterns.</returns>
    public static AIFunction CreateGrepSearchTool(
        string baseDir,
        string description = "Search for patterns in files within the project")
    {
        return AIFunctionFactory.Create(
            ([Description("The search pattern (can be text or regex depending on isRegex parameter)")] string pattern,
             [Description("The relative file path or directory to search in")] string path,
             [Description("Whether the pattern is a regular expression (default: false)")] bool isRegex = false,
             [Description("Maximum number of results to return (default: 50)")] int maxResults = 50) =>
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    throw new ArgumentException("Search pattern cannot be empty", nameof(pattern));
                }

                if (!ToolHelpers.TryGetSafeFullPath(baseDir, path, out var searchPath))
                {
                    throw new ArgumentException("The provided path is invalid or outside the allowed base directory.");
                }

                if (!Path.Exists(searchPath))
                {
                    throw new ArgumentException($"Path {path} does not exist", nameof(path));
                }

                var matches = new List<object>();
                var files = new List<string>();

                if (File.Exists(searchPath))
                {
                    files.Add(searchPath);
                }
                else if (Directory.Exists(searchPath))
                {
                    files.AddRange(Directory.GetFiles(searchPath, "*", SearchOption.AllDirectories));
                }

                Regex? regex = null;
                if (isRegex)
                {
                    try
                    {
                        regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    }
                    catch (ArgumentException ex)
                    {
                        throw new ArgumentException($"Invalid regular expression: {ex.Message}", nameof(pattern));
                    }
                }

                foreach (var file in files)
                {
                    try
                    {
                        var lines = File.ReadAllLines(file);
                        for (int i = 0; i < lines.Length; i++)
                        {
                            bool isMatch = isRegex
                                ? regex!.IsMatch(lines[i])
                                : lines[i].Contains(pattern, StringComparison.OrdinalIgnoreCase);

                            if (isMatch)
                            {
                                matches.Add(new
                                {
                                    FilePath = Path.GetRelativePath(baseDir, file),
                                    LineNumber = i + 1,
                                    Content = lines[i].Trim()
                                });

                                if (matches.Count >= maxResults)
                                {
                                    return new { Matches = matches, TotalMatches = matches.Count };
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Skip files that can't be read (binary, permission issues, etc.)
                        continue;
                    }
                }

                return new { Matches = matches, TotalMatches = matches.Count };
            },
            "GrepSearch",
            description);
    }
}
