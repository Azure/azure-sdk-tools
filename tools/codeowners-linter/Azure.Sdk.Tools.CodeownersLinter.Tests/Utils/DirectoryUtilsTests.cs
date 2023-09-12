using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersLinter.Constants;
using Azure.Sdk.Tools.CodeownersLinter.Utils;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersLinter.Tests.Utils
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    /// <summary>
    /// DirectoryUtils tests will not test every function in the DirectoryUtils. The reasons for this are
    /// partially because it requires a repository with a CODEOWNERS to run. Further, IsValidGlobPatternForRepo 
    /// is effectively a wrapper around FileSystemGlobbing's matcher and IsValidRepositoryPath is a wrapper
    /// that just checks Directory.Exists || File.Exists on the passed in source path.
    /// The only function that really needs to be tested is IsValidCodeownersGlobPattern which is checking
    /// the source path for invalid CODEOWNERS patterns.
    /// </summary>
    public class DirectoryUtilsTests
    {
        /// <summary>
        /// Verify that IsValidCodeownersGlobPattern returns true and has no errors with valid glob patterns
        /// </summary>
        /// <param name="glob">Valid glob pattern</param>
        [Category("Utils")]
        [Category("Directory")]
        // These are valid glob patterns taken from various azure-sdk* CODEOWNERS files
        [TestCase("/sdk/**/azure-resourcemanager-*/")]
        [TestCase("/**/android.md")]
        [TestCase("/sdk/azurestack*")]
        public void TestValidCodeownersGlobPatterns(string glob)
        {
            // For the purposes of testing this function, the repoRoot is not necessary.
            DirectoryUtils directoryUtils = new DirectoryUtils();
            List<string> errorStrings = new List<string>();
            bool isValid = directoryUtils.IsValidCodeownersGlobPattern(glob, errorStrings);
            Assert.IsTrue(isValid, $"IsValidCodeownersGlobPattern for '{glob}' should have returned true but did not.");
            Assert.That(errorStrings.Count, Is.EqualTo(0), $"IsValidCodeownersGlobPattern for '{glob}' should not have returned any error strings for a valid pattern but returned {errorStrings.Count}.");
        }

        /// <summary>
        /// Verify that IsValidCodeownersGlobPattern returns false with the expected error string for
        /// invalid glob patterns. Invalid glob patterns for CODEOWNERS are defined in the GitHub docs
        /// https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners#codeowners-syntax
        /// </summary>
        /// <param name="glob">The glob pattern to test. Each pattern returns a specific error.</param>
        /// <param name="expectedPartialError">The expected partial message which will have the glob pattern prepdended.</param>
        [Category("Utils")]
        [Category("Directory")]
        // \#*\#
        [TestCase("\\#*\\#", ErrorMessageConstants.ContainsEscapedPoundPartial)]
        // !.vscode/cspell.json
        [TestCase("!.vscode/cspell.json", ErrorMessageConstants.ContainsNegationPartial)]
        // *.[Rr]e[Ss]harper
        [TestCase("*.[Rr]e[Ss]harper", ErrorMessageConstants.ContainsRangePartial)]
        public void TestInValidCodeownersGlobPatterns(string glob, string expectedPartialError)
        {
            string expectedError = $"{glob}{expectedPartialError}";
            // For the purposes of testing this function, the repoRoot is not necessary.
            DirectoryUtils directoryUtils = new DirectoryUtils();
            List<string> errorStrings = new List<string>();
            bool isValid = directoryUtils.IsValidCodeownersGlobPattern(glob, errorStrings);
            Assert.IsFalse(isValid, $"IsValidCodeownersGlobPattern for '{glob}' should have returned false but did not.");
            Assert.That(errorStrings.Count, Is.EqualTo(1), $"IsValidCodeownersGlobPattern for '{glob}' should have returned a single error string but returned {errorStrings.Count} error strings.");
            Assert.That(errorStrings[0], Is.EqualTo(expectedError), $"IsValidCodeownersGlobPattern for '{glob}' should have returned '{expectedError}' but returned '{errorStrings[0]}'");
        }
    }
}
