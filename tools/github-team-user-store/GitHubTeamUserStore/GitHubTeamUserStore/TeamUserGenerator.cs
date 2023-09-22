using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GitHubTeamUserStore.Constants;
using Azure.Storage.Blobs;

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
        /// teams is less than 1/10th of that. The team/user data is serialized into json and stored in azure blob storage.
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="teamUserBlobStorageUri">The URI, including SAS, of the team/user blob storage URI</param>
        /// <param name="userOrgVisibilityBlobStorageUri">The URI, including SAS, of the user/org visibility blob</param>
        /// <returns>true if everything stored successfully, false otherwise</returns>
        public static async Task<bool> GenerateAndStoreTeamUserAndOrgData(GitHubEventClient gitHubEventClient,
                                                                          string teamUserBlobStorageUri,
                                                                          string userOrgVisibilityBlobStorageUri)
        {
            // Team/User dictionary where the team name is the key and the list users is the value. It's worth noting
            // that this is the list of Logins (Octokit.User.Login) which are what's used in CODEOWNERS, @mentions, etc.
            Dictionary<string, List<string>> teamUserDict = new Dictionary<string, List<string>>();
            // The BlobUriBuilder for team/user storage is stored for the verify call
            Uri teamUserBlobUri = new Uri(teamUserBlobStorageUri);
            BlobUriBuilder teamUserBlobUriBuilder = new BlobUriBuilder(teamUserBlobUri);

            Team azureSdkWrite = await gitHubEventClient.GetTeamById(ProductAndTeamConstants.AzureSdkWriteTeamId);
            await CreateTeamUserEntry(gitHubEventClient, azureSdkWrite, teamUserDict);
            // Serializing the Dictionary<string, List<string>> directly won't work with the JsonSerializer but
            // a List<KeyValuePair<string, List<string>>> will and it's easy enough to convert to/from.
            var list = teamUserDict.ToList();
            string jsonString = JsonSerializer.Serialize(list);
            await gitHubEventClient.UploadDataToBlobStorage(jsonString, teamUserBlobUriBuilder);
            if (await VerifyStoredTeamUsers(gitHubEventClient, teamUserBlobUriBuilder, teamUserDict))
            {
                Console.WriteLine("team/user data stored successfully.");
                // don't bother generating the data unless the team/user data stored
                return await GenerateAndStoreUserOrgData(gitHubEventClient, userOrgVisibilityBlobStorageUri, teamUserDict);
            }
            else
            {
                Console.WriteLine("There were issues with generated vs stored data. See above for specifics.");
                return false;
            }
        }

        /// <summary>
        /// Call GitHub to get users for the team and add a dictionary entry for the team/users. Note: GitHub returns 
        /// a distinct list of all of the users, including users from any/all child teams. After that, get the list
        /// of child teams and recursively call GetUsersForTeam for each one of those to create their team/user entries.
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
        /// This method is called after the team/user data is stored in blob storage. It verifies that the
        /// team/user data from blob storage is the same as the in-memory data that was used to create the blob.
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <returns>True, if the team data in storage matches the in-memory data that was used to create the blob, otherwise, false.</returns>
        private static async Task<bool> VerifyStoredTeamUsers(GitHubEventClient gitHubEventClient, 
                                                              BlobUriBuilder blobUriBuilder, 
                                                              Dictionary<string, List<string>> teamUserDict)
        {
            string rawJson = await gitHubEventClient.GetBlobDataFromStorage(blobUriBuilder);
            var list = JsonSerializer.Deserialize<List<KeyValuePair<string, List<string>>>>(rawJson);
            var storedDictionary = list.ToDictionary((keyItem) => keyItem.Key, (valueItem) => valueItem.Value);

            // Verify the dictionary from storage contains everything from the teamUserDict
            if (teamUserDict.Keys.Count != storedDictionary.Keys.Count)
            {
                // At this point list the teams and return, don't bother looking at the users.
                Console.WriteLine($"Error! Created dictionary has {teamUserDict.Keys.Count} teams and stored dictionary has {storedDictionary.Keys.Count} teams.");
                Console.WriteLine(string.Format("created list teams {0}", string.Join(", ", teamUserDict.Keys)));
                Console.WriteLine(string.Format("stored list teams {0}", string.Join(", ", storedDictionary.Keys)));
                return false;
            }

            bool hasError = false;
            // If the number of teams in the dictionaries are equal, look at the users for every team.
            foreach (string key in teamUserDict.Keys)
            {
                var users = teamUserDict[key];
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

        /// <summary>
        /// This function requires that the team/user data is generated first. It'll use the users from
        /// the azure-sdk-write group, which is the all inclusive list of users with write permissions,
        /// to generate the org visibility data.
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="userOrgVisibilityBlobStorageUri">The URI, including SAS, of the user/org visibility blob</param>
        /// <returns></returns>
        public static async Task<bool> GenerateAndStoreUserOrgData(GitHubEventClient gitHubEventClient,
                                                                   string userOrgVisibilityBlobStorageUri,
                                                                   Dictionary<string, List<string>> teamUserDict)
        {
            // The user list of azure-sdk-write will contain a distinct list of all users from all child teams. For
            // each one, check if they're a member of azure and store that in here.
            Dictionary<string, bool> userOrgDict = new Dictionary<string, bool>();

            Uri userOrgVisBlobUri = new Uri(userOrgVisibilityBlobStorageUri);
            BlobUriBuilder userOrgVisBlobUriBuilder = new BlobUriBuilder(userOrgVisBlobUri);

            // The user list of azure-sdk-write will be a distinct list of all users from all child teams. All
            // users are Azure org members, otherwise they wouldn't be in there. Using the list, create a dictionary
            // with the user as the key and the value, a boolean, true if they're public and false otherwise.
            List<string> allWriteUsers = teamUserDict[ProductAndTeamConstants.AzureSdkWriteTeamName];
            foreach (var user in allWriteUsers)
            {
                userOrgDict[user] = await gitHubEventClient.IsUserPublicMemberOfOrg(ProductAndTeamConstants.Azure, user);
            }
            // Dictionary<string, bool> will serialize as-is and doesn't need to be changed for serialization
            string jsonString = JsonSerializer.Serialize(userOrgDict);
            await gitHubEventClient.UploadDataToBlobStorage(jsonString, userOrgVisBlobUriBuilder);
            bool dataMatches = await VerifyStoredUserOrgData(gitHubEventClient, userOrgVisBlobUriBuilder, userOrgDict);
            if (dataMatches)
            {
                Console.WriteLine("user/org visibility data stored successfully.");
            }
            else
            {
                Console.WriteLine("There were issues with generated vs stored user/org visibility data. See above for specifics.");
            }
            return dataMatches;
        }

        public static async Task<bool> VerifyStoredUserOrgData(GitHubEventClient gitHubEventClient, 
                                                               BlobUriBuilder blobUriBuilder,
                                                                Dictionary<string, bool> userOrgDict)
        {

            string rawJson = await gitHubEventClient.GetBlobDataFromStorage(blobUriBuilder);
            var storedUserOrgDict = JsonSerializer.Deserialize<Dictionary<string, bool>>(rawJson);
            if (userOrgDict.Keys.Count != storedUserOrgDict.Keys.Count)
            {
                // At this point dictionaries are different, don't bother looking at org membership, output the strings of users (keys)
                // so they can be compared.
                Console.WriteLine($"Error! Created user/org dictionary has {userOrgDict.Keys.Count} users and stored dictionary has {storedUserOrgDict.Keys.Count} users.");
                Console.WriteLine(string.Format("created list users {0}", string.Join(", ", userOrgDict.Keys)));
                Console.WriteLine(string.Format("stored list users {0}", string.Join(", ", storedUserOrgDict.Keys)));
                return false;
            }

            // Verify that the userOrgDict and the storedUserOrgDict both contain the same set of keys
            foreach (string user in userOrgDict.Keys)
            {
                if (!storedUserOrgDict.ContainsKey(user))
                {
                    Console.WriteLine("Error! Created user/org dictionary has different users than the stored dictionary.");
                    Console.WriteLine(string.Format("created list users {0}", string.Join(", ", userOrgDict.Keys)));
                    Console.WriteLine(string.Format("stored list users {0}", string.Join(", ", storedUserOrgDict.Keys)));
                    return false;
                }
            }

            // A this point both dictionaries contain the same set of keys, now verify that the org visibility data is the same.
            // Look at every entry and report all of the errors
            bool hasError = false;
            foreach (string user in userOrgDict.Keys)
            {
                if (userOrgDict[user] != storedUserOrgDict[user])
                {
                    hasError = true;
                    Console.WriteLine($"The created dictionary entry for {user} is '{userOrgDict[user]}' and in stored it is '{storedUserOrgDict[user]}'");
                }
            }
            return !hasError;
        }
    }
}
