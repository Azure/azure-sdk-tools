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
    class Program
    {
        private static readonly HttpClient client = new HttpClient();
        /// <summary>
        /// Retrieves codeowners information for specific section of the repo
        /// </summary>
        /// <param name="targetDirectory">The directory whose information is to be retrieved</param>
        /// <param name="rootDirectory">The root of the repo or $(Build.SourcesDirectory) on DevOps</param>
        /// <param name="vsoOwningUsers">Variable for setting user aliases</param>
        /// <param name="vsoOwningTeams">Variable for setting user aliases</param>
        /// <param name="vsoOwningLabels">Variable for setting user aliases</param>
        /// <returns></returns>

        public static void Main(
            string targetDirectory,
            string rootDirectory,
            string vsoOwningUsers,
            string vsoOwningTeams,
            string vsoOwningLabels
            )
        {
            var target = targetDirectory.ToLower().Trim();
            var codeOwnersLocation = Path.Join(rootDirectory, ".github", "CODEOWNERS");
            var parsedEntries = CodeOwnersFile.ParseFile(codeOwnersLocation);
            var filteredEntries = findClosestMatch(target, parsedEntries);

            client.BaseAddress = new Uri("https://api.github.com/");
            client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("CodeOwnerRetriever", "1.0"));
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            
            List<string> users = new List<string>();
            List<string> teams = new List<string>();
            List<string> labels = new List<string>();
            if (filteredEntries != null)
            {
                Console.WriteLine($"Found the closest code owners to match {target}:");
                Console.WriteLine(String.Join(", ", filteredEntries.Owners));
                foreach (var alias in filteredEntries.Owners)
                {
                    var userUriStub = $"users/{alias}";
                    if (VerifyAlias(userUriStub))
                    {
                        users.Add(alias);
                        continue;
                    }
                    else
                    {
                        // Assume its a team alias
                        teams.Add(alias);
                    }
                }
                labels.AddRange(filteredEntries.PRLabels);
                labels.AddRange(filteredEntries.ServiceLabels);
            }

            if (vsoOwningUsers != null) {
                var presentOwningUsers = Environment.GetEnvironmentVariable(vsoOwningUsers);
                if (presentOwningUsers != null)
                {
                    foreach (var item in presentOwningUsers.Split(","))
                    {
                        users.Add(item.Trim());
                    }
                }
                Console.WriteLine(String.Format("##vso[task.setvariable variable={0};]{1}", vsoOwningUsers, String.Join(",", users)));
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
                Console.WriteLine(String.Format("##vso[task.setvariable variable={0};]{1}", vsoOwningTeams, (String.Join(",", teams))));
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
                Console.WriteLine(String.Format("##vso[task.setvariable variable={0};]{1}", vsoOwningLabels, (String.Join(",", labels))));
            }
        }

        private static bool VerifyAlias(string uriStub)
        {
            try {
                var response = client.GetAsync(uriStub);
                if (response.Result.IsSuccessStatusCode)
                {
                    return true;
                }
                return false;
            } catch 
            {
                Console.WriteLine($"Http call {uriStub} to failed.");
                throw;
            }
        }

        private static CodeOwnerEntry findClosestMatch(string filePath, List<CodeOwnerEntry> entries)
        {
            // Normalize the start and end of the paths by trimming slash
            filePath = filePath.Trim('/');

            // We want to find the match closest to the bottom of the codeowners file
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                string pathExpression = entries[i].PathExpression.Trim('/');

                // Note that this only matches on paths without glob patterns which is good enough
                // for our current scenarios but in the future might need to support globs
                if (filePath.StartsWith(pathExpression, StringComparison.OrdinalIgnoreCase))
                {
                    return entries[i];
                }
            }

            return null;
        }
    }
}
