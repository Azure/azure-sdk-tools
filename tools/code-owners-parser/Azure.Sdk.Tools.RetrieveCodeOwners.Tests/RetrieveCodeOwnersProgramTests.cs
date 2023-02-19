using System;
using System.Collections.Generic;
using System.Text.Json;
using Azure.Sdk.Tools.CodeOwnersParser;
using NUnit.Framework;

namespace Azure.Sdk.Tools.RetrieveCodeOwners.Tests;

/// <summary>
/// Test class for Azure.Sdk.Tools.RetrieveCodeOwners.Program.Main(),
///
/// The tests assertion expectations are set to match GitHub CODEOWNERS interpreter behavior,
/// as explained here:
/// https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners
/// and as observed based on manual tests and verification.
///
/// For additional related tests, please see:
/// - Azure.Sdk.Tools.CodeOwnersParser.Tests.CodeownersFileTests
/// </summary>
[TestFixture]
public class RetrieveCodeOwnersProgramTests
{
    private static readonly TestCase[] testCases =
    {
        // @formatter:off
        new ("a.txt"         , new CodeownersEntry("/*",            new List<string> { "star" })),
        new ("b.txt"         , new CodeownersEntry("/*",            new List<string> { "star" })),
        new ("foo/a.txt"     , new CodeownersEntry("/foo/**/a.txt", new List<string> { "foo_2star_a" })),
        new ("foo/b.txt"     , new CodeownersEntry("/**",           new List<string> { "2star" })),
        new ("foo/bar/a.txt" , new CodeownersEntry("/foo/*/a.txt",  new List<string> { "foo_star_a_1", "foo_star_a_2" })),
        new ("foo/bar/b.txt" , new CodeownersEntry("/**",           new List<string> { "2star" })),
        new ("baz/cor/c.txt" , new CodeownersEntry("/baz*",         new List<string> { "baz_star" })),
        new ("baz_.txt"      , new CodeownersEntry("/baz*",         new List<string> { "baz_star" })),
        new ("qux/abc/d.txt" , new CodeownersEntry("/qux/",         new List<string> { "qux" })),
        new ("cor.txt"       , new CodeownersEntry("/*",            new List<string> { "star" })),
        new ("cor2/a.txt"    , new CodeownersEntry("/**",           new List<string> { "2star" })),
        new ("cor/gra/a.txt" , new CodeownersEntry("/**",           new List<string> { "2star" }))
        // @formatter:on
    };

    public record TestCase(
        string TargetPath,
        CodeownersEntry ExpectedCodeownersEntry);

    [TestCaseSource(nameof(testCases))]
    public void OutputsCorrectCodeownersOnSimpleTargetPath(TestCase testCase)
    {
        const string targetDir = "./TestData/InputDir";
        const string codeownersFilePathOrUrl = "./TestData/test_CODEOWNERS";
        const bool excludeNonUserAliases = false;

        var targetPath = testCase.TargetPath;
        var expectedEntry = testCase.ExpectedCodeownersEntry;

        // Act
        (string actualOutput, string actualErr, int returnCode) = RunProgramMain(
            targetPath,
            codeownersFilePathOrUrl,
            excludeNonUserAliases,
            targetDir);

        CodeownersEntry actualEntry = TryDeserializeActualEntryFromSimpleTargetPath(actualOutput, actualErr);

        Assert.Multiple(() =>
        {
            Assert.That(actualEntry, Is.EqualTo(expectedEntry), $"path: {targetPath}");
            Assert.That(returnCode, Is.EqualTo(0));
            Assert.That(actualErr, Is.EqualTo(string.Empty));
        });
    }

