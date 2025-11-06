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
        protected override Language Language => Language.JS;
        private static int profileCount = 0;

        public override async Task<(string output, string error, object context)> SetupAsync(
            string project,
            string languageVersion,
            string primaryPackage,
            IDictionary<string, string> packageVersions,
            bool debug)
        {
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            string rootPackageJsonFile = Path.Combine(WorkingDirectory, "package.json");
            var rootPackageJson = JObject.Parse(File.ReadAllText(rootPackageJsonFile));

            var projectDirectory = Path.Combine(WorkingDirectory, project);
            var projectFile = Path.Combine(projectDirectory, "package.json");
            var projectJson = JObject.Parse(File.ReadAllText(projectFile));

            var testUtilsProjectFile = Path.Combine(WorkingDirectory, "sdk", "test-utils", "perf", "package.json");
            var testUtilsProjectJson = JObject.Parse(File.ReadAllText(testUtilsProjectFile));


            // 1. Modify package.json
            // 2. pnpm install --no-frozen-lockfile
            // 3. Extract project name from package.json in project folder
            // 4. pnpm build --filter projectName...
            // 5. Get runtime package versions
            //    A. pnpm --filter projectName deploy --prod --legacy <deployDirectory>
            //    C. npm ls --all

            foreach (var v in packageVersions)
            {
                var packageName = v.Key;
                var packageVersion = v.Value;

                if (packageVersion != Program.PackageVersionSource)
                {
                    rootPackageJson["pnpm"]["overrides"][packageName] = packageVersion;

                    foreach (var packageJson in new JObject[] { projectJson, testUtilsProjectJson })
                    {
                        foreach (var dependencyType in new string[] { "dependencies", "devDependencies" })
                        {
                            // Only update existing dependencies.  Not necessary to add dependencies not already present.
                            if (packageJson[dependencyType]?[packageName] != null)
                            {
                                packageJson[dependencyType][packageName] = packageVersion;
                            }
                        }
                    }
                }
            }

            File.Copy(rootPackageJsonFile, rootPackageJsonFile + ".bak", overwrite: true);
            File.WriteAllText(rootPackageJsonFile, rootPackageJson.ToString() + Environment.NewLine);

            File.Copy(projectFile, projectFile + ".bak", overwrite: true);
            File.WriteAllText(projectFile, projectJson.ToString() + Environment.NewLine);

            File.Copy(testUtilsProjectFile, testUtilsProjectFile + ".bak", overwrite: true);
            File.WriteAllText(testUtilsProjectFile, testUtilsProjectJson.ToString() + Environment.NewLine);

            await Util.RunAsync("npm", "install -g pnpm", WorkingDirectory, outputBuilder: outputBuilder, errorBuilder: errorBuilder);
            await Util.RunAsync("pnpm", "install --no-frozen-lockfile", WorkingDirectory, outputBuilder: outputBuilder, errorBuilder: errorBuilder);

            var projectName = projectJson["name"];

            await Util.RunAsync("pnpm", $"build --filter {projectName}...", WorkingDirectory,
                outputBuilder: outputBuilder, errorBuilder: errorBuilder);

            // Deploy project to 
            var deployDirectory = Path.Combine(WorkingDirectory, "common", "deploy", project);
            await Util.RunAsync("pnpm", $"--filter {projectName} deploy --prod --legacy {deployDirectory}",
                WorkingDirectory, outputBuilder: outputBuilder, errorBuilder: errorBuilder);

            // "npm ls" frequently returns an error code (that can be ignored) due to "missing" dev dependencies
            var npmListResult = await Util.RunAsync("npm", "ls --depth=1 --omit dev",
                deployDirectory, outputBuilder: outputBuilder, errorBuilder: errorBuilder, throwOnError: false);

            var runtimePackageVersions = GetRuntimePackageVersions(npmListResult.StandardOutput);

            return (outputBuilder.ToString(), errorBuilder.ToString(), runtimePackageVersions);
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
            var runtimePackageVersions = (Dictionary<string, string>)context;

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            var projectDirectory = Path.Combine(WorkingDirectory, project);

            if (profile)
            {
                // "@azure/storage-blob" -> "storage-blob"
                var stripPackageName = primaryPackage.Substring(primaryPackage.LastIndexOf('/') + 1);

                var formattedArgs = arguments.Replace(" --", "_").Replace("--", "_").Replace(" ", "-");
                var profileFilename = $"{packageVersions[primaryPackage]}_{testName}_{formattedArgs}_{profileCount++}.cpuprofile";
                var profileDir = Util.GetProfileDirectory(WorkingDirectory);
                var profileOutputPath = Path.GetFullPath(Path.Combine(profileDir, stripPackageName, profileFilename));

                arguments += $" --profile --profile-path {profileOutputPath}";
            }
            var testResult = await Util.RunAsync("npm", $"run perf-test:node -- {testName} {arguments}",
                projectDirectory, outputBuilder: outputBuilder, errorBuilder: errorBuilder, throwOnError: false);

            var match = Regex.Match(testResult.StandardOutput, @"\((.*) ops/s", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

            var opsPerSecond = -1d;
            if (match.Success)
            {
                opsPerSecond = double.Parse(match.Groups[1].Value);
            }

            return new IterationResult
            {
                PackageVersions = runtimePackageVersions,
                OperationsPerSecond = opsPerSecond,
                StandardOutput = outputBuilder.ToString(),
                StandardError = errorBuilder.ToString(),
            };
        }

        // === Versions ===
        // @azure-tests/perf-storage-blob@1.0.0 /mnt/vss/_work/1/s/sdk/storage/storage-blob-perf-tests
        // ├── @azure/core-http@2.2.6 -> /mnt/vss/_work/1/s/sdk/core/core-http
        // ├── @azure/core-rest-pipeline@1.9.1 -> /mnt/vss/_work/1/s/sdk/core/core-rest-pipeline
        // ├── @azure/storage-blob@12.11.0 -> /mnt/vss/_work/1/s/common/temp/node_modules/.pnpm/@azure+storage-blob@12.11.0/node_modules/@azure/storage-blob
        // ├── @azure/test-utils-perf@1.0.0 -> /mnt/vss/_work/1/s/sdk/test-utils/perf            
        public static Dictionary<string, string> GetRuntimePackageVersions(string standardOutput)
        {
            var runtimePackageVersions = new Dictionary<string, List<string>>();

            foreach (var line in standardOutput.ToLines())
            {
                // "Extraneous" packages are not listed on the parent package's dependencies list and should be skipped
                if (line.Contains("UNMET DEPENDENCY", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var match = Regex.Match(line, @"^[^@]*(@azure.*?)@(.*)$");
                if (match.Success)
                {
                    var name = match.Groups[1].Value;
                    var version = match.Groups[2].Value.Trim();

                    if (version.Contains("node_modules", StringComparison.OrdinalIgnoreCase))
                    {
                        version = version[..version.IndexOf(' ')];
                    }

                    version = version.Replace(" deduped", string.Empty);

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
            // Cleanup previous deployments
            Util.DeleteIfExists(Path.Combine(WorkingDirectory, "common", "deploy"));

            string rootPackageJsonFile = Path.Combine(WorkingDirectory, "package.json");
            var projectDirectory = Path.Combine(WorkingDirectory, project);
            var projectFile = Path.Combine(projectDirectory, "package.json");
            var testUtilsProjectFile = Path.Combine(WorkingDirectory, "sdk", "test-utils", "perf", "package.json");

            // Restore backups
            File.Move(rootPackageJsonFile + ".bak", rootPackageJsonFile, overwrite: true);
            File.Move(projectFile + ".bak", projectFile, overwrite: true);
            File.Move(testUtilsProjectFile + ".bak", testUtilsProjectFile, overwrite: true);

            return Task.CompletedTask;
        }
    }
}
