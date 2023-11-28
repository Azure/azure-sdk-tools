using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using Azure.Sdk.Tools.CodeownersUtils.Errors;
using Azure.Sdk.Tools.CodeownersUtils.Caches;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.CodeownersUtils.Utils;

namespace Azure.Sdk.Tools.CodeownersUtils.Tests
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
            // OwnerDataUtils requires a TeamUserCache and a UserOrgVisibilityCache populated with their
            // respective data. Live data really can't be used for this because it can change.
            TeamUserCache teamUserCache = new TeamUserCache(null);
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
            teamUserCache.TeamUserDict = teamUserDict;

            UserOrgVisibilityCache userOrgVisibilityCache = new UserOrgVisibilityCache(null);
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
            userOrgVisibilityCache.UserOrgVisibilityDict = userOrgVisDict;
            return new OwnerDataUtils(teamUserCache, userOrgVisibilityCache);
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
            RepoLabelCache repoLabelCache = new RepoLabelCache(null);
            repoLabelCache.RepoLabelDict = repoLabelDict;
            return new RepoLabelDataUtils(repoLabelCache, TestRepositoryName);
        }

        /// <summary>
        /// Given the actual and expected lists of errors, labels or owners, verify they both contain the same items.
        /// </summary>
        /// <param name="actuaList">The actual list of errors, labels or owners</param>
        /// <param name="expectedList">The expected list of errors, labels or owners</param>
        /// <returns>True if the lists are both equal, false otherwise</returns>
        public static bool StringListsAreEqual(List<string> actuaList, List<string> expectedList)
        {
            return actuaList.SequenceEqual(expectedList);
        }

        /// <summary>
        /// Given two CodeownersEntry lists, verify they're equal.
        /// </summary>
        /// <param name="actualCodeownerEntries">The actual, or parsed, list of Codeowners entries.</param>
        /// <param name="expectedCodeownerEntries">The expected list of Codeowners entries</param>
        /// <returns>True if they're equal, false otherwise</returns>
        public static bool CodeownersEntryListsAreEqual(List<CodeownersEntry> actualCodeownerEntries, 
                                                        List<CodeownersEntry> expectedCodeownerEntries)
        {
            // SequenceEqual determines whether two sequences are equal by comparing the length and
            // then by comparing the elements by using the default equality comparer for their type.
            // This has the side benefit of testing CodeownersEntry's Equals operator which compares
            // the PathExpression and every moniker list of labels and owners for equality
            return actualCodeownerEntries.SequenceEqual(expectedCodeownerEntries);
        }

        /// <summary>
        /// Given two list of Codeowners entries, format a string to be printed out with a test failure.
        /// </summary>
        /// <param name="actualCodeownerEntries">The actual, or parsed, list of Codeowners entries.</param>
        /// <param name="expectedCodeownerEntries">The expected list of Codeowners entries</param>
        /// <returns>A formatted string containing the differences.</returns>
        public static string FormatCodeownersListDifferences(List<CodeownersEntry> actualCodeownerEntries,
                                                             List<CodeownersEntry> expectedCodeownerEntries)
        {
            string diffString = "";
            // Get the items that are only in the actualList
            List<CodeownersEntry> diffActual = actualCodeownerEntries.Except(expectedCodeownerEntries).ToList();
            // Get the items that are only in the expectedList
            List<CodeownersEntry> diffExpected = expectedCodeownerEntries.Except(actualCodeownerEntries).ToList();

            // Entries that do compare the same will not be in either list. The combination of the two lists are
            // the diffs which can consiste of both completely missing entries and entries that don't compare the same.
            if (diffActual.Count > 0 && diffExpected.Count > 0)
            {
                diffString = "The list of parsed extries and expected entries had differences in both lists.\n";
                diffString += "Parsed entries not in the expected entries:\n\n";
                diffString += string.Join(Environment.NewLine, diffActual.Select(d => d.ToString()));
                diffString += "\n\nExpected entries not in the parsed entries:\n";
                diffString += string.Join(Environment.NewLine, diffExpected.Select(d => d.ToString()));
            }
            // This is the case where the actual contained more entries than the expected
            else if (diffActual.Count > 0)
            {
                diffString = "The list of parsed entries contains more items than expected. The additional items are as follows:\n";
                diffString += string.Join(Environment.NewLine, diffActual.Select(d => d.ToString()));
            }
            // this is the case where only the expected contained more entries than the actual
            else
            {
                diffString = "The list of expected entries contains more items than parsed. The additional items are as follows:\n";
                diffString += string.Join(Environment.NewLine, diffExpected.Select(d => d.ToString()));
            }

            return diffString;
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

        /// <summary>
        /// This function is used to help generate the json strings that will be deserialized for the parsing tests.
        /// </summary>
        /// <param name="codeownersEntries">List&lt;CodeownersEntry&gt; to be serialized</param>
        public static void TempGetSerializedString(List<CodeownersEntry> codeownersEntries)
        {
            JsonSerializerOptions jsonSerializerOptions = new()
            {
                WriteIndented = true
            };
            string jsonString = JsonSerializer.Serialize(codeownersEntries, jsonSerializerOptions);
            Console.WriteLine(jsonString);
            Console.WriteLine("something to break on");
        }
    }
}
