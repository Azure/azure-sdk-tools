using Azure.Sdk.Tools.PerfAutomation.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public class Net : LanguageBase
    {
        protected override Language Language => Language.Net;

        public override async Task<(string output, string error, string context)> SetupAsync(
            string project, string languageVersion, IDictionary<string, string> packageVersions)
        {
            var projectFile = Path.Combine(WorkingDirectory, project);

            File.Copy(projectFile, projectFile + ".bak", overwrite: true);

            var projectContents = File.ReadAllText(projectFile);
            var additionalBuildArguments = String.Empty;

            foreach (var v in packageVersions)
            {
                var packageName = v.Key;
                var packageVersion = v.Value;

                if (packageVersion == Program.PackageVersionSource)
                {
                    // Force all transitive dependencies to use project references, to ensure all packages are build from source.
                    // The default is for transitive dependencies to use package references to the latest published version.
                    additionalBuildArguments = "-p:UseProjectReferenceToAzureClients=true";
                }
                else
                {
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
                        throw new InvalidOperationException(
                            $"Project file {projectFile} does not contain existing package or project reference to {packageName}");
                    }

                    projectContents = Regex.Replace(
                        projectContents,
                        pattern,
                        @$"<PackageReference Include=""{packageName}"" VersionOverride=""{packageVersion}"" />",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline
                    );
                }
            }

            File.WriteAllText(projectFile, projectContents);

            var processArguments = $"build -c release -f {languageVersion} {additionalBuildArguments} {project}";

            var result = await Util.RunAsync("dotnet", processArguments, workingDirectory: WorkingDirectory);

            return (result.StandardOutput, result.StandardError, null);
        }

        public override async Task<IterationResult> RunAsync(string project, string languageVersion, string testName, string arguments, string context)
        {
            var processArguments = $"run --no-build -c release -f {languageVersion} -p {project} -- " +
                $"{testName} {arguments}";

            var result = await Util.RunAsync("dotnet", processArguments, workingDirectory: WorkingDirectory, throwOnError: false);

            var match = Regex.Match(result.StandardOutput, @"\((.*) ops/s", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

            var opsPerSecond = -1d;
            if (match.Success)
            {
                opsPerSecond = double.Parse(match.Groups[1].Value);
            }

            return new IterationResult
            {
                OperationsPerSecond = opsPerSecond,
                StandardError = result.StandardError,
                StandardOutput = result.StandardOutput,
            };
        }

        public override Task CleanupAsync(string project)
        {
            var projectFile = Path.Combine(WorkingDirectory, project);

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
