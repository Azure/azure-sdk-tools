using System.Collections.Generic;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeOwnersParser.Tests;

/// <summary>
/// Please see the comment on:
/// - Azure.Sdk.Tools.CodeOwnersParser.Tests.CodeownersFileTests.testCases
/// </summary>
[TestFixture]
public class CodeownersFileTests
{
    /// <summary>
    /// A battery of test cases specifying the behavior of logic matching target
    /// path to CODEOWNERS paths.
    ///
    /// For further details, please see:
    /// - Class comment for Azure.Sdk.Tools.CodeOwnersParser.MatchedCodeOwnerEntry
    /// - Azure.Sdk.Tools.RetrieveCodeOwners.Tests.RetrieveCodeOwnersProgramTests
    /// - https://github.com/Azure/azure-sdk-tools/issues/2770
    /// - https://github.com/Azure/azure-sdk-tools/issues/4859
    /// </summary>
    private static readonly TestCase[] testCases =
    {
        // @formatter:off
        //    Path:          Expected match:
        //    Codeowners , Target        , |
        new(       "/**" , "a"           , true  ),
        new(       "/**" , "A"           , true  ),
        new(       "/**" , "/a"          , true  ),
        new(       "/**" , "a/"          , true  ),
        new(       "/**" , "/a/"         , true  ),
        new(       "/**" , "/a/b"        , true  ),
        new(       "/**" , "/a/b/"       , true  ),
        new(       "/**" , "/a/b/c"      , true  ),
        new(       "/**" , "["           , true  ),
        new(       "/**" , "]"           , true  ),
        new(        "/"  , "a"           , false ),
        new(        "/"  , "A"           , false ),
        new(        "/"  , "/a"          , false ),
        new(        "/"  , "a/"          , false ),
        new(        "/"  , "/a/"         , false ),
        new(        "/"  , "/a/b"        , false ),
        new(        "/a" , "a"           , true  ),
        new(        "/a" , "A"           , false ),
        new(        "/a" , "/a"          , true  ),
        new(        "/a" , "a/"          , false ),
        new(        "/a" , "/a/"         , false ),
        new(        "/a" , "/a/b"        , false ),
        new(        "/a" , "/a/b/"       , false ),
        new(        "/a" , "/a\\ b"      , false ),
        new(        "/a" , "/x/a/b"      , false ),
        new(         "a" , "a"           , false ),
        new(         "a" , "ab"          , false ),
        new(         "a" , "ab/"         , false ),
        new(         "a" , "/ab/"        , false ),
        new(         "a" , "A"           , false ),
        new(         "a" , "/a"          , false ),
        new(         "a" , "a/"          , false ),
        new(         "a" , "/a/"         , false ),
        new(         "a" , "/a/b"        , false ),
        new(         "a" , "/a/b/"       , false ),
        new(         "a" , "/x/a/b"      , false ),
        new(       "/a/" , "a"           , false ),
        new(       "/a/" , "/a"          , false ),
        new(       "/a/" , "a/"          , true  ),
        new(       "/a/" , "/a/"         , true  ),
        new(       "/a/" , "/a/b"        , true  ),
        new(       "/a/" , "/a\\ b"      , false ),
        new(       "/a/" , "/a\\ b/"     , false ),
        new(       "/a/" , "/a/a\\ b/"   , true  ),
        new(       "/a/" , "/a/b/"       , true  ),
        new(       "/a/" , "/A/b/"       , false ),
        new(       "/a/" , "/x/a/b"      , false ),
        new(     "/a/b/" , "/a"          , false ),
        new(     "/a/b/" , "/a/"         , false ),
        new(     "/a/b/" , "/a/b"        , false ),
        new(     "/a/b/" , "/a/b/"       , true  ),
        new(     "/a/b/" , "/a/b/c"      , true  ),
        new(     "/a/b/" , "/a/b/c/"     , true  ),
        new(     "/a/b/" , "/a/b/c/d"    , true  ),
        new(      "/a/b" , "/a"          , false ),
        new(      "/a/b" , "/a/"         , false ),
        new(      "/a/b" , "/a/b"        , true  ),
        new(      "/a/b" , "/a/b/"       , false ),
        new(      "/a/b" , "/a/bc"       , false ),
        new(      "/a/b" , "/a/bc/"      , false ),
        new(      "/a/b" , "/a/b/c"      , false ),
        new(      "/a/b" , "/a/b/c/"     , false ),
        new(      "/a/b" , "/a/b/c/d"    , false ),
        new(       "/!a" , "!a"          , false ),
        new(       "/!a" , "b"           , false ),
        new(      "/a[b" , "a[b"         , false ),
        new(      "/a]b" , "a]b"         , false ),
        new(      "/a?b" , "a?b"         , false ),
        new(      "/a?b" , "axb"         , false ),
        new(        "/a" , "*"           , false ),
        new(        "/*" , "*"           , false ),
        new(        "/*" , "a"           , true  ),
        new(        "/*" , "a/"          , false ),
        new(        "/*" , "/a"          , true  ),
        new(        "/*" , "/a/"         , false ),
        new(        "/*" , "a/b"         , false ),
        new(        "/*" , "/a/b"        , false ),
        new(        "/*" , "["           , true  ),
        new(        "/*" , "]"           , true  ),
        new(        "/*" , "!"           , true  ),
        new(       "/**" , "!"           , true  ),
        new(       "/a*" , "a"           , true  ),
        new(       "/a*" , "a/x"         , true  ),
        new(       "/a*" , "a/x/d"       , true  ),
        new(       "/a*" , "ab"          , true  ),
        new(       "/a*" , "ab/x"        , true  ),
        new(       "/a*" , "ab/x/d"      , true  ),
        new(     "/a/**" , "a"           , false ),
        new(     "/*/**" , "a"           , false ),
        new(     "/*/**" , "a/"          , false ),
        new(     "/*/**" , "a/b"         , false ),
        new(       "/*/" , "a"           , false ),
        new(       "/*/" , "a/"          , true  ),
        new(      "/*/b" , "a/b"         , true  ),
        new(     "/**/a" , "a"           , true  ),
        new(     "/**/a" , "x/ba"        , false ),
        new(      "/a/*" , "a"           , false ),
        new(      "/a/*" , "a/"          , true  ),
        new(      "/a/*" , "a/b"         , true  ),
        new(      "/a/*" , "a/b/"        , false ),
        new(      "/a/*" , "a/b/c"       , false ),
        new(     "/a/*/" , "a"           , false ),
        new(     "/a/*/" , "a/"          , false ),
        new(     "/a/*/" , "a/b"         , false ),
        new(     "/a/*/" , "a/b/"        , true  ),
        new(     "/a/*/" , "a/b/c"       , true  ),
        new(     "/a/**" , "a"           , false ),
        new(     "/a/**" , "a/"          , false ),
        new(     "/a/**" , "a/b"         , false ),
        new(     "/a/**" , "a/b/"        , false ),
        new(     "/a/**" , "a/b/c"       , false ),
        new(    "/a/**/" , "a"           , false ),
        new(    "/a/**/" , "a/"          , false ),
        new(    "/a/**/" , "a/b"         , false ),
        new(    "/a/**/" , "a/b/"        , false ),
        new(    "/a/**/" , "a/b/c"       , false ),
        new(    "/**/a/" , "a"           , false ),
        new(    "/**/a/" , "a/"          , true  ),
        new(    "/**/a/" , "a/b"         , true  ),
        new(    "/**/b/" , "a/b"         , false ),
        new(    "/**/b/" , "a/b/"        , true  ),
        new(    "/**/b/" , "a/c/"        , false ),
        new(   "/a/*/b/" , "a/b/"        , false ),
        new(   "/a/*/b/" , "a/x/b/"      , true  ),
        new(   "/a/*/b/" , "a/x/b/c"     , true  ),
        new(   "/a/*/b/" , "a/x/c"       , false ),
        new(   "/a/*/b/" , "a/x/y/b"     , false ),
        new(    "/a**b/" , "a/x/y/b"     , false ),
        new(  "/a/**/b/" , "a/b"         , false ),
        new(  "/a/**/b/" , "a/b/"        , true  ),
        new(  "/a/**/b/" , "a/x/b/"      , true  ),
        new(  "/a/**/b/" , "a/x/y/b/"    , true  ),
        new(  "/a/**/b/" , "a/x/y/c"     , false ),
        new(  "/a/**/b/" , "a-b/"        , false ),
        new(     "a/*/*" , "a/b"         , false ),
        new(  "/a/*/*/d" , "a/b/c/d"     , true  ),
        new(  "/a/*/*/d" , "a/b/x/c/d"   , false ),
        new( "/a/**/*/d" , "a/b/x/c/d"   , true  ),
        new(     "*/*/b" , "a/b"         , false ),
        new(      "/a*/" , "abc/"        , true  ),
        new(      "/a*/" , "ab/c/"       , true  ),
        new(     "/*b*/" , "axbyc/"      , true  ),
        new(      "/*c/" , "abc/"        , true  ),
        new(      "/*c/" , "a/abc/"      , false ),
        new(     "/a*c/" , "axbyc/"      , true  ),
        new(     "/a*c/" , "axb/yc/"     , false ),
        new(  "/**/*x*/" , "a/b/cxy/d"   , true  ),
        new(   "/a/*.md" , "a/x.md"      , true  ),
        new( "/*/*/*.md" , "a/b/x.md"    , true  ),
        new(  "/**/*.md" , "a/b.md/x.md" , true  ),
        new(   "**/*.md" , "a/b.md/x.md" , false ),
        new(     "/*.md" , "a/md"        , false ),
        new(      "/a.*" , "a.b"         , true  ),
        new(      "/a.*" , "a.b/"        , true  ),
        new(      "/a.*" , "x/a.b/"      , false ),
        new(     "/a.*/" , "a.b"         , false ),
        new(     "/a.*/" , "a.b/"        , true  ),
        new(    "/**/*x*/AB/*/CD" , "a/b/cxy/AB/fff/CD"     , true  ),
        new(    "/**/*x*/AB/*/CD" , "a/b/cxy/AB/ff/ff/CD"   , false ),
        new( "/**/*x*/AB/**/CD/*" , "a/b/cxy/AB/ff/ff/CD"   , false ),
        new( "/**/*x*/AB/**/CD/*" , "a/b/cxy/AB/ff/ff/CD/"  , true  ),
        new( "/**/*x*/AB/**/CD/*" , "a/b/cxy/AB/[]/!!/CD/h" , true  ),

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

        VerifyGetMatchingCodeownersEntry(testCase, codeownersEntries, testCase.ExpectedNewMatch);
    }

    private static void VerifyGetMatchingCodeownersEntry(
        TestCase testCase,
        List<CodeownersEntry> codeownersEntries,
        bool expectedMatch)
    {
        CodeownersEntry entry =
            // Act
            CodeownersFile.GetMatchingCodeownersEntry(testCase.TargetPath,
                codeownersEntries);

        Assert.That(entry.Owners, Has.Count.EqualTo(expectedMatch ? 1 : 0));
    }

    public record TestCase(
        string CodeownersPath,
        string TargetPath,
        bool ExpectedNewMatch);
}
