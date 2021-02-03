using Azure.Sdk.Tools.PerfAutomation.Models;
using Microsoft.Crank.Agent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PerfAutomation
{
    static class Net
    {
        private static void SetPackageVersions(string projectFile, IDictionary<string, string> packageVersions)
        {
            // Create backup.  Throw if exists, since this shouldn't happen
            File.Copy(projectFile, projectFile + ".bak", overwrite: false);

            var projectContents = File.ReadAllText(projectFile);
            
            foreach (var v in packageVersions)
            {
                var packageName = v.Key;
                var packageVersion = v.Value;

                if (packageVersion == "master")
                {
                    continue;
                }
                else
                {
                    projectContents = Regex.Replace(
                        projectContents,
                        $"<ProjectReference [^>]*{packageName}.csproj[^<]*/>",
                        @$"<PackageReference Include=""{packageName}"" VersionOverride=""{packageVersion}"" />",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline
                    );
                }
            }

            File.WriteAllText(projectFile, projectContents);
        }

        private static void UnsetPackageVersions(string projectFile)
        {
            // Restore backup
            File.Move(projectFile + ".bak", projectFile, overwrite: true);
        }

        public static async Task<Result> RunAsync(string workingDirectory, LanguageSettings languageSettings,
            string arguments, IDictionary<string, string> packageVersions)
        {
            var projectFile = Path.Combine(workingDirectory, languageSettings.Project);

            try
            {
                SetPackageVersions(projectFile, packageVersions);

                var processArguments = $"run -c release -f netcoreapp2.1 -p {languageSettings.Project} -- " +
                    $"{languageSettings.TestName} {arguments}";

                var result = await ProcessUtil.RunAsync(
                    "dotnet",
                    processArguments,
                    workingDirectory: workingDirectory,
                    log: true,
                    captureOutput: true,
                    captureError: true
                );

                var match = Regex.Match(result.StandardOutput, @"\((.*) ops/s", RegexOptions.RightToLeft);
                var opsPerSecond = double.Parse(match.Groups[1].Value);

                return new Result
                {
                    OperationsPerSecond = opsPerSecond,
                    StandardError = result.StandardError,
                    StandardOutput = result.StandardOutput,
                };
            }
            finally
            {
                UnsetPackageVersions(projectFile);
            }
        }

        /*
        === Warmup ===
        Current         Total           Average
        622025          622025          617437.38

        === Results ===
        Completed 622,025 operations in a weighted-average of 1.01s (617,437.38 ops/s, 0.000 s/op)

        === Test ===
        Current         Total           Average
        693696          693696          692328.31

        === Results ===
        Completed 693,696 operations in a weighted-average of 1.00s (692,328.31 ops/s, 0.000 s/op)
        */
    }
}
