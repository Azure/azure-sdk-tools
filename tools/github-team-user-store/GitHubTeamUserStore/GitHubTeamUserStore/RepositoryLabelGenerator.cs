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
            bool succeeded = false;

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
                Console.WriteLine($"repository/label data written successfully to {repoLabelOutputPath}.");
                succeeded = true;
            }
            finally
            {
                Console.WriteLine($"=== Finished repository/label cache build: {(succeeded ? "success" : "failure")} ({repoLabelOutputPath}) ===");
            }

            return succeeded;
        }
    }
}
