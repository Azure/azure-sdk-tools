using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Azure.Sdk.Tools.Assets.MaintenanceTool.Model;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Store;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Scan
{
    /// <summary>
    /// Used to walk through repo configurations and locate all assets.
    /// </summary>
    public class AssetsScanner
    {
        public string WorkingDirectory { get; set; }
        public static readonly string GIT_TOKEN_ENV_VAR = "GIT_TOKEN";

        private string ResultsFile 
            => Path.Combine(WorkingDirectory, "output.json");

        public GitProcessHandler handler { get; set; } = new GitProcessHandler();

        public AssetsScanner(string workingDirectory)
        {
            WorkingDirectory = workingDirectory;
        }

        public AssetsScanner()
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        /// <summary>
        /// Walk a run configuration an create a resultSet of all found assets.json references.
        /// 
        /// This function automatically takes previous output into account by checking in the current
        /// working directory for an "output.json" file.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public AssetsResultSet Scan(RunConfiguration config)
        {
            var resultSet = new List<AssetsResult>();
            var existingResults = ParseExistingResults();

            Parallel.ForEach(config.Repos, repoConfig =>
            {
                resultSet.AddRange(ScanRepo(repoConfig, existingResults));
            });

            var newResults = new AssetsResultSet(resultSet);

            Save(newResults);

            return newResults;
        }

        /// <summary>
        /// If the tool is invoked in a directory containing an `"output.json" file, that file will be parsed
        /// for it's results. The file itself is merely a List of type AssetsResult serialized to disk.
        /// </summary>
        /// <returns></returns>
        public AssetsResultSet? ParseExistingResults()
        {
            if (File.Exists(ResultsFile))
            {
                using var stream = System.IO.File.OpenRead(ResultsFile);
                using var doc = JsonDocument.Parse(stream);

                var results = JsonSerializer.Deserialize<List<AssetsResult>>(doc);

                if (results != null)
                {
                    return new AssetsResultSet(results);
                }
            }

            return null;
        }

        /// <summary>
        /// Given a repo configuration, scan the repo and return the assetsresults from all targeted branches.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="previousOutput"></param>
        /// <returns></returns>
        private List<AssetsResult> ScanRepo(RepoConfiguration config, AssetsResultSet? previousOutput)
        {
            string? envOverride = Environment.GetEnvironmentVariable(GIT_TOKEN_ENV_VAR);
            var authString = string.Empty;
            if (!string.IsNullOrWhiteSpace(envOverride))
            {
                authString = $"{envOverride}@";
            }

            var targetRepoUri = $"https://{authString}github.com/{config.Repo}.git";
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var results = new List<AssetsResult>();

            try
            {
                if (!Directory.Exists(workingDirectory))
                {
                    Directory.CreateDirectory(workingDirectory);
                }

                foreach (var branch in config.Branches)
                {
                    var commitsOnBranch = GetBranchCommits(targetRepoUri, branch, config.ScanStartDate, workingDirectory);
                    var unretrievedCommits = ResolveUnhandledCommits(commitsOnBranch, previousOutput);

                    results.AddRange(GetAssetsResults(config.Repo, unretrievedCommits, workingDirectory));

                    if (previousOutput != null)
                    {
                        foreach (var commit in commitsOnBranch.Where(commit => !unretrievedCommits.Contains(commit)))
                        {
                            results.AddRange(previousOutput.ByOriginSHA[commit]);
                        }
                    }
                }
            }
            finally
            {
                CleanupWorkingDirectory(workingDirectory);
            }

            return results;
        }

        /// <summary>
        /// Clones a specific branch, then returns all commit shas newer than our targeted date.
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="branch"></param>
        /// <param name="workingDirectory"></param>
        /// <returns></returns>
        private List<string> GetBranchCommits(string uri, string branch, DateTime since, string workingDirectory)
        {
            var commitSHAs = new List<string>();
            try
            {
                // if git is already initialized, we just need to checkout a specific branch
                if (!Directory.Exists(Path.Combine(workingDirectory, ".git")))
                {
                    handler.Run($"clone {uri} --branch {branch} --single-branch .", workingDirectory);
                }
                else
                {
                    handler.Run($"fetch origin {branch}", workingDirectory);
                    handler.Run($"branch {branch} FETCH_HEAD", workingDirectory);
                    handler.Run($"checkout {branch}", workingDirectory);
                    Cleanup(workingDirectory);
                }

                var tagResult = handler.Run($"log --since={since.ToString("yyyy-MM-dd")} --format=format:%H", workingDirectory);
                commitSHAs.AddRange(tagResult.StdOut.Split(Environment.NewLine).Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)));
            }
            catch (GitProcessException gitException)
            {
                // special case handling here?
                Console.WriteLine(gitException.ToString());
                Environment.Exit(1);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Environment.Exit(1);
            }

            return commitSHAs;
        }

        /// <summary>
        /// We only need to process each commit _once_, as commit SHAs are immutable in git. Given that, once we have
        /// a list of commits from a targeted branch, we need to check against the previous results to ensure we don't 
        /// reprocess those and emit duplicate assetsResults.
        /// 
        /// This function completes that operation and returns the set of unprocessed commit SHAs.
        /// </summary>
        /// <param name="commits"></param>
        /// <param name="previousResults"></param>
        /// <returns></returns>
        private List<string> ResolveUnhandledCommits(List<string> commits, AssetsResultSet? previousResults)
        {
            if (previousResults == null)
            {
                return commits;
            }
            else
            {
                return commits.Where(x => !previousResults.ByOriginSHA.ContainsKey(x)).ToList();
            }
        }

        /// <summary>
        /// Used to easily parse an assets.json and grab only the properties that this tool cares about.
        /// </summary>
        private class Assets
        {
            public Assets()
            {
                AssetsRepo = string.Empty;
                Tag = string.Empty;
            }

            public string AssetsRepo { get; set; }

            public string Tag { get; set; }
        }

        /// <summary>
        /// Deserialize an assets.json from disk into a class instance to retrieve the targeted Tag and Assets Repository.
        /// </summary>
        /// <param name="assetsJson"></param>
        /// <returns></returns>
        private Assets? ExtractAssetsData(string assetsJson)
        {
            return JsonSerializer.Deserialize<Assets>(File.ReadAllText(assetsJson));
        }

        /// <summary>
        /// Find all assets.jsons beneath a targeted folder. Return AssetsResults for each, populating
        /// other metadata as necessary.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="commit"></param>
        /// <param name="workingDirectory"></param>
        /// <returns></returns>
        private List<AssetsResult> ScanDirectory(string repo, string commit, string workingDirectory)
        {
            Matcher matcher = new();
            List<AssetsResult> locatedAssets = new List<AssetsResult>();
            matcher.AddIncludePatterns(new[] { "**/assets.json" });
            IEnumerable<string> assetsJsons = matcher.GetResultsInFullPath(workingDirectory);

            foreach (var assetsJson in assetsJsons)
            {
                var path = Path.GetRelativePath(workingDirectory, assetsJson).Replace("\\", "/");
                var assetsData = ExtractAssetsData(assetsJson);

                if (assetsData != null)
                {
                    var newResult = new AssetsResult(repo, commit, path, assetsData.Tag, assetsData.AssetsRepo, null);
                    locatedAssets.Add(newResult);
                }
            }

            return locatedAssets;
        }

        /// <summary>
        /// Walks a set of targeted commits, extracting all available assets.jsons from each.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="commits"></param>
        /// <param name="workingDirectory"></param>
        /// <returns></returns>
        private List<AssetsResult> GetAssetsResults(string repo, List<string> commits, string workingDirectory)
        {
            var allResults = new List<AssetsResult>();
            foreach (var commit in commits)
            {
                handler.Run($"checkout {commit}", workingDirectory);
                Cleanup(workingDirectory);
                allResults.AddRange(ScanDirectory(repo, commit, workingDirectory));
            }

            return allResults;
        }

        /// <summary>
        /// Cleans up a git repo. When swapping between commits, we don't want to accidentally include assets.jsons that are
        /// present simply because a folder didn't auto delete itself when we switched commits.
        /// </summary>
        /// <param name="workingDirectory"></param>
        private void Cleanup(string workingDirectory)
        {
            try
            {
                handler.Run("clean -xdf", workingDirectory);
            }
            catch (GitProcessException gitException)
            {
                Console.WriteLine(gitException.ToString());
                Environment.Exit(1);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Intended to be aimed at a specific .git folder. Walks every file and ensures that
        /// any wonky permissions that could prevent deletion are removed.
        /// 
        /// This is necessary because certain `.pack` files created by git cannot be deleted without
        /// adjusting these permissions.
        /// </summary>
        /// <param name="gitfolder"></param>
        private void SetPermissionsAndDelete(string gitfolder)
        {
            File.SetAttributes(gitfolder, FileAttributes.Normal);

            string[] files = Directory.GetFiles(gitfolder);
            string[] dirs = Directory.GetDirectories(gitfolder);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                SetPermissionsAndDelete(dir);
            }

            Directory.Delete(gitfolder, false);
        }

        /// <summary>
        /// The .git folder's .pack files can be super finicky to delete from code.
        /// This function abstracts the necessary permissions update and cleans that folder for us.
        /// </summary>
        /// <param name="workingDirectory"></param>
        private void CleanupWorkingDirectory(string workingDirectory)
        {
            var possibleGitDir = Path.Combine(workingDirectory, ".git");

            if (Directory.Exists(possibleGitDir))
            {
                SetPermissionsAndDelete(possibleGitDir);
            }

            Directory.Delete(workingDirectory, true);
        }

        /// <summary>
        /// Writes a resultSet to disk.
        /// </summary>
        /// <param name="newResults"></param>
        private void Save(AssetsResultSet newResults)
        {
            using (var stream = System.IO.File.OpenWrite(ResultsFile))
            {
                stream.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(newResults.Results)));
            }
        }
    }
}
