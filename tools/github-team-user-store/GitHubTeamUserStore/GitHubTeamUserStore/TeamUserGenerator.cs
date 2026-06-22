using GitHubTeamUserStore.Constants;
using System.Text.Json;

namespace GitHubTeamUserStore
{
    public class TeamUserGenerator
    {

        /// <summary>
        /// Generate the team/user lists for each and every team under azure-sdk-write using the OSP team
        /// children and team members endpoints. Starting from the azure-sdk-write root avoids the older GitHub
        /// team walk entirely and keeps the OSP traversal bounded to one known team tree instead of enumerating
        /// unrelated teams and then looking up their membership, parents, or children. The resulting team/user
        /// data is serialized into json and written to a local file using the current cache contract where the
        /// team display name is the key.
        /// </summary>
        /// <param name="openSourceApiClient">Authenticated OpenSourceApiClient</param>
        /// <param name="teamUserOutputPath">The file where the team/user cache will be written.</param>
        /// <param name="userOrgVisibilityOutputPath">The file where the user/org visibility cache will be written.</param>
        /// <returns>True if everything is written successfully, false otherwise.</returns>
        public static async Task<bool> GenerateAndWriteTeamUserAndOrgData(OpenSourceApiClient openSourceApiClient,
                                                                          string teamUserOutputPath,
                                                                          string userOrgVisibilityOutputPath)
        {
            Console.WriteLine($"=== Starting team/user cache build: {teamUserOutputPath} ===");

            // Team/User dictionary where the team name is the key and the list users is the value. It's worth noting
            // that this is the list of GitHub logins which are what's used in CODEOWNERS, @mentions, etc.
            Dictionary<string, List<string>> teamUserDict = new Dictionary<string, List<string>>();
            Dictionary<string, string> teamNameToSlug = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProductAndTeamConstants.AzureSdkWriteTeamName] = ProductAndTeamConstants.AzureSdkWriteTeamSlug
            };
            Dictionary<string, string> slugToTeamName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProductAndTeamConstants.AzureSdkWriteTeamSlug] = ProductAndTeamConstants.AzureSdkWriteTeamName
            };
            Queue<(string Name, string Slug)> teamsToProcess = new Queue<(string Name, string Slug)>();
            teamsToProcess.Enqueue((ProductAndTeamConstants.AzureSdkWriteTeamName, ProductAndTeamConstants.AzureSdkWriteTeamSlug));

            bool teamUserDataWritten = false;
            try
            {
                while (teamsToProcess.Count > 0)
                {
                    var (teamName, teamSlug) = teamsToProcess.Dequeue();
                    var teamMembers = CreateDistinctMembers(await openSourceApiClient.GetAzureTeamMembers(teamSlug));
                    if (teamMembers.Count > 0)
                    {
                        teamUserDict.Add(teamName, teamMembers);
                    }
                    else
                    {
                        // It seems better to report this than to add a team to the dictionary with no users
                        Console.WriteLine($"Warning: team {teamName} has no members and will not be added to the dictionary.");
                    }

                    var childTeams = await openSourceApiClient.GetAzureChildTeams(teamSlug);
                    foreach (var childTeam in childTeams)
                    {
                        QueueChildTeam(childTeam.Name, childTeam.Slug, teamNameToSlug, slugToTeamName, teamsToProcess);
                    }
                }

                if (!teamUserDict.ContainsKey(ProductAndTeamConstants.AzureSdkWriteTeamName))
                {
                    throw new InvalidOperationException("The generated team/user dictionary is missing the required azure-sdk-write entry.");
                }

                // Serializing the Dictionary<string, List<string>> directly won't work with the JsonSerializer but
                // a List<KeyValuePair<string, List<string>>> will and it's easy enough to convert to/from.
                var list = teamUserDict.ToList();
                string jsonString = JsonSerializer.Serialize(list);
                await File.WriteAllTextAsync(teamUserOutputPath, jsonString);
                Console.WriteLine($"team/user data written successfully to {teamUserOutputPath}.");
                teamUserDataWritten = true;
            }
            finally
            {
                Console.WriteLine($"=== Finished team/user cache build: {(teamUserDataWritten ? "success" : "failure")} ({teamUserOutputPath}) ===");
            }

            if (!teamUserDataWritten)
            {
                return false;
            }

            return await GenerateAndWriteUserOrgData(openSourceApiClient, userOrgVisibilityOutputPath, teamUserDict);
        }

        private static void QueueChildTeam(string teamName,
                                           string teamSlug,
                                           Dictionary<string, string> teamNameToSlug,
                                           Dictionary<string, string> slugToTeamName,
                                           Queue<(string Name, string Slug)> teamsToProcess)
        {
            if (slugToTeamName.TryGetValue(teamSlug, out string existingTeamName))
            {
                if (!string.Equals(existingTeamName, teamName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"The team slug '{teamSlug}' was returned with conflicting names '{existingTeamName}' and '{teamName}'.");
                }

                return;
            }

            if (teamNameToSlug.TryGetValue(teamName, out string existingTeamSlug) &&
                !string.Equals(existingTeamSlug, teamSlug, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"The team name '{teamName}' maps to multiple slugs: '{existingTeamSlug}' and '{teamSlug}'.");
            }

            teamNameToSlug[teamName] = teamSlug;
            slugToTeamName[teamSlug] = teamName;
            teamsToProcess.Enqueue((teamName, teamSlug));
        }

        private static List<string> CreateDistinctMembers(IReadOnlyList<string> teamMembers)
        {
            HashSet<string> seenMembers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> distinctMembers = new List<string>(teamMembers.Count);
            foreach (string teamMember in teamMembers)
            {
                if (seenMembers.Add(teamMember))
                {
                    distinctMembers.Add(teamMember);
                }
            }

            return distinctMembers;
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
            Console.WriteLine($"=== Starting user/org visibility cache build: {userOrgVisibilityOutputPath} ===");

            Dictionary<string, bool> userOrgDict = new Dictionary<string, bool>(StringComparer.InvariantCultureIgnoreCase);
            bool userOrgDataWritten = false;

            try
            {
                // The user list of azure-sdk-write will be a distinct list of all users from all child teams. All
                // users are Azure org members, otherwise they wouldn't be in there. Using the list, create a dictionary
                // with the user as the key and the value, a boolean, true if they're public and false otherwise.
                if (!teamUserDict.TryGetValue(ProductAndTeamConstants.AzureSdkWriteTeamName, out List<string> allWriteUsers))
                {
                    throw new InvalidOperationException("The generated team/user dictionary is missing the required azure-sdk-write entry.");
                }

                HashSet<string> publicAzureMembers = await openSourceApiClient.GetPublicOrgMembers(ProductAndTeamConstants.Azure);
                foreach (var user in allWriteUsers)
                {
                    userOrgDict[user] = publicAzureMembers.Contains(user);
                }

                string jsonString = JsonSerializer.Serialize(userOrgDict);
                await File.WriteAllTextAsync(userOrgVisibilityOutputPath, jsonString);
                Console.WriteLine($"user/org visibility data written successfully to {userOrgVisibilityOutputPath}.");
                userOrgDataWritten = true;
            }
            finally
            {
                Console.WriteLine($"=== Finished user/org visibility cache build: {(userOrgDataWritten ? "success" : "failure")} ({userOrgVisibilityOutputPath}) ===");
            }

            return userOrgDataWritten;
        }
    }
}
