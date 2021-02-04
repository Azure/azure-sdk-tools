using Azure.Sdk.Tools.PerfAutomation.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public class Net : ILanguage
    {

        public async Task<(string output, string error, string context)> SetupAsync(string project, IDictionary<string, string> packageVersions)
        {
            var workingDirectory = Program.Config.WorkingDirectories[Language.Net];
            var projectFile = Path.Combine(workingDirectory, project);

            File.Copy(projectFile, projectFile + ".bak", overwrite: true);

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
                    // TODO: Use XmlDocument instead of Regex
                    projectContents = Regex.Replace(
                        projectContents,
                        $"<ProjectReference [^>]*{packageName}.csproj[^<]*/>",
                        @$"<PackageReference Include=""{packageName}"" VersionOverride=""{packageVersion}"" />",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline
                    );
                }
            }

            File.WriteAllText(projectFile, projectContents);

            var processArguments = $"build -c release -f netcoreapp2.1 {project}";

            var result = await Util.RunAsync("dotnet", processArguments, workingDirectory: workingDirectory);

            return (result.StandardOutput, result.StandardError, null);
        }

        public async Task<Result> RunAsync(string project, string testName, string arguments, string context)
        {
            var workingDirectory = Program.Config.WorkingDirectories[Language.Net];

            var processArguments = $"run --no-build -c release -f netcoreapp2.1 -p {project} -- " +
                $"{testName} {arguments}";

            var result = await Util.RunAsync("dotnet", processArguments, workingDirectory: workingDirectory);

            var match = Regex.Match(result.StandardOutput, @"\((.*) ops/s", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);
            var opsPerSecond = double.Parse(match.Groups[1].Value);

            return new Result
            {
                OperationsPerSecond = opsPerSecond,
                StandardError = result.StandardError,
                StandardOutput = result.StandardOutput,
            };
        }

        public Task CleanupAsync(string project)
        {
            var workingDirectory = Program.Config.WorkingDirectories[Language.Net];
            var projectFile = Path.Combine(workingDirectory, project);

            // Restore backup
            File.Move(projectFile + ".bak", projectFile, overwrite: true);

            return Task.CompletedTask;
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
