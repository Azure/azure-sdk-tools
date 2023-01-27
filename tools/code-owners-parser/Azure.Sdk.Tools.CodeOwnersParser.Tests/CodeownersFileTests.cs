using System.Collections.Generic;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeOwnersParser.Tests;

[TestFixture]
public class CodeownersFileTests
{
    /// <summary>
    /// A battery of test cases specifying behavior of new logic matching target
    /// path to CODEOWNERS entries, and comparing it to existing, legacy logic.
    ///
    /// The logic that has changed is located in CodeownersFile.GetMatchingCodeownersEntry.
    ///
    /// In the test case table below, any discrepancy between legacy and new
    /// matcher expected matches that doesn't pertain to wildcard matching denotes
    /// a potential backward compatibility and/or existing defect in the legacy matcher.
    ///
    /// For further details, please see:
    /// - Class comment for Azure.Sdk.Tools.CodeOwnersParser.MatchedCodeOwnerEntry
    /// - https://github.com/Azure/azure-sdk-tools/issues/2770
    /// - https://github.com/Azure/azure-sdk-tools/issues/4859
    /// </summary>
    private static readonly TestCase[] testCases =
    {
        // @formatter:off
        //    Path:               Expected match:
        //    Codeowners , Target        , Legacy , New
        new(        "/"  , "a"           ,   true , true  ),
        new(        "/"  , "A"           ,   true , true  ),
        new(        "/"  , "/a"          ,   true , true  ),
        new(        "/"  , "a/"          ,   true , true  ),
        new(        "/"  , "/a/"         ,   true , true  ),
        new(        "/"  , "/a/b"        ,   true , true  ),
        new(        "/a" , "a"           ,   true , true ),
        new(        "/a" , "A"           ,   true , false ),
        new(        "/a" , "/a"          ,   true , true  ),
        new(        "/a" , "a/"          ,   true , false ),
        new(        "/a" , "/a/"         ,   true , false ),
        new(        "/a" , "/a/b"        ,   true , false ),
        new(        "/a" , "/a/b/"       ,   true , false ),
        new(        "/a" , "/a\\ b"      ,   true , false ),
        new(        "/a" , "/x/a/b"      ,  false , false ),
        new(         "a" , "a"           ,   true , true  ),
        new(         "a" , "A"           ,   true , false ),
        new(         "a" , "/a"          ,   true , true  ),
        new(         "a" , "a/"          ,   true , false ),
        new(         "a" , "/a/"         ,   true , false ),
        new(         "a" , "/a/b"        ,   true , false ),
        new(         "a" , "/a/b/"       ,   true , false ),
        new(         "a" , "/x/a/b"      ,  false , false ),
        new(       "/a/" , "a"           ,   true , false ),
        new(       "/a/" , "/a"          ,   true , false ),
        new(       "/a/" , "a/"          ,   true , true  ),
        new(       "/a/" , "/a/"         ,   true , true  ),
        new(       "/a/" , "/a/b"        ,   true , true  ),
        new(       "/a/" , "/a\\ b"      ,   true , false ),
        new(       "/a/" , "/a\\ b/"     ,   true , false ),
        new(       "/a/" , "/a/a\\ b/"   ,   true , true  ),
        new(       "/a/" , "/a/b/"       ,   true , true  ),
        new(       "/a/" , "/A/b/"       ,   true , false ),
        new(       "/a/" , "/x/a/b"      ,  false , false ),
        new(     "/a/b/" , "/a"          ,  false , false ),
        new(     "/a/b/" , "/a/"         ,  false , false ),
        new(     "/a/b/" , "/a/b"        ,   true , false ),
        new(     "/a/b/" , "/a/b/"       ,   true , true  ),
        new(     "/a/b/" , "/a/b/c"      ,   true , true  ),
        new(     "/a/b/" , "/a/b/c/"     ,   true , true  ),
        new(     "/a/b/" , "/a/b/c/d"    ,   true , true  ),
        new(      "/a/b" , "/a"          ,  false , false ),
        new(      "/a/b" , "/a/"         ,  false , false ),
        new(      "/a/b" , "/a/b"        ,   true , true  ),
        new(      "/a/b" , "/a/b/"       ,   true , false ),
        new(      "/a/b" , "/a/b/c"      ,   true , false ),
        new(      "/a/b" , "/a/b/c/"     ,   true , false ),
        new(      "/a/b" , "/a/b/c/d"    ,   true , false ),
        new(       "/!a" , "!a"          ,   true , false ),
        new(       "/!a" , "b"           ,  false , false ),
        new(      "/a[b" , "a[b"         ,   true , false ),
        new(      "/a]b" , "a]b"         ,   true , false ),
        new(      "/a?b" , "a?b"         ,   true , false ),
        new(      "/a?b" , "axb"         ,  false , false ),
        new(        "/a" , "*"           ,  false , false ),
        new(        "/*" , "*"           ,   true , false ),
        new(        "/*" , "a"           ,  false , true  ),
        new(        "/*" , "a/"          ,  false , false ),
        new(        "/*" , "a/b"         ,  false , false ),
        new(        "/*" , "["           ,  false , true  ),
        new(        "/*" , "]"           ,  false , true  ),
        new(        "/*" , "!"           ,  false , true  ),
        new(       "/**" , "a"           ,  false , false ),
        new(       "/**" , "a/"          ,  false , false ),
        new(       "/**" , "a/b"         ,  false , false ),
        new(       "/**" , "["           ,  false , false ),
        new(       "/**" , "]"           ,  false , false ),
        new(       "/**" , "!"           ,  false , false ),
        new(     "/a/**" , "a"           ,  false , false ),
        new(     "/*/**" , "a"           ,  false , false ),
        new(     "/*/**" , "a/"          ,  false , false ),
        new(     "/*/**" , "a/b"         ,  false , false ),
        new(       "/*/" , "a"           ,  false , false ),
        new(       "/*/" , "a/"          ,  false , true  ),
        new(      "/*/b" , "a/b"         ,  false , true  ),
        new(     "/**/a" , "a"           ,  false , true  ),
        new(     "/**/a" , "a"           ,  false , true  ),
        new(     "/**/a" , "x/ba"        ,  false , false ),
        new(     "/**/a" , "x/ba"        ,  false , false ),
        new(      "/a/*" , "a"           ,  false , false ),
        new(      "/a/*" , "a/"          ,  false , true  ),
        new(      "/a/*" , "a/b"         ,  false , true  ),
        new(      "/a/*" , "a/b/"        ,  false , false ),
        new(      "/a/*" , "a/b/c"       ,  false , false ),
        new(     "/a/*/" , "a"           ,  false , false ),
        new(     "/a/*/" , "a/"          ,  false , false ),
        new(     "/a/*/" , "a/b"         ,  false , false ),
        new(     "/a/*/" , "a/b/"        ,  false , true  ),
        new(     "/a/*/" , "a/b/c"       ,  false , true  ),
        new(     "/a/**" , "a"           ,  false , false ),
        new(     "/a/**" , "a/"          ,  false , false ),
        new(     "/a/**" , "a/b"         ,  false , false ),
        new(     "/a/**" , "a/b/"        ,  false , false ),
        new(     "/a/**" , "a/b/c"       ,  false , false ),
        new(    "/a/**/" , "a"           ,  false , false ),
        new(    "/a/**/" , "a/"          ,  false , false ),
        new(    "/a/**/" , "a/b"         ,  false , false ),
        new(    "/a/**/" , "a/b/"        ,  false , false ),
        new(    "/a/**/" , "a/b/c"       ,  false , false ),
        new(    "/**/a/" , "a"           ,  false , false ),
        new(    "/**/a/" , "a/"          ,  false , true  ),
        new(    "/**/a/" , "a/b"         ,  false , true  ),
        new(    "/**/b/" , "a/b"         ,  false , false ),
        new(    "/**/b/" , "a/b/"        ,  false , true  ),
        new(    "/**/b/" , "a/c/"        ,  false , false ),
        new(   "/a/*/b/" , "a/b/"        ,  false , false ),
        new(   "/a/*/b/" , "a/x/b/"      ,  false , true  ),
        new(   "/a/*/b/" , "a/x/b/c"     ,  false , true  ),
        new(   "/a/*/b/" , "a/x/c"       ,  false , false ),
        new(   "/a/*/b/" , "a/x/y/b"     ,  false , false ),
        new(    "/a**b/" , "a/x/y/b"     ,  false , false ),
        new(  "/a/**/b/" , "a/b"         ,  false , false ),
        new(  "/a/**/b/" , "a/b/"        ,  false , true  ),
        new(  "/a/**/b/" , "a/x/b/"      ,  false , true  ),
        new(  "/a/**/b/" , "a/x/y/b/"    ,  false , true  ),
        new(  "/a/**/b/" , "a/x/y/c"     ,  false , false ),
        new(  "/a/**/b/" , "a-b/"        ,  false , false ),
        new(     "a/*/*" , "a/b"         ,  false , false ),
        new(  "/a/*/*/d" , "a/b/c/d"     ,  false , true  ),
        new(  "/a/*/*/d" , "a/b/x/c/d"   ,  false , false ),
        new( "/a/**/*/d" , "a/b/x/c/d"   ,  false , true  ),
        new(     "*/*/b" , "a/b"         ,  false , false ),
        new(      "/a*/" , "abc/"        ,  false , true  ),
        new(      "/a*/" , "ab/c/"       ,  false , true  ),
        new(     "/*b*/" , "axbyc/"      ,  false , true  ),
        new(      "/*c/" , "abc/"        ,  false , true  ),
        new(      "/*c/" , "a/abc/"      ,  false , false ),
        new(     "/a*c/" , "axbyc/"      ,  false , true  ),
        new(     "/a*c/" , "axb/yc/"     ,  false , false ),
        new(  "/**/*x*/" , "a/b/cxy/d"   ,  false , true  ),
        new(   "/a/*.md" , "a/x.md"      ,  false , true  ),
        new( "/*/*/*.md" , "a/b/x.md"    ,  false , true  ),
        new(  "/**/*.md" , "a/b.md/x.md" ,  false , true  ),
        new(   "**/*.md" , "a/b.md/x.md" ,  false , false ),
        new(     "/*.md" , "a/md"        ,  false , false ),
        new(      "/a.*" , "a.b"         ,  false , true  ),
        new(      "/a.*" , "a.b/"        ,  false , false ),
        new(      "/a.*" , "x/a.b/"      ,  false , false ),
        new(     "/a.*/" , "a.b"         ,  false , false ),
        new(     "/a.*/" , "a.b/"        ,  false , true  ),
        new(    "/**/*x*/AB/*/CD" , "a/b/cxy/AB/fff/CD"     , false, true  ),
        new(    "/**/*x*/AB/*/CD" , "a/b/cxy/AB/ff/ff/CD"   , false, false ),
        new( "/**/*x*/AB/**/CD/*" , "a/b/cxy/AB/ff/ff/CD"   , false, false ),
        new( "/**/*x*/AB/**/CD/*" , "a/b/cxy/AB/ff/ff/CD/"  , false, true  ),
        new( "/**/*x*/AB/**/CD/*" , "a/b/cxy/AB/[]/!!/CD/h" , false, true  ),

        // @formatter:on
    };

