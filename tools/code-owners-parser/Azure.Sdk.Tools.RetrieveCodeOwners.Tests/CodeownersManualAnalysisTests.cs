using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Azure.Sdk.Tools.CodeOwnersParser;
using NUnit.Framework;

namespace Azure.Sdk.Tools.RetrieveCodeOwners.Tests;

/// <summary>
/// A class containing a set of tools, implemented as unit tests,
/// allowing you to view and diff owners of files of locally cloned repositories,
/// by obtaining the owners based on specified CODEOWNERS files.
///
/// These tools are to be run manually, locally, by a developer.
/// They do not participate in an automated regression test suite.
///
/// To run these tools, you will have to first manually comment out the "Ignore"
/// property of the "TestFixture" annotation below.
/// Then run the desired tool as a unit test, either from your IDE,
/// or "dotnet test" command line tool.
///
/// These tools assume you have made local repo clones of relevant repositories.
/// Ensure that the local repo clones you run these tools against are clean.
/// This is because these tools do not support .gitignore.
/// Hence if you do local builds you might add minutes to runtime, and get spurious results.
///
/// For explanation how to interpret and work with the output .csv file produced by these
/// tools, see comment on
///
///   WriteOwnersDiffToCsv
///
/// Related work:
/// Enable the new, regex-based, wildcard-supporting CODEOWNERS matcher
/// https://github.com/Azure/azure-sdk-tools/pull/5088
/// </summary>
[TestFixture(Ignore = "Tools to be used manually")]
public class CodeownersManualAnalysisTests
{
    private const string OwnersDiffOutputPathSuffix = "_owners_diff.csv";
    private const string OwnersDataOutputPathSuffix = "_owners.csv";

    /// <summary>
    /// Given name of the language langName, returns path to a local clone of "azure-sdk-for-langName"
    /// repository.
    ///
    /// This method assumes you have ensured the local clone is present at appropriate path ahead of time.
    ///
    /// </summary>
    private static string LangRepoTargetDirPathSuffix(string langName) => "/../azure-sdk-for-" + langName;

    private const string CodeownersFilePathSuffix = "/.github/CODEOWNERS";

    /// <summary>
    /// This file is expected to be manually created by you, in your local repo clone.
    /// For details of usage of this file, see:
    ///
    ///   WriteTwoCodeownersFilesOwnersDiffToCsv
    /// 
    /// </summary>
    private const string SecondaryCodeownersFilePathSuffix = "/.github/CODEOWNERS2";

    // Current dir, ".", is expected to be a dir in local clone of Azure/azure-sdk-tools repo,
    // where "." denotes "<cloneRoot>/artifacts/bin/Azure.Sdk.Tools.CodeOwnersParser.Tests/Debug/net6.0".
    private const string CurrentDir = "/artifacts/bin/Azure.Sdk.Tools.CodeOwnersParser.Tests/Debug/net6.0";
    
    #region Tests - Owners data

    [Test] // Runtime <1s
    public void OwnersForAzureDev()
        => WriteOwnersToCsv(
            targetDirPathSuffix: "/../azure-dev",
            outputFileNamePrefix: "azure-dev",
            ignoredPathPrefixes: ".git|artifacts");

    // @formatter:off
    [Test] public void OwnersForAzureSdkForAndroid() => WriteLangRepoOwnersToCsv("android"); // Runtime <1s
    [Test] public void OwnersForAzureSdkForC()       => WriteLangRepoOwnersToCsv("c");       // Runtime <1s
    [Test] public void OwnersForAzureSdkForCpp()     => WriteLangRepoOwnersToCsv("cpp");     // Runtime <1s
    [Test] public void OwnersForAzureSdkForGo()      => WriteLangRepoOwnersToCsv("go");      // Runtime <1s
    [Test] public void OwnersForAzureSdkForIos()     => WriteLangRepoOwnersToCsv("ios");     // Runtime <1s
    [Test] public void OwnersForAzureSdkForJava()    => WriteLangRepoOwnersToCsv("java");    // Runtime ~1m 11s
    [Test] public void OwnersForAzureSdkForJs()      => WriteLangRepoOwnersToCsv("js");      // Runtime ~1m 53s
    [Test] public void OwnersForAzureSdkForNet()     => WriteLangRepoOwnersToCsv("net");     // Runtime ~30s
    [Test] public void OwnersForAzureSdkForPython()  => WriteLangRepoOwnersToCsv("python");  // Runtime ~30s
    // @formatter:on

