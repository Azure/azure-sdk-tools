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

        private string ResultsFile { get
            {
                return Path.Combine(WorkingDirectory, "output.json");
            }
        }

        public GitProcessHandler handler { get; set; } = new GitProcessHandler();

        public AssetsScanner(string workingDirectory) {
            WorkingDirectory = workingDirectory;
        }

        public AssetsScanner()
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        public AssetsResultSet Scan(RunConfiguration config)
        {
            var resultSet = new List<AssetsResult>();
            var existingResults = ParseExistingResults();

            Parallel.ForEach(config.Repos, repoConfig =>
            {
                resultSet.AddRange(ScanRepo(repoConfig, existingResults));
            });

            var newResults = new AssetsResultSet(resultSet, existingResults);

            Save(newResults);

            return newResults;
        }

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
                if(!Directory.Exists(workingDirectory))
                {
                    Directory.CreateDirectory(workingDirectory);
                }

                foreach(var branch in config.Branches)
                {
                    var commitsOnBranch = GetBranchCommits(targetRepoUri, branch, config.ScanStartDate, workingDirectory);
                    var unretrievedCommits = ResolveUnhandledCommits(commitsOnBranch, previousOutput);

                    results.AddRange(GetAssetsResults(config.Repo, unretrievedCommits, workingDirectory));

                    if (previousOutput != null)
                    {
                        foreach (var commit in commitsOnBranch.Where(x => !unretrievedCommits.Contains(x)))
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
                if (!Directory.Exists(Path.Combine(workingDirectory, ".git"))) {
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
            catch(GitProcessException gitException)
            {
                // special case handling here?
                Console.WriteLine(gitException.ToString());
                Environment.Exit(1);
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
                Environment.Exit(1);
            }

            return commitSHAs;
        }

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

        private Assets? ExtractAssetsData(string assetsJson)
        {
            return JsonSerializer.Deserialize<Assets>(File.ReadAllText(assetsJson));
        }

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

        private void Cleanup(string workingDirectory)
        {
            try
            {
                handler.Run("clean -xdf", workingDirectory);
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

        private void CleanupWorkingDirectory(string workingDirectory)
        {
            var possibleGitDir = Path.Combine(workingDirectory, ".git");

            if (Directory.Exists(possibleGitDir))
            {
                SetPermissionsAndDelete(possibleGitDir);
            }

            Directory.Delete(workingDirectory, true);
        }

        private void Save(AssetsResultSet newResults)
        {
            using (var stream = System.IO.File.OpenWrite(ResultsFile))
            {
                stream.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(newResults.Results)));
            }
        }
    }
}
