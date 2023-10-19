using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using Azure.Sdk.Tools.CodeownersUtils.Verification;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersUtils.Tests.Verification
{
    /// <summary>
    /// Tests for label parsing and verification. LabelsTests requires a RepoLabelDataUtils with populated RepoLabelCache 
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
        // Verify that an invalid label and ServiceAttention on a PRLabel moniker reports "ServiceAttention is not a valid PRLabel" and that
        // the label is not valid for the repository.
        [TestCase($"# {MonikerConstants.PRLabel}: %{TestHelpers.TestLabelNamePartial}55 %{LabelConstants.ServiceAttention}",
                  MonikerConstants.PRLabel,
                  ErrorMessageConstants.ServiceAttentionIsNotAValidPRLabel,
                  $"'{TestHelpers.TestLabelNamePartial}55'{ErrorMessageConstants.InvalidRepositoryLabelPartial}")]
        // Service Label moniker with more than 2 labels is reported as an error. Labels are still verified and any of those errors are also reported.
        [TestCase($"# {MonikerConstants.ServiceLabel}: %{TestHelpers.TestLabelNamePartial}0 %{TestHelpers.TestLabelNamePartial}55 %{TestHelpers.TestLabelNamePartial}3",
                  MonikerConstants.ServiceLabel,
                  $"'{TestHelpers.TestLabelNamePartial}55'{ErrorMessageConstants.InvalidRepositoryLabelPartial}")]
        // Service Label moniker with only ServiceAttention is an error
        [TestCase($"# {MonikerConstants.ServiceLabel}: {LabelConstants.ServiceAttention}",
                  MonikerConstants.ServiceLabel,
                  ErrorMessageConstants.ServiceLabelMustContainAServiceLabel)]
        public void TestVerifyLabels(string line, string moniker, params string[] expectedErrorMessages)
        {
            var expectedErrorList = expectedErrorMessages.ToList();
            List<string> actualErrorList = new List<string>();
            Labels.VerifyLabels(_repoLabelDataUtils, line, moniker, actualErrorList);
            if (!TestHelpers.StringListsAreEqual(actualErrorList, expectedErrorList))
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
