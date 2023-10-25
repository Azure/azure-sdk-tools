using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using Azure.Sdk.Tools.CodeownersUtils.Verification;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersUtils.Tests.Utils
{
    /// <summary>
    /// OwnerDataUtilsTests requires a RepoLabelDataUtils with populated RepoLabelCache 
    /// </summary>
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class RepoLabelDataUtilsTests
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
        /// Test whether or not the label exists in the repo/label data. The lookup
        /// for both the label and the repository need to be setup case insensitive
        /// since GitHub is case insensitive but case preserving.
        /// </summary>
        /// <param name="label">The label to test.</param>
        /// <param name="expectedResult">True if the label should exist, false otherwise.</param>
        [Category("Utils")]
        [Category("Labels")]
        [TestCase($"{TestHelpers.TestLabelNamePartial}0", true)]
        [TestCase($"{TestHelpers.TestLabelNamePartial}4", true)]
        [TestCase($"{TestHelpers.TestOwnerNamePartial}5", false)]
        public void TestLabelInRepo(string label, bool expectedResult)
        {
            // Test owner lookup with preserved case owner
            bool isLabelInRepo = _repoLabelDataUtils.LabelInRepo(label);
            Assert.That(isLabelInRepo, Is.EqualTo(expectedResult), $"LabelInRepo for {label} should have returned {expectedResult} and did not");

            // Test owner lookup with uppercase owner
            string labelUpper = label.ToUpper();
            isLabelInRepo = _repoLabelDataUtils.LabelInRepo(labelUpper);
            Assert.That(isLabelInRepo, Is.EqualTo(expectedResult), $"LabelInRepo for {labelUpper} should have returned {expectedResult} and did not");

            // Test owner lookup with lowercase owner
            string labelLower = label.ToLower();
            isLabelInRepo = _repoLabelDataUtils.LabelInRepo(labelLower);
            Assert.That(isLabelInRepo, Is.EqualTo(expectedResult), $"LabelInRepo for {labelLower} should have returned {expectedResult} and did not");
        }
    }
}