    [Test] // Runtime <1s
    public void OwnersForAzureSdkTools()
        => WriteOwnersToCsv(
            targetDirPathSuffix: "",
            outputFileNamePrefix: "azure-sdk-tools",
            ignoredPathPrefixes: ".git|artifacts");

    #endregion

    #region Tests - Owners diffs for differing CODEOWNERS contents.

    [Test] // Runtime <1s
    public void OwnersDiffForAzureDev()
        => WriteTwoCodeownersFilesOwnersDiffToCsv(
            targetDirPathSuffix: "/../azure-dev",
            outputFileNamePrefix: "azure-dev",
            ignoredPathPrefixes: ".git|artifacts");

    // https://github.com/Azure/azure-sdk-for-android/blob/main/.github/CODEOWNERS
    // No build failure notifications are configured for this repo.
    // Runtime: <1s
    [Test] public void OwnersDiffForAzureSdkForAndroid() => WriteLangRepoOwnersDiffToCsv("android");

    // https://github.com/Azure/azure-sdk-for-c/blob/main/.github/CODEOWNERS
    // Runtime: <1s
    [Test] public void OwnersDiffForAzureSdkForC() => WriteLangRepoOwnersDiffToCsv("c");

    // https://github.com/Azure/azure-sdk-for-cpp/blob/main/.github/CODEOWNERS
    // Runtime: <1s
    [Test] public void OwnersDiffForAzureSdkForCpp() => WriteLangRepoOwnersDiffToCsv("cpp");

    // https://github.com/Azure/azure-sdk-for-go/blob/main/.github/CODEOWNERS
    // Runtime: ~2s
    [Test] public void OwnersDiffForAzureSdkForGo() => WriteLangRepoOwnersDiffToCsv("go");

    // https://github.com/Azure/azure-sdk-for-ios/blob/main/.github/CODEOWNERS
    // No build failure notifications are configured for this repo.
    // Runtime: <1s
    [Test] public void OwnersDiffForAzureSdkForIos() => WriteLangRepoOwnersDiffToCsv("ios");

    // https://github.com/Azure/azure-sdk-for-java/blob/main/.github/CODEOWNERS
    // Runtime: ~2m 32s
    [Test] public void OwnersDiffForAzureSdkForJava() => WriteLangRepoOwnersDiffToCsv("java");

    // https://github.com/Azure/azure-sdk-for-js/blob/main/.github/CODEOWNERS
    // Runtime: ~3m 49s
    [Test] public void OwnersDiffForAzureSdkForJs() => WriteLangRepoOwnersDiffToCsv("js");

    // https://github.com/Azure/azure-sdk-for-net/blob/main/.github/CODEOWNERS
    // Runtime: ~1m 01s
    [Test] public void OwnersDiffForAzureSdkForNet() => WriteLangRepoOwnersDiffToCsv("net");

    // https://github.com/Azure/azure-sdk-for-python/blob/main/.github/CODEOWNERS
    // Runtime: ~45s
    [Test] public void OwnersDiffForAzureSdkForPython() => WriteLangRepoOwnersDiffToCsv("python");

    #endregion

    #region Parameterized tests - Owners

    private void WriteLangRepoOwnersToCsv(string langName)
        => WriteOwnersToCsv(
            targetDirPathSuffix: LangRepoTargetDirPathSuffix(langName),
            outputFileNamePrefix: $"azure-sdk-for-{langName}",
            ignoredPathPrefixes: ".git|artifacts");

