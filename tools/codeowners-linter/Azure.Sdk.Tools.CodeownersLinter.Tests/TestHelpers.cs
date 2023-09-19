using System;
using System.Collections.Generic;
using System.IO;
using Azure.Sdk.Tools.CodeownersLinter.Constants;
using Azure.Sdk.Tools.CodeownersLinter.Errors;
using Azure.Sdk.Tools.CodeownersLinter.Holders;
using Azure.Sdk.Tools.CodeownersLinter.Utils;
using Azure.Sdk.Tools.CodeOwnersParser;

namespace Azure.Sdk.Tools.CodeownersLinter.Tests
{
    public static class TestHelpers
    {
        public const string TestRepositoryName = @"Azure\azure-sdk-fake";
        // The partial string will have a number appended to the end of them
        public const string TestLabelNamePartial = "TestLabel";
        public const string TestOwnerNamePartial = "TestOwner";
        public const string TestTeamNamePartial = "TestTeam";
        // This will control the max number of test users, test teams and test labels
        private const int _maxItems = 5;

        /// <summary>
        /// Create the team/user and user/org visibility data for the OwnerDataUtils to be used in testing.
        /// The user/org visibility data will consist of 5 users, TestOwner0..TestOwner4, with only even users
        /// being visible.
        /// The team/user data will consist of 5 teams with the number of users in each team equal to the
        /// team number, TestTeam0...TestTeam4.The users in each team will consist of the same users in 
        /// the user/org data.
        /// </summary>
        /// <returns>Populated OwnerDataUtils</returns>
        public static OwnerDataUtils SetupOwnerData()
        {
            // OwnerDataUtils requires a TeamUserHolder and a UserOrgVisibilityHolder populated with their
            // respective data. Live data really can't be used for this because it can change.
            TeamUserHolder teamUserHolder = new TeamUserHolder(null);
            Dictionary<string, List<string>> teamUserDict = new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);

            // Create 5 teams
            for (int i = 0;i < _maxItems; i++)
            {
                string team = $"{TestTeamNamePartial}{i}";
                List<string> users = new List<string>();
                // Teams will have a number of users equal to their team number with a
                // max of 5. The user/org visibility will have data for each user.
                // Note the stopping condition is j <= i, this is so team 0 has 1 user
                // and team 4 has all five users
                for (int j = 0; j <= i; j++)
                {
                    string user = $"{TestOwnerNamePartial}{j}";
                    users.Add(user);
                }
                teamUserDict.Add(team, users);
            }
            teamUserHolder.TeamUserDict = teamUserDict;

            UserOrgVisibilityHolder userOrgVisibilityHolder = new UserOrgVisibilityHolder(null);
            Dictionary<string, bool> userOrgVisDict = new Dictionary<string, bool>(StringComparer.InvariantCultureIgnoreCase);
            // Make every even user visible
            for (int i = 0;i < 5;i++)
            {
                string user = $"{TestOwnerNamePartial}{i}";
                if (i % 2 == 0)
                {
                    userOrgVisDict.Add(user, true);
                }
                else
                {
                    userOrgVisDict.Add(user, false);
                }
            }
            userOrgVisibilityHolder.UserOrgVisibilityDict = userOrgVisDict;
            return new OwnerDataUtils(teamUserHolder, userOrgVisibilityHolder);
        }

        /// <summary>
        /// Create the repo/label data for testing.
        /// </summary>
        /// <returns>Populated RepoLabelDataUtils</returns>
        public static RepoLabelDataUtils SetupRepoLabelData()
        {
            Dictionary<string, HashSet<string>> repoLabelDict = new Dictionary<string, HashSet<string>>(StringComparer.InvariantCultureIgnoreCase);
            HashSet<string> repoLabels = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            for (int i = 0;i < 5;i++)
            {
                string label = $"{TestLabelNamePartial}{i}";
                repoLabels.Add(label);
            }
            // Last but not least, add the Service Attention Label
            repoLabels.Add(LabelConstants.ServiceAttention);
            repoLabelDict.Add(TestRepositoryName, repoLabels);
            RepoLabelHolder repoLabelHolder = new RepoLabelHolder(null);
            repoLabelHolder.RepoLabelDict = repoLabelDict;
            return new RepoLabelDataUtils(repoLabelHolder, TestRepositoryName);
        }

        /// <summary>
        /// Given the actual and expected lists of labels or errors, verify they both contain the same items.
        /// </summary>
        /// <param name="actuaList">The list of parsed labels</param>
        /// <param name="expectedList">The list of expected labels</param>
        /// <returns></returns>
        public static bool ListsAreEqual(List<string> actuaList, List<string> expectedList)
        {
            if (actuaList.Count != expectedList.Count)
            {
                return false;
            }
            foreach (string label in expectedList)
            {
                if (!actuaList.Contains(label))
                {
                    return false;
                }
            }
            // If the list lengths are equal and all the expected labels exist in the actual labels
            // list then everything is good. 
            return true;
        }

        /// <summary>
        /// Given a list of BaseError, create a string with embedded newlines, that can be used in reporting
        /// in test failures.
        /// </summary>
        /// <param name="errors">List&lt;BaseError&gt; that need to be formatted.</param>
        /// <returns>string, formatted list of errors</returns>
        public static string FormatErrorMessageFromErrorList(List<BaseError> errors)
        {
            string errorString = "";
            foreach (BaseError error in errors)
            {
                errorString += error + Environment.NewLine;
            }
            return errorString;
        }
    }
}
