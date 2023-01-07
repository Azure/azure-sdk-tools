using System;
using System.Collections.Generic;
using Microsoft.Extensions.FileSystemGlobbing;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeOwnersParser.Tests;

[TestFixture]
public class CodeOwnersFileTests
{
    /// <summary>
    /// A battery of test cases specifying behavior of new logic matching target
    /// path to CODEOWNERS entries , and comparing it to existing, legacy logic.
    ///
    /// The logic that has changed lives in CodeOwnersFile.FindOwnersForClosestMatch.
    ///
    /// The new logic supports matching against wildcards, while the old one doesn't.
    ///
    /// In the test case table below, any discrepancy between legacy and new
    /// parser expected matches that doesn't pertain to wildcard matching denotes
    /// a potential backward compatibility and/or existing defect in the legacy parser.
    /// 
    /// For further details, please see:
    /// https://github.com/Azure/azure-sdk-tools/issues/2770
    /// </summary>
    private static readonly TestCase[] testCases =
    {
        // @formatter:off
        //       TestCase:        Path:                 Expected match:
        //           Name,        Target , Codeown.   , Legacy , New
        new(         "1" ,           "a" , "a"        ,   true , true  ),
        new(         "2" ,           "a" , "a/"       ,   true , false ), // New parser doesn't match as codeowners path expects directory, but it is unclear if target is directory, or not.
        new(         "3" ,         "a/b" , "a/b"      ,   true , true  ),
        new(         "4" ,         "a/b" , "/a/b"     ,   true , true  ),
        new(         "5" ,         "a/b" , "a/b/"     ,   true , false ), // New parser doesn't match as codeowners path expects directory, but it is unclear if target is directory, or not.
        new(         "6" ,        "/a/b" , "a/b"      ,   true , true  ),
        new(         "7" ,        "/a/b" , "/a/b"     ,   true , true  ),
        new(         "8" ,        "/a/b" , "a/b/"     ,   true , false ), // New parser doesn't match as codeowners path expects directory, but it is unclear if target is directory, or not.
        new(         "9" ,        "a/b/" , "a/b"      ,   true , true  ),
        new(        "10" ,        "a/b/" , "/a/b"     ,   true , true  ),
        new(        "11" ,        "a/b/" , "a/b/"     ,   true , true  ),
        new(        "12" ,       "/a/b/" , "a/b"      ,   true , true  ),
        new(        "13" ,       "/a/b/" , "/a/b"     ,   true , true  ),
        new(        "14" ,       "/a/b/" , "a/b/"     ,   true , true  ),
        new(        "15" ,       "/a/b/" , "/a/b/"    ,   true , true  ),
        new(        "16" ,      "/a/b/c" , "a/b"      ,   true , true  ),
        new(        "17" ,      "/a/b/c" , "/a/b"     ,   true , true  ),
        new(        "18" ,      "/a/b/c" , "a/b/"     ,   true , true  ),
        new(        "19" ,    "/a/b/c/d" , "/a/b/"    ,   true , true  ),
        new(    "casing" ,         "ABC" , "abc"      ,   true , false ), // New parser doesn't match as it is case-sensitive, per codeowners spec
        new(  "chained1" ,       "a/b/c" , "a"        ,   true , true  ),
        new(  "chained2" ,       "a/b/c" , "b"        ,  false , true  ), // New parser matches per codeowners and .gitignore spec
        new(  "chained3" ,       "a/b/c" , "b/"       ,  false , true  ), // New parser matches per codeowners and .gitignore spec
        new(  "chained4" ,       "a/b/c" , "c"        ,  false , true  ), // New parser matches per codeowners and .gitignore spec
        new(  "chained5" ,       "a/b/c" , "c/"       ,  false , false ),
        new(  "chained6" ,     "a/b/c/d" , "c/"       ,  false , true  ), // New parser matches per codeowners and .gitignore spec
        new(  "chained7" ,   "a/b/c/d/e" , "c/"       ,  false , true  ), // New parser matches per codeowners and .gitignore spec
        new(  "chained8" ,       "a/b/c" , "b/c"      ,  false , false ), // TODO need to verify if CODEOWNERS actually follows this rule of "middle slashes prevent path relativity" from .gitignore, or not.
        new(  "chained9" ,           "a" , "a/b/c"    ,  false , false ),
        new( "chained10" ,           "c" , "a/b/c"    ,  false , false ),
        // Cases not supported by the new parser.
        new(   "unsupp1" ,          "!a" , "!a"       ,   true , false ),
        new(   "unsupp2" ,           "b" , "!a"       ,  false , false ),
        new(   "unsupp3" ,        "a[b"  , "a[b"      ,   true , false ),
        new(   "unsupp4" ,        "a]b"  , "a]b"      ,   true , false ),
        new(   "unsupp5" ,        "a?b"  , "a?b"      ,   true , false ),
        new(   "unsupp6" ,        "axb"  , "a?b"      ,  false , false ),
        // The cases below test for wildcard support by the new parser. Legacy parser skips over wildcards.
        new(       "**1" ,           "a" , "**/a"     ,  false , true  ), 
        new(       "**2" ,           "a" , "**/b/a"   ,  false , false ), 
        new(       "**3" ,           "a" , "**/a/b"   ,  false , false ), 
        new(       "**4" ,           "a" , "/**/a"    ,  false , true  ),
        new(       "**5" ,         "a/b" , "a/**/b"   ,  false , true  ),
        new(       "**6" ,       "a/x/b" , "a/**/b"   ,  false , true  ),
        new(       "**7" ,       "a/y/b" , "a/**/b"   ,  false , true  ),
        new(       "**8" ,     "a/x/y/b" , "a/**/b"   ,  false , true  ),
        new(       "**9" ,   "c/a/x/y/b" , "a/**/b"   ,  false , false ),
        new(       "*10" ,   "a/b/cxy/d" , "/**/*x*/" ,  false , true  ),
        new(        "1*" ,           "a" , "*"        ,  false , true  ),
        new(        "2*" ,         "a/b" , "a/*"      ,  false , true  ),
        // There is discrepancy between GitHub CODEOWNERS behavior [1] and .gitignore behavior here
        // CODEOWNERS will not match this path, while .gitignore will
        // [1] The "docs/*" example in https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners#example-of-a-codeowners-file
        // [2] Confirmed empirically. The .gitignore will match "a/*" to "a/b" and thus ignore everything.
        new(        "3*" ,       "a/b/c" , "a/*"      ,  false , true  ),
        new(        "4*" ,       "x/a/b" , "a/*"      ,  false , false ),
        new(       "1*/" ,         "a/b" , "a/*/"     ,  false , false ),
        new(       "2*/" ,        "a/b/" , "a/*/"     ,  false , true  ),
        new(       "3*/" ,       "a/b/c" , "a/*/"     ,  false , true  ),
        new(      "1*/*" ,         "a/b" , "a/*/*"    ,  false , false ),
        new(      "2*/*" ,     "a/b/c/d" , "a/*/*/d"  ,  false , true  ),
        new(      "3*/*" ,   "a/b/x/c/d" , "a/*/*/d"  ,  false , false ),
        new(     "1**/*" ,   "a/b/x/c/d" , "a/**/*/d" ,  false , true  ),
        new(        "*1" ,         "a/b" , "*/b"      ,  false , true  ),
        new(      "*/*1" ,         "a/b" , "*/*/b"    ,  false , false ),
        new(       "1**" ,           "a" , "a/**"     ,  false , false ),
        new(       "2**" ,          "a/" , "a/**"     ,  false , true  ),
        new(       "3**" ,         "a/b" , "a/**"     ,  false , true  ),
        new(       "4**" ,        "a/b/" , "a/**"     ,  false , true  ),
        new(    "*.ext1" ,      "a/x.md" , "*.md"     ,  false , true  ),
        new(    "*.ext2" ,    "a/b/x.md" , "*.md"     ,  false , true  ),
        new(    "*.ext3" , "a/b.md/x.md" , "*.md"     ,  false , true  ),
        new(    "*.ext4" ,        "a/md" , "*.md"     ,  false , false ),
        new(    "*.ext5" ,         "a.b" , "a.*"      ,  false , true  ),
        new(    "*.ext6" ,        "a.b/" , "a.*"      ,  false , true  ),
        new(    "*.ext5" ,         "a.b" , "a.*/"     ,  false , false ),
        new(    "*.ext7" ,        "a.b/" , "a.*/"     ,  false , true  ),
        new(    "*.ext8" ,        "a.b"  , "/a.*"     ,  false , true  ),
        new(    "*.ext9" ,        "a.b/" , "/a.*"     ,  false , true  ),
        new(   "*.ext10" ,      "x/a.b/" , "/a.*"     ,  false , false ),
        // New parser should return false, but returns true due to https://github.com/dotnet/runtime/issues/80076
        // TODO globbug1 actually covers-up problem with the parser, where it converts "*" to "**/*".
        new(  "globbug1" ,         "a/b" , "*"        ,  false , true  ),
        new(  "globbug2" ,      "a/b/c/" , "a/*/"     ,  false , true  )
        // @formatter:on
    };

