using GitHubTeamUserStore.Constants;
using Octokit;
using System.Text.Json;

namespace GitHubTeamUserStore
{
    public class TeamUserGenerator
    {

        /// <summary>
        /// Generate the team/user lists for each and every team under azure-sdk-write. Every team and user in a CODEOWNERS
        /// file must have azure-sdk-write permissions in order to be in there which means every team/user will be under
        /// of azure-sdk-write. This is done to limit the number of calls made because if team/user data for every team
        /// under Azure was pulled there would be roughly 1933 teams. Getting the team/user data for that many teams would
        /// end up taking 2500-3000 GitHub API calls whereas getting the team/user data for azure-sdk-write and its child
        /// teams is less than 1/10th of that. The team/user data is serialized into json and written to a local file.
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="openSourceApiClient">Authenticated OpenSourceApiClient</param>
        /// <param name="teamUserOutputPath">The file where the team/user cache will be written.</param>
        /// <param name="userOrgVisibilityOutputPath">The file where the user/org visibility cache will be written.</param>
        /// <returns>True if everything is written and verified successfully, false otherwise.</returns>
        public static async Task<bool> GenerateAndWriteTeamUserAndOrgData(GitHubEventClient gitHubEventClient,
                                                                          OpenSourceApiClient openSourceApiClient,
                                                                          string teamUserOutputPath,
                                                                          string userOrgVisibilityOutputPath)
        {
            // Team/User dictionary where the team name is the key and the list users is the value. It's worth noting
            // that this is the list of Logins (Octokit.User.Login) which are what's used in CODEOWNERS, @mentions, etc.
            Dictionary<string, List<string>> teamUserDict = new Dictionary<string, List<string>>();

            Team azureSdkWrite = await gitHubEventClient.GetTeamById(ProductAndTeamConstants.AzureSdkWriteTeamId);
            await CreateTeamUserEntry(gitHubEventClient, azureSdkWrite, teamUserDict);
            // Serializing the Dictionary<string, List<string>> directly won't work with the JsonSerializer but
            // a List<KeyValuePair<string, List<string>>> will and it's easy enough to convert to/from.
            var list = teamUserDict.ToList();
            string jsonString = JsonSerializer.Serialize(list);
            await File.WriteAllTextAsync(teamUserOutputPath, jsonString);
            if (await VerifyWrittenTeamUsers(teamUserOutputPath, teamUserDict))
            {
                Console.WriteLine($"team/user data written successfully to {teamUserOutputPath}.");
                return await GenerateAndWriteUserOrgData(openSourceApiClient, userOrgVisibilityOutputPath, teamUserDict);
            }
            else
            {
                Console.WriteLine("There were issues with the written team/user data. See above for specifics.");
                return false;
            }
        }

        /// <summary>
        /// Call GitHub to get users for the team and add a dictionary entry for the team/users. Note: GitHub returns 
        /// a distinct list of all of the users, including users from any/all child teams. After that, get the list
        /// of child teams and recursively call CreateTeamUserEntry for each one of those to create their team/user entries.
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="team">Octokit.Team to get users for.</param>
        public static async Task CreateTeamUserEntry(GitHubEventClient gitHubEventClient, 
                                                     Team team, 
                                                     Dictionary<string, List<string>> teamUserDict)
        {
            // If this team has already been added to the dictionary then there's nothing to do. This
            // should prevent any weirdness if there ends up being some kind of circular team reference
            if (teamUserDict.ContainsKey(team.Name))
            {
                return;
            }
            // Get all of the team members
            var teamMembers = await gitHubEventClient.GetTeamMembers(team);
            if (teamMembers.Count > 0)
            {
                // Just need a List<string> containing the logins from the returned
                // list of users. The Login is what's used in @mentions, assignments etc
                var members = teamMembers.Select(s => s.Login).ToList();
                teamUserDict.Add(team.Name, members);
            }
            else
            {
                // It seems better to report this than to add a team to the dictionary with no users
                Console.WriteLine($"Warning: team {team.Name} has no members and will not be added to the dictionary.");
            }
            var childTeams = await gitHubEventClient.GetAllChildTeams(team);
            foreach (Team childTeam in childTeams)
            {
                await CreateTeamUserEntry(gitHubEventClient, childTeam, teamUserDict);
            }
        }