    /// <summary>
    /// Exercises Azure.Sdk.Tools.CodeOwnersParser.Tests.CodeownersFileTests.testCases.
    /// See comment on that member for details.
    /// </summary>
    [TestCaseSource(nameof(testCases))]
    public void TestGetMatchingCodeownersEntry(TestCase testCase)
    {
        List<CodeownersEntry> codeownersEntries =
            CodeownersFile.GetCodeownersEntries(testCase.CodeownersPath + "@owner");

        VerifyGetMatchingCodeownersEntry(testCase, codeownersEntries, useRegexMatcher: false, testCase.ExpectedLegacyMatch);
        VerifyGetMatchingCodeownersEntry(testCase, codeownersEntries, useRegexMatcher: true, testCase.ExpectedNewMatch);
    }

    private static void VerifyGetMatchingCodeownersEntry(
        TestCase testCase,
        List<CodeownersEntry> codeownersEntries,
        bool useRegexMatcher,
        bool expectedMatch)
    {
        CodeownersEntry entry =
            // Act
            CodeownersFile.GetMatchingCodeownersEntry(testCase.TargetPath,
                codeownersEntries, useRegexMatcher);

        Assert.That(entry.Owners, Has.Count.EqualTo(expectedMatch ? 1 : 0));
    }

    public record TestCase(
        string CodeownersPath,
        string TargetPath,
        bool ExpectedLegacyMatch,
        bool ExpectedNewMatch);
}
