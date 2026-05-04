using GitHubTeamUserStore.Constants;
using System.Text.Json;

namespace GitHubTeamUserStore
{
    public class RepositoryLabelGenerator
    {
        public static async Task<bool> GenerateAndWriteRepositoryLabels(OpenSourceApiClient openSourceApiClient,
                                                                        string repoLabelOutputPath,
                                                                        string repositoryListFile)
        {
            Console.WriteLine($"=== Starting repository/label cache build: {repoLabelOutputPath} ===");

            // Repository name is the key, with the list of that repository's labels as the as the value
            Dictionary<string, HashSet<string>> repoLabelDict = new Dictionary<string, HashSet<string>>();
            bool dataMatches = false;

            try
            {
                // Load the repository list file
                string fullPath = Path.GetFullPath(repositoryListFile);
                if (!File.Exists(fullPath))
                {
                    throw new ArgumentException($"The path provided '{repositoryListFile}' does not exist");
                }

                var repositories = File.ReadAllLines(fullPath);
                foreach (string repository in repositories)
                {
                    if (string.IsNullOrWhiteSpace(repository))
                    {
                        continue;
                    }
                    // The repositories in the file start with "Azure/" and the output dictionary keeps that full
                    // repository name as the key, but the OSP repository labels endpoint uses only the repository
                    // segment in the request path.
                    string repoWithoutOrg = repository;
                    string repoPrefix = $"{ProductAndTeamConstants.Azure}/";
                    if (repoWithoutOrg.StartsWith(repoPrefix, StringComparison.Ordinal))
                    {
                        repoWithoutOrg = repoWithoutOrg[repoPrefix.Length..];
                    }
                    var labelsHash = await openSourceApiClient.GetAzureRepositoryLabels(repoWithoutOrg);
                    repoLabelDict[repository] = labelsHash;
                }

                string jsonString = JsonSerializer.Serialize(repoLabelDict);
                await File.WriteAllTextAsync(repoLabelOutputPath, jsonString);
                dataMatches = await VerifyWrittenRepositoryLabelData(repoLabelOutputPath, repoLabelDict);
                if (dataMatches)
                {
                    Console.WriteLine($"repository/label data written successfully to {repoLabelOutputPath}.");
                }
                else
                {
                    Console.WriteLine("There were issues with the written repository/label data. See above for specifics.");
                }
            }
            finally
            {
                Console.WriteLine($"=== Finished repository/label cache build: {(dataMatches ? "success" : "failure")} ({repoLabelOutputPath}) ===");
            }

            return dataMatches;
        }

        private static async Task<bool> VerifyWrittenRepositoryLabelData(string repoLabelOutputPath,
                                                                         Dictionary<string, HashSet<string>> repoLabelDict)
        {
            string rawJson = await File.ReadAllTextAsync(repoLabelOutputPath);
            var writtenRepoLabelDict = JsonSerializer.Deserialize<Dictionary<string, HashSet<string>>>(rawJson)
                ?? throw new InvalidOperationException($"Unable to deserialize repository/label data from {repoLabelOutputPath}.");
            if (repoLabelDict.Keys.Count != writtenRepoLabelDict.Keys.Count)
            {
                Console.WriteLine($"Error! Created repo/label dictionary has {repoLabelDict.Keys.Count} repositories and written dictionary has {writtenRepoLabelDict.Keys.Count} repositories.");
                Console.WriteLine(string.Format("created list repositories {0}", string.Join(", ", repoLabelDict.Keys)));
                Console.WriteLine(string.Format("written list repositories {0}", string.Join(", ", writtenRepoLabelDict.Keys)));
                return false;
            }

            foreach (string repository in repoLabelDict.Keys)
            {
                if (!writtenRepoLabelDict.ContainsKey(repository))
                {
                    Console.WriteLine("Error! Created repo/label dictionary has different repositories than the written dictionary.");
                    Console.WriteLine(string.Format("created dictionary repositories {0}", string.Join(", ", repoLabelDict.Keys)));
                    Console.WriteLine(string.Format("written dictionary repositories {0}", string.Join(", ", writtenRepoLabelDict.Keys)));
                    return false;
                }
            }

            bool hasError = false;
            foreach (string repository in repoLabelDict.Keys)
            {
                if (!repoLabelDict[repository].SetEquals(writtenRepoLabelDict[repository]))
                {
                    hasError = true;
                    Console.WriteLine($"The created dictionary entry for {repository} has different labels than the written dictionary.");
                    Console.WriteLine(string.Format("created dictionary labels {0}", string.Join(", ", repoLabelDict[repository])));
                    Console.WriteLine(string.Format("written dictionary labels {0}", string.Join(", ", writtenRepoLabelDict[repository])));
                }
            }
            return !hasError;
        }
    }
}
