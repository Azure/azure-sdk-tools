using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersLinter.Constants;
using Azure.Sdk.Tools.CodeownersLinter.Errors;
using Azure.Sdk.Tools.CodeownersLinter.Tests.Mocks;
using Azure.Sdk.Tools.CodeownersLinter.Utils;
using Azure.Sdk.Tools.CodeownersLinter.Verifications;
using Azure.Sdk.Tools.CodeOwnersParser;
using Azure.Sdk.Tools.CodeOwnersParser.Constants;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersLinter.Tests.Verifications
{
    /// <summary>
    /// Tests for CodeownersFormat. These tests require the following:
    /// 1. OwnerDataUtils with populated team/user and user/org visibility data
    /// 2. RepoLabelDataUtils with populated RepoLabelHolder
    /// 3. A mock DirectoryUtils that doesn't actually do directory verification.
    /// </summary>
    public class CodeownersFormatTests
    {
        private OwnerDataUtils _ownerDataUtils;
        private RepoLabelDataUtils _repoLabelDataUtils;
        private DirectoryUtilsMock _directoryUtilsMock;

        [OneTimeSetUp]
        // Initialize a DirectoryUtilsMock, OwnerDataUtils and RepoLabelDataUtils
        public void InitTestData()
        {
            _directoryUtilsMock = new DirectoryUtilsMock();
            _ownerDataUtils = TestHelpers.SetupOwnerData();
            _repoLabelDataUtils = TestHelpers.SetupRepoLabelData();
            // None of the tests will work if the repo/label data doesn't exist.
            // While the previous function call created the test repo/label data,
            // this call just ensures no changes were made to the util that'll
            // mess things up.
            if (!_repoLabelDataUtils.RepoLabelDataExists())
            {
                throw new ArgumentException($"Test repo/label data should have been created for {TestHelpers.TestRepositoryName} but was not.");
            }
        }

        /// <summary>
        /// Tests to verify that IsSourcePathOwnerLine correctly identifies source path/owner lines.
        /// A CODEOWNERS line is considered to be a source path/owner line if the line isn't a comment
        ///  line and isn't blank/whitespace.
        /// </summary>
        /// <param name="line">The line of source to verify</param>
        /// <param name="expectSourcePathOwnerLine">true if the line should verify as a source/path owner line, false otherwise.</param>
        [Category("CodeownersFormat")]
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
            bool isSourcePathOwnerLine = CodeownersFormat.IsSourcePathOwnerLine(line);
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
            int actualEndBlockLineNumber = CodeownersFormat.FindBlockEnd(startBlockLineNumber, codeownersFile);
            Assert.That(actualEndBlockLineNumber, Is.EqualTo(expectedEndBlockLineNumber), $"The expected end line number for {testCodeownerFile} was {expectedEndBlockLineNumber} but the actual end line number was {actualEndBlockLineNumber}");
        }

        /// <summary>
        /// Test the VerifySingleLine function. Note that all the parse and verify functions are tested
        /// in the Labels and Owners tests. Any expected actualErrors are just to ensure that the SingleLineError
        /// created correctly contains all of the strings.
        /// </summary>
        /// <param name="line">The CODEOWNERS line to verify</param>
        /// <param name="isSourcePathOwnerLine">True if the line is a source path/owner line.</param>
        /// <param name="expectOwnersIfMoniker">True if the line is a moniker line and owners are expected. This is the case where certain monikers require owners if the block doesn't end in source path/owner line.</param>
        /// <param name="moniker">The moniker, if the line is a moniker line, null otherwise.</param>
        /// <param name="expectedErrorMessages">Expected error strings on the SingleLineError, if any.</param>
        [Category("CodeownersFormat")]
        [Category("Verification")]
        //**source path/owner line scenarios**
        // valid team and owner
        [TestCase($"/sdk/subDir1/subDir1 @{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}1 @{TestHelpers.TestOwnerNamePartial}2",
                  true,
                  false,
                  null)]
        // malformed team entry (missing the @Azure/)
        [TestCase($"/sdk/subDir1/subDir1 @{TestHelpers.TestTeamNamePartial}1", 
                  true, 
                  false, 
                  null, 
                  $"{TestHelpers.TestTeamNamePartial}1{ErrorMessageConstants.MalformedTeamEntryPartial}")]
        // invalid team, invalid user, non-public user, public user and a valid team
        [TestCase($"/sdk/subDir1/subDir1  @{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}54\t@{TestHelpers.TestOwnerNamePartial}6 @{TestHelpers.TestOwnerNamePartial}2\t@{TestHelpers.TestOwnerNamePartial}3 @Azure/{TestHelpers.TestTeamNamePartial}3", 
                  true, 
                  false, 
                  null,
                  $"{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}54{ErrorMessageConstants.InvalidTeamPartial}",
                  $"{TestHelpers.TestOwnerNamePartial}6{ErrorMessageConstants.InvalidUserPartial}",
                  $"{TestHelpers.TestOwnerNamePartial}3{ErrorMessageConstants.NotAPublicMemberOfAzurePartial}")]
        //**Moniker owner scenarios**
        // valid team and valid user
        [TestCase($"# {MonikerConstants.ServiceOwners}: @{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}4 @{TestHelpers.TestOwnerNamePartial}2", 
                  false, 
                  true, 
                  MonikerConstants.ServiceOwners)]
        [TestCase($"#{MonikerConstants.MissingFolder}: @{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}2 @{TestHelpers.TestOwnerNamePartial}0", 
                  false, 
                  true, 
                  MonikerConstants.ServiceOwners)]
        // invalid team, invalid user, non-public user, public user and a valid team
        [TestCase($"# {MonikerConstants.AzureSdkOwners}: @{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}54\t@{TestHelpers.TestOwnerNamePartial}6 @{TestHelpers.TestOwnerNamePartial}2\t@{TestHelpers.TestOwnerNamePartial}3 @Azure/{TestHelpers.TestTeamNamePartial}3",
                  false, 
                  true, 
                  MonikerConstants.AzureSdkOwners,
                  $"{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}54{ErrorMessageConstants.InvalidTeamPartial}",
                  $"{TestHelpers.TestOwnerNamePartial}6{ErrorMessageConstants.InvalidUserPartial}",
                  $"{TestHelpers.TestOwnerNamePartial}3{ErrorMessageConstants.NotAPublicMemberOfAzurePartial}")]
        //**Moniker label scenarios**
        // valid labels with and without % signs. The old style used % sign as the delimiter but only really required
        // two labels for ServiceLabel entries because of the Service Attention label. The new style just assumes everything
        // after the : (trimmed, of course) is the label
        [TestCase($"# {MonikerConstants.PRLabel}: %{TestHelpers.TestLabelNamePartial}0", 
                  false, 
                  false,
                  MonikerConstants.PRLabel)]
        [TestCase($"# {MonikerConstants.PRLabel}: {TestHelpers.TestLabelNamePartial}2",
                  false,
                  false,
                  MonikerConstants.PRLabel)]
        // ServiceLabel can only have two labels if one of them is ServiceAttention
        [TestCase($"# {MonikerConstants.ServiceLabel}: %{TestHelpers.TestLabelNamePartial}1 %{LabelConstants.ServiceAttention}",
                  false,
                  false,
                  MonikerConstants.ServiceLabel)]
        [TestCase($"# {MonikerConstants.ServiceLabel}: {TestHelpers.TestLabelNamePartial}4",
                  false,
                  false,
                  MonikerConstants.ServiceLabel)]
        // ServiceAttention is not a valid PRLabel
        [TestCase($"# {MonikerConstants.PRLabel}: {LabelConstants.ServiceAttention}",
                  false,
                  false,
                  MonikerConstants.PRLabel,
                  ErrorMessageConstants.ServiceAttentionIsNotAValidPRLabel)]
        // Too many PRLabels
        [TestCase($"# {MonikerConstants.PRLabel}: %{TestHelpers.TestLabelNamePartial}2\t%{TestHelpers.TestLabelNamePartial}3",
                  false,
                  false,
                  MonikerConstants.PRLabel,
                  ErrorMessageConstants.TooManyPRLabels)]
        // Too many PRLabel and one of them is invalid
        [TestCase($"# {MonikerConstants.PRLabel}: %{TestHelpers.TestLabelNamePartial}987 %{TestHelpers.TestLabelNamePartial}4",
                  false,
                  false,
                  MonikerConstants.PRLabel,
                  ErrorMessageConstants.TooManyPRLabels,
                  $"'{TestHelpers.TestLabelNamePartial}987'{ErrorMessageConstants.InvalidRepositoryLabelPartial}")]
        // Too many labels for ServiceLabel. - Two labels and one of them isn't ServiceAttention
        [TestCase($"# {MonikerConstants.ServiceLabel}: %{TestHelpers.TestLabelNamePartial}2\t%{TestHelpers.TestLabelNamePartial}3",
                  false,
                  false,
                  MonikerConstants.ServiceLabel,
                  ErrorMessageConstants.TooManyServiceLabels)]
        // Too many labels for ServiceLabel. Three labels and one of them is ServiceAttention
        [TestCase($"# {MonikerConstants.ServiceLabel}: %{TestHelpers.TestLabelNamePartial}2\t%{TestHelpers.TestLabelNamePartial}3 %{LabelConstants.ServiceAttention}",
                  false,
                  false,
                  MonikerConstants.ServiceLabel,
                  ErrorMessageConstants.TooManyServiceLabels)]
        // Too many labels for ServiceLabel. Three labels and one of them is ServiceAttention and one is invalid
        [TestCase($"# {MonikerConstants.ServiceLabel}: %{TestHelpers.TestLabelNamePartial}2\t%{TestHelpers.TestLabelNamePartial}345 %{LabelConstants.ServiceAttention}",
                  false,
                  false,
                  MonikerConstants.ServiceLabel,
                  ErrorMessageConstants.TooManyServiceLabels,
                  $"'{TestHelpers.TestLabelNamePartial}345'{ErrorMessageConstants.InvalidRepositoryLabelPartial}")]
        // ServiceLabel with only ServiceAttention is an error
        [TestCase($"# {MonikerConstants.ServiceLabel}: %{LabelConstants.ServiceAttention}",
                  false,
                  false,
                  MonikerConstants.ServiceLabel,
                  ErrorMessageConstants.ServiceLabelMustContainAServiceLabel)]
        public void TestVerifySingleLine(string line, 
                                         bool isSourcePathOwnerLine, 
                                         bool expectOwnersIfMoniker,
                                         string moniker,
                                         params string[] expectedErrorMessages)
        {
            // Convert the array to List
            var expectedErrorMessagesList = expectedErrorMessages.ToList();
            // The line number for reporting doesn't matter for the testcases except to
            // check, if there are any actualErrors, that the line number is set correctly.
            int lineNumberForReporting = 42;
            List<BaseError> actualErrors = new List<BaseError>();
            CodeownersFormat.VerifySingleLine(_directoryUtilsMock,
                                              _ownerDataUtils,
                                              _repoLabelDataUtils,
                                              actualErrors,
                                              lineNumberForReporting,
                                              line,
                                              isSourcePathOwnerLine,
                                              expectOwnersIfMoniker,
                                              moniker);
            // Check and see if there were any actualErrors returned and whether or not an error was expected.
            if (expectedErrorMessagesList.Count == 0)
            {
                // The number of actualErrors from VerifySingleLine will always be one SingleLineError
                // with one or more error strings
                if (actualErrors.Count > 0)
                {
                    string actualErrorsString = string.Join(Environment.NewLine, actualErrors[0].Errors);
                    Assert.Fail($"VerifySingleLine for {line} had no expected errors but returned\n{actualErrorsString}");
                }
            }
            // Test expects errors
            else
            {
                if (actualErrors.Count == 0)
                {
                    string expectedErrorsString = string.Join(Environment.NewLine, expectedErrorMessagesList);
                    Assert.Fail($"VerifySingleLine for {line} did not produce the any errors but should have had the following errors\n{expectedErrorsString}");
                }
                else
                {
                    if (!TestHelpers.ListsAreEqual(expectedErrorMessagesList, actualErrors[0].Errors))
                    {
                        string expectedErrorsString = string.Join(Environment.NewLine, expectedErrorMessagesList);
                        string actualErrorsString = string.Join(Environment.NewLine, actualErrors[0].Errors);
                        Assert.Fail($"VerifySingleLine for {line} should have had the following errors\n{expectedErrorsString}\nbut instead had\n{actualErrorsString}");
                    }
                }
            }
        }

        /// <summary>
        /// Test Block Verification. The purpose here isn't to test owners or labels but rather to
        /// test the contents of the block to ensure its completeness. For example, the PRLabel 
        /// moniker requires that the block ends in a source path/owner line. Or a ServiceLabel
        /// must be paired with ServiceOwners, NotInRepo or be part of a block that ends in a
        /// source path/owner line.
        /// </summary>
        /// <param name="testCodeownerFile">The test codeowners file</param>
        /// <param name="startBlockLineNumber">The start line number of the block</param>
        /// <param name="endBlockLineNumber">The end line number of the block</param>
        /// <param name="expectedErrorMessages">Expected error messages, if any</param>
        [Category("CodeownersFormat")]
        [Category("Verification")]
        // Success cases, these files shouldn't produce any errors
        [TestCase("CodeownersTestFiles/VerifyBlock/SingleSourcePathLine", 1, 1)]
        [TestCase("CodeownersTestFiles/VerifyBlock/PRLabelAndSourcePath", 1, 2)]
        [TestCase("CodeownersTestFiles/VerifyBlock/ServiceLabelAndSourcePath", 1, 2)]
        [TestCase("CodeownersTestFiles/VerifyBlock/ServiceLabelAndMissingPath", 1, 2)]
        [TestCase("CodeownersTestFiles/VerifyBlock/ServiceLabelAndServiceOwners", 1, 2)]
        [TestCase("CodeownersTestFiles/VerifyBlock/MonikersEndsInSourcePath", 1, 4)]
        // Monikers that need to be in a block that ends in a source/path
        [TestCase("CodeownersTestFiles/VerifyBlock/MonikersMissingSourcePath", 1, 1,
                  $"{MonikerConstants.PRLabel}{ErrorMessageConstants.NeedsToEndWithSourceOwnerPartial}")]
        [TestCase("CodeownersTestFiles/VerifyBlock/MonikersMissingSourcePath", 5, 5,
                  $"{MonikerConstants.PRLabel}{ErrorMessageConstants.NeedsToEndWithSourceOwnerPartial}")]
        [TestCase("CodeownersTestFiles/VerifyBlock/MonikersMissingSourcePath", 7, 7,
                  $"{MonikerConstants.PRLabel}{ErrorMessageConstants.NeedsToEndWithSourceOwnerPartial}")]
        [TestCase("CodeownersTestFiles/VerifyBlock/MonikersMissingSourcePath", 11, 11,
                  $"{MonikerConstants.AzureSdkOwners}{ErrorMessageConstants.NeedsToEndWithSourceOwnerPartial}")]
        [TestCase("CodeownersTestFiles/VerifyBlock/MonikersMissingSourcePath", 15, 15,
                  $"{MonikerConstants.AzureSdkOwners}{ErrorMessageConstants.NeedsToEndWithSourceOwnerPartial}")]
        [TestCase("CodeownersTestFiles/VerifyBlock/MonikersMissingSourcePath", 17, 17,
                  $"{MonikerConstants.AzureSdkOwners}{ErrorMessageConstants.NeedsToEndWithSourceOwnerPartial}")]
        [TestCase("CodeownersTestFiles/VerifyBlock/MonikersMissingSourcePath", 21, 21,
                  ErrorMessageConstants.ServiceLabelNeedsOwners)]
        [TestCase("CodeownersTestFiles/VerifyBlock/MonikersMissingSourcePath", 25, 25,
                  ErrorMessageConstants.ServiceLabelNeedsOwners)]
        [TestCase("CodeownersTestFiles/VerifyBlock/MonikersMissingSourcePath", 27, 27,
                  ErrorMessageConstants.ServiceLabelNeedsOwners)]
        // Duplicate Moniker Errors
        [TestCase("CodeownersTestFiles/VerifyBlock/DuplicateMonikers", 3, 5,
                  $"{MonikerConstants.PRLabel}{ErrorMessageConstants.DuplicateMonikerInBlockPartial}")]
        [TestCase("CodeownersTestFiles/VerifyBlock/DuplicateMonikers", 8, 10,
                  $"{MonikerConstants.AzureSdkOwners}{ErrorMessageConstants.DuplicateMonikerInBlockPartial}")]
        [TestCase("CodeownersTestFiles/VerifyBlock/DuplicateMonikers", 13, 15,
                  $"{MonikerConstants.ServiceLabel}{ErrorMessageConstants.DuplicateMonikerInBlockPartial}")]
        [TestCase("CodeownersTestFiles/VerifyBlock/DuplicateMonikers", 18, 20,
                  $"{MonikerConstants.ServiceLabel}{ErrorMessageConstants.DuplicateMonikerInBlockPartial}")]
        [TestCase("CodeownersTestFiles/VerifyBlock/DuplicateMonikers", 23, 25,
                  $"{MonikerConstants.ServiceLabel}{ErrorMessageConstants.DuplicateMonikerInBlockPartial}")]
        [TestCase("CodeownersTestFiles/VerifyBlock/DuplicateMonikers", 28, 30,
                  $"{MonikerConstants.MissingFolder}{ErrorMessageConstants.DuplicateMonikerInBlockPartial}")]
        [TestCase("CodeownersTestFiles/VerifyBlock/DuplicateMonikers", 33, 35,
                  $"{MonikerConstants.ServiceOwners}{ErrorMessageConstants.DuplicateMonikerInBlockPartial}")]
        // ServiceLabel ends in source path/owner line with ServiceOwners or MissingPath, /<NotInRepo>/, in the same block
        [TestCase("CodeownersTestFiles/VerifyBlock/ServiceLabelTooManyOwnersAndMonikers", 2, 4,
                  ErrorMessageConstants.ServiceLabelHasTooManyOwners)]
        [TestCase("CodeownersTestFiles/VerifyBlock/ServiceLabelTooManyOwnersAndMonikers", 8, 10,
                  ErrorMessageConstants.ServiceLabelHasTooManyOwners)]
        // ServiceLabel is part of a block that has both ServiceOwners and MissingPath, /<NotInRepo>/.
        [TestCase("CodeownersTestFiles/VerifyBlock/ServiceLabelTooManyOwnersAndMonikers", 13, 15,
                  ErrorMessageConstants.ServiceLabelHasTooManyOwnerMonikers)]
        public void TestVerifyBlock(string testCodeownerFile, 
                                    int startBlockLineNumber, 
                                    int endBlockLineNumber, 
                                    params string[] expectedErrorMessages)
        {
            // Convert the array to List
            var expectedErrorMessagesList = expectedErrorMessages.ToList();
            // Load the codeowners file
            List<string> codeownersFile = FileHelpers.LoadFileAsStringList(testCodeownerFile);
            List<BaseError> returnedErrors = new List<BaseError>();
            CodeownersFormat.VerifyBlock(_directoryUtilsMock,
                                         _ownerDataUtils,
                                         _repoLabelDataUtils,
                                         returnedErrors,
                                         startBlockLineNumber,
                                         endBlockLineNumber,
                                         codeownersFile);

            // Ensure that the actual error list only contains BlockFormatting errors. For example,
            // an AzureSdkOwners needs to be in a block that ends in a source/path owner line whether
            // or not it has owners defined but, without owners defined and not being part of a block
            // which ends in a source path/owner line, it'll cause a SingleLineError (for no owners) as
            // well as the BlockFormattingError. The SingleLineErrors are checked elsewhere and only
            // BlockFormattingErrors matter for these testcases.
            var blockFormattingErrors = returnedErrors.OfType<BlockFormattingError>().ToList();

            // Check and see if there were any actualErrors returned and whether or not an error was expected.
            if (expectedErrorMessagesList.Count == 0)
            {
                // The number of actualErrors from VerifySingleLine will always be one SingleLineError
                // with one or more error strings
                if (blockFormattingErrors.Count > 0)
                {
                    string actualErrorsString = string.Join(Environment.NewLine, blockFormattingErrors[0].Errors);
                    Assert.Fail($"VerifyBlock for {testCodeownerFile}, start/end lines {startBlockLineNumber}/{endBlockLineNumber}, had no expected errors but returned\n{actualErrorsString}");
                }
            }
            // Test expects errors
            else
            {
                if (blockFormattingErrors.Count == 0)
                {
                    string expectedErrorsString = string.Join(Environment.NewLine, expectedErrorMessagesList);
                    Assert.Fail($"VerifyBlock for {testCodeownerFile}, start/end lines {startBlockLineNumber}/{endBlockLineNumber}, did not produce the any errors but should have had the following errors\n{expectedErrorsString}");
                }
                else
                {
                    if (!TestHelpers.ListsAreEqual(expectedErrorMessagesList, blockFormattingErrors[0].Errors))
                    {
                        string expectedErrorsString = string.Join(Environment.NewLine, expectedErrorMessagesList);
                        string actualErrorsString = string.Join(Environment.NewLine, blockFormattingErrors[0].Errors);
                        Assert.Fail($"VerifyBlock for {testCodeownerFile}, start/end lines {startBlockLineNumber}/{endBlockLineNumber}, should have had the following errors\n{expectedErrorsString}\nbut instead had\n{actualErrorsString}");
                    }
                }
            }
        }

        /// <summary>
        /// End to end tests. All the individual pieces parts have all been tested, these
        /// are just the end to end pieces. The test(s) with errors are going to use an
        /// existing baseline file for verification if errors are expected. Additionally,
        /// they'll also generate a baseline file re-verify with that.
        /// </summary>
        /// <param name="testCodeownerFile">The file that contains the CODEOWNERS data for a given scenario.</param>
        /// <param name="testBaselineFile">The file that contains the expected baseline errors for a given scenario.</param>
        [TestCase("CodeownersTestFiles/EndToEnd/NoErrors", null)]
        [TestCase("CodeownersTestFiles/EndToEnd/WithErrors", "CodeownersTestFiles/EndToEnd/WithErrors_baseline.txt")]
        public void TestLintCodeownersFile(string testCodeownerFile,
                                           string testBaselineFile)
        {
            List<BaseError> actualErrors = CodeownersFormat.LintCodeownersFile(_directoryUtilsMock,
                                                                               _ownerDataUtils,
                                                                               _repoLabelDataUtils,
                                                                               testCodeownerFile);
            // If errors weren't expected...
            if (testBaselineFile == null)
            {
                // ...but were produced
                if (actualErrors.Count > 0)
                {
                    string errorString = TestHelpers.FormatErrorMessageFromErrorList(actualErrors);
                    Assert.Fail($"LintCodeownersFile for {testCodeownerFile} should not have produced any errors but had {actualErrors.Count} errors.\nErrors:\n{errorString}");
                }
            }
            // If errors are expected...
            else
            {
                // ...but none were produced
                if (actualErrors.Count == 0)
                {
                    Assert.Fail($"LintCodeownersFile for {testCodeownerFile} expected errors, testBaselineFile={testBaselineFile}, but did not produce any.");
                }
                else
                {
                    // Last but not least, make sure the errors produced match what is expected. To do this, the baseline file
                    // and filtering will be used.
                    BaselineUtils baselineUtils = new BaselineUtils(testBaselineFile);
                    var filteredErrors = baselineUtils.FilterErrorsUsingBaseline(actualErrors);
                    // The filter file contains all of the expected errors. After filtering, the list of filtered
                    // errors should be empty.
                    if (filteredErrors.Count > 0)
                    {
                        string errorString = TestHelpers.FormatErrorMessageFromErrorList(filteredErrors);
                        Assert.Fail($"LintCodeownersFile for {testCodeownerFile} expected errors, testBaselineFile={testBaselineFile} which should have filtered all expected errors but filtering returned {filteredErrors.Count} errors.\nUnfiltered Errors:\n{errorString}.");
                    }
                }
            }
        }
    }
}
