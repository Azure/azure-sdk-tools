using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersUtils.Tests.Utils
{
    public class ParsingUtilsTests
    {
        private OwnerDataUtils _ownerDataUtils;

        [OneTimeSetUp]
        public void InitRepoLabelData()
        {
            _ownerDataUtils = TestHelpers.SetupOwnerData();
        }

        /// <summary>
        /// Test the ParsingUtils.ParseLabelsFromLine parser. The Moniker should be irrelevant since the parser should 
        /// be able to parse the owners regardless. This needs to be able to check the old sytax and the new
        /// syntax. The old syntax required the percent sign before the labels because ServiceLabel required 
        /// the service label AND the Service Attention label because a tool was used to generate the FabricBot
        /// JSon for a rule from these. The new syntax just requires Moniker: Label, where everything after the
        /// colon is the label.
        /// </summary>
        /// <param name="line">CODEOWNERS line to parse</param>
        /// <param name="expectedLabels">Expected list of labels to be parsed. Note, List&lt;string&gt; would be ideal here but NUnit requires a constant expression and won't allow one to be constructed.</param>
        [Category("Labels")]
        [Category("Parsing")]
        // Ensure that parsing doesn't fail if the line doesn't have any labels
        // For the other test, ones with labels, test cases  and ensure that spaces vs tabs don't matter for parsing.
        [TestCase($"# {MonikerConstants.ServiceLabel}:")]
        // The % sign should be {SeparatorConstants.Label} but NUnit isn't allowing the
        // character constant in the string declaration
        [TestCase($"# {MonikerConstants.PRLabel}: %{TestHelpers.TestLabelNamePartial}0",
                  $"{TestHelpers.TestLabelNamePartial}0")]
        [TestCase($"# {MonikerConstants.PRLabel}:\t%{TestHelpers.TestLabelNamePartial}4",
                  $"{TestHelpers.TestLabelNamePartial}4")]
        [TestCase($"# {MonikerConstants.ServiceLabel}: %{TestHelpers.TestLabelNamePartial}1\t%{TestHelpers.TestLabelNamePartial}2",
                  $"{TestHelpers.TestLabelNamePartial}1",
                  $"{TestHelpers.TestLabelNamePartial}2")]
        [TestCase($"# {MonikerConstants.ServiceLabel}:\t%{TestHelpers.TestLabelNamePartial}3 %{TestHelpers.TestLabelNamePartial}4",
                  $"{TestHelpers.TestLabelNamePartial}3",
                  $"{TestHelpers.TestLabelNamePartial}4")]
        // The new syntax doesn't have {SeparatorConstants.Label} before the label, everything after
        // the <Moniker>:
        [TestCase($"# {MonikerConstants.PRLabel}: {TestHelpers.TestLabelNamePartial}1",
                  $"{TestHelpers.TestLabelNamePartial}1")]
        [TestCase($"# {MonikerConstants.ServiceLabel}: \t{TestHelpers.TestLabelNamePartial}2",
                  $"{TestHelpers.TestLabelNamePartial}2")]
        public void TestParseLabelsFromLine(string line, params string[] expectedLabels)
        {
            // Convert the array to List
            var expectedLabelsList = expectedLabels.ToList();
            var parsedLabelsList = ParsingUtils.ParseLabelsFromLine(line);
            if (!TestHelpers.StringListsAreEqual(parsedLabelsList, expectedLabelsList))
            {
                string expectedLabelsForError = "Empty List";
                string parsedLabelsForError = "Empty List";
                if (expectedLabelsList.Count > 0)
                {
                    expectedLabelsForError = string.Join(",", expectedLabelsList);
                }
                if (parsedLabelsList.Count > 0)
                {
                    parsedLabelsForError = string.Join(",", parsedLabelsList);
                }
                Assert.Fail($"ParseLabelsFromLine for '{line}' should have returned {expectedLabelsForError} but instead returned {parsedLabelsForError}");
            }
        }

        /// <summary>
        /// Test ParsingUtils.ParseOwnersFromLine for linter. The linter doesn't expand teams because it needs
        /// to verify the team entries.
        /// </summary>
        /// <param name="line">The CODEOWNERS line to parse</param>
        /// <param name="expectedOwners">Expected list of owners to be parsed which includes team entries.</param>
        [Category("SourceOwners")]
        [Category("Parsing")]
        // source path/owner line with only users
        [TestCase($"/sdk/FakePath1  @{TestHelpers.TestOwnerNamePartial}0 @{TestHelpers.TestOwnerNamePartial}4",
            $"{TestHelpers.TestOwnerNamePartial}0",
            $"{TestHelpers.TestOwnerNamePartial}4")]
        // source path/owner line with users and a team should return the user and the team
        [TestCase($"/sdk/FakePath2  @{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}2\t@{TestHelpers.TestOwnerNamePartial}0",
            $"{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}2",
            $"{TestHelpers.TestOwnerNamePartial}0")]
        // Case where a moniker has no owners
        [TestCase($"# {MonikerConstants.AzureSdkOwners}:")]
        // Again, using the SeparatorConstant.Owner instead of '@' would be ideal but NUnit won't
        // allow the character constant to be within the string declaration.
        [TestCase($"# {MonikerConstants.ServiceOwners}: @{TestHelpers.TestOwnerNamePartial}0\t@{TestHelpers.TestOwnerNamePartial}4",
            $"{TestHelpers.TestOwnerNamePartial}0",
            $"{TestHelpers.TestOwnerNamePartial}4")]
        [TestCase($"# {MonikerConstants.ServiceOwners}: @{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}3\t@{TestHelpers.TestOwnerNamePartial}4",
            $"{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}3",
            $"{TestHelpers.TestOwnerNamePartial}4")]
        [TestCase($"#{MonikerConstants.MissingFolder}: @{TestHelpers.TestOwnerNamePartial}1\t@{TestHelpers.TestOwnerNamePartial}2",
            $"{TestHelpers.TestOwnerNamePartial}1",
            $"{TestHelpers.TestOwnerNamePartial}2")]
        public void TestParseOwnersFromLineForLinter(string line, params string[] expectedOwners)
        {
            // Convert the array to List
            var expectedOwnersList = expectedOwners.ToList();
            var parsedOwnersList = ParsingUtils.ParseOwnersFromLine(_ownerDataUtils, line, false /* linter doesn't expand teams */ );
            if (!TestHelpers.StringListsAreEqual(parsedOwnersList, expectedOwnersList))
            {
                string expectedOwnersForError = "Empty List";
                string parsedOwnersForError = "Empty List";
                if (expectedOwnersList.Count > 0)
                {
                    expectedOwnersForError = string.Join(",", expectedOwnersList);
                }
                if (parsedOwnersList.Count > 0)
                {
                    parsedOwnersForError = string.Join(",", parsedOwnersList);
                }
                Assert.Fail($"ParseOwnersFromLine for '{line}' should have returned {expectedOwnersForError} but instead returned {parsedOwnersForError}");
            }
        }

        /// <summary>
        /// Test ParsingUtils.ParseOwnersFromLine for the parser, which does expand the teams and returns
        /// only the distinct list of users.
        /// </summary>
        /// <param name="line">The CODEOWNERS line to parse</param>
        /// <param name="expectedOwners">Expected list of owners to be parsed which includes owners from expanded teams.</param>
        [Category("SourceOwners")]
        [Category("Parsing")]
        // source path/owner line with only users
        [TestCase($"/sdk/FakePath1  @{TestHelpers.TestOwnerNamePartial}0 @{TestHelpers.TestOwnerNamePartial}4",
            $"{TestHelpers.TestOwnerNamePartial}0",
            $"{TestHelpers.TestOwnerNamePartial}4")]
        // source path/owner line with users and team with no intersection.
        [TestCase($"/sdk/FakePath2  @{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}2\t@{TestHelpers.TestOwnerNamePartial}4",
            $"{TestHelpers.TestOwnerNamePartial}0",
            $"{TestHelpers.TestOwnerNamePartial}1",
            $"{TestHelpers.TestOwnerNamePartial}2",
            $"{TestHelpers.TestOwnerNamePartial}4")]
        // moniker line with users that is also in the team
        [TestCase($"# {MonikerConstants.ServiceOwners}:  @{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}2\t@{TestHelpers.TestOwnerNamePartial}2",
            $"{TestHelpers.TestOwnerNamePartial}0",
            $"{TestHelpers.TestOwnerNamePartial}1",
            $"{TestHelpers.TestOwnerNamePartial}2")]
        // moniker line with only teams that have overlapping users
        [TestCase($"# {MonikerConstants.AzureSdkOwners}:  @{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}2  @{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}4",
            $"{TestHelpers.TestOwnerNamePartial}0",
            $"{TestHelpers.TestOwnerNamePartial}1",
            $"{TestHelpers.TestOwnerNamePartial}2",
            $"{TestHelpers.TestOwnerNamePartial}3",
            $"{TestHelpers.TestOwnerNamePartial}4")]
        // Case where a moniker has no owners
        [TestCase($"# {MonikerConstants.ServiceOwners}:")]
        public void TestParseOwnersFromLineForParser(string line, params string[] expectedOwners)
        {
            // Convert the array to List
            var expectedOwnersList = expectedOwners.ToList();
            var parsedOwnersList = ParsingUtils.ParseOwnersFromLine(_ownerDataUtils, line, true /* parser will always expand teams */ );
            if (!TestHelpers.StringListsAreEqual(parsedOwnersList, expectedOwnersList))
            {
                string expectedOwnersForError = "Empty List";
                string parsedOwnersForError = "Empty List";
                if (expectedOwnersList.Count > 0)
                {
                    expectedOwnersForError = string.Join(",", expectedOwnersList);
                }
                if (parsedOwnersList.Count > 0)
                {
                    parsedOwnersForError = string.Join(",", parsedOwnersList);
                }
                Assert.Fail($"ParseOwnersFromLine for '{line}' should have returned {expectedOwnersForError} but instead returned {parsedOwnersForError}");
            }
        }

        /// <summary>
        /// Test the Parsing.IsMonikerOrSourceLine. Anything that isn't a moniker or source line is effectively
        /// ignored when parsing. 
        /// </summary>
        /// <param name="line">The CODEOWNERS line to test.</param>
        /// <param name="expectMonikerOrSourceLine">Whether or not the line being tested should be detected as a moniker or source line.</param>
        // Source line with owners
        [TestCase("/fakePath1/fakePath2    @fakeOwner1  @fakeOwner2", true)]
        // Source line with no owners
        [TestCase("/fakePath1/fakePath2", true)]
        // Monikers with users or labels
        [TestCase($"# {MonikerConstants.AzureSdkOwners}:          @fakeOwner1 @fakeOwner2", true)]
        [TestCase($"#{MonikerConstants.MissingFolder}     @fakeOwner1 @fakeOwner2", true)]
        [TestCase($"# {MonikerConstants.PRLabel}: %Fake Label", true)]
        [TestCase($"# {MonikerConstants.ServiceLabel}: %Fake Label", true)]
        [TestCase($"# {MonikerConstants.ServiceOwners}:", true)]
        // Moniker only lines, without their owners or labels, still need to be positively identified
        [TestCase($"# {MonikerConstants.AzureSdkOwners}:", true)]
        [TestCase($"#{MonikerConstants.MissingFolder}", true)]
        [TestCase($"# {MonikerConstants.PRLabel}:", true)]
        [TestCase($"# {MonikerConstants.ServiceLabel}:", true)]
        [TestCase($"# {MonikerConstants.ServiceOwners}:", true)]
        // Some random lines that should not be detected as moniker or source lines
        [TestCase("  \t", false)]
        [TestCase("", false)]
        [TestCase("# Just a comment line", false)]
        // If the comment has a moniker in it but doesn't have the end colon, for the monikers that require
        // them, then it shouldn't be detected as a moniker line. Note: This obviously will not work for
        // the MissingFolder moniker which doesn't end with a colon but should for every other moniker.
        [TestCase($"# {MonikerConstants.PRLabel} isn't moniker line (missing colon), but a comment with a moniker in it", false)]
        public void TestIsMonikerOrSourceLine(string line, bool expectMonikerOrSourceLine)
        {
            bool isMonikerOrSourceLine = ParsingUtils.IsMonikerOrSourceLine(line);
            Assert.That(expectMonikerOrSourceLine, Is.EqualTo(isMonikerOrSourceLine), $"IsMonikerOrSourceLine for {line} should have returned {expectMonikerOrSourceLine}");
        }

        /// <summary>
        /// Tests to verify that IsSourcePathOwnerLine correctly identifies source path/owner lines.
        /// A CODEOWNERS line is considered to be a source path/owner line if the line isn't a comment
        ///  line and isn't blank/whitespace.
        /// </summary>
        /// <param name="line">The line of source to verify</param>
        /// <param name="expectSourcePathOwnerLine">true if the line should verify as a source/path owner line, false otherwise.</param>
        [Category("CodeownersLinter")]
        [Category("Verification")]
        // Path with no owners
        [TestCase("/someDir/someSubDir", true)]
        // Path with owners
        [TestCase("/someDir/someSubDir @owner1 @owner2", true)]
        // Blank/Whitespace tests
        [TestCase("", false)]
        [TestCase(" ", false)]
        [TestCase("\t", false)]
        [TestCase("\r", false)]
        [TestCase("\r\n", false)]
        // Comment/Moniker lines
        [TestCase("# it doesn't actually matter, it starts with a comment", false)]
        [TestCase($"#{MonikerConstants.PRLabel}", false)]
        [TestCase($"#{MonikerConstants.MissingFolder}", false)]
        public void TestIsSourcePathOwnerLine(string line, bool expectSourcePathOwnerLine)
        {
            bool isSourcePathOwnerLine = ParsingUtils.IsSourcePathOwnerLine(line);
            // If the line is expected to be a source path/owner line but the function says it isn't
            if (expectSourcePathOwnerLine)
            {
                Assert.That(isSourcePathOwnerLine, Is.True, $"line '{line}' should have verified as a source path/owner line");
            }
            else
            {
                Assert.That(isSourcePathOwnerLine, Is.False, $"line '{line}' should not have verified as a source path/owner line");
            }
        }

        /// <summary>
        /// Test the FindBlockEnd function. There are a couple of things of note here:
        /// 1. Each test scenario should have its own CODEOWNERS file in Tests.FakeCodeowners/FindBlockEndTests.
        ///    This will allow tests to run in parallel and prevent any updates or changes to one test from 
        ///    affectiing every test.
        /// 2. If there are specific notes required for the scenario, they should be comments in the file.
        /// 3. A block must start with a non-blank line and will end in one of the following ways:
        ///    a. A blank line
        ///    b. A source path/owner line
        ///    c. The end of the file
        /// Note: Editing the Codeowners test files in VS shows line numbers as 1 based but the lines
        ///       are actually 0 based for processing.
        /// </summary>
        /// <param name="testCodeownerFile">The test codeowners file</param>
        /// <param name="startBlockLineNumber">The line number of the block start</param>
        /// <param name="expectedEndBlockLineNumber">The expected line end number</param>
        [TestCase("CodeownersTestFiles/FindBlockEnd/SingleSourcePathLine", 0, 0)]
        [TestCase("CodeownersTestFiles/FindBlockEnd/PRLabelAndSourcePath", 1, 2)]
        [TestCase("CodeownersTestFiles/FindBlockEnd/ServiceLabelAndSourcePath", 0, 2)]
        [TestCase("CodeownersTestFiles/FindBlockEnd/ServiceLabelAndMissingFolder", 1, 2)]
        [TestCase("CodeownersTestFiles/FindBlockEnd/AllMonikersEndsWithSourcePath", 1, 5)]
        public void TestFindBlockEnd(string testCodeownerFile, int startBlockLineNumber, int expectedEndBlockLineNumber)
        {
            List<string> codeownersFile = FileHelpers.LoadFileAsStringList(testCodeownerFile);
            int actualEndBlockLineNumber = ParsingUtils.FindBlockEnd(startBlockLineNumber, codeownersFile);
            Assert.That(actualEndBlockLineNumber, Is.EqualTo(expectedEndBlockLineNumber), $"The expected end line number for {testCodeownerFile} was {expectedEndBlockLineNumber} but the actual end line number was {actualEndBlockLineNumber}");
        }
    }
}
