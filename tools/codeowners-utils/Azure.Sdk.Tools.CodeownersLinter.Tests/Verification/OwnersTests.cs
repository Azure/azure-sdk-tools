using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using Azure.Sdk.Tools.CodeownersUtils.Verification;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersUtils.Tests.Verification
{
    /// <summary>
    /// Tests for owners parsing and varification. OwnersTests requires a OwnerDataUtils with populated team/user and user/org visibility data 
    /// </summary>
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]

    public class OwnersTests
    {
        private OwnerDataUtils _ownerDataUtils;

        [OneTimeSetUp]
        public void InitRepoLabelData()
        {
            _ownerDataUtils = TestHelpers.SetupOwnerData();
        }

        /// <summary>
        /// Test Owners.VerifyOwners. It's worth noting that VerifyOwners validates individual owners and teams on a given line and
        /// does not expand teams.
        /// </summary>
        /// <param name="line">The CODEOWNERS line to parse.</param>
        /// <param name="expectOwners">Whether or not owners are expected. Some monikers may or may not have owners if their block ends in a source path/owner line and some source/path owner lines might not have owners.</param>
        /// <param name="expectedErrorMessages">If owners are expected, the expected list of owners to be parsed.</param>
        [Category("SourceOwners")]
        [Category("Verification")]
        // Source path/owner line with no errors
        [TestCase($"/sdk/SomePath @{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}0\t@{TestHelpers.TestOwnerNamePartial}2",true)]
        // Moniker Owner lines with no errors
        [TestCase($"# {MonikerConstants.AzureSdkOwners}: @{TestHelpers.TestOwnerNamePartial}0 @{TestHelpers.TestOwnerNamePartial}4", true)]
        [TestCase($"# {MonikerConstants.ServiceOwners}: @{TestHelpers.TestOwnerNamePartial}4 @{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}1\t\t@{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}3", true)]
        // AzureSdkOwners, with no owner defined is legal for a block that ends in a source path/owner line.
        [TestCase($"# {MonikerConstants.AzureSdkOwners}:", false)]
        // Source path/owner line with no owners should complain
        [TestCase($"/sdk/SomePath", true, ErrorMessageConstants.NoOwnersDefined)]
        // AzureSdkOwners, with no owner defined is not legal if the block doesn't end in a source path/owner line.
        [TestCase($"# {MonikerConstants.AzureSdkOwners}:", true, ErrorMessageConstants.NoOwnersDefined)]
        // At this point whether or not the line is a moniker or source path/owner line is irrelevant.
        // Test each error individually.
        // Invalid team
        [TestCase($"# {MonikerConstants.ServiceOwners}: @{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}12", true,
            $"{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}12{ErrorMessageConstants.InvalidTeamPartial}")]
        // Invalid User
        [TestCase($"# {MonikerConstants.ServiceOwners}: @{TestHelpers.TestOwnerNamePartial}456", true,
            $"{TestHelpers.TestOwnerNamePartial}456{ErrorMessageConstants.InvalidUserPartial}")]
        // Non-public member
        [TestCase($"# {MonikerConstants.ServiceOwners}: @{TestHelpers.TestOwnerNamePartial}1", true,
            $"{TestHelpers.TestOwnerNamePartial}1{ErrorMessageConstants.NotAPublicMemberOfAzurePartial}")]
        // Malformed team entry (missing @Azure/) but team otherwise exists in the azure-sdk-write dictionary
        [TestCase($"/sdk/SomePath @{TestHelpers.TestTeamNamePartial}0", true,
            $"{TestHelpers.TestTeamNamePartial}0{ErrorMessageConstants.MalformedTeamEntryPartial}")]
        // All the owners errors on a single line (except no owners errors)
        [TestCase($"/sdk/SomePath @{TestHelpers.TestTeamNamePartial}0\t@{TestHelpers.TestOwnerNamePartial}1  @{TestHelpers.TestOwnerNamePartial}456\t\t\t@{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}12", 
            true,
            $"{TestHelpers.TestTeamNamePartial}0{ErrorMessageConstants.MalformedTeamEntryPartial}",
            $"{TestHelpers.TestOwnerNamePartial}1{ErrorMessageConstants.NotAPublicMemberOfAzurePartial}",
            $"{TestHelpers.TestOwnerNamePartial}456{ErrorMessageConstants.InvalidUserPartial}",
            $"{OrgConstants.Azure}/{TestHelpers.TestTeamNamePartial}12{ErrorMessageConstants.InvalidTeamPartial}")]
        public void TestVerifyOwners(string line, bool expectOwners, params string[] expectedErrorMessages)
        {
            // Convert the array to List
            var expectedErrorList = expectedErrorMessages.ToList();
            List<string> actualErrorList = new List<string>();
            Owners.VerifyOwners(_ownerDataUtils, line, expectOwners, actualErrorList);
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
                Assert.Fail($"VerifyOwners for '{line}' should have returned:\n{expectedErrors}\nbut instead returned\n{actualErrors}");
            }
        }
    }
}
