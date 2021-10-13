using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeOwnersParser;

namespace Azure.Sdk.Tools.RetrieveCodeOwners
{
    public class CodeOwnersInfo 
    {
        public List<string> users { get; set; }
        public List<string> teams { get; set; }
        public List<string> labels { get; set; }
    }
    class Program
    {
        private static readonly HttpClient client = new HttpClient();
        /// <summary>
        /// Retrieves codeowners information for specific section of the repo
        /// </summary>
        /// <param name="targetDirectory">The directory whose information is to be retrieved</param>
        /// <param name="rootDirectory">The root of the repo or $(Build.SourcesDirectory) on DevOps</param>
        /// <param name="authToken">GitHub api auth token</param>
        /// <param name="vsoOwningUsers">Variable for setting user aliases</param>
        /// <param name="vsoOwningTeams">Variable for setting user aliases</param>
        /// <param name="vsoOwningLabels">Variable for setting user aliases</param>
        /// <returns></returns>
        static async Task<string> Main(
            string targetDirectory,
            string rootDirectory,
            string authToken,
            string vsoOwningUsers,
            string vsoOwningTeams,
            string vsoOwningLabels)
        {
            var target = targetDirectory.ToLower().Trim();
            var codeOwnersLocation = Path.Join(rootDirectory, ".github", "CODEOWNERS");

            var parsedEntries = CodeOwnersFile.ParseFile(codeOwnersLocation);
            var filteredEntries = parsedEntries.Where(
                entries => entries.PathExpression.Trim(new char[] {'/','\\' }).Equals(targetDirectory.Trim(new char[] { '/', '\\' })));

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            List<string> users = new List<string>();
            List<string> teams = new List<string>();
            List<string> labels = new List<string>();

            if (filteredEntries.Count() > 0)
            {
                Console.WriteLine($"Found a folder to match {target}");

                foreach (var entry in filteredEntries)
                {
                    foreach (var alias in entry.Owners)
                    {
                        if (alias.IndexOf('/') != -1) // Check if it's a team alias e.g. Azure/azure-sdk-eng
                        {
                            var org = alias.Substring(0, alias.IndexOf('/'));
                            var team_slug = alias.Substring(alias.IndexOf('/') + 1);
                            var teamApiUrl = $"https://api.github.com/orgs/{org}/teams/{team_slug}";
                            if (await VerifyAlias(teamApiUrl)) 
                            {
                                teams.Add(team_slug);
                                continue;
                            }
                        }
                        else 
                        {
                            var userApiUrl = $"https://api.github.com/users/{alias}";
                            if (await VerifyAlias(userApiUrl))
                            {
                                users.Add(alias);
                                continue;
                            }
                        }
                        Console.WriteLine($"Alias {alias} is neither a recognized github user nor a team");
                    }
                    labels.AddRange(entry.PRLabels);
                    labels.AddRange(entry.ServiceLabels);
                }
            }

            if (vsoOwningUsers != null) {
                var presentOwningLabels = Environment.GetEnvironmentVariable(vsoOwningUsers);
                if (presentOwningLabels != null)
                {
                    foreach (var item in presentOwningLabels.Split(","))
                    {
                        users.Add(item.Trim());
                    }
                }
                Console.WriteLine(String.Format("##vso[task.setvariable variable=VsoOwningUsers;]{0}", (String.Join(",", users))));
            }

            if (vsoOwningTeams != null)
            {
                var presentOwningTeams = Environment.GetEnvironmentVariable(vsoOwningTeams);
                if (presentOwningTeams != null)
                {
                    foreach (var item in presentOwningTeams.Split(","))
                    {
                        teams.Add(item.Trim());
                    }
                }
                Console.WriteLine(String.Format("##vso[task.setvariable variable=vsoOwningTeams;]{0}", (String.Join(",", teams))));
            }

            if (vsoOwningLabels != null)
            {
                var presentOwningLabels = Environment.GetEnvironmentVariable(vsoOwningLabels);
                if (presentOwningLabels != null)
                {
                    foreach (var item in presentOwningLabels.Split(","))
                    {
                        users.Add(item.Trim());
                    }
                }
                Console.WriteLine(String.Format("##vso[task.setvariable variable=vsoOwningLabels;]{0}", (String.Join(",", labels))));
            }
            var SerializerPptions = new JsonSerializerOptions() { WriteIndented = true };
            return JsonSerializer.Serialize<CodeOwnersInfo>(
                new CodeOwnersInfo() { users = users, teams = teams, labels = labels }, SerializerPptions);
        }

        private static async Task<bool> VerifyAlias(string apiUrl)
        {
            try {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                return false;
            } catch 
            {
                Console.WriteLine($"Http call {apiUrl} to failed.");
                throw;
            }
        }
    }
}
