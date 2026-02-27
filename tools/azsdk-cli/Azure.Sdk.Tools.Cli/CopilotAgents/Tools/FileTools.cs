// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
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
    /// <param name="includeLineNumbers">If true, prefix each line with its 1-based line number (e.g., "1: content").</param>
    /// <param name="description">Optional custom description for the tool.</param>
    /// <returns>An AIFunction that reads files.</returns>
    public static AIFunction CreateReadFileTool(
        string baseDir,
        bool includeLineNumbers = false,
        string? description = null)
    {
        description ??= includeLineNumbers
            ? "Read the contents of a file with line numbers prefixed to each line"
            : "Read the contents of a file";

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

                if (includeLineNumbers)
                {
                    var lines = await File.ReadAllLinesAsync(path);
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < lines.Length; i++)
                    {
                        sb.Append(i + 1).Append(": ").AppendLine(lines[i]);
                    }
                    return sb.ToString();
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
}
