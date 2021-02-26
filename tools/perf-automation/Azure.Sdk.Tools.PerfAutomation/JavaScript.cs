using Azure.Sdk.Tools.PerfAutomation.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public class JavaScript : LanguageBase
    {
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

            await Util.RunAsync("rush", "update", WorkingDirectory, outputBuilder: outputBuilder, errorBuilder: errorBuilder);

            if (track1)
            {
                await Util.RunAsync("npm", "run setup", projectDirectory,
                    outputBuilder: outputBuilder, errorBuilder: errorBuilder);
            }
            else
            {
                var projectName = projectJson["name"];
                await Util.RunAsync("rush", $"build --to {projectName}", WorkingDirectory,
                    outputBuilder: outputBuilder, errorBuilder: errorBuilder);
            }

            return (outputBuilder.ToString(), errorBuilder.ToString(), null);
        }

        public override async Task<IterationResult> RunAsync(string project, string languageVersion, string testName, string arguments, string context)
        {
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            var projectDirectory = Path.Combine(WorkingDirectory, project);

            // TODO: Investigate why "npm list" is not printing all output

            // Dump "npm list 2>nul | findstr @azure" to stdout
            // "npm list" fails with exit code 1, but it succeeds enough to print the versions we care about
            var npmListResult = await Util.RunAsync("npm", "list", projectDirectory, throwOnError: false);

            foreach (var line in npmListResult.StandardOutput.ToLines().Where(l => l.Contains("@azure")))
            {
                outputBuilder.AppendLine(line);
            }

            // 1. cd project
            // 2. npm run perf-test:node -- testName arguments

            return new IterationResult
            {
                StandardOutput = outputBuilder.ToString(),
                StandardError = errorBuilder.ToString(),
            };
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
