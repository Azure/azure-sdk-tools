using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Azure.Sdk.Tools.CodeOwnersParser;

namespace Azure.Sdk.Tools.RetrieveCodeOwners;

/// <summary>
/// See Program.Main comment.
/// </summary>
public static class Program
{
    /// <summary>
    /// See comment on Azure.Sdk.Tools.RetrieveCodeOwners.Program.Main,
    /// for parameter "ignoredPathPrefixes".
    /// </summary>
    public const string DefaultIgnoredPrefixes = ".git";

    /// <summary>
    /// Given targetPath and CODEOWNERS file path or https url codeownersFilePathOrUrl,
    /// prints out to stdout owners of the targetPath as determined by the CODEOWNERS data.
    /// </summary>
    /// <param name="targetPath">The path whose owners are to be determined. Can be a glob path.</param>
    /// <param name="codeownersFilePathOrUrl">The https url or path to the CODEOWNERS file.</param>
    /// <param name="excludeNonUserAliases">Whether owners that aren't users should be excluded from
    /// the returned owners.</param>
    /// <param name="targetDir">
    /// The directory to search for file paths in case targetPath is a glob path. Unused otherwise.
    /// </param>
    /// <param name="ignoredPathPrefixes">
    /// A list of path prefixes, separated by |, to ignore when doing
    /// glob-matching against glob targetPath.
    /// Applies only if targetPath is a glob path. Unused otherwise.
    /// Defaults to ".git".
    /// Example usage: ".git|foo|bar"
    /// </param>
    /// <param name="useRegexMatcher">
    /// Whether to use the new matcher for CODEOWNERS file paths, that supports matching
    /// entries with * and **, instead of silently ignoring them.
    /// </param>
    /// <returns>
    /// On STDOUT: The JSON representation of the matched CodeownersEntry.
    /// "new CodeownersEntry()" if no path in the CODEOWNERS data matches.
    /// <br/><br/>
    /// From the Main method: exit code. 0 if successful, 1 if error.
    /// </returns>
    public static int Main(
        string targetPath,
        string codeownersFilePathOrUrl,
        bool excludeNonUserAliases = false,
        string? targetDir = null,
        string ignoredPathPrefixes = DefaultIgnoredPrefixes,
        bool useRegexMatcher = false)
    {
        try 
        {
            Trace.Assert(!string.IsNullOrWhiteSpace(targetPath));

            targetPath = targetPath.Trim();
            targetDir = targetDir?.Trim();
            codeownersFilePathOrUrl = codeownersFilePathOrUrl.Trim();

            Trace.Assert(!string.IsNullOrWhiteSpace(codeownersFilePathOrUrl));
            Trace.Assert(!targetPath.IsGlobFilePath() 
                         || (targetDir != null && Directory.Exists(targetDir)));

            // The "object" here is effectively an union of two types: T1 | T2,
            // where T1 is the type returned by GetCodeownersForGlobPath
            // and T2 is the type returned by GetCodeownersForSimplePath.
            object codeownersData = targetPath.IsGlobFilePath()
                ? GetCodeownersForGlobPath(
                    new GlobFilePath(targetPath),
                    targetDir!,
                    codeownersFilePathOrUrl,
                    excludeNonUserAliases,
                    SplitIgnoredPathPrefixes(),
                    useRegexMatcher)
                : GetCodeownersForSimplePath(
                    targetPath,
                    codeownersFilePathOrUrl,
                    excludeNonUserAliases,
                    useRegexMatcher);

            string codeownersJson = JsonSerializer.Serialize(
                codeownersData,
                new JsonSerializerOptions { WriteIndented = true });

            Console.WriteLine(codeownersJson);
            return 0;

            string[] SplitIgnoredPathPrefixes()
                => ignoredPathPrefixes.Split(
                    "|",
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }
        catch (Exception e) 
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    private static Dictionary<string, CodeownersEntry> GetCodeownersForGlobPath(
        GlobFilePath targetPath,
        string targetDir,
        string codeownersFilePathOrUrl,
        bool excludeNonUserAliases,
        string[]? ignoredPathPrefixes = null,
        bool useRegexMatcher = false)
    {
        ignoredPathPrefixes ??= Array.Empty<string>();

        Dictionary<string, CodeownersEntry> codeownersEntries =
            CodeownersFile.GetMatchingCodeownersEntries(
                targetPath,
                targetDir,
                codeownersFilePathOrUrl,
                ignoredPathPrefixes,
                useRegexMatcher);

        if (excludeNonUserAliases)
            codeownersEntries.Values.ToList().ForEach(entry => entry.ExcludeNonUserAliases());

        return codeownersEntries;
    }

    private static CodeownersEntry GetCodeownersForSimplePath(
        string targetPath,
        string codeownersFilePathOrUrl,
        bool excludeNonUserAliases,
        bool useRegexMatcher = false)
    {
        CodeownersEntry codeownersEntry =
            CodeownersFile.GetMatchingCodeownersEntry(
                targetPath,
                codeownersFilePathOrUrl,
                useRegexMatcher);

        if (excludeNonUserAliases)
            codeownersEntry.ExcludeNonUserAliases();

        return codeownersEntry;
    }
}
