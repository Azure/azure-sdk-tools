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
/// by obtaining the owners based on specified CODEOWNERS files,
/// or using specified matching logic.
///
/// These tools are to be run manually, locally, by a developer.
/// They do not participate in an automated regression test suite.
///
/// To run these tools, you will have to first manually comment out the "Ignore"
/// property of the "TestFixture" annotation below.
/// Then run the desired tool as a unit test, either from your IDE,
/// or "dotnet test" command line tool.
///
/// You can import the tool output into Excel, using .CSV import wizard and
/// selecting "|" as column separator.
/// </summary>
[TestFixture(Ignore = "Tools to be used manually")]
public class CodeownersDiffTests
{
    private const string DefaultIgnoredPathPrefixes = Program.DefaultIgnoredPrefixes;
    private const string OwnersDiffOutputPathSuffix = "_owners_diff.csv";
    private const string OwnersDataOutputPathSuffix = "_owners.csv";

    // All these paths assume that appropriate repositories are cloned into the same local
    // directory as "azure-sdk-tools" repo, containing this logic.
    // So the dir layout is like:
    // <commonParent>/azure-sdk-tools/
    // <commonParent>/azure-sdk-for-net/
    // <commonParent>/azure-sdk-for-java/
    // <commonParent>/azure-sdk-for-.../
    // ...
    private const string AzureSdkForNetTargetDirPathSuffix = "/../azure-sdk-for-net";
    private const string AzureSdkForNetCodeownersPathSuffix = AzureSdkForNetTargetDirPathSuffix + "/.github/CODEOWNERS";
    // TODO: add more repos here.

    #region Owners diff

    [Test]
    public void WriteToFileCodeownersMatcherDiffForAzureSdkTools()
    {
        // Empty string here means to just use the root directory of the local "azure-sdk-tools" clone.
        var targetDirPathSuffix = ""; 
        var codeownersPathSuffix = "/.github/CODEOWNERS";
        var ignoredPrefixes = ".git|artifacts";
        WriteToFileOwnersDiff(new[]
        {
            (targetDirPathSuffix, codeownersPathSuffix, ignoredPrefixes, useRegexMatcher: false),
            (targetDirPathSuffix, codeownersPathSuffix, ignoredPrefixes, useRegexMatcher: true)
        }, outputFilePrefix: "azure-sdk-tools");
    }

    [Test]
    public void WriteToFileCodeownersMatcherDiffForAzureSdkForNet()
    {
        WriteToFileOwnersDiff(
            new[]
            {
                (AzureSdkForNetTargetDirPathSuffix, AzureSdkForNetCodeownersPathSuffix,
                    DefaultIgnoredPathPrefixes, useRegexMatcher: false),
                (AzureSdkForNetTargetDirPathSuffix, AzureSdkForNetCodeownersPathSuffix,
                    DefaultIgnoredPathPrefixes, useRegexMatcher: true)
            },
            outputFilePrefix: "azure-sdk-for-net");
    }

    #endregion
    
    #region Owners data

    [Test]
    public void WriteToFileRegexMatcherCodeownersForAzureSdkTools()
    {
        // Empty string here means to just use the root directory of the local "azure-sdk-tools" clone.
        var targetDirPathSuffix = ""; 
        var codeownersPathSuffix = "/.github/CODEOWNERS";
        var ignoredPrefixes = ".git|artifacts";
        WriteToFileOwnersData(
            targetDirPathSuffix,
            codeownersPathSuffix,
            ignoredPrefixes,
            useRegexMatcher: true,
            outputFilePrefix: "azure-sdk-tools");
    }

    [Test]
    public void WriteToFileRegexMatcherCodeownersForAzureSdkForNet()
        => WriteToFileOwnersData(
            AzureSdkForNetTargetDirPathSuffix,
            AzureSdkForNetCodeownersPathSuffix,
            DefaultIgnoredPathPrefixes,
            useRegexMatcher: true,
            outputFilePrefix: "azure-sdk-for-net");

    #endregion

    private static void WriteToFileOwnersData(
        string targetDirPathSuffix, 
        string codeownersPathSuffix, 
        string ignoredPrefixes, 
        bool useRegexMatcher,
        string outputFilePrefix)
    {
        var stopwatch = Stopwatch.StartNew();
        
        Dictionary<string, CodeownersEntry> ownersData = RunMain(
            targetDirPathSuffix,
            codeownersPathSuffix,
            ignoredPrefixes,
            useRegexMatcher);

        List<string> outputLines =
            new List<string> { "PATH | PATH EXPRESSION | COMMA-SEPARATED OWNERS" };
        foreach (var kvp in ownersData)
        {
            string path = kvp.Key;
            CodeownersEntry entry = kvp.Value;
            outputLines.Add(
                $"{path} " +
                $"| {(entry.IsValid ? entry.PathExpression : "_____")} " +
                $"| {string.Join(",", entry.Owners)}");
        }

        var outputFilePath = outputFilePrefix + OwnersDataOutputPathSuffix;
        File.WriteAllLines(outputFilePath, outputLines);
        Console.WriteLine($"DONE writing out owners. " +
                          $"Output written out to {Path.GetFullPath(outputFilePath)}. " +
                          $"Time taken: {stopwatch.Elapsed}.");
    }

    private static void WriteToFileOwnersDiff((
        string targetDirPathSuffix, 
        string codeownersPathSuffix, 
        string ignoredPrefixes, 
        bool useRegexMatcher)[] input,
        string outputFilePrefix)
    {
        var stopwatch = Stopwatch.StartNew();

        Dictionary<string, CodeownersEntry> leftOwners = RunMain(
            input[0].targetDirPathSuffix,
            input[0].codeownersPathSuffix,
            input[0].ignoredPrefixes,
            input[0].useRegexMatcher);
        Dictionary<string, CodeownersEntry> rightOwners = RunMain(
            input[1].targetDirPathSuffix,
            input[1].codeownersPathSuffix,
            input[1].ignoredPrefixes,
            input[1].useRegexMatcher);

        string[] diffLines = PathOwnersDiff(leftOwners, rightOwners);

        var outputFilePath = outputFilePrefix + OwnersDiffOutputPathSuffix;
        File.WriteAllLines(outputFilePath, diffLines);
        Console.WriteLine($"DONE diffing. " +
                          $"Output written out to {Path.GetFullPath(outputFilePath)}. " +
                          $"Time taken: {stopwatch.Elapsed}.");
    }

    private static Dictionary<string, CodeownersEntry> RunMain(
        string targetDirPathSuffix,
        string codeownersPathSuffixToRootDir,
        string ignoredPathPrefixes,
        bool useRegexMatcher)
    {
        // Current dir, ".", is "/artifacts/bin/Azure.Sdk.Tools.CodeOwnersParser.Tests/Debug/net6.0".
        const string currentDir = "/artifacts/bin/Azure.Sdk.Tools.CodeOwnersParser.Tests/Debug/net6.0";
        string rootDir = PathNavigatingToRootDir(currentDir);

        string actualOutput, actualErr;
        int returnCode;
        using (var consoleOutput = new ConsoleOutput())
        {
            // Act
            returnCode = Program.Main(
                targetPath: "/**",
                codeownersFilePathOrUrl: rootDir + codeownersPathSuffixToRootDir,
                excludeNonUserAliases: false,
                targetDir: rootDir + targetDirPathSuffix,
                ignoredPathPrefixes,
                useRegexMatcher);

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
}
