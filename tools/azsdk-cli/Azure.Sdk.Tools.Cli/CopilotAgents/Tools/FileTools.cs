// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using Azure.Sdk.Tools.Cli.Microagents;
using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.CopilotAgents.Tools;

/// <summary>
/// Factory methods for creating file-related AIFunction tools for copilot agents.
/// </summary>
public static class FileTools
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "bin", "obj", "dist", ".vs", ".idea",
        "packages", "TestResults", "__pycache__", ".tox", "target"
    };

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

                // Use GetFileSystemEntries to return both files and directories (parity with Microagents)
                var entries = recursive
                    ? EnumerateFileSystemEntries(path, searchPattern)
                        .Select(f => Path.GetRelativePath(baseDir, f))
                        .ToArray()
                    : Directory.GetFileSystemEntries(path, searchPattern, SearchOption.TopDirectoryOnly)
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

                // Regex with timeout to prevent ReDoS from LLM-generated patterns
                Regex? regex = null;
                if (isRegex)
                {
                    try
                    {
                        regex = new Regex(pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));
                    }
                    catch (ArgumentException ex)
                    {
                        throw new ArgumentException($"Invalid regex pattern: {ex.Message}", nameof(pattern));
                    }
                }
                var files = File.Exists(searchPath)
                    ? [searchPath]
                    : EnumerateFileSystemEntries(searchPath, filesOnly: true).Take(2000).ToArray();

                var matches = new ConcurrentBag<GrepMatch>();
                var totalMatches = 0;
                var skippedFiles = 0;
                var readErrors = 0;
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8)
                };

                Parallel.ForEach(files.Select((file, index) => (file, index)), parallelOptions, (entry, state) =>
                {
                    if (Volatile.Read(ref totalMatches) >= maxResults)
                    {
                        state.Stop();
                        return;
                    }

                    try
                    {
                        // Skip files > 2MB (use ReadFile for large files)
                        if (new FileInfo(entry.file).Length > 2 * 1024 * 1024)
                        {
                            Interlocked.Increment(ref skippedFiles);
                            return;
                        }

                        var lineNum = 0;
                        foreach (var line in File.ReadLines(entry.file))
                        {
                            lineNum++;
                            if (!(isRegex ? regex!.IsMatch(line) : line.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                            {
                                continue;
                            }

                            var found = Interlocked.Increment(ref totalMatches);
                            if (found > maxResults)
                            {
                                state.Stop();
                                break;
                            }

                            matches.Add(new GrepMatch(
                                entry.index,
                                lineNum,
                                Path.GetRelativePath(baseDir, entry.file),
                                line.TrimEnd()));

                            if (found >= maxResults)
                            {
                                state.Stop();
                                break;
                            }
                        }
                    }
                    catch
                    {
                        Interlocked.Increment(ref skippedFiles);
                        Interlocked.Increment(ref readErrors);
                    }
                });

                var orderedMatches = matches
                    .OrderBy(m => m.FileIndex)
                    .ThenBy(m => m.LineNumber)
                    .Take(maxResults)
                    .Select(m => new
                    {
                        m.FilePath,
                        m.LineNumber,
                        m.Content
                    })
                    .ToArray();

                return new
                {
                    Matches = orderedMatches,
                    TotalMatches = orderedMatches.Length,
                    SkippedFiles = Volatile.Read(ref skippedFiles),
                    ReadErrors = Volatile.Read(ref readErrors)
                };
            },
            "GrepSearch",
            description);
    }

    private static IEnumerable<string> EnumerateFileSystemEntries(string rootPath, string searchPattern = "*", bool filesOnly = false)
    {
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            IEnumerable<string> entries;
            try
            {
                entries = filesOnly
                    ? Directory.EnumerateFiles(current, searchPattern, SearchOption.TopDirectoryOnly)
                    : Directory.EnumerateFileSystemEntries(current, searchPattern, SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var entry in entries)
            {
                yield return entry;
            }

            IEnumerable<string> subdirectories;
            try
            {
                subdirectories = Directory.EnumerateDirectories(current);
            }
            catch
            {
                continue;
            }

            foreach (var subdirectory in subdirectories)
            {
                if (IgnoredDirectories.Contains(Path.GetFileName(subdirectory)))
                {
                    continue;
                }

                pending.Push(subdirectory);
            }
        }
    }

    private readonly record struct GrepMatch(int FileIndex, int LineNumber, string FilePath, string Content);
}
