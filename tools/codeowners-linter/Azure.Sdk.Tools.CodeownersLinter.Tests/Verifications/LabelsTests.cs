using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Sdk.Tools.CodeownersLinter.Constants;
using Azure.Sdk.Tools.CodeownersLinter.Utils;
using Azure.Sdk.Tools.CodeownersLinter.Verifications;
using Azure.Sdk.Tools.CodeOwnersParser.Constants;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersLinter.Tests.Verifications
{
    /// <summary>
    /// Tests for label parsing and verification. LabelsTests requires a RepoLabelDataUtils with populated RepoLabelHolder 
    /// </summary>
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]

    public class LabelsTests
    {
        private RepoLabelDataUtils _repoLabelDataUtils;

        [OneTimeSetUp]
        public void InitRepoLabelData()
        {
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
        /// Test the Labels.ParseLabelsFromLine parser. The Moniker should be irrelevant since the parser should 
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
            var parsedLabelsList = Labels.ParseLabelsFromLine(line);
            if (!TestHelpers.ListsAreEqual(parsedLabelsList, expectedLabelsList))
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
        /// Given a CODEOWNERS line with monikers that expect labels, parse the labels and
        /// verify that the labels exist in the repository.
        /// </summary>
        /// <param name="line">The CODEOWNERS line to parse. Note that service labels with and without % seperator </param>
        /// <param name="expectedErrorMessages">Expected error messages, if the line contains errors</param>
        [Category("Labels")]
        [Category("Verification")]
        // PRLabel and Service Label with no errors with {SeparatorConstants.Label} before the label(s)
        [TestCase($"# {MonikerConstants.PRLabel}: %{TestHelpers.TestLabelNamePartial}0",
                       MonikerConstants.PRLabel)]
        [TestCase($"# {MonikerConstants.ServiceLabel}:\t%{TestHelpers.TestLabelNamePartial}1",
                       MonikerConstants.ServiceLabel)]
        [TestCase($"# {MonikerConstants.ServiceLabel}: %{TestHelpers.TestLabelNamePartial}2\t%{LabelConstants.ServiceAttention}",
                    MonikerConstants.ServiceLabel)]
        // PRLabel and Service Label with no errors without % before the label. This is from the new syntax
        // where everything after the :, trimmed, is treated as the label
        [TestCase($"# {MonikerConstants.PRLabel}:\t{TestHelpers.TestLabelNamePartial}4",
                    MonikerConstants.PRLabel)]
        [TestCase($"# {MonikerConstants.ServiceLabel}: {TestHelpers.TestLabelNamePartial}0",
                    MonikerConstants.ServiceLabel)]
        // PRLabel and Service Label with no label
        [TestCase($"# {MonikerConstants.PRLabel}:",
                  MonikerConstants.PRLabel,
                  $"{ErrorMessageConstants.MissingLabelForMoniker}")]
        [TestCase($"# {MonikerConstants.ServiceLabel}:",
                  MonikerConstants.ServiceLabel,
                  $"{ErrorMessageConstants.MissingLabelForMoniker}")]
        // PRLabel with an invalid label for the repository
        [TestCase($"# {MonikerConstants.PRLabel}: %{TestHelpers.TestLabelNamePartial}567",
                  MonikerConstants.PRLabel,
                  $"'{TestHelpers.TestLabelNamePartial}567'{ErrorMessageConstants.InvalidRepositoryLabelPartial}")]
        // ServiceLabel with an invalid label for the repository
        [TestCase($"# {MonikerConstants.ServiceLabel}: %{TestHelpers.TestLabelNamePartial}567 %{LabelConstants.ServiceAttention}",
                  MonikerConstants.ServiceLabel,
                  $"'{TestHelpers.TestLabelNamePartial}567'{ErrorMessageConstants.InvalidRepositoryLabelPartial}")]
        // Verify that ServiceAttention on a PRLabel is reported as an error
        [TestCase($"# {MonikerConstants.PRLabel}: {LabelConstants.ServiceAttention}",
                  MonikerConstants.PRLabel,
                  ErrorMessageConstants.ServiceAttentionIsNotAValidPRLabel)]
        // Verify that ServiceAttention on a PRLabel along with an invalid label reports
        // the "too many labels", "ServiceAttention is not a valid PRLabel" and that
        // the label is not valid for the repository.
        [TestCase($"# {MonikerConstants.PRLabel}: %{TestHelpers.TestLabelNamePartial}55 %{LabelConstants.ServiceAttention}",
                  MonikerConstants.PRLabel,
                  ErrorMessageConstants.TooManyPRLabels,
                  ErrorMessageConstants.ServiceAttentionIsNotAValidPRLabel,
                  $"'{TestHelpers.TestLabelNamePartial}55'{ErrorMessageConstants.InvalidRepositoryLabelPartial}")]
        // Service Label moniker with more than 2 labels is reported as an error. Labels are still verified and any of those errors are also reported.
        [TestCase($"# {MonikerConstants.ServiceLabel}: %{TestHelpers.TestLabelNamePartial}0 %{TestHelpers.TestLabelNamePartial}55 %{TestHelpers.TestLabelNamePartial}3",
                  MonikerConstants.ServiceLabel,
                  ErrorMessageConstants.TooManyServiceLabels, 
                  $"'{TestHelpers.TestLabelNamePartial}55'{ErrorMessageConstants.InvalidRepositoryLabelPartial}")]
        // Service Label moniker with 2 labels is an error unless one of them is Service Attention
        [TestCase($"# {MonikerConstants.ServiceLabel}: %{TestHelpers.TestLabelNamePartial}3 %{TestHelpers.TestLabelNamePartial}4",
                  MonikerConstants.ServiceLabel,
                  ErrorMessageConstants.TooManyServiceLabels)]
        // Service Label moniker with only ServiceAttention is an error
        [TestCase($"# {MonikerConstants.ServiceLabel}: {LabelConstants.ServiceAttention}",
                  MonikerConstants.ServiceLabel,
                  ErrorMessageConstants.ServiceLabelMustContainAServiceLabel)]
        public void TestVerifyLabels(string line, string moniker, params string[] expectedErrorMessages)
        {
            var expectedErrorList = expectedErrorMessages.ToList();
            List<string> actualErrorList = new List<string>();
            Labels.VerifyLabels(_repoLabelDataUtils, line, moniker, actualErrorList);
            if (!TestHelpers.ListsAreEqual(actualErrorList, expectedErrorList))
            {
                string expectedErrors = "Empty List";
                string actualErrors = "Empty List";
                if (expectedErrorList.Count > 0)
                {
                    expectedErrors = string.Join(Environment.NewLine, expectedErrorList);
                }
                if (actualErrorList.Count > 0)
                {
                    actualErrors = string.Join(Environment.NewLine, actualErrorList);
                }
                Assert.Fail($"VerifyLabels for '{line}' should have returned:\n{expectedErrors}\nbut instead returned\n{actualErrors}");
            }
        }
    }
}
