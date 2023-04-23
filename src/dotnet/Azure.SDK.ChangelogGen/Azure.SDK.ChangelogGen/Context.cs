// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable
using System.Reflection;
using System.Text.Json;
using Azure.SDK.ChangelogGen.Utilities;
using LibGit2Sharp;
using Microsoft.Extensions.Configuration;

namespace Azure.SDK.ChangelogGen
{
    public enum MergeMode
    {
        Group,
        Line,
        OverWrite,
    };

    public class Context
    {
        public string ApiFile { get; private set; }
        public string ApiFileGithubKey => GetGithubKey(ApiFile);

        public string BaselineVersion { get; private set; }
        public string BaselineVersionReleaseDate { get; private set; }
        public string BaselineGithubTag { get; private set; }

        public string BranchToFindBaseline { get; private set; }

        public string ChangeLogMdFile => Path.Combine(PackageFolder, "CHANGELOG.md");
        public bool OverwriteChangeLogMdFile { get; private set; }
        public MergeMode MergeMode { get; private set; }
        public bool LogSettings { get; private set; } = false;

        public string RepoRoot { get; private set; }

        private DirectoryInfo PackageDir { get; set; }
        public string PackageFolder => PackageDir.FullName;
        public string PackageName => PackageDir.Name;

        public string AutorestMdFile => Path.Combine(PackageFolder, "src/autorest.md");
        public string AutorestMdGithubKey => GetGithubKey(AutorestMdFile);

        public string AzureCoreChangeLogGithubKey => "sdk/core/Azure.Core/CHANGELOG.md";
        public string AzureCoreChangeLogMdFile => Path.Combine(RepoRoot, AzureCoreChangeLogGithubKey);

        public string AzureResourceManagerChangeLogGithubKey => "sdk/resourcemanager/Azure.ResourceManager/CHANGELOG.MD";
        public string AzureResourceManagerChangeLogMdFile => Path.Combine(RepoRoot, AzureResourceManagerChangeLogGithubKey);

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions() { WriteIndented = true });
        }

        public void Init(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            var settings = builder.Build().GetSection("settings");

            ApiFile = settings["apiFile"] ?? "";
            BaselineVersion = settings["baseline"] ?? "";
            BranchToFindBaseline = settings["branchToFindBaseline"];
            if (string.IsNullOrEmpty(BranchToFindBaseline))
                BranchToFindBaseline = "local";
            OverwriteChangeLogMdFile = bool.Parse(settings["overwriteChangeLogMdFile"] ?? "true");
            MergeMode = Enum.Parse<MergeMode>(settings["mergeMode"] ?? "Group", true);
            LogSettings = bool.Parse(settings["logSettings"] ?? "false");

            if (args.Length > 0 && !string.IsNullOrEmpty(args[0]))
                ApiFile = args[0];
            if (string.IsNullOrEmpty(ApiFile))
            {
                Logger.Log("Usage: ChangeLogGen.exe apiFilePath");
                throw new InvalidOperationException("Can't find apiFile in console argument or appSettings.json file");
            }
            ApiFile = Path.GetFullPath(ApiFile!);
            if (!File.Exists(ApiFile))
                throw new InvalidOperationException("Given ApiFile doesn't exist");
            this.PackageDir = new DirectoryInfo(Path.Combine(Path.GetDirectoryName(ApiFile)!, ".."));

            RepoRoot = FindRepoRoot();
            UpdateBaselineAndTag();
        }

        private string GetGithubKey(string fullPath)
        {
            return Path.GetRelativePath(RepoRoot, fullPath);
        }

        private string FindRepoRoot()
        {
            string repoFolder = Path.GetDirectoryName(ApiFile);
            while (true)
            {
                if (repoFolder == null)
                {
                    throw new InvalidOperationException($"Can't find repo root folder from {ApiFile}");
                }
                string gitFile = Path.Combine(repoFolder, ".git");
                if (Directory.Exists(gitFile))
                {
                    Logger.Log($"Repo root folder found at {repoFolder}");
                    break;
                }
                else
                {
                    repoFolder = Path.GetDirectoryName(repoFolder);
                }
            }
            return repoFolder;
        }

        private void UpdateBaselineAndTag()
        {
            if (string.IsNullOrEmpty(BaselineVersion))
            {
                if (string.IsNullOrEmpty(BranchToFindBaseline) || BranchToFindBaseline.Equals("local", StringComparison.OrdinalIgnoreCase))
                {
                    BaselineVersion = Helper.GetLastReleaseVersionFromFile(ChangeLogMdFile, out string releaseDate);
                    BaselineVersionReleaseDate = releaseDate;
                }
                else
                {
                    using (Repository repo = new Repository(RepoRoot))
                    {
                        Logger.Log($"Baseline undefined. Try to use the last release defined in changelog in {BranchToFindBaseline} branch");

                        string changelogFileKey = Path.GetRelativePath(RepoRoot, ChangeLogMdFile);
                        Branch baselineBranch = repo.GetBranch(BranchToFindBaseline);
                        BaselineVersion = baselineBranch.GetLastReleaseversionFromGitBranch(changelogFileKey, out string releaseDate);
                        BaselineVersionReleaseDate = releaseDate;
                    }
                }
            }
            BaselineGithubTag = $"{PackageName}_{BaselineVersion}";
        }
    }
}

