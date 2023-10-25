// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.SDK.ChangelogGen.Compare;
using Azure.SDK.ChangelogGen.Report;
using Azure.SDK.ChangelogGen.Utilities;
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
        public string ReleaseVersion { get; private set; }
        public string ReleaseDate { get; private set; }
        public bool UpdateReleaseVersionDate { get; private set; }

        public string ApiFile { get; private set; }
        public string ApiFileGithubKey => GetGithubKey(ApiFile);

        public string BaselineVersion { get; private set; }
        public string BaselineVersionReleaseDate { get; private set; }
        public string BaselineGithubTag { get; private set; }

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

        public string AzureResourceManagerChangeLogGithubKey => "sdk/resourcemanager/Azure.ResourceManager/CHANGELOG.md";
        public string AzureResourceManagerChangeLogMdFile => Path.Combine(RepoRoot, AzureResourceManagerChangeLogGithubKey);

        public bool IsPreview => Helper.IsPreviewRelease(this.ReleaseVersion);

        public List<ChangeCatogory> ApiChangeFilter { get; private set; }
        public List<Release> ReleasesInChangelog { get; private set; }

        public Release CurRelease => this.ReleasesInChangelog[0];

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions() { WriteIndented = true });
        }

        public bool Init(string[] args)
        {
            if (args == null || args.Length != 3)
                throw new ArgumentException($"Invalid command argumens: {string.Join(" ", args)}");

            var builder = new ConfigurationBuilder()
                .SetBasePath(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            var settings = builder.Build().GetSection("settings");

            this.ApiFile = string.IsNullOrEmpty(args[0]) ? settings["apiFile"] : args[0];
            this.ReleaseVersion = string.IsNullOrEmpty(args[1]) ? settings["releaseVersion"] : args[1];
            this.ReleaseDate = string.IsNullOrEmpty(args[2]) ? settings["releaseDate"] : args[2];

            this.ApiFile = Path.GetFullPath(this.ApiFile!);
            if (!File.Exists(this.ApiFile))
            {
                Logger.Error("Given ApiFile doesn't exist: " + this.ApiFile);
                return false;
            }

            if (!Regex.IsMatch(this.ReleaseDate, @"^\d{4}-\d{2}-\d{2}$"))
            {
                Logger.Error($"Unexpected format of release date {this.ReleaseDate}. Expected format: xxxx-xx-xx, i.e. 2022-01-01");
                return false;
            }

            this.BaselineVersion = settings["baseline"] ?? "";
            this.OverwriteChangeLogMdFile = bool.Parse(settings["overwriteChangeLogMdFile"] ?? "true");
            this.MergeMode = Enum.Parse<MergeMode>(settings["mergeMode"] ?? "Group", true);
            this.LogSettings = bool.Parse(settings["logSettings"] ?? "false");
            // apiChangeFilter is expected in format like Obsoleted | Added | Removed | ...
            this.ApiChangeFilter = settings["apiChangeFilter"].Split("|", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(v => Enum.Parse<ChangeCatogory>(v, true)).ToList();
            this.UpdateReleaseVersionDate = bool.Parse(settings["updateReleaseVersionDate"] ?? "true");
            this.RepoRoot = FindRepoRoot();
            this.PackageDir = new DirectoryInfo(Path.Combine(Path.GetDirectoryName(this.ApiFile)!, ".."));

            string changelogContent = File.ReadAllText(ChangeLogMdFile);
            this.ReleasesInChangelog = Release.FromChangelog(changelogContent);
            if (this.ReleasesInChangelog.Count == 0)
                throw new InvalidDataException("No release found in changelog.md. At least one Release (Unreleased) expected");
            if (this.ReleasesInChangelog[0].ReleaseDate != "Unreleased")
            {
                if (this.ReleasesInChangelog[0].Version != this.ReleaseVersion)
                {
                    throw new InvalidDataException($"The last release ('{this.ReleasesInChangelog[0].ReleaseDate}') in changelog.md is expected to be marked as 'Unreleased' or the same as the given version.");
                }
                else
                {
                    Logger.Warning($"Last release version in changelog.md is the same as given one. Generated changelog will be merged to it: {this.ReleaseVersion}");
                }
            }

            if (string.IsNullOrEmpty(BaselineVersion))
            {
                // Only consider preview release as baseline only when current release is a preview release
                var lastRelease = this.ReleasesInChangelog.Skip(1).FirstOrDefault(r => IsPreview || !Helper.IsPreviewRelease(r.Version));
                if (lastRelease != null)
                {
                    BaselineVersion = lastRelease.Version;
                    BaselineVersionReleaseDate = lastRelease.ReleaseDate;
                }
                else
                {
                    Logger.Warning($"No baseline found and exit without doing anything which means current release is the first {(IsPreview ? "" : "stable")} release whose changelog is expected to be drafted manually.");
                    return false;
                }
            }
            BaselineGithubTag = $"{PackageName}_{BaselineVersion}";
            return true;
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
    }
}