    /// <summary>
    /// A repro for https://github.com/dotnet/runtime/issues/80076
    /// </summary>
    [Test]
    public void TestGlobBugRepro()
    {
        var globMatcher = new Matcher(StringComparison.Ordinal);
        globMatcher.AddInclude("/*/");

        var dir = new InMemoryDirectoryInfo(
            rootDir: "/", 
            files: new List<string> { "/a/b" });

        var patternMatchingResult = globMatcher.Execute(dir);
        // The expected behavior is "Is.False", but actual behavior is "Is.True".
        Assert.That(patternMatchingResult.HasMatches, Is.True);
    }

    /// <summary>
    /// Exercises Azure.Sdk.Tools.CodeOwnersParser.Tests.CodeOwnersFileTests.testCases.
    /// See comment on that member for details.
    /// </summary>
    [TestCaseSource(nameof(testCases))]
    public void TestParseAndFindOwnersForClosestMatch(TestCase testCase)
    {
        List<CodeOwnerEntry>? codeownersEntries =
            CodeOwnersFile.ParseContent(testCase.CodeownersPath + "@owner");

        VerifyFindOwnersForClosestMatch(testCase, codeownersEntries, useNewImpl: false, testCase.ExpectedLegacyMatch);
        VerifyFindOwnersForClosestMatch(testCase, codeownersEntries, useNewImpl: true, testCase.ExpectedNewMatch);
    }

    private static void VerifyFindOwnersForClosestMatch(TestCase testCase,
        List<CodeOwnerEntry> codeownersEntries,
        bool useNewImpl,
        bool expectedMatch)
    {
        CodeOwnerEntry? entryLegacy =
            // Act
            CodeOwnersFile.FindOwnersForClosestMatch(
                codeownersEntries,
                testCase.TargetPath,
                useNewFindOwnersForClosestMatchImpl: useNewImpl);

        Assert.That(entryLegacy.Owners.Count, Is.EqualTo(expectedMatch ? 1 : 0));
    }

    // ReSharper disable once NotAccessedPositionalProperty.Global
    //   Reason: Name is present to make it easier to refer to and distinguish test cases in VS test runner.
    public record TestCase(string Name, string TargetPath, string CodeownersPath, bool ExpectedLegacyMatch, bool ExpectedNewMatch);
}
