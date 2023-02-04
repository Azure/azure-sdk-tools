using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Azure.Sdk.Tools.CodeOwnersParser;
using NUnit.Framework;

namespace Azure.Sdk.Tools.RetrieveCodeOwners.Tests;

/// <summary>
/// Test class for Azure.Sdk.Tools.RetrieveCodeOwners.Program.Main(),
/// for scenario in which targetPath is a glob path, i.e.
/// targetPath.IsGlobPath() returns true.
///
/// The tests assertion expectations are set to match GitHub CODEOWNERS interpreter behavior,
/// as explained here:
/// https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners
/// and as observed based on manual tests and verification.
/// </summary>
[TestFixture]
public class ProgramGlobPathTests
{
    /// <summary>
    /// Given:
    ///
    ///   file system contents as seen in TestData/InputDir
    /// 
    ///   codeownersFilePathOrUrl contents as seen in TestData/glob_path_CODEOWNERS
    ///
    ///   targetPath of /**
    ///
    ///   excludeNonUserAliases set to false and useRegexMatcher set to true.
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
    public void OutputsCodeownersForGlobPath()
    {
        const string targetDir = "./TestData/InputDir";
        const string targetPath = "/**";
        const string codeownersFilePathOrUrl = "./TestData/glob_path_CODEOWNERS";
        const bool excludeNonUserAliases = false;
        const bool useRegexMatcher = true;

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
            // @formatter:on
        };
        
        string actualOutput, actualErr;
        int returnCode;
        using (var consoleOutput = new ConsoleOutput())
        {
            // Act
            returnCode = Program.Main(
                targetPath,
                codeownersFilePathOrUrl,
                excludeNonUserAliases,
                targetDir,
                useRegexMatcher: useRegexMatcher);

            actualOutput = consoleOutput.GetStdout();
            actualErr = consoleOutput.GetStderr();
        }

        var actualEntries = TryDeserializeActualEntries(actualOutput, actualErr);

        Assert.Multiple(() =>
        {
            AssertEntries(actualEntries, expectedEntries);
            Assert.That(returnCode, Is.EqualTo(0));
            Assert.That(actualErr, Is.EqualTo(string.Empty));
        });
    }

    private static Dictionary<string, CodeownersEntry> TryDeserializeActualEntries(
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