        /// <summary>
        /// This method is called after the team/user data is written locally. It verifies that the
        /// team/user data from disk is the same as the in-memory data that was used to create the file.
        /// </summary>
        /// <param name="teamUserOutputPath">The file containing the written team/user data.</param>
        /// <returns>True, if the data on disk matches the in-memory data that was used to create the file, otherwise, false.</returns>
        private static async Task<bool> VerifyWrittenTeamUsers(string teamUserOutputPath,
                                                              Dictionary<string, List<string>> teamUserDict)
        {
            string rawJson = await File.ReadAllTextAsync(teamUserOutputPath);
            var list = JsonSerializer.Deserialize<List<KeyValuePair<string, List<string>>>>(rawJson)
                ?? throw new InvalidOperationException($"Unable to deserialize team/user data from {teamUserOutputPath}.");
            var writtenDictionary = list.ToDictionary((keyItem) => keyItem.Key, (valueItem) => valueItem.Value);

            // Verify the dictionary from disk contains everything from the in-memory dictionary.
            if (teamUserDict.Keys.Count != writtenDictionary.Keys.Count)
            {
                Console.WriteLine($"Error! Created dictionary has {teamUserDict.Keys.Count} teams and written dictionary has {writtenDictionary.Keys.Count} teams.");
                Console.WriteLine(string.Format("created list teams {0}", string.Join(", ", teamUserDict.Keys)));
                Console.WriteLine(string.Format("written list teams {0}", string.Join(", ", writtenDictionary.Keys)));
                return false;
            }

            bool hasError = false;
            foreach (string key in teamUserDict.Keys)
            {
                if (!writtenDictionary.ContainsKey(key))
                {
                    Console.WriteLine($"Error! Written dictionary does not contain the team {key}.");
                    Console.WriteLine(string.Format("created list teams {0}", string.Join(", ", teamUserDict.Keys)));
                    Console.WriteLine(string.Format("written list teams {0}", string.Join(", ", writtenDictionary.Keys)));
                    return false;
                }

                var users = teamUserDict[key].OrderBy(user => user, StringComparer.OrdinalIgnoreCase).ToList();
                var writtenUsers = writtenDictionary[key].OrderBy(user => user, StringComparer.OrdinalIgnoreCase).ToList();
                if (users.Count != writtenUsers.Count)
                {
                    hasError = true;
                    Console.WriteLine($"Error! Created dictionary for team {key} has {users.Count} users and written dictionary has {writtenUsers.Count} users.");
                    Console.WriteLine(string.Format("created list users {0}", string.Join(", ", users)));
                    Console.WriteLine(string.Format("written list users {0}", string.Join(", ", writtenUsers)));
                }
                else if (!users.SequenceEqual(writtenUsers, StringComparer.OrdinalIgnoreCase))
                {
                    hasError = true;
                    Console.WriteLine($"Error! Created dictionary for team {key} has different users than the written dictionary.");
                    Console.WriteLine(string.Format("created list users {0}", string.Join(", ", users)));
                    Console.WriteLine(string.Format("written list users {0}", string.Join(", ", writtenUsers)));
                }
            }
            return !hasError;
        }

        /// <summary>
        /// This function requires that the team/user data is generated first. It'll use the users from
        /// the azure-sdk-write group, which is the all inclusive list of users with write permissions,
        /// to generate the org visibility data.
        /// </summary>
        /// <param name="openSourceApiClient">Authenticated OpenSourceApiClient</param>
        /// <param name="userOrgVisibilityOutputPath">The file where the user/org visibility cache will be written.</param>
        private static async Task<bool> GenerateAndWriteUserOrgData(OpenSourceApiClient openSourceApiClient,
                                                                    string userOrgVisibilityOutputPath,
                                                                    Dictionary<string, List<string>> teamUserDict)
        {
            Dictionary<string, bool> userOrgDict = new Dictionary<string, bool>(StringComparer.InvariantCultureIgnoreCase);

            // The user list of azure-sdk-write will be a distinct list of all users from all child teams. All
            // users are Azure org members, otherwise they wouldn't be in there. Using the list, create a dictionary
            // with the user as the key and the value, a boolean, true if they're public and false otherwise.
            List<string> allWriteUsers = teamUserDict[ProductAndTeamConstants.AzureSdkWriteTeamName];
            HashSet<string> publicAzureMembers = await openSourceApiClient.GetPublicOrgMembers(ProductAndTeamConstants.Azure);
            foreach (var user in allWriteUsers)
            {
                userOrgDict[user] = publicAzureMembers.Contains(user);
            }

            string jsonString = JsonSerializer.Serialize(userOrgDict);
            await File.WriteAllTextAsync(userOrgVisibilityOutputPath, jsonString);
            bool dataMatches = await VerifyWrittenUserOrgData(userOrgVisibilityOutputPath, userOrgDict);
            if (dataMatches)
            {
                Console.WriteLine($"user/org visibility data written successfully to {userOrgVisibilityOutputPath}.");
            }
            else
            {
                Console.WriteLine("There were issues with the written user/org visibility data. See above for specifics.");
            }
            return dataMatches;
        }

        private static async Task<bool> VerifyWrittenUserOrgData(string userOrgVisibilityOutputPath,
                                                                 Dictionary<string, bool> userOrgDict)
        {
            string rawJson = await File.ReadAllTextAsync(userOrgVisibilityOutputPath);
            var writtenUserOrgDict = JsonSerializer.Deserialize<Dictionary<string, bool>>(rawJson)
                ?? throw new InvalidOperationException($"Unable to deserialize user/org visibility data from {userOrgVisibilityOutputPath}.");
            if (userOrgDict.Keys.Count != writtenUserOrgDict.Keys.Count)
            {
                Console.WriteLine($"Error! Created user/org dictionary has {userOrgDict.Keys.Count} users and written dictionary has {writtenUserOrgDict.Keys.Count} users.");
                Console.WriteLine(string.Format("created list users {0}", string.Join(", ", userOrgDict.Keys)));
                Console.WriteLine(string.Format("written list users {0}", string.Join(", ", writtenUserOrgDict.Keys)));
                return false;
            }

            foreach (string user in userOrgDict.Keys)
            {
                if (!writtenUserOrgDict.ContainsKey(user))
                {
                    Console.WriteLine("Error! Created user/org dictionary has different users than the written dictionary.");
                    Console.WriteLine(string.Format("created list users {0}", string.Join(", ", userOrgDict.Keys)));
                    Console.WriteLine(string.Format("written list users {0}", string.Join(", ", writtenUserOrgDict.Keys)));
                    return false;
                }
            }

            bool hasError = false;
            foreach (string user in userOrgDict.Keys)
            {
                if (userOrgDict[user] != writtenUserOrgDict[user])
                {
                    hasError = true;
                    Console.WriteLine($"The created dictionary entry for {user} is '{userOrgDict[user]}' and in written it is '{writtenUserOrgDict[user]}'");
                }
            }
            return !hasError;
        }
    }
}
