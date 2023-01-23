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
/// </summary>
[TestFixture]
public class ProgramGlobPathTests
{
    /// <summary>
    /// Given:
    /// <br/><br/>
    /// codeownersFilePathOrUrl contents of:
    /// <code>
    /// 
    ///   / @slash
    ///   /* @star
    ///   /foo/**/a.txt @foo_2star_a
    ///   /foo/*/a.txt @foo_star_a_1 @foo_star_a_2
    /// 
    /// </code>
    /// 
    /// targetDir contents of:
    /// 
    /// <code>
    /// 
    ///   /a.txt
    ///   /b.txt
    ///   /foo/a.txt
    ///   /foo/b.txt
    ///   /foo/bar/a.txt
    ///   /foo/bar/b.txt
    /// 
    /// </code>
    /// 
    /// targetPath of:
    /// 
    /// <code>
    /// 
    ///   /**
    /// 
    /// </code>
    ///
    /// excludeNonUserAliases set to false and useRegexMatcher set to true.
    /// <br/><br/>
    /// When:
    ///   The retrieve-codeowners tool is executed on these inputs.
    /// <br/><br/>
    /// Then:
    ///   The tool should return on STDOUT owners matched in following way:
    /// 
    /// <code>
    ///   /a.txt @star
    ///   /b.txt @star
    ///   /foo/a.txt @foo_2star_a
    ///   /foo/b.txt @slash
    ///   /foo/bar/a.txt @foo_star_a_1 @foo_star_a_2
    ///   /foo/bar/b.txt @slash
    /// </code>
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
            ["foo/b.txt"]     = new CodeownersEntry("/",             new List<string> { "slash" }),
            ["foo/bar/a.txt"] = new CodeownersEntry("/foo/*/a.txt",  new List<string> { "foo_star_a_1", "foo_star_a_2" }),
            ["foo/bar/b.txt"] = new CodeownersEntry("/",             new List<string> { "slash" }),
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
            Assert.That(expectedEntries[path], Is.EqualTo(actualEntry), $"path: {path}");
        }

        Assert.That(actualEntries, Has.Count.EqualTo(expectedEntries.Count));
    }
}
