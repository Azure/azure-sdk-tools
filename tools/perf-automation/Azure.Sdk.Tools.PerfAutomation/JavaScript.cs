using Azure.Sdk.Tools.PerfAutomation.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public class JavaScript : LanguageBase
    {
        private const string _rush = "common/scripts/install-run-rush.js";

        protected override Language Language => Language.JS;

        public override async Task<(string output, string error, string context)> SetupAsync(
            string project, string languageVersion, IDictionary<string, string> packageVersions)
        {
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            var commonVersionsFile = Path.Combine(WorkingDirectory, "common", "config", "rush", "common-versions.json");
            var commonVersionsJson = JObject.Parse(File.ReadAllText(commonVersionsFile));

            var projectDirectory = Path.Combine(WorkingDirectory, project);
            var projectFile = Path.Combine(projectDirectory, "package.json");
            var projectJson = JObject.Parse(File.ReadAllText(projectFile));

            var track1 = projectDirectory.EndsWith("track-1", StringComparison.OrdinalIgnoreCase);
            var track2 = !track1;

            // Track 1
            // 1. If not source, modify package.json
            // 2. rush update (OR install)
            // 3. [projectDir] npm run setup
            //
            // Track 2
            // 1. If not source, modify package.json and common-versions.json
            // 2. rush update
            // 3. Extract project name from package.json in project folder
            // 4. rush build -t projectName

            foreach (var v in packageVersions)
            {
                var packageName = v.Key;
                var packageVersion = v.Value;

                if (packageVersion != Program.PackageVersionSource)
                {
                    foreach (var dependencyType in new string[] { "dependencies", "devDependencies" })
                    {
                        if (projectJson[dependencyType]?[packageName] != null)
                        {
                            projectJson[dependencyType][packageName] = packageVersion;
                        }
                    }

                    if (track2)
                    {
                        if (commonVersionsJson["allowedAlternativeVersions"]?[packageName] != null)
                        {
                            ((JArray)commonVersionsJson["allowedAlternativeVersions"][packageName]).Add(packageVersion);
                        }
                        else
                        {
                            commonVersionsJson["allowedAlternativeVersions"][packageName] = new JArray(packageVersion);
                        }
                    }
                }
            }

            File.Copy(commonVersionsFile, commonVersionsFile + ".bak", overwrite: true);
            File.WriteAllText(commonVersionsFile, commonVersionsJson.ToString() + Environment.NewLine);

            File.Copy(projectFile, projectFile + ".bak", overwrite: true);
            File.WriteAllText(projectFile, projectJson.ToString() + Environment.NewLine);

            await Util.RunAsync("node", $"{_rush} update", WorkingDirectory, outputBuilder: outputBuilder, errorBuilder: errorBuilder);

            if (track1)
            {
                await Util.RunAsync("npm", "run setup", projectDirectory,
                    outputBuilder: outputBuilder, errorBuilder: errorBuilder);
            }
            else
            {
                var projectName = projectJson["name"];
                await Util.RunAsync("node", $"{_rush} build --to {projectName}", WorkingDirectory,
                    outputBuilder: outputBuilder, errorBuilder: errorBuilder);
            }

            return (outputBuilder.ToString(), errorBuilder.ToString(), null);
        }

        public override async Task<IterationResult> RunAsync(string project, string languageVersion,
            IDictionary<string, string> packageVersions, string testName, string arguments, string context)
        {
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            var projectDirectory = Path.Combine(WorkingDirectory, project);

            var testResult = await Util.RunAsync("npm", $"run perf-test:node -- {testName} --list-transitive-dependencies {arguments}",
                projectDirectory, outputBuilder: outputBuilder, errorBuilder: errorBuilder, throwOnError: false);

            var match = Regex.Match(testResult.StandardOutput, @"\((.*) ops/s", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

            var opsPerSecond = -1d;
            if (match.Success)
            {
                opsPerSecond = double.Parse(match.Groups[1].Value);
            }

            return new IterationResult
            {
                PackageVersions = GetRuntimePackageVersions(testResult.StandardOutput),
                OperationsPerSecond = opsPerSecond,
                StandardOutput = outputBuilder.ToString(),
                StandardError = errorBuilder.ToString(),
            };
        }

        // === Versions ===
        // @azure-tests/perf-storage-blob@1.0.0 /mnt/vss/_work/1/s/sdk/storage/perf-tests/storage-blob
        // ├── @azure/core-http@2.2.6 -> /mnt/vss/_work/1/s/sdk/core/core-http
        // ├── @azure/core-rest-pipeline@1.9.1 -> /mnt/vss/_work/1/s/sdk/core/core-rest-pipeline
        // ├── @azure/storage-blob@12.11.0 -> /mnt/vss/_work/1/s/common/temp/node_modules/.pnpm/@azure+storage-blob@12.11.0/node_modules/@azure/storage-blob
        // ├── @azure/test-utils-perf@1.0.0 -> /mnt/vss/_work/1/s/sdk/test-utils/perf            
        public static Dictionary<string, string> GetRuntimePackageVersions(string standardOutput)
        {
            var runtimePackageVersions = new Dictionary<string, List<string>>();

            var versionOutputStart = standardOutput.IndexOf("=== Versions ===", StringComparison.OrdinalIgnoreCase);
            var versionOutputEnd = standardOutput.IndexOf("=== Parsed options ===", StringComparison.OrdinalIgnoreCase);

            if (versionOutputStart == -1 | versionOutputEnd == -1)
            {
                return new Dictionary<string, string>();
            }

            var versionOutput = standardOutput.Substring(versionOutputStart, versionOutputEnd);

            foreach (var line in versionOutput.ToLines())
            {
                if (line.Contains("UNMET DEPENDENCY", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var match = Regex.Match(line, @"(@azure.*?)@(.*)$");
                if (match.Success)
                {
                    var name = match.Groups[1].Value;
                    var version = match.Groups[2].Value.Trim();

                    if (version.Contains("node_modules", StringComparison.OrdinalIgnoreCase))
                    {
                        version = version[..version.IndexOf(' ')];
                    }

                    version = version.Replace(" extraneous", string.Empty).Replace(" deduped", string.Empty);

                    if (!runtimePackageVersions.ContainsKey(name))
                    {
                        runtimePackageVersions[name] = new List<string>();
                    }
                    runtimePackageVersions[name].Add(version);
                }
            }

            foreach (var name in runtimePackageVersions.Keys)
            {
                IEnumerable<string> versions = runtimePackageVersions[name];

                // Remove duplicates
                versions = versions.Distinct();

                // Remove versions that are a substring of another version, to favor the more detailed version
                // Example: "1.2.3", "1.2.3 -> /mnt/vss/_work/1/s/sdk/core/core-http"
                versions = versions.Where(v1 => !versions.Any(v2 => v2.StartsWith(v1 + " "))).ToList();

                // Sort alphabetically (ideally would sort by semver, but should be close enough)
                versions = versions.OrderBy(v => v);

                runtimePackageVersions[name] = versions.ToList();
            }

            return runtimePackageVersions
                .Select(kvp => new KeyValuePair<string, string>(kvp.Key, string.Join(", ", kvp.Value)))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public override IDictionary<string, string> FilterRuntimePackageVersions(IDictionary<string, string> runtimePackageVersions)
        {
            return runtimePackageVersions?
                .Where(kvp => !kvp.Key.StartsWith("@azure-tests/", StringComparison.OrdinalIgnoreCase) &&
                              !kvp.Key.EndsWith("perf", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public override Task CleanupAsync(string project)
        {
            var projectDirectory = Path.Combine(WorkingDirectory, project);

            var commonVersionsFile = Path.Combine(WorkingDirectory, "common", "config", "rush", "common-versions.json");
            var projectFile = Path.Combine(projectDirectory, "package.json");

            // Restore backups
            File.Move(commonVersionsFile + ".bak", commonVersionsFile, overwrite: true);
            File.Move(projectFile + ".bak", projectFile, overwrite: true);

            return Task.CompletedTask;
        }
    }
}