    private void WriteOwnersToCsv(
        string targetDirPathSuffix,
        string outputFileNamePrefix,
        string ignoredPathPrefixes = Program.DefaultIgnoredPrefixes)
    {
        string rootDir = PathNavigatingToRootDir(CurrentDir);
        string targetDir = rootDir + targetDirPathSuffix;
        Debug.Assert(Directory.Exists(targetDir),
            $"Ensure you have cloned the repo into '{targetDir}'. " +
            "See comments on CodeownersManualAnalysisTests and WriteOwnersToCsv for details.");
        Debug.Assert(File.Exists(targetDir + CodeownersFilePathSuffix), 
            $"Ensure you have cloned the repo into '{targetDir}'. " +
            "See comments on CodeownersManualAnalysisTests and WriteOwnersToCsv for details.");
        WriteOwnersToCsv(
            targetDirPathSuffix,
            CodeownersFilePathSuffix,
            ignoredPathPrefixes,
            outputFileNamePrefix);
    }

    #endregion

    #region Parameterized tests - Owners diff

    private void WriteLangRepoOwnersDiffToCsv(string langName)
        => WriteTwoCodeownersFilesOwnersDiffToCsv(
            targetDirPathSuffix: LangRepoTargetDirPathSuffix(langName),
            outputFileNamePrefix: $"azure-sdk-for-{langName}",
            ignoredPathPrefixes: ".git|artifacts");

    /// <summary>
    /// This method is an invocation of:
    ///
    ///     WriteOwnersDiffToCsv
    ///
    /// with following meanings bound to LEFT and RIGHT:
    ///
    /// LEFT: RetrieveCodeowners configuration given input local repository clone CODEOWNERS file.
    ///
    /// RIGHT: RetrieveCodeowners configuration given input repository CODEOWNERS2 file,
    /// located beside CODEOWNERS file.
    ///
    /// The CODEOWNERS2 file is expected to be created manually by you. This way you can diff CODEOWNERS
    /// to whatever version of it you want to express in CODEOWNERS2. For example, CODEOWNERS2 could have
    /// contents of CODEOWNERS as seen in an open PR pending being merged.
    ///
    /// Note that modifying or reordering existing paths may always impact which PR reviewers are auto-assigned,
    /// but the build failure notification recipients changes apply only to paths that represent
    /// build definition .yml files.
    /// </summary>
    private void WriteTwoCodeownersFilesOwnersDiffToCsv(
        string targetDirPathSuffix,
        string outputFileNamePrefix,
        string ignoredPathPrefixes = Program.DefaultIgnoredPrefixes)
    {
        string rootDir = PathNavigatingToRootDir(CurrentDir);
        string targetDir = rootDir + targetDirPathSuffix;
        Debug.Assert(Directory.Exists(targetDir),
            $"Ensure you have cloned the repo into '{targetDir}'. " +
            "See comments on CodeownersManualAnalysisTests and WriteTwoCodeownersFilesOwnersDiffToCsv for details.");
        Debug.Assert(File.Exists(targetDir + CodeownersFilePathSuffix), 
            $"Ensure you have cloned the repo into '{targetDir}'. " +
            "See comments on CodeownersManualAnalysisTests and WriteTwoCodeownersFilesOwnersDiffToCsv for details.");
        Debug.Assert(File.Exists(targetDir + SecondaryCodeownersFilePathSuffix), 
            $"Ensure you have created '{Path.GetFullPath(targetDir + SecondaryCodeownersFilePathSuffix)}'. " +
            $"See comment on WriteTwoCodeownersFilesOwnersDiffToCsv for details.");

        WriteOwnersDiffToCsv(
            new[]
            {
                (targetDirPathSuffix, CodeownersFilePathSuffix, ignoredPathPrefixes),
                (targetDirPathSuffix, SecondaryCodeownersFilePathSuffix, ignoredPathPrefixes)
            },
            outputFileNamePrefix);
    }

    #endregion

    #region private static