    /// <summary>
    /// Given:
    ///
    ///   file system contents as seen in TestData/InputDir
    /// 
    ///   codeownersFilePathOrUrl contents as seen in TestData/glob_path_CODEOWNERS
    ///
    ///   targetPath of /**
    ///
    ///   excludeNonUserAliases set to false
    /// 
    /// When:
    ///   The retrieve-codeowners tool is executed on these inputs.
    ///
    /// Then:
    ///   The tool should return on STDOUT owners matched as seen in the
    ///   "expectedEntries" dictionary.
    /// 
    /// </summary>
    [Test]
    public void OutputsCorrectCodeownersOnGlobTargetPath()
    {
        const string targetDir = "./TestData/InputDir";
        const string targetPath = "/**";
        const string codeownersFilePathOrUrl = "./TestData/test_CODEOWNERS";
        const bool excludeNonUserAliases = false;

        var expectedEntries = new Dictionary<string, CodeownersEntry>
        {
            // @formatter:off
            ["a.txt"]         = new CodeownersEntry("/*",            new List<string> { "star" }),
            ["b.txt"]         = new CodeownersEntry("/*",            new List<string> { "star" }),
            ["foo/a.txt"]     = new CodeownersEntry("/foo/**/a.txt", new List<string> { "foo_2star_a" }),
            ["foo/b.txt"]     = new CodeownersEntry("/**",           new List<string> { "2star" }),
            ["foo/bar/a.txt"] = new CodeownersEntry("/foo/*/a.txt",  new List<string> { "foo_star_a_1", "foo_star_a_2" }),
            ["foo/bar/b.txt"] = new CodeownersEntry("/**",           new List<string> { "2star" }),
            ["baz/cor/c.txt"] = new CodeownersEntry("/baz*",         new List<string> { "baz_star" }),
            ["baz_.txt"]      = new CodeownersEntry("/baz*",         new List<string> { "baz_star" }),
            ["qux/abc/d.txt"] = new CodeownersEntry("/qux/",         new List<string> { "qux" }),
            ["cor.txt"]       = new CodeownersEntry("/*",            new List<string> { "star" }),
            ["cor2/a.txt"]    = new CodeownersEntry("/**",           new List<string> { "2star" }),
            ["cor/gra/a.txt"] = new CodeownersEntry("/**",           new List<string> { "2star" }),
            // @formatter:on
        };

        // Act
        (string actualOutput, string actualErr, int returnCode) = RunProgramMain(
            targetPath,
            codeownersFilePathOrUrl,
            excludeNonUserAliases,
            targetDir);

        Dictionary<string, CodeownersEntry> actualEntries = TryDeserializeActualEntriesFromGlobTargetPath(actualOutput, actualErr);

        Assert.Multiple(() =>
        {
            AssertEntries(actualEntries, expectedEntries);
            Assert.That(returnCode, Is.EqualTo(0));
            Assert.That(actualErr, Is.EqualTo(string.Empty));
        });
    }

    private static (string actualOutput, string actualErr, int returnCode) RunProgramMain(
        string targetPath,
        string codeownersFilePathOrUrl,
        bool excludeNonUserAliases,
        string targetDir)
    {
        string actualOutput, actualErr;
        int returnCode;
        using (var consoleOutput = new ConsoleOutput())
        {
            // Act
            returnCode = Program.Main(
                targetPath,
                codeownersFilePathOrUrl,
                excludeNonUserAliases,
                targetDir);

            actualOutput = consoleOutput.GetStdout();
            actualErr = consoleOutput.GetStderr();
        }

        return (actualOutput, actualErr, returnCode);
    }

    private static CodeownersEntry TryDeserializeActualEntryFromSimpleTargetPath(
        string actualOutput,
        string actualErr)
    {
        CodeownersEntry actualEntry;
        try
        {
            actualEntry =
                JsonSerializer.Deserialize<CodeownersEntry>(actualOutput)!;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            Console.WriteLine("actualOutput: " + actualOutput);
            Console.WriteLine("actualErr: " + actualErr);
            throw;
        }

        return actualEntry;
    }

    private static Dictionary<string, CodeownersEntry> TryDeserializeActualEntriesFromGlobTargetPath(
        string actualOutput,
        string actualErr)
    {
        Dictionary<string, CodeownersEntry> actualEntries;
        try
        {
            actualEntries =
                JsonSerializer.Deserialize<Dictionary<string, CodeownersEntry>>(actualOutput)!;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            Console.WriteLine("actualOutput: " + actualOutput);
            Console.WriteLine("actualErr: " + actualErr);
            throw;
        }

        return actualEntries;
    }

    private static void AssertEntries(
        Dictionary<string, CodeownersEntry> actualEntries,
        Dictionary<string, CodeownersEntry> expectedEntries)
    {
        foreach (KeyValuePair<string, CodeownersEntry> kvp in actualEntries)
        {
            string path = kvp.Key;
            CodeownersEntry actualEntry = kvp.Value;
            Assert.That(actualEntry, Is.EqualTo(expectedEntries[path]), $"path: {path}");
        }

        Assert.That(actualEntries, Has.Count.EqualTo(expectedEntries.Count));
    }
}
