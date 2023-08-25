using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using GitHubTeamUserStore.Constants;
using Octokit;

namespace GitHubTeamUserStore
{
    public class RepositoryLabelGenerator
    {

        public static async Task<bool> GenerateAndStoreRepositoryLabels(GitHubEventClient gitHubEventClient, string repoLabelBlobStorageUri, string repositoryListFile)
        {
            // Repository name is the key, with the list of that repository's labels as the as the value
            Dictionary<string, HashSet<string>> repoLabelDict = new Dictionary<string, HashSet<string>>();
            Uri repoLabelBlobUri = new Uri(repoLabelBlobStorageUri);
            BlobUriBuilder repoLabelBlobUriBuilder = new BlobUriBuilder(repoLabelBlobUri);

            // Load the repostiory list file
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
                // The repositories in the file will all start with "Azure/" which is fine for storage considering
                // that the $(Build.Repository.Name), in a pipeline, will also start with "Azure/" but "Azure/"
                // needs to be stripped off for the GetRepositoryLabels call which requires the Org and repository
                // be separate arguments. The dictionary key will be full repository name.
                string repoWithoutOrg = repository;
                if (repoWithoutOrg.StartsWith($"{ProductAndTeamConstants.Azure}/"))
                {
                    repoWithoutOrg = repoWithoutOrg.Replace($"{ProductAndTeamConstants.Azure}/", "");
                }
                var labelsHash = await gitHubEventClient.GetRepositoryLabels(repoWithoutOrg);
                repoLabelDict[repository] = labelsHash;
            }
            // Dictionary<string, HashSet<string>> will serialize as-is and doesn't need to be changed for serialization
            string jsonString = JsonSerializer.Serialize(repoLabelDict);
            await gitHubEventClient.UploadDataToBlobStorage(jsonString, repoLabelBlobUriBuilder);
            bool dataMatches = await VerifyStoredRepositoryLabelData(gitHubEventClient, repoLabelBlobUriBuilder, repoLabelDict);
            if (dataMatches)
            {
                Console.WriteLine("repository/label data stored successfully.");
            }
            else
            {
                Console.WriteLine("There were issues with generated vs stored repository/label data. See above for specifics.");
            }
            return dataMatches;
        }

        public static async Task<bool> VerifyStoredRepositoryLabelData(GitHubEventClient gitHubEventClient, 
                                                                       BlobUriBuilder blobUriBuilder,
                                                                       Dictionary<string, HashSet<string>> repoLabelDict)
        {

            string rawJson = await gitHubEventClient.GetBlobDataFromStorage(blobUriBuilder);
            var storedRepoLabelDict = JsonSerializer.Deserialize<Dictionary<string, HashSet<string>>>(rawJson);
            if (repoLabelDict.Keys.Count != storedRepoLabelDict.Keys.Count)
            {
                Console.WriteLine($"Error! Created repo/label dictionary has {repoLabelDict.Keys.Count} repositories and stored dictionary has {storedRepoLabelDict.Keys.Count} repositories.");
                Console.WriteLine(string.Format("created list users {0}", string.Join(", ", repoLabelDict.Keys)));
                Console.WriteLine(string.Format("stored list users {0}", string.Join(", ", storedRepoLabelDict.Keys)));
                return false;
            }

            // Verify that the _repoLabelDict and the storedRepoLabelDict both contain the same set of keys (repositories)
            foreach (string repository in repoLabelDict.Keys)
            {
                if (!storedRepoLabelDict.ContainsKey(repository))
                {
                    Console.WriteLine("Error! Created repo/label dictionary has different repositories than the stored dictionary.");
                    Console.WriteLine(string.Format("created dictionary repositories {0}", string.Join(", ", repoLabelDict.Keys)));
                    Console.WriteLine(string.Format("stored dictionary repositories {0}", string.Join(", ", storedRepoLabelDict.Keys)));
                    return false;
                }
            }

            // A this point both dictionaries contain the same set of repositories, now verify that the labels for repository match
            bool hasError = false;
            foreach (string repository in repoLabelDict.Keys)
            {
                if (repoLabelDict[repository].Count != storedRepoLabelDict[repository].Count)
                {
                    hasError = true;
                    Console.WriteLine($"The created dictionary entry for {repository} is has a different number of labels, {repoLabelDict[repository].Count}, than the stored dictionary, {storedRepoLabelDict[repository].Count}.");
                    Console.WriteLine(string.Format("created dictionary repositories {0}", string.Join(", ", repoLabelDict[repository])));
                    Console.WriteLine(string.Format("stored dictionary repositories {0}", string.Join(", ", storedRepoLabelDict[repository])));
                    // If the number of labels differs, don't bother checking individual entries, move on to the next repository
                    continue;
                }

                foreach (string label in repoLabelDict[repository])
                {
                    if (!storedRepoLabelDict[repository].Contains(label))
                    {
                        hasError = true;
                        Console.WriteLine($"The created entry for {repository} has the same number of entries but different labels than the stored diectionary");
                        Console.WriteLine(string.Format("created dictionary label {0}", string.Join(", ", repoLabelDict[repository])));
                        Console.WriteLine(string.Format("stored dictionary label {0}", string.Join(", ", storedRepoLabelDict[repository])));
                        // After the differences are reported for this repository entry, move on to the next outer loop iteration
                        break;
                    }
                }
            }
            return !hasError;
        }
    }
}
