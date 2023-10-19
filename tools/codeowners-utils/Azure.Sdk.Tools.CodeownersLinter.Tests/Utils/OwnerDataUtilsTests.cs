using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Caches;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersUtils.Tests.Utils
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    /// <summary>
    /// OwnerDataUtilsTests requires an OwnerDataUtils with both TeamUserCache and UserOrgVisibilityCache pre-populated
    /// with test data.
    /// </summary>
    public class OwnerDataUtilsTests
    {
        private OwnerDataUtils _ownerDataUtils;

        [OneTimeSetUp]
        public void InitOwnerData()
        {
            _ownerDataUtils = TestHelpers.SetupOwnerData();
        }

        /// <summary>
        /// This is testing the OwnerDataUtil's IsWriteOwner function. In GitHub, the
        /// ownerName is case insensitive but case preserving which means that an owner's
        /// login could be OwnerName but CODEOWNERS file can have ownerName or ownername
        /// or OwnerName and they're all the same owner. This means that the unlying struture
        /// holding the owners, Dictionary&ltstring, bool%gt, needs to be created with
        /// a case insensitive comparison.
        /// </summary>
        /// <param name="owner">The owner to check</param>
        /// <param name="expectedResult">True if the owner being tested should be found in the write owners.</param>
        [Category("Utils")]
        [Category("Owner")]
        [TestCase($"{TestHelpers.TestOwnerNamePartial}0", true)]
        [TestCase($"{TestHelpers.TestOwnerNamePartial}1", true)]
        [TestCase($"{TestHelpers.TestOwnerNamePartial}5", false)]
        public void TestIsWriteOwner(string owner, bool expectedResult)
        {
            // Test owner lookup with preserved case owner
            bool isWriteOwner = _ownerDataUtils.IsWriteOwner(owner);
            Assert.That(isWriteOwner, Is.EqualTo(expectedResult), $"IsWriteOwner for {owner} should have returned {expectedResult} and did not");

            // Test owner lookup with uppercase owner
            string ownerUpper = owner.ToUpper();
            isWriteOwner = _ownerDataUtils.IsWriteOwner(ownerUpper);
            Assert.That(isWriteOwner, Is.EqualTo(expectedResult), $"IsWriteOwner for {ownerUpper} should have returned {expectedResult} and did not");

            // Test owner lookup with lowercase owner
            string ownerLower = owner.ToLower();
            isWriteOwner = _ownerDataUtils.IsWriteOwner(ownerLower);
            Assert.That(isWriteOwner, Is.EqualTo(expectedResult), $"IsWriteOwner for {ownerLower} should have returned {expectedResult} and did not");
        }

        /// <summary>
        /// This is testing the OwnerDataUtil's IsWriteTeam function. In GitHub, the
        /// team name is case insensitive but case preserving which means that an team
        /// could be TeamName but CODEOWNERS file can have teamName or teamname
        /// or TeamName and they're all the same team. This means that the unlying struture
        /// holding the team/user data, Dictionary&ltstring, List&lt;string&gt;, needs to
        /// be created with a case insensitive comparison.
        /// </summary>
        /// <param name="team">The team name to check</param>
        /// <param name="expectedResult">True if the team being tested should be found in the team/user dictionary.</param>
        [Category("Utils")]
        [Category("Owner")]
        [TestCase($"{TestHelpers.TestTeamNamePartial}2", true)]
        [TestCase($"{TestHelpers.TestTeamNamePartial}4", true)]
        [TestCase($"{TestHelpers.TestTeamNamePartial}6", false)]
        public void TestIsWriteTeam(string team, bool expectedResult)
        {
            // Test team lookup with preserved case team
            bool isWriteTeam = _ownerDataUtils.IsWriteTeam(team);
            Assert.That(isWriteTeam, Is.EqualTo(expectedResult), $"IsWriteTeam for {team} should have returned {expectedResult} and did not");

            // Test team lookup with uppercase team
            string teamUpper = team.ToUpper();
            isWriteTeam = _ownerDataUtils.IsWriteTeam(teamUpper);
            Assert.That(isWriteTeam, Is.EqualTo(expectedResult), $"IsWriteTeam for {teamUpper} should have returned {expectedResult} and did not");

            // Test team lookup with lowercase team
            string teamLower = team.ToLower();
            isWriteTeam = _ownerDataUtils.IsWriteTeam(teamLower);
            Assert.That(isWriteTeam, Is.EqualTo(expectedResult), $"IsWriteTeam for {teamLower} should have returned {expectedResult} and did not");
        }

        /// <summary>
        /// This is testing the OwnerDataUtil's IsPublicAzureMember function. In GitHub, the
        /// ownerName is case insensitive but case preserving which means that an owner's
        /// login could be OwnerName but CODEOWNERS file can have ownerName or ownername
        /// or OwnerName and they're all the same owner. This means that the unlying struture
        /// holding the owners, Dictionary&ltstring, bool%gt, needs to be created with
        /// a case insensitive comparison. If an owner doesn't exist, the call returns false.
        /// </summary>
        /// <param name="owner">The owner to check</param>
        /// <param name="expectedResult">True if the owner should be public.</param>
        [Category("Utils")]
        [Category("Owner")]
        // Even users are public, odd users are not. 
        [TestCase($"{TestHelpers.TestOwnerNamePartial}0", true)]
        [TestCase($"{TestHelpers.TestOwnerNamePartial}3", false)]
        // This user does not exist
        [TestCase($"{TestHelpers.TestOwnerNamePartial}5", false)]
        public void TestIsPublicAzureMember(string owner, bool expectedResult)
        {
            // Test owner lookup with preserved case owner
            bool isPublicAzureMember = _ownerDataUtils.IsPublicAzureMember(owner);
            Assert.That(isPublicAzureMember, Is.EqualTo(expectedResult), $"isPublicAzureMember for {owner} should have returned {expectedResult} and did not");

            // Test owner lookup with uppercase owner
            string ownerUpper = owner.ToUpper();
            isPublicAzureMember = _ownerDataUtils.IsPublicAzureMember(ownerUpper);
            Assert.That(isPublicAzureMember, Is.EqualTo(expectedResult), $"isPublicAzureMember for {ownerUpper} should have returned {expectedResult} and did not");

            // Test owner lookup with lowercase owner
            string ownerLower = owner.ToLower();
            isPublicAzureMember = _ownerDataUtils.IsPublicAzureMember(ownerLower);
            Assert.That(isPublicAzureMember, Is.EqualTo(expectedResult), $"isPublicAzureMember for {ownerLower} should have returned {expectedResult} and did not");
        }
    }
}