    /// <summary>
    /// This method is similar to:
    ///
    ///     WriteOwnersDiffToCsv
    ///
    /// Except it is not doing any diffing: it just evaluates one invocation of
    /// Azure.Sdk.Tools.RetrieveCodeOwners.Program.Main
    /// and returns its information, in similar, but simplified table format.
    ///
    /// If given path, provided in column PATH, did not match any path in CODEOWNERS file,
    /// the column PATH EXPRESSION will have a value of _____ .
    ///
    /// In addition, this method also does an validation of CODEOWNERS paths
    /// and if it find a problem with given path, it returns output lines with ISSUE column
    /// populated and PATH column empty, as there is no path to speak of - only CODEOWNERS path,
    /// provided in PATH EXPRESSION column, is present.
    ///
    /// The ISSUE column has following codes:
    ///
    ///   INVALID_PATH_CONTAINS_UNSUPPORTED_FRAGMENTS
    ///     All CODEOWNERS paths must not contain unsupported path fragments, as defined by:
    ///       Azure.Sdk.Tools.CodeOwnersParser.MatchedCodeownersEntry.ContainsUnsupportedFragments
    /// 
    ///   INVALID_PATH_SHOULD_START_WITH_SLASH
    ///     All CODEOWNERS paths must start with "/", but given path doesn't.
    ///     Such path will still be processed by our CODEOWNERS interpreter, but nevertheless it is
    ///     invalid and should be fixed.
    /// 
    ///   INVALID_PATH_MATCHES_DIR_EXACTLY
    ///   INVALID_PATH_MATCHES_DIR_EXACTLY_AND_NAME_PREFIX
    ///   INVALID_PATH_MATCHES_NAME_PREFIX
    ///     CODEOWNERS file contains a simple (i.e. without wildcards) path that is expected to match against
    ///     a file, as it does not end with "/". However, the repository contains one or more of the following:
    ///     - a directory with the same path
    ///     - a directory with such path being its name prefix: e.g. the path is /foobar and the dir is /foobarbaz/
    ///     - a file with such path being its name prefix: e.g. the path is /foobar and the file is /foobarbaz.txt
    ///
    ///     Such paths are invalid because they ambiguous and need to be disambiguated.
    ///     If the match is only to exact directory, then such CODEOWNERS path will never match any input path.
    ///     Usually the proper fix
    ///     is to add the missing suffix "/" to the path to make it correctly match against the existing directory.
    ///     If the match is to directory prefix, then this can be solved by appending "*/". This will match both
    ///     exact directories, and directory prefixes.
    ///     If the match is to file name prefix only, this can be fixed by appending "*".
    ///     If the match is both to directory and file name prefixes, possibly multiple paths need to be used,
    ///     one with "*/" suffix and one with "*" suffix.
    /// 
    ///   WILDCARD_FILE_PATH_NEEDS_MANUAL_EVAL
    ///     Same situation as above, but the CODEOWNERS path is a file path with a wildcard, hence current
    ///     validation implementation cannot yet determine if it should be a path to directory or not.
    ///     Hence, this needs to be checked manually by ensuring that the wildcard file path matches
    ///     at least one file in the repository.
    ///
    /// Known limitation:
    ///   If given CODEOWNERS path has no owners listed on its line, this method will not report such path as invalid.
    /// </summary>
    private static void WriteOwnersToCsv(
        string targetDirPathSuffix, 
        string codeownersFilePathSuffix, 
        string ignoredPrefixes, 
        string outputFilePrefix)
    {
        var stopwatch = Stopwatch.StartNew();
        string rootDir = PathNavigatingToRootDir(CurrentDir);
        string targetDir = rootDir + targetDirPathSuffix;
        
        Dictionary<string, CodeownersEntry> ownersData = RetrieveCodeowners(
            targetDirPathSuffix,
            codeownersFilePathSuffix,
            ignoredPrefixes);

        List<string> outputLines =
            new List<string> { "PATH | PATH EXPRESSION | OWNERS | ISSUE" };
        foreach (KeyValuePair<string, CodeownersEntry> kvp in ownersData)
        {
            string path = kvp.Key;
            CodeownersEntry entry = kvp.Value;
            outputLines.Add(
                $"{path} " +
                $"| {(entry.IsValid ? entry.PathExpression : "_____")} " +
                $"| {string.Join(",", entry.Owners)}");
        }

        outputLines.AddRange(PathsWithIssues(targetDir, codeownersFilePathSuffix, paths: ownersData.Keys.ToArray()));

        var outputFilePath = outputFilePrefix + OwnersDataOutputPathSuffix;
        File.WriteAllLines(outputFilePath, outputLines);
        Console.WriteLine($"DONE writing out owners. " +
                          $"Output written out to {Path.GetFullPath(outputFilePath)}. " +
                          $"Time taken: {stopwatch.Elapsed}.");
    }

