using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <summary>
    /// A battery of test cases exercising the Azure.Sdk.Tools.RetrieveCodeOwners.Program.Main executable.
    ///
    /// Each test case is composed of a targetPath and expected CodeownersEntry that is to match against
    /// the targetPath when the executable is executed.
    ///
    /// These test battery is used in the following ways:
    ///
    /// 1. In OutputsCorrectCodeownersOnSimpleTargetPath parameterized unit test, each test case is exercised
    /// by running the executable with targetPath provided as input targetPath.
    ///
    /// 2. In OutputsCorrectCodeownersOnGlobTargetPath the entire test battery is asserted against
    /// by running the executable with targetPaths set to "/**", thus entering the glob-matching mode and finding
    /// all the targetPaths present in this battery.
    ///
    /// Preconditions for running tests against this battery:
    /// - directory "./TestData/InputDir" and file  "./TestData/test_CODEOWNERS" contain appropriate contents
    /// - the exercised executable is passed as input appropriate arguments pointing to the file system;
    ///   consult the aforementioned tests for concrete values.
    /// </summary>
    private static readonly TestCase[] testCases =
    {
        // @formatter:off
        //   targetPath        expected CodeownersEntry
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

    private static Dictionary<string, CodeownersEntry> TestCasesAsDictionary
        => testCases.ToDictionary(
            testCase => testCase.TargetPath,
            testCase => testCase.ExpectedCodeownersEntry);

    /// <summary>
    /// Please see comment on RetrieveCodeOwnersProgramTests.testCases
    /// </summary>
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
    /// Please see comment on RetrieveCodeOwnersProgramTests.testCases
    /// </summary>
    [Test]
    public void OutputsCorrectCodeownersOnGlobTargetPath()
    {
        const string targetDir = "./TestData/InputDir";
        const string targetPath = "/**";
        const string codeownersFilePathOrUrl = "./TestData/test_CODEOWNERS";
        const bool excludeNonUserAliases = false;

        Dictionary<string, CodeownersEntry> expectedEntriesByPath = TestCasesAsDictionary;

        // Act
        (string actualOutput, string actualErr, int returnCode) = RunProgramMain(
            targetPath,
            codeownersFilePathOrUrl,
            excludeNonUserAliases,
            targetDir);

        Dictionary<string, CodeownersEntry> actualEntriesByPath = TryDeserializeActualEntriesFromGlobTargetPath(actualOutput, actualErr);

        Assert.Multiple(() =>
        {
            AssertEntries(actualEntriesByPath, expectedEntriesByPath);
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

    /// <summary>
    /// Please see comment on RetrieveCodeOwnersProgramTests.testCases
    /// </summary>
    public record TestCase(
        string TargetPath,
        CodeownersEntry ExpectedCodeownersEntry);
}
