using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GitHubTeamUserStore.Constants;

namespace GitHubTeamUserStore
{
    public class TeamUserGenerator
    {
        // Team/User dictionary where the team name is the key and the list users is the value. It's worth noting
        // that this is the list of Logins (Octokit.User.Login) which are what's used in CODEOWNERS, @mentions, etc.
        private static Dictionary<string, List<string>> _teamUserDict = new Dictionary<string, List<string>>();

        /// <summary>
        /// Generate the team/user lists for each and every team under azure-sdk-write. Every team and user in a CODEOWNERS
        /// file must have azure-sdk-write permissions in order to be in there which means every team/user will be under
        /// of azure-sdk-write. This is done to limit the number of calls made because if team/user data for every team
        /// under Azure was pulled there would be roughly 1933 teams. Getting the team/user data for that many teams would
        /// end up taking 2500-3000 GitHub API calls whereas getting the team/user data for azure-sdk-write and its child
        /// teams is less than 1/10th of that. The team/user data is serialized into json and stored in azure blob storage.
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <returns></returns>
        public static async Task GenerateAndStoreTeamUserList(GitHubEventClient gitHubEventClient)
        {
            Team azureSdkWrite = await gitHubEventClient.GetTeamById(ProductAndTeamConstants.AzureSdkWriteTeamId);
            await CreateTeamUserEntry(gitHubEventClient, azureSdkWrite);
            // Serializing the Dictionary<string, List<string>> directly won't work with the JsonSerializer but
            // a List<KeyValuePair<string, List<string>>> will and it's easy enough to convert to/from.
            var list = _teamUserDict.ToList();
            string jsonString = JsonSerializer.Serialize(list);
            await gitHubEventClient.UploadToBlobStorage(jsonString);
        }

        /// <summary>
        /// Call GitHub to get users for the team and add a dictionary entry for the team/users. Note: GitHub returns 
        /// a distinct list of all of the users, including users from any/all child teams. After that, get the list
        /// of child teams and recursively call GetUsersForTeam for each one of those to create their team/user entries.
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="team">Octokit.Team to get users for.</param>
        public static async Task CreateTeamUserEntry(GitHubEventClient gitHubEventClient, Team team)
        {
            // If this team has already been added to the dictionary then there's nothing to do. This
            // should prevent any weirdness if there ends up being some kind of circular team reference
            if (_teamUserDict.ContainsKey(team.Name))
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
                _teamUserDict.Add(team.Name, members);
            }
            else
            {
                // It seems better to report this than to add a team to the dictionary with no users
                Console.WriteLine($"Warning: team {team.Name} has no members and will not be added to the dictionary.");
            }
            var childTeams = await gitHubEventClient.GetAllChildTeams(team);
            foreach (Team childTeam in childTeams)
            {
                await CreateTeamUserEntry(gitHubEventClient, childTeam);
            }
        }

        /// <summary>
        /// This method is called after the team/user data is stored in blob storage. It verifies that the
        /// team/user data from blob storage is the same as the in-memory data that was used to create the blob.
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <returns>True, if the team data in storage matches the in-memory data that was used to create the blob otherwise false.</returns>
        public static async Task<bool> VerifyStoredTeamUsers(GitHubEventClient gitHubEventClient)
        {
            bool hasError = false;
            string rawJson = await gitHubEventClient.GetTeamUserBlobFromStorage();
            var list = JsonSerializer.Deserialize<List<KeyValuePair<string, List<string>>>>(rawJson);
            var storedDictionary = list.ToDictionary((keyItem) => keyItem.Key, (valueItem) => valueItem.Value);

            // Verify the dictionary from storage contains everything from the _teamUserDict
            if (_teamUserDict.Keys.Count != storedDictionary.Keys.Count)
            {
                // At this point list the teams and return, don't bother looking at the users.
                Console.WriteLine($"Error! Created dictionary has {_teamUserDict.Keys.Count} teams and stored dictionary has {storedDictionary.Keys.Count} teams.");
                Console.WriteLine(string.Format("created list teams {0}", string.Join(", ", _teamUserDict.Keys)));
                Console.WriteLine(string.Format("stored list teams {0}", string.Join(", ", storedDictionary.Keys)));
                return !hasError;
            }

            // If the number of teams in the dictionaries are equal, look at the users for every team.
            foreach (string key in _teamUserDict.Keys)
            {
                var users = _teamUserDict[key];
                var storedUsers = storedDictionary[key];
                // Since these are just lists of strings, calling sort will sort them in ascending order.
                // This makes things easier to find differences if there's an error
                users.Sort();
                storedUsers.Sort();
                if (users.Count != storedUsers.Count)
                {
                    hasError = true;
                    Console.WriteLine($"Error! Created dictionary for team {key} has {users.Count} and stored dictionary has {storedUsers.Count}");
                    Console.WriteLine(string.Format("created list users {0}", string.Join(", ", users)));
                    Console.WriteLine(string.Format("stored list users {0}", string.Join(", ", storedUsers)));
                }
                else
                {
                    foreach (var user in users)
                    {
                        // As soon as difference is found, output all the users for each team and move on to the next team
                        if (!storedUsers.Contains(user))
                        {
                            hasError = true;
                            Console.WriteLine($"Error! Created dictionary for team {key} has different users than the stored dictionary");
                            Console.WriteLine(string.Format("created list users {0}", string.Join(", ", users)));
                            Console.WriteLine(string.Format("stored list users {0}", string.Join(", ", storedUsers)));
                            break;
                        }
                    }
                }
            }
            return !hasError;
        }
    }
}