    // Possible future work:
    // instead of returning lines with issues, consider returning the modified & fixed CODEOWNERS file. 
    // It could work by reading all the lines, then replacing the wrong
    // lines by using dict replacement. Need to be careful about retaining spaces to not misalign,
    // e.g.
    // "sdk/  @own1" --> "/sdk/ @own1" // space removed to keep alignment
    // but also:
    // "sdk/ @own1" --> "/sdk/ @own1" // space not removed, because it would be invalid.
    private static List<string> PathsWithIssues(
        string targetDir,
        string codeownersPathSuffix,
        string[] paths)
    {
        List<string> outputLines = new List<string>();
        List<CodeownersEntry> entries =
            CodeownersFile.GetCodeownersEntriesFromFileOrUrl(targetDir + codeownersPathSuffix)
                .Where(entry => !entry.PathExpression.StartsWith("#"))
                .ToList();

        outputLines.AddRange(PathsWithMissingPrefixSlash(entries));
        outputLines.AddRange(PathsWithMissingSuffixSlash(targetDir, entries, paths));
        outputLines.AddRange(InvalidPaths(entries));
        // TODO: add a check here for CODEOWNERS paths that do not match any dir or file.

        return outputLines;
    }

    private static List<string> PathsWithMissingPrefixSlash(List<CodeownersEntry> entries)
        => entries
            .Where(entry => !entry.PathExpression.StartsWith("/"))
            .Select(entry =>
                "|" +
                $"{entry.PathExpression} " +
                $"| {string.Join(",", entry.Owners)}" +
                "| INVALID_PATH_SHOULD_START_WITH_SLASH")
            .ToList();

    private static List<string> PathsWithMissingSuffixSlash(
        string targetDir,
        List<CodeownersEntry> entries,
        string[] paths)
    {
        List<string> outputLines = new List<string>();
        foreach (CodeownersEntry entry in entries.Where(entry => !entry.PathExpression.EndsWith("/")))
        {
            if (entry.ContainsWildcard)
            {
                // We do not support "the path is to file while it should be to directory" validation for paths
                // with wildcards yet. To do that, we would first need to resolve the path and see if there exists
                // a concrete path that includes the CODEOWNERS paths supposed-file-name as
                // infix dir.
                // For example, /a/**/b could match against /a/foo/b/c, meaning
                // the path is invalid.
                outputLines.Add(
                    "|" +
                    $"{entry.PathExpression} " +
                    $"| {string.Join(",", entry.Owners)}" +
                    "| WILDCARD_FILE_PATH_NEEDS_MANUAL_EVAL");
            }
            else
            {
                string trimmedPathExpression = entry.PathExpression.TrimStart('/');

                bool matchesDirExactly = MatchesDirExactly(targetDir, trimmedPathExpression);
                bool matchesNamePrefix = MatchesNamePrefix(paths, trimmedPathExpression);

                if (matchesDirExactly || matchesNamePrefix)
                {
                    string msgCode = matchesDirExactly && matchesNamePrefix ? "MATCHES_DIR_EXACTLY_AND_NAME_PREFIX" :
                        matchesDirExactly ? "MATCHES_DIR_EXACTLY" : "MATCHES_NAME_PREFIX";

                    outputLines.Add(
                        "|" +
                        $"{entry.PathExpression} " +
                        $"| {string.Join(",", entry.Owners)}" +
                        $"| INVALID_PATH_{msgCode}");
                }
            }
        }
        return outputLines;
    }

    private static bool MatchesNamePrefix(string[] paths, string trimmedPathExpression)
        => paths.Any(
            path =>
            {
                string trimmedPath = path.TrimStart('/');
                bool pathIsChildDir = trimmedPath.Contains("/")
                                      && trimmedPath.Length > trimmedPathExpression.Length
                                      && trimmedPath.Substring(trimmedPathExpression.Length).StartsWith('/');
                return trimmedPath.StartsWith(trimmedPathExpression)
                       && trimmedPath.Length != trimmedPathExpression.Length
                       && !pathIsChildDir;
            });

