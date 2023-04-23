// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.RegularExpressions;
using Azure.SDK.ChangelogGen.Compare;
using Azure.SDK.ChangelogGen.Report;
using Azure.SDK.ChangelogGen.Utilities;
using LibGit2Sharp;
using Markdig.Parsers;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.SDK.ChangelogGen
{
    public class Program
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="args">apiFilePath baseApiVersion </param>
        /// <exception cref="InvalidOperationException"></exception>
        static void Main(string[] args)
        {
            try
            {
                if (args.Length == 1 && new string[] { "-h", "/h", "-help", "/help", "-?", "/?" }.Contains(args[0].ToLower()))
                {
                    Logger.Log("Usage: ChangeLogGen.exe apiFilePath");
                    return;
                }

                Context context = new Context();
                try
                {
                    context.Init(args);
                }
                catch (NoReleaseFoundException)
                {
                    Logger.Log("Exit without doing anything because this is the initial release(No release information found in the changelog.md)");
                    return;
                }

                if (context.LogSettings)
                    Logger.Log("Generating Changelog based on following settings: \n" + context.ToString());

                Logger.Warning("Please make sure followings (local branch and tags) are up-to-date: \n" +
                        $"  1. Api File: {context.ApiFile}\n" +
                        $"  2. Changelog: {context.ChangeLogMdFile}\n" +
                        $"  3. Azure.Core changelog: {context.AzureCoreChangeLogMdFile}\n" +
                        $"  4. Azure.ResourceManager changelog: {context.AzureResourceManagerChangeLogMdFile}\n" +
                        $"  5. Baseline release's tag/releasedate: {context.BaselineGithubTag}/{context.BaselineVersionReleaseDate}\n");

                ChangeLogResult result = new ChangeLogResult();
                using (Repository repo = new Repository(context.RepoRoot))
                {
                    Tree baselineTree = repo.GetTreeByTag(context.BaselineGithubTag);
                    Logger.Log("Start checking api files");
                    Logger.Log("  Check ApiFile at: " + context.ApiFile);
                    Logger.Log("  Baseline: same file with Github tag " + context.BaselineGithubTag);
                    string curApiFileContent = File.ReadAllText(context.ApiFile);
                    string baseApiFileContent = baselineTree.GetFileContent(context.ApiFileGithubKey);
                    result.ApiChange = CompareApi(curApiFileContent, baseApiFileContent);

                    Logger.Log("Start checking Azure Core");
                    Logger.Log("  Check AzureCore changelog at: " + context.AzureCoreChangeLogMdFile);
                    Logger.Log("  Baseline: same file with Github tag " + context.BaselineGithubTag);
                    string curAzureCoreVersion = Helper.GetLastReleaseVersionFromFile(context.AzureCoreChangeLogMdFile, out string releaseDate);
                    string baseAzureCoreVersion = baselineTree.GetLastReleaseVersionFromGitTree(context.AzureCoreChangeLogGithubKey, out releaseDate);
                    result.AzureCoreVersionChange = CompareVersion(curAzureCoreVersion, baseAzureCoreVersion, "Azure.Core");

                    Logger.Log("Start checking Azure ResourceManager");
                    Logger.Log("  Check Azure.ResourceManager changelog at: " + context.AzureResourceManagerChangeLogMdFile);
                    Logger.Log("  Baseline: same file with Github tag " + context.BaselineGithubTag);
                    string curAzureRMVersion = Helper.GetLastReleaseVersionFromFile(context.AzureResourceManagerChangeLogMdFile, out releaseDate);
                    string baseAzureRMVersion = baselineTree.GetLastReleaseVersionFromGitTree(context.AzureResourceManagerChangeLogGithubKey, out releaseDate);
                    result.AzureResourceManagerVersionChange = CompareVersion(curAzureRMVersion, baseAzureRMVersion, "Azure.ResourceManager");
                }
                Release nextRelease = result.GenerateReleaseNote();

                Logger.Log($"Load current changelog from {context.ChangeLogMdFile}");
                List<Release> releases = LoadReleasesFromChangelogMdFile(context);

                nextRelease.MergeTo(releases[0], context.MergeMode);

                Logger.Warning($"Release Note generated as below: \r\n" + releases[0].ToString());
                if (context.OverwriteChangeLogMdFile)
                {
                    string newChangelog = Release.ToChangeLog(releases);
                    File.WriteAllText(context.ChangeLogMdFile, newChangelog);
                    Logger.Log($"Changelog.md file updated at {context.ChangeLogMdFile}");
                }
                else
                {
                    Logger.Log("Skip update changelog.md file");
                }
            }
            catch (Exception e)
            {
                Logger.Error("Error occurs when generating changelog: \n" + e.Message);
                Logger.Error("Detail exception: \n" + e.ToString());
            }
        }

        private static List<Release> LoadReleasesFromChangelogMdFile(Context context)
        {
            string curChangelog = File.ReadAllText(context.ChangeLogMdFile);
            List<Release> releases = Release.FromChangelog(curChangelog);
            if (releases.Count == 0)
                throw new InvalidOperationException("At least one Release (Unreleased) expected in changelog");

            int lastReleaseIndex = releases.FindIndex(r => r.Version == context.BaselineVersion);
            if (lastReleaseIndex == -1)
                throw new InvalidOperationException("Can't find baseline release in the changelog: " + context.BaselineVersion);
            if (lastReleaseIndex == 0)
                throw new InvalidOperationException("Can't merge changelog to baseline release which is the last release info in the changelog");
            if (lastReleaseIndex > 1)
            {
                Logger.Warning($"{context.BaselineVersion} is not the last release in the changelog. Following release found after it: \r\n{releases.Take(lastReleaseIndex).Select(r => r.Version)}");
            }
            Logger.Log($"Generated changelog will be merged to {releases[0].Version}");
            return releases;
        }

        public static StringValueChange? CompareVersion(string curVersion, string baseVersion, string name)
        {
            if (string.Equals(curVersion, baseVersion, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Log($"No version change for {name}: {baseVersion} -> {curVersion}");
                return null;
            }
            else
            {
                var svc = new StringValueChange(curVersion, baseVersion, $"Upgraded {name} from {baseVersion} to {curVersion}");
                Logger.Log($"version change detected for {name}: {baseVersion} -> {curVersion}");
                return svc;
            }
        }

        public static string GetSpecVersion(string autorestMdContent)
        {
            var autorestMd = MarkdownParser.Parse(autorestMdContent);
            var codeGenConfig = autorestMd.LoadYaml();
            string specReadme = "";
            if (codeGenConfig.TryGetValue("require", out object? value))
            {
                string require = ((string)value!).Replace("\\", "/");
                const string SPEC_PREFIX_BLOB = @"https://github.com/Azure/azure-rest-api-specs/blob/";
                const string SPEC_PREFIX_TREE = @"https://github.com/Azure/azure-rest-api-specs/tree/";
                const string SPEC_RAW_PREFIX = @"https://raw.githubusercontent.com/Azure/azure-rest-api-specs/";
                string webPath = "";
                if (require.StartsWith(SPEC_RAW_PREFIX))
                    webPath = require;
                else if(require.StartsWith(SPEC_PREFIX_BLOB))
                    webPath = SPEC_RAW_PREFIX + require.Substring(SPEC_PREFIX_BLOB.Length);
                else if(require.StartsWith(SPEC_PREFIX_TREE))
                    webPath = SPEC_RAW_PREFIX + require.Substring(SPEC_PREFIX_TREE.Length);

                if(!string.IsNullOrEmpty(webPath))
                {
                    using (HttpClient hc = new HttpClient())
                    {
                        specReadme = hc.GetStringAsync(webPath).Result;
                    }
                }
                else
                {
                    if (File.Exists(require))
                    {
                        specReadme = File.ReadAllText(require);
                    }
                }
            }

            Dictionary<string, object> specConfig = codeGenConfig;
            if (!string.IsNullOrEmpty(specReadme))
            {
                var specMd = MarkdownParser.Parse(specReadme);
                specConfig = specMd.LoadYaml();
            }
            else
            {
                Logger.Log("No readme info found in Require in autorest.md. Try to parse input-file info from autorest.md directly");
            }
            if (!specConfig.TryGetValue("input-file", out object? arr) || arr == null || arr is not IEnumerable<object>)
            {
                throw new InvalidOperationException("Failed to get input-file from spec readme.md");
            }

            //version in format 2020-10-01 or 2020-10-01-preview
            Regex regVersion = new Regex(@"/(?<ver>\d{4}\-\d{2}-\d{2}(-preview)?)/");
            IEnumerable<object> specFiles = (IEnumerable<object>)arr;
            var biggestMatch = specFiles.SelectMany(sf => regVersion.Matches(sf.ToString() ?? "")).OrderBy(s => s.Value).First();
            var foundVersion = biggestMatch.Groups["ver"].Value;
            return foundVersion;
        }

        private static StringValueChange? CompareSpecVersion(string curAutorestMd, string baseAutorestMd)
        {
            string curVersion = GetSpecVersion(curAutorestMd);
            string baselineVersion = GetSpecVersion(baseAutorestMd);

            if (string.Equals(curVersion, baselineVersion, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Log($"No change found in Spec Version: {baselineVersion} -> {curVersion}");
                return null;
            }
            else
            {
                Logger.Log($"Spec Version change detected: {baselineVersion} -> {curVersion}");
                return new StringValueChange(curVersion, baselineVersion, $"Upgraded API version from {baselineVersion} to {curVersion}");
            }
        }

        public static ChangeSet CompareApi(string curApiFileContent, string baselineApiFileContent)
        {
            string netRuntimePath = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
            // TODO: make sure all the needed dll are listed here
            List<string> runtimeRefs = new List<string>()
            {
                typeof(BinaryData).Assembly.Location,
                Path.Combine(netRuntimePath, "System.Runtime.dll")
            };
            List<string> azureRefs = new List<string>()
            {
                typeof(Azure.ResourceManager.ArmClient).Assembly.Location,
                typeof(Azure.Core.AzureLocation).Assembly.Location
            };
            List<string> allRefs = runtimeRefs.Concat(azureRefs).ToList();
            Assembly curApi = CompileHelper.Compile("curApi.dll", curApiFileContent, allRefs);
            Assembly baseApi = CompileHelper.Compile("baseApi.dll", baselineApiFileContent, allRefs);

            ApiComparer comparer = new ApiComparer(curApi, baseApi);
            var r = comparer.Compare();
            Logger.Log($"{r.Changes.Count} changes found in API");
            return r;
        }


    }
}
