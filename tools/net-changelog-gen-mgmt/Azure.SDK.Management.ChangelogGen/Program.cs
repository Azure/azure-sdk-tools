// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using Azure.SDK.ChangelogGen.Compare;
using Azure.SDK.ChangelogGen.Report;
using Azure.SDK.ChangelogGen.Utilities;
using LibGit2Sharp;

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
                const string USAGE = "ChangeLogGen.exe apiFilePath releaseVersion releaseDate(xxxx-xx-xx)";
                if (args.Length == 0 || (args.Length == 1 && new string[] { "-h", "/h", "-help", "/help", "-?", "/?" }.Contains(args[0].ToLower())))
                {
                    Logger.Log($"Usage: {USAGE}");
                    return;
                }
                if (args.Length != 3)
                {
                    Logger.Error($"Invalid arguments. Expected Usage: {USAGE}");
                    return;
                }

                Context context = new Context();
                if (!context.Init(args))
                {
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
                    // we only compare api change for stable version
                    if (!context.IsPreview)
                    {
                        Logger.Log("Start checking api files");
                        Logger.Log("  Check ApiFile at: " + context.ApiFile);
                        Logger.Log("  Baseline: same file with Github tag " + context.BaselineGithubTag);
                        string curApiFileContent = File.ReadAllText(context.ApiFile);
                        string baseApiFileContent = baselineTree.GetFileContent(context.ApiFileGithubKey);
                        result.ApiChange = CompareApi(curApiFileContent, baseApiFileContent);
                    }
                    else
                    {
                        Logger.Log("Skip API comparison for preview version");
                    }

                    SpecHelper.ValidateTypeSpecInput(context.TypeSpecLocationFile, context.MetadataFile);
                    Logger.Log("Start checking TypeSpec API versions");
                    Logger.Log("  Check metadata.json at: " + context.MetadataFile);
                    Logger.Log("  Baseline: same file with Github tag " + context.BaselineGithubTag);
                    string curMetadata = File.ReadAllText(context.MetadataFile);
                    if (baselineTree.TryGetFileContent(context.MetadataGithubKey, out string baseMetadata))
                    {
                        result.SpecVersionChange = CompareApiVersions(curMetadata, baseMetadata);
                    }
                    else
                    {
                        Logger.Warning($"Skip TypeSpec API version comparison because metadata.json was not found in baseline tag {context.BaselineGithubTag}");
                    }

                    Logger.Log("Start checking Azure Core");
                    Logger.Log("  Check AzureCore changelog at: " + context.AzureCoreChangeLogMdFile);
                    Logger.Log("  Baseline: same file with Github tag " + context.BaselineGithubTag);
                    string curAzureCoreVersion = Helper.GetLastReleaseVersionFromFile(context.AzureCoreChangeLogMdFile, context.IsPreview, out string releaseDate);
                    string baseAzureCoreVersion = baselineTree.GetLastReleaseVersionFromGitTree(context.AzureCoreChangeLogGithubKey, context.IsPreview, out releaseDate);
                    result.AzureCoreVersionChange = CompareVersion(curAzureCoreVersion, baseAzureCoreVersion, "Azure.Core");

                    Logger.Log("Start checking Azure ResourceManager");
                    Logger.Log("  Check Azure.ResourceManager changelog at: " + context.AzureResourceManagerChangeLogMdFile);
                    Logger.Log("  Baseline: same file with Github tag " + context.BaselineGithubTag);
                    string curAzureRMVersion = Helper.GetLastReleaseVersionFromFile(context.AzureResourceManagerChangeLogMdFile, context.IsPreview, out releaseDate);
                    string baseAzureRMVersion = baselineTree.GetLastReleaseVersionFromGitTree(context.AzureResourceManagerChangeLogGithubKey, context.IsPreview, out releaseDate);
                    result.AzureResourceManagerVersionChange = CompareVersion(curAzureRMVersion, baseAzureRMVersion, "Azure.ResourceManager");
                }
                Release nextRelease = result.GenerateReleaseNote(context.ReleaseVersion, context.ReleaseDate, context.ApiChangeFilter);


                if (context.UpdateReleaseVersionDate)
                {
                    context.CurRelease.Version = context.ReleaseVersion;
                    context.CurRelease.ReleaseDate = context.ReleaseDate;
                }

                if (nextRelease.Groups.Count == 0)
                {
                    Logger.Warning("No change detected to generate release notes");
                }

                nextRelease.MergeTo(context.CurRelease, context.MergeMode);

                Logger.Warning($"Release Note generated as below: \r\n" + context.CurRelease.ToString());
                if (context.OverwriteChangeLogMdFile)
                {
                    string newChangelog = Release.ToChangeLog(context.ReleasesInChangelog);
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

        public static StringValueChange? CompareApiVersions(string curMetadata, string baseMetadata)
        {
            IReadOnlyDictionary<string, string> curApiVersions = SpecHelper.GetApiVersions(curMetadata);
            IReadOnlyDictionary<string, string> baseApiVersions = SpecHelper.GetApiVersions(baseMetadata);
            string curVersion = FormatApiVersions(curApiVersions);
            string baseVersion = FormatApiVersions(baseApiVersions);

            if (string.Equals(curVersion, baseVersion, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Log($"No change found in TypeSpec API versions: {baseVersion} -> {curVersion}");
                return null;
            }

            Logger.Log($"TypeSpec API version change detected: {baseVersion} -> {curVersion}");
            return new StringValueChange(curVersion, baseVersion,
                $"Upgraded TypeSpec API version from '{baseVersion}' to '{curVersion}'.");
        }

        private static string FormatApiVersions(IReadOnlyDictionary<string, string> apiVersions)
        {
            return string.Join(";", apiVersions.OrderBy(version => version.Key, StringComparer.OrdinalIgnoreCase)
                .Select(version => $"{version.Key}: {version.Value}"));
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
                typeof(Azure.Core.AzureLocation).Assembly.Location,
                typeof(System.ClientModel.Primitives.ModelReaderWriter).Assembly.Location,
                typeof(System.Net.IPAddress).Assembly.Location
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