    private static bool MatchesDirExactly(string targetDir, string trimmedPathExpression)
    {
        string pathToDir = Path.Combine(
            targetDir,
            trimmedPathExpression.Replace('/', Path.DirectorySeparatorChar));
        return Directory.Exists(pathToDir);
    }

    private static List<string> InvalidPaths(List<CodeownersEntry> entries)
        => entries
            .Where(entry => !MatchedCodeownersEntry.IsCodeownersPathValid(entry.PathExpression))
            .Select(
                entry =>
                    "|" +
                    $"{entry.PathExpression} " +
                    $"| {string.Join(",", entry.Owners)}" +
                    "| INVALID_PATH")
            .ToList();

    /// <summary>
    /// Writes to .csv file the difference of owners for all paths in given repository,
    /// between two invocations of Azure.Sdk.Tools.RetrieveCodeOwners.Program.Main,
    /// denoted as LEFT and RIGHT. RetrieveCodeOwners.Program.Main method reads
    /// all files in given input repository, and tries to find owners for them based on
    /// CODEOWNERS matching configuration given as its parameters.
    ///
    /// You can import the test output into Excel, using .csv import wizard and
    /// selecting "|" as column separator.
    /// 
    /// The resulting .csv file has following headers:
    /// 
    /// DIFF CODE | PATH | LEFT PATH EXPRESSION | RIGHT PATH EXPRESSION | LEFT OWNERS | RIGHT OWNERS
    ///
    /// where LEFT  denotes the RetrieveCodeOwners.Program.Main configuration as provided by input[0].
    /// and   RIGHT denotes the RetrieveCodeOwners.Program.Main configuration as provided by input[1].
    /// 
    /// The columns have following values and meanings:
    ///
    /// DIFF CODE:
    ///   PATH _____-RIGHT
    ///     A file with given path, given in the column PATH, was not matched to any CODEOWNERS
    ///     path when using the LEFT configuration but it was matched when using the RIGHT configuration.
    ///
    ///   PATH LEFT -_____
    ///     Analogous to the case described above, but LEFT configuration has matched, and RIGHT didn't.
    ///
    ///   PATH _____-_____
    ///     A file with given path did not match to any CODEOWNERS path, whether using the LEFT
    ///     configuration or RIGHT configuration.
    ///     Such file has effectively no owners assigned, no matter which configuration is used.
    ///
    ///   OWNERS DIFF
    ///     A file with given path matched both when using LEFT and RIGHT configurations, but
    ///     the CODEOWNERS path to which it matched has different set of owners.
    ///
    /// PATH:
    ///     A path to the file being matched against CODEOWNERS path to determine owners.
    ///
    ///  LEFT PATH EXPRESSION:
    /// RIGHT PATH EXPRESSION:
    ///     A CODEOWNERS path that matched to PATH when using LEFT (or RIGHT, respectively) configuration.
    ///
    ///  LEFT OWNERS:
    /// RIGHT OWNERS:
    ///     The owners assigned to given LEFT PATH EXPRESSION (or RIGHT PATH EXPRESSION, respectively).
    /// </summary>
    private static void WriteOwnersDiffToCsv(
        (
            string targetDirPathSuffix,
            string codeownersFilePathSuffix,
            string ignoredPrefixes
            )[] input,
        string outputFilePrefix)
    {
        var stopwatch = Stopwatch.StartNew();

        Dictionary<string, CodeownersEntry> leftOwners = RetrieveCodeowners(
            input[0].targetDirPathSuffix,
            input[0].codeownersFilePathSuffix,
            input[0].ignoredPrefixes);
        Dictionary<string, CodeownersEntry> rightOwners = RetrieveCodeowners(
            input[1].targetDirPathSuffix,
            input[1].codeownersFilePathSuffix,
            input[1].ignoredPrefixes);

        string[] diffLines = PathOwnersDiff(leftOwners, rightOwners);

        var outputFilePath = outputFilePrefix + OwnersDiffOutputPathSuffix;
        File.WriteAllLines(outputFilePath, diffLines);
        Console.WriteLine($"DONE diffing. " +
                          $"Output written out to {Path.GetFullPath(outputFilePath)}. " +
                          $"Time taken: {stopwatch.Elapsed}.");
    }

