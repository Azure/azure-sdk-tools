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

    public static bool IsGlobFilePath(string path)
        => path.Contains('*');

    public List<string> ResolveGlob(string directoryPath)
    {
        var globMatcher = new Matcher(StringComparison.Ordinal);
        globMatcher.AddInclude(this.filePath);
        
        List<string> matchedPaths = globMatcher.GetResultsInFullPath(directoryPath).ToList();
        
        matchedPaths = matchedPaths
            .Select(path => Path.GetRelativePath(directoryPath, path).Replace("\\", "/"))
            .ToList();

        return matchedPaths;
    }
}
