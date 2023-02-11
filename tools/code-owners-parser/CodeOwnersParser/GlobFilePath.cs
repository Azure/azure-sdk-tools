using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Azure.Sdk.Tools.CodeOwnersParser;

public class GlobFilePath
{
    private readonly string filePath;

    public GlobFilePath(string globFilePath)
    {
        Debug.Assert(globFilePath.IsGlobFilePath());
        this.filePath = globFilePath;
    }

    /// <summary>
    /// The '*' is the only character that can denote glob pattern
    /// in the used globbing library, per:
    /// - https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.filesystemglobbing.matcher?view=dotnet-plat-ext-7.0#remarks
    /// - https://learn.microsoft.com/en-us/dotnet/core/extensions/file-globbing#pattern-formats
    /// </summary>
    public static bool IsGlobFilePath(string path)
        => path.Contains('*');

    public List<string> ResolveGlob(string directoryPath, string[]? ignoredPathPrefixes)
    {
        ignoredPathPrefixes ??= Array.Empty<string>();

        var globMatcher = new Matcher(StringComparison.Ordinal);
        globMatcher.AddInclude(this.filePath);
        
        List<string> matchedPaths = globMatcher.GetResultsInFullPath(directoryPath).ToList();
        
        matchedPaths = matchedPaths
            .Select(path => Path.GetRelativePath(directoryPath, path).Replace("\\", "/"))
            .Where(path => ignoredPathPrefixes.All(prefix => !path.StartsWith(prefix)))
            .ToList();

        return matchedPaths;
    }
}