    private static Dictionary<string, CodeownersEntry> RetrieveCodeowners(
        string targetDirPathSuffix,
        string codeownersFilePathSuffixToRootDir,
        string ignoredPathPrefixes)
    {
        string rootDir = PathNavigatingToRootDir(CurrentDir);
        string targetDir = rootDir + targetDirPathSuffix;
        string codeownersFilePath = targetDir + codeownersFilePathSuffixToRootDir;
        Debug.Assert(Directory.Exists(targetDir));
        Debug.Assert(File.Exists(codeownersFilePath));

        string actualOutput, actualErr;
        int returnCode;
        using (var consoleOutput = new ConsoleOutput())
        {
            // Act
            returnCode = Program.Main(
                targetPath: "/**",
                codeownersFilePath,
                // false because we want to see the full owners diff, but observe that
                // for the build failure notification recipients determination it should be true,
                // because Contacts.GetMatchingCodeownersEntry() calls ExcludeNonUserAliases().
                excludeNonUserAliases: false, 
                targetDir,
                ignoredPathPrefixes);

            actualOutput = consoleOutput.GetStdout();
            actualErr = consoleOutput.GetStderr();
        }

        var actualEntries = JsonSerializer.Deserialize<Dictionary<string, CodeownersEntry>>(actualOutput)!;
        return actualEntries;
    }

    private static string PathNavigatingToRootDir(string currentDir)
        => "./" + string.Join(
            "/",
            currentDir
                .Split("/", StringSplitOptions.RemoveEmptyEntries)
                .Select(_ => ".."));

    private static string[] PathOwnersDiff(
        Dictionary<string, CodeownersEntry> left,
        Dictionary<string, CodeownersEntry> right)
    {
        Debug.Assert(
            left.Keys.ToHashSet().SetEquals(right.Keys.ToHashSet()),
            "The compared maps of owner data are expected to have the same paths (keys).");

        List<string> outputLines = new List<string>
        {
            "DIFF CODE | PATH | LEFT PATH EXPRESSION | RIGHT PATH EXPRESSION | LEFT OWNERS | RIGHT OWNERS"
        };
        foreach (string path in left.Keys)
        {
            if (left[path].IsValid && right[path].IsValid)
            {
                // Path matched against an entry in both "left" and "right" owners data.
                // Here we determine if the owners lists match.
                outputLines.AddRange(PathOwnersDiff(path, left[path], right[path]));
            }
            else if (left[path].IsValid && !right[path].IsValid)
            {
                // Path matched against an entry in the "left" owners data, but not in the right.
                outputLines.Add($"PATH  LEFT -_____ " +
                                $"| {path} | {left[path].PathExpression} | | {string.Join(",",left[path].Owners)} |");
            }
            else if (!left[path].IsValid && right[path].IsValid)
            {
                // Path matched against an entry in the "right" owners data, but not in the right.
                outputLines.Add($"PATH  _____-RIGHT " +
                                $"| {path} | | {right[path].PathExpression} | | {string.Join(",",right[path].Owners)}");
            }
            else
            {
                // Path did not match against any owners data, not in "left" nor "right".
                outputLines.Add($"PATH  _____-_____ | {path} |");
            }
        }

        return outputLines.ToArray();
    }

    private static string[] PathOwnersDiff(
        string path,
        CodeownersEntry left,
        CodeownersEntry right)
    {
        Debug.Assert(left.IsValid);
        Debug.Assert(right.IsValid);

        List<string> outputLines = new List<string>();

        if (!left.Owners.ToHashSet().SetEquals(right.Owners.ToHashSet()))
        {
            // Given path owners differ between "left" an "right" owners data.
            outputLines.Add(
                $"OWNERS DIFF | {path} " +
                $"| {left.PathExpression} | {right.PathExpression} " +
                $"| {string.Join(", ", left.Owners)} | {string.Join(", ", right.Owners)}");
        }

        return outputLines.ToArray();
    }

    #endregion
}
