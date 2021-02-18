using Azure.Sdk.Tools.PerfAutomation.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public class JavaScript : ILanguage
    {
        public async Task<(string output, string error, string context)> SetupAsync(
            string project, string languageVersion, IDictionary<string, string> packageVersions)
        {
            var workingDirectory = Program.Config.WorkingDirectories[Language.JS];

            var commonVersionsFile = Path.Combine(workingDirectory, "common", "config", "rush", "common-versions.json");
            var commonVersionsJson = JObject.Parse(File.ReadAllText(commonVersionsFile));

            var projectDirectory = Path.Combine(workingDirectory, project);
            var projectFile = Path.Combine(projectDirectory, "package.json");
            var projectJson = JObject.Parse(File.ReadAllText(projectFile));

            var track1 = projectDirectory.EndsWith("track-1", StringComparison.OrdinalIgnoreCase);
            var track2 = !track1;

            // - Track 1
            //   1. If not source, modify package.json
            //   2. rush update (OR install)
            //   3. cd project
            //   4. npm run setup
            //
            // - Track 2
            //   1. If not source, modify package.json and common-versions.json
            //   2. rush update
            //   3. Extract project name from package.json in project folder
            //   4. rush build -t projectName

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

            await Task.CompletedTask;
            return (null, null, null);
        }

        public async Task<IterationResult> RunAsync(string project, string languageVersion, string testName, string arguments, string context)
        {
            // Dump "npm list 2>nul | findstr @azure" to stdout

            // 1. cd project
            // 2. npm run perf-test:node -- testName arguments

            await Task.CompletedTask;

            return new IterationResult
            {
            };
        }

        public Task CleanupAsync(string project)
        {
            var workingDirectory = Program.Config.WorkingDirectories[Language.JS];
            var projectDirectory = Path.Combine(workingDirectory, project);

            var commonVersionsFile = Path.Combine(workingDirectory, "common", "config", "rush", "common-versions.json");
            var projectFile = Path.Combine(projectDirectory, "package.json");

            // Restore backups
            File.Move(commonVersionsFile + ".bak", commonVersionsFile, overwrite: true);
            File.Move(projectFile + ".bak", projectFile, overwrite: true);

            return Task.CompletedTask;
        }

    }
}
