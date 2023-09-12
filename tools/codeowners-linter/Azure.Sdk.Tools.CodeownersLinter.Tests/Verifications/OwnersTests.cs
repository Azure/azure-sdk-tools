using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersLinter.Constants;
using Azure.Sdk.Tools.CodeownersLinter.Utils;
using Azure.Sdk.Tools.CodeownersLinter.Verifications;
using Azure.Sdk.Tools.CodeOwnersParser.Constants;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersLinter.Tests.Verifications
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
        /// Test Owners.ParseOwnersFromLine
        /// </summary>
        /// <param name="line">The CODEOWNERS line to parse</param>
        /// <param name="expectedOwners">Expected list of labels to be parsed.</param>
        [Category("Owners")]
        [Category("Parsing")]
        // Case where a moniker has no owners
        [TestCase($"# {MonikerConstants.AzureSdkOwners}:")]
        // Again, using the SeparatorConstant.Owner instead of '@' would be ideal but NUnit won't
        // allow the character constant to be within the string declaration.
        [TestCase($"# {MonikerConstants.ServiceOwners}: @{TestHelpers.TestOwnerNamePartial}0\t@{TestHelpers.TestOwnerNamePartial}4", 
            $"{TestHelpers.TestOwnerNamePartial}0",
            $"{TestHelpers.TestOwnerNamePartial}4")]
        [TestCase($"# {MonikerConstants.ServiceOwners}: @{OrgConstants.Azure}/{TestHelpers.TestOwnerNamePartial}3\t@{TestHelpers.TestOwnerNamePartial}4",
            $"{OrgConstants.Azure}/{TestHelpers.TestOwnerNamePartial}3",
            $"{TestHelpers.TestOwnerNamePartial}4")]
        [TestCase($"#{MonikerConstants.MissingFolder}: @{TestHelpers.TestOwnerNamePartial}1\t@{TestHelpers.TestOwnerNamePartial}2",
            $"{TestHelpers.TestOwnerNamePartial}1",
            $"{TestHelpers.TestOwnerNamePartial}2")]
        public void TestParseOwnersFromLine(string line, params string[] expectedOwners)
        {
            // Convert the array to List
            var expectedOwnersList = expectedOwners.ToList();
            var parsedOwnersList = Owners.ParseOwnersFromLine(line);
            if (!TestHelpers.ListsAreEqual(parsedOwnersList, expectedOwnersList))
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
    }
}
