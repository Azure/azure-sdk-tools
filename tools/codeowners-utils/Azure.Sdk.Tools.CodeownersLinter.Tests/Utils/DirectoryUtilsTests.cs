using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersUtils.Tests.Utils
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    /// <summary>
    /// DirectoryUtils tests will not test every function in the DirectoryUtils. The reasons for this are
    /// partially because it requires a repository with a CODEOWNERS to run. Further, IsValidGlobPatternForRepo 
    /// is effectively a wrapper around FileSystemGlobbing's matcher and IsValidRepositoryPath is a wrapper
    /// that just checks Directory.Exists || File.Exists on the passed in source path.
    /// The only function that really needs to be tested is IsValidCodeownersPathExpression which is checking
    /// the source path for invalid CODEOWNERS patterns.
    /// </summary>
    public class DirectoryUtilsTests
    {
        /// <summary>
        /// Verify that IsValidCodeownersPathExpression returns true and has no errors with valid glob patterns
        /// </summary>
        /// <param name="glob">Valid glob pattern</param>
        [Category("Utils")]
        [Category("Directory")]
        // These are valid glob patterns taken from various azure-sdk CODEOWNERS files
        [TestCase("/sdk/**/azure-resourcemanager-*/")]
        [TestCase("/**/README.md")]
        [TestCase("/*.md")]
        [TestCase("/SomeDirectory/*")]
        [TestCase("/**")]
        public void TestValidCodeownersGlobPatterns(string glob)
        {
            // For the purposes of testing this function, the repoRoot is not necessary.
            List<string> errorStrings = new List<string>();
            bool isValid = DirectoryUtils.IsValidCodeownersPathExpression(glob, errorStrings);
            Assert.IsTrue(isValid, $"IsValidCodeownersPathExpression for '{glob}' should have returned true but did not.");
            Assert.That(errorStrings.Count, Is.EqualTo(0), $"IsValidCodeownersPathExpression for '{glob}' should not have returned any error strings for a valid pattern but returned {errorStrings.Count}.");
        }

        /// <summary>
        /// Verify that IsValidCodeownersPathExpression returns false with the expected error string for
        /// invalid glob patterns. Invalid glob patterns for CODEOWNERS are defined in the GitHub docs
        /// https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners#codeowners-syntax
        /// While a line can contain multiple errors, the testcases are each setup to target an verify a specific error.
        /// </summary>
        /// <param name="glob">The glob pattern to test. Each pattern returns a specific error.</param>
        /// <param name="expectedError">The expected error message.</param>
        /// <param name="isPartial">Whether or not the error message is partial and needs the glob prepended to match.</param>
        [Category("Utils")]
        [Category("Directory")]
        // Contains invalid characters. Note: everything starts with a slash otherwise it'll give the "must start with a /"
        // error on top of the expected error.
        [TestCase("/\\#*\\#", ErrorMessageConstants.ContainsEscapedPoundPartial)]
        [TestCase("/!.vscode/cspell.json", ErrorMessageConstants.ContainsNegationPartial)]
        [TestCase("/*.[Rr]e[Ss]harper", ErrorMessageConstants.ContainsRangePartial)]
        [TestCase("/s?b", ErrorMessageConstants.ContainsQuestionMarkPartial)]
        // contains invalid sequences
        [TestCase("/", ErrorMessageConstants.PathIsSingleSlash, false)]
        [TestCase("/**/", ErrorMessageConstants.PathIsSingleSlashTwoAsterisksSingleSlash, false)]
        [TestCase("/sdk/foo*", ErrorMessageConstants.GlobCannotEndInWildCardPartial)]
        [TestCase("/sdk/**/", ErrorMessageConstants.GlobCannotEndWithSingleSlashTwoAsterisksSingleSlashPartial)]
        [TestCase("/sdk/**", ErrorMessageConstants.GlobCannotEndWithSingleSlashTwoAsterisksPartial)]
        [TestCase("sdk/whatever/", ErrorMessageConstants.MustStartWithASlashPartial)]
        public void TestInvalidCodeownersGlobPatterns(string glob, string expectedError, bool isPartial=true)
        {
            string expectedErrorMessage = expectedError;
            if (isPartial)
            {
                expectedErrorMessage = $"{glob}{expectedError}";
            }
                
            // For the purposes of testing this function, the repoRoot is not necessary.
            List<string> errorStrings = new List<string>();
            bool isValid = DirectoryUtils.IsValidCodeownersPathExpression(glob, errorStrings);
            Assert.IsFalse(isValid, $"IsValidCodeownersPathExpression for '{glob}' should have returned false but did not.");
            Assert.That(errorStrings.Count, Is.EqualTo(1), $"IsValidCodeownersPathExpression for '{glob}' should have returned a single error string but returned {errorStrings.Count} error strings.");
            Assert.That(errorStrings[0], Is.EqualTo(expectedErrorMessage), $"IsValidCodeownersPathExpression for '{glob}' should have returned '{expectedErrorMessage}' but returned '{errorStrings[0]}'");
        }

        /// <summary>
        /// Test PathExpressionMatchesTargetPath. 
        /// </summary>
        /// <param name="pathExpression"></param>
        /// <param name="targetPath"></param>
        /// <param name="expectMatch"></param>
        [TestCase("/**", "a", true)]
        [TestCase("/**", "A", true)]
        [TestCase("/**", "/a", true)]
        [TestCase("/**", "a/", true)]
        [TestCase("/**", "/a/", true)]
        [TestCase("/**", "/a/b", true)]
        [TestCase("/**", "/a/b/", true)]
        [TestCase("/**", "/a/b/c", true)]
        [TestCase("/**", "[", true)]
        [TestCase("/**", "]", true)]
        [TestCase("/", "a", false)]
        [TestCase("/", "A", false)]
        [TestCase("/", "/a", false)]
        [TestCase("/", "a/", false)]
        [TestCase("/", "/a/", false)]
        [TestCase("/", "/a/b", false)]
        [TestCase("/a", "a", true)]
        [TestCase("/a", "A", false)]
        [TestCase("/a", "/a", true)]
        [TestCase("/a", "a/", false)]
        [TestCase("/a", "/a/", false)]
        [TestCase("/a", "/a/b", false)]
        [TestCase("/a", "/a/b/", false)]
        [TestCase("/a", "/a\\ b", false)]
        [TestCase("/a", "/x/a/b", false)]
        [TestCase("a", "a", false)]
        [TestCase("a", "ab", false)]
        [TestCase("a", "ab/", false)]
        [TestCase("a", "/ab/", false)]
        [TestCase("a", "A", false)]
        [TestCase("a", "/a", false)]
        [TestCase("a", "a/", false)]
        [TestCase("a", "/a/", false)]
        [TestCase("a", "/a/b", false)]
        [TestCase("a", "/a/b/", false)]
        [TestCase("a", "/x/a/b", false)]
        [TestCase("/a/", "a", false)]
        [TestCase("/a/", "/a", false)]
        [TestCase("/a/", "a/", true)]
        [TestCase("/a/", "/a/", true)]
        [TestCase("/a/", "/a/b", true)]
        [TestCase("/a/", "/a/a\\ b/", true)]
        [TestCase("/a/", "/a/b/", true)]
        [TestCase("/a/", "/A/b/", false)]
        [TestCase("/a/", "/x/a/b", false)]
        [TestCase("/a/b/", "/a", false)]
        [TestCase("/a/b/", "/a/", false)]
        [TestCase("/a/b/", "/a/b", false)]
        [TestCase("/a/b/", "/a/b/", true)]
        [TestCase("/a/b/", "/a/b/c", true)]
        [TestCase("/a/b/", "/a/b/c/", true)]
        [TestCase("/a/b/", "/a/b/c/d", true)]
        [TestCase("/a/b", "/a", false)]
        [TestCase("/a/b", "/a/", false)]
        [TestCase("/a/b", "/a/b", true)]
        [TestCase("/a/b", "/a/b/", false)]
        [TestCase("/a/b", "/a/bc", false)]
        [TestCase("/a/b", "/a/bc/", false)]
        [TestCase("/a/b", "/a/b/c", false)]
        [TestCase("/a/b", "/a/b/c/", false)]
        [TestCase("/a/b", "/a/b/c/d", false)]
        [TestCase("/!a", "!a", false)]
        [TestCase("/!a", "b", false)]
        [TestCase("/a[b", "a[b", false)]
        [TestCase("/a]b", "a]b", false)]
        [TestCase("/a?b", "a?b", false)]
        [TestCase("/a?b", "axb", false)]
        [TestCase("/a", "*", false)]
        [TestCase("/*", "*", false)]
        [TestCase("/*", "a", true)]
        [TestCase("/*", "a/", false)]
        [TestCase("/*", "/a", true)]
        [TestCase("/*", "/a/", false)]
        [TestCase("/*", "a/b", false)]
        [TestCase("/*", "/a/b", false)]
        [TestCase("/*", "[", true)]
        [TestCase("/*", "]", true)]
        [TestCase("/*", "!", true)]
        [TestCase("/**", "!", true)]
        [TestCase("/a*", "a/x", false)]
        [TestCase("/a*", "a/x/d", false)]
        [TestCase("/a*.md", "ab.md", true)]
        [TestCase("/a*", "ab/x", false)]
        [TestCase("/a*", "ab/x/d", false)]
        [TestCase("/a/**", "a", false)]
        [TestCase("/*/**", "a", false)]
        [TestCase("/*/**", "a/", false)]
        [TestCase("/*/**", "a/b", false)]
        [TestCase("/*/", "a", false)]
        [TestCase("/*/", "a/", true)]
        [TestCase("/*/b", "a/b", true)]
        [TestCase("/**/a", "a", true)]
        [TestCase("/**/a", "x/ba", false)]
        [TestCase("/a/*", "a", false)]
        [TestCase("/a/*", "a/", true)]
        [TestCase("/a/*", "a/b", true)]
        [TestCase("/a/*", "a/b/", false)]
        [TestCase("/a/*", "a/b/c", false)]
        [TestCase("/a/*/", "a", false)]
        [TestCase("/a/*/", "a/", false)]
        [TestCase("/a/*/", "a/b", false)]
        [TestCase("/a/*/", "a/b/", true)]
        [TestCase("/a/*/", "a/b/c", true)]
        [TestCase("/a/**", "a", false)]
        [TestCase("/a/**", "a/", false)]
        [TestCase("/a/**", "a/b", false)]
        [TestCase("/a/**", "a/b/", false)]
        [TestCase("/a/**", "a/b/c", false)]
        [TestCase("/a/**/", "a", false)]
        [TestCase("/a/**/", "a/", false)]
        [TestCase("/a/**/", "a/b", false)]
        [TestCase("/a/**/", "a/b/", false)]
        [TestCase("/a/**/", "a/b/c", false)]
        [TestCase("/**/a/", "a", false)]
        [TestCase("/**/a/", "a/", true)]
        [TestCase("/**/a/", "a/b", true)]
        [TestCase("/**/b/", "a/b", false)]
        [TestCase("/**/b/", "a/b/", true)]
        [TestCase("/**/b/", "a/c/", false)]
        [TestCase("/a/*/b/", "a/b/", false)]
        [TestCase("/a/*/b/", "a/x/b/", true)]
        [TestCase("/a/*/b/", "a/x/b/c", true)]
        [TestCase("/a/*/b/", "a/x/c", false)]
        [TestCase("/a/*/b/", "a/x/y/b", false)]
        [TestCase("/a**b/", "a/x/y/b", false)]
        [TestCase("/a/**/b/", "a/b", false)]
        [TestCase("/a/**/b/", "a/b/", true)]
        [TestCase("/a/**/b/", "a/x/b/", true)]
        [TestCase("/a/**/b/", "a/x/y/b/", true)]
        [TestCase("/a/**/b/", "a/x/y/c", false)]
        [TestCase("/a/**/b/", "a-b/", false)]
        [TestCase("a/*/*", "a/b", false)]
        [TestCase("/a/*/*/d", "a/b/c/d", true)]
        [TestCase("/a/*/*/d", "a/b/x/c/d", false)]
        [TestCase("/a/**/*/d", "a/b/x/c/d", true)]
        [TestCase("*/*/b", "a/b", false)]
        [TestCase("/a*/", "abc/", true)]
        [TestCase("/a*/", "ab/c/", true)]
        [TestCase("/*b*/", "axbyc/", true)]
        [TestCase("/*c/", "abc/", true)]
        [TestCase("/*c/", "a/abc/", false)]
        [TestCase("/a*c/", "axbyc/", true)]
        [TestCase("/a*c/", "axb/yc/", false)]
        [TestCase("/**/*x*/", "a/b/cxy/d", true)]
        [TestCase("/a/*.md", "a/x.md", true)]
        [TestCase("/*/*/*.md", "a/b/x.md", true)]
        [TestCase("/**/*.md", "a/b.md/x.md", true)]
        [TestCase("**/*.md", "a/b.md/x.md", false)]
        [TestCase("/*.md", "a/md", false)]
        [TestCase("/a.*", "a.b", false)]
        [TestCase("/a.*", "x/a.b/", false)]
        [TestCase("/a.*/", "a.b", false)]
        [TestCase("/a.*/", "a.b/", true)]
        [TestCase("/**/*x*/AB/*/CD", "a/b/cxy/AB/fff/CD", true)]
        [TestCase("/**/*x*/AB/*/CD", "a/b/cxy/AB/ff/ff/CD", false)]
        [TestCase("/**/*x*/AB/**/CD/*", "a/b/cxy/AB/ff/ff/CD", false)]
        [TestCase("/**/*x*/AB/**/CD/*", "a/b/cxy/AB/ff/ff/CD/", true)]
        [TestCase("/**/*x*/AB/**/CD/*", "a/b/cxy/AB/[]/!!/CD/h", true)]
        public void TestPathExpressionMatchesTargetPath(string pathExpression, string targetPath, bool expectMatch)
        {
            bool hasMatch = DirectoryUtils.PathExpressionMatchesTargetPath(pathExpression, targetPath);
            Assert.That(hasMatch, Is.EqualTo(expectMatch), $"The pathExpression, {pathExpression}, for targetPath, {targetPath}, should have returned {expectMatch} and did not.");
        }
    }
}
