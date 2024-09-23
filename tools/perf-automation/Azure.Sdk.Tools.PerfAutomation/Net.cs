using Azure.Sdk.Tools.PerfAutomation.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public class Net : LanguageBase
    {
        protected override Language Language => Language.Net;

        private string CoreTestFrameworkProjectFile =>
            Path.Combine(WorkingDirectory, "sdk", "core", "Azure.Core.TestFramework", "src", "Azure.Core.TestFramework.csproj");

        // Azure.Core.TestFramework.TestEnvironment requires publishing under the "artifacts" folder to find the repository root.
        private string PublishDirectory => Path.Join(WorkingDirectory, "artifacts", "perf");

        public override async Task<(string output, string error, object context)> SetupAsync(
            string project,
            string languageVersion,
            string primaryPackage,
            IDictionary<string, string> packageVersions,
            bool debug)
        {
            var projectFile = Path.Combine(WorkingDirectory, project);

            await UpdatePackageVersions(projectFile, packageVersions);

            // All perf projects reference Azure.Core.TestFramework.  Update it to ensure consistent versions.
            await UpdatePackageVersions(CoreTestFrameworkProjectFile, packageVersions);

            Util.DeleteIfExists(PublishDirectory);

            var additionalBuildArguments = "";
            if (packageVersions.Values.Contains(Program.PackageVersionSource))
            {
                // Force all transitive dependencies to use project references, to ensure all packages are built from source.
                // The default is for transitive dependencies to use package references to the latest published version.
                additionalBuildArguments += "-p:UseProjectReferenceToAzureClients=true";
            }

            // Disable source link, since it's not needed for perf runs, and also fails in sparse checkout repos
            var processArguments = $"publish -c release -f net{languageVersion}.0 -o {PublishDirectory} -p:EnableSourceLink=false {additionalBuildArguments} {project}";

            var result = await Util.RunAsync("dotnet", processArguments, workingDirectory: WorkingDirectory);

            return (result.StandardOutput, result.StandardError, null);
        }

        private static async Task UpdatePackageVersions(string projectFile, IDictionary<string, string> packageVersions)
        {
            File.Copy(projectFile, projectFile + ".bak", overwrite: true);

            var projectContents = File.ReadAllText(projectFile);

            foreach (var v in packageVersions)
            {
                var packageName = v.Key;
                var packageVersion = v.Value;

                if (packageVersion != Program.PackageVersionSource) {
                    // TODO: Use XmlDocument instead of Regex

                    // Existing reference might be to package or project:
                    // - <PackageReference Include="Microsoft.Azure.Storage.Blob" />
                    // - <ProjectReference Include="$(MSBuildThisFileDirectory)..\..\src\Azure.Storage.Blobs.csproj" />

                    string pattern;
                    var packageReferencePattern = $"<PackageReference [^>]*{packageName}[^<]*/>";
                    var projectReferencePattern = $"<ProjectReference [^>]*{packageName}.csproj[^<]*/>";

                    if (Regex.IsMatch(projectContents, packageReferencePattern))
                    {
                        pattern = packageReferencePattern;
                    }
                    else if (Regex.IsMatch(projectContents, projectReferencePattern))
                    {
                        pattern = projectReferencePattern;
                    }
                    else
                    {
                        continue;
                    }

                    projectContents = Regex.Replace(
                        projectContents,
                        pattern,
                        @$"<PackageReference Include=""{packageName}"" VersionOverride=""{packageVersion}"" />",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline
                    );
                }
            }

            await File.WriteAllTextAsync(projectFile, projectContents);
        }

        public override async Task<IterationResult> RunAsync(
            string project,
            string languageVersion,
            string primaryPackage,
            IDictionary<string, string> packageVersions,
            string testName,
            string arguments,
            bool profile,
            string profilerOptions,
            object context)
        {
            var dllName = Path.GetFileNameWithoutExtension(project) + ".dll";
            var dllPath = Path.Combine(PublishDirectory, dllName);

            var result = profile
                ? await Util.RunAsync("dotnet-trace", $"collect --format NetTrace --show-child-io --output {GetProfileOutputFile(testName, arguments)} -- dotnet {dllPath} {testName} {arguments}", WorkingDirectory, throwOnError: false)
                : await Util.RunAsync("dotnet", $"{dllPath} {testName} {arguments}", WorkingDirectory, throwOnError: false);

            // Completed 693,696 operations in a weighted-average of 1.00s (692,328.31 ops/s, 0.000 s/op)
            var match = Regex.Match(result.StandardOutput, @"\((.*) ops/s", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

            var opsPerSecond = -1d;
            if (match.Success)
            {
                opsPerSecond = double.Parse(match.Groups[1].Value);
            }

            return new IterationResult
            {
                PackageVersions = GetRuntimePackageVersions(result.StandardOutput),
                OperationsPerSecond = opsPerSecond,
                StandardError = result.StandardError,
                StandardOutput = result.StandardOutput,
            };
        }

        private string GetProfileOutputFile(string testName, string arguments)
        {
            var fileName = $"{testName}_{arguments.Replace(" --", "_").Replace("--", "_").Replace(" ", "-")}_{DateTime.Now.ToString("yyyyMMdd_hhmmss")}.nettrace";
            return Path.GetFullPath(Path.Combine(Util.GetProfileDirectory(WorkingDirectory), fileName));
        }

        // === Versions ===
        // Runtime:         3.1.27
        // Azure.Core:
        //   Referenced:    1.25.0.0
        //   Loaded:        1.25.0.0
        //   Informational: 1.25.0+c8aaee521e662ddfb238d5ad1f2f9a79233f97f6
        //   JITOptimizer:  Enabled
        // Azure.Storage.Blobs:
        //   Referenced:    12.13.0.0
        //   Loaded:        12.13.0.0
        //   Informational: 12.13.0+dd17f33e411562517144e4b6b16f5ea910e5c5ae
        //   JITOptimizer:  Enabled
        // Azure.Storage.Blobs.Perf:
        //   Loaded:        1.0.0.0
        //   Informational: 1.0.0-alpha.20220719.3+5e7750d5d3d4754b657da8430ea805591522c43b
        //   JITOptimizer:  Enabled
        // Azure.Storage.Common:
        //   Referenced:    12.12.0.0
        //   Loaded:        12.12.0.0
        //   Informational: 12.12.0+dd17f33e411562517144e4b6b16f5ea910e5c5ae
        //   JITOptimizer:  Enabled
        // Azure.Test.Perf:
        //   Referenced:    1.0.0.0
        //   Loaded:        1.0.0.0
        //   Informational: 1.0.0-alpha.20220719.3+5e7750d5d3d4754b657da8430ea805591522c43b
        //   JITOptimizer:  Enabled
        public static Dictionary<string, string> GetRuntimePackageVersions(string standardOutput)
        {
            var runtimePackageVersions = new Dictionary<string, string>();

            var versionOutputStart = standardOutput.LastIndexOf("=== Versions ===", StringComparison.OrdinalIgnoreCase);
            if (versionOutputStart == -1)
            {
                return runtimePackageVersions;
            }

            var versionOutput = standardOutput[versionOutputStart..];

            var matches = Regex.Matches(versionOutput, @"(Azure.*?):.*?Informational: (\S*)", RegexOptions.Singleline);
            foreach (Match match in matches)
            {
                runtimePackageVersions.Add(match.Groups[1].Value, match.Groups[2].Value);
            }

            return runtimePackageVersions;
        }

        public override IDictionary<string, string> FilterRuntimePackageVersions(IDictionary<string, string> runtimePackageVersions)
        {
            // Ignore packages ending with ".Perf", to only show versions of shipping packages
            return runtimePackageVersions?
                .Where(kvp => !kvp.Key.EndsWith(".Perf", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public override Task CleanupAsync(string project)
        {
            Util.DeleteIfExists(PublishDirectory);
            RestoreBackup(CoreTestFrameworkProjectFile);
            RestoreBackup(Path.Combine(WorkingDirectory, project));
            return Task.CompletedTask;
        }

        private static void RestoreBackup(string projectFile)
        {
            File.Move(projectFile + ".bak", projectFile, overwrite: true);
        }
    }
}
