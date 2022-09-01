using Azure.Sdk.Tools.PerfAutomation.Models;
using Microsoft.Crank.Agent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public class Java : LanguageBase
    {
        protected override Language Language => Language.Java;

        private string PerfCoreProjectFile => Path.Combine(WorkingDirectory, "common", "perf-test-core", "pom.xml");

        private static readonly Dictionary<string, string> _buildEnvironment = new Dictionary<string, string>()
        {
            // Prevents error "InvocationTargetException: Java heap space" in azure-storage-file-datalake when compiling azure-storage-perf
            { "MAVEN_OPTS", "-Xmx1024m" },
        };

        public override async Task<(string output, string error, string context)> SetupAsync(
            string project, string languageVersion, IDictionary<string, string> packageVersions)
        {
            var projectFile = Path.Combine(WorkingDirectory, project, "pom.xml");

            await UpdatePackageVersions(packageVersions, WorkingDirectory);

            var result = await Util.RunAsync("mvn", $"clean package -T1C -am -Denforcer.skip=true -DskipTests=true -Dmaven.javadoc.skip=true --no-transfer-progress --pl {project}",
                WorkingDirectory, environmentVariables: _buildEnvironment);

            /*
            [11:27:11.796] [INFO] Building jar: C:\Git\java\sdk\storage\azure-storage-perf\target\azure-storage-perf-1.0.0-beta.1-jar-with-dependencies.jar
            */

            var buildMatch = Regex.Match(result.StandardOutput, @"Building jar: (.*with-dependencies\.jar)", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);
            var jar = buildMatch.Groups[1].Value;

            return (result.StandardOutput, result.StandardError, jar);
        }

        private static async Task UpdatePackageVersions(IDictionary<string, string> packageVersions, string workingDirectory)
        {
            string versionsPath = Path.Combine(workingDirectory, "eng", "versioning");

            bool sourceRun = packageVersions.Values.All(version => string.Equals(version, "source"));
            if (sourceRun)
            {
                // All packages are set so source, treat this as if it were a From Source CI run.
                // This call updates all dependency versions to source versions.
                await ProcessUtil.RunAsync("python", $"set_versions.py --build-type client --pst", workingDirectory: versionsPath);
            } 
            else
            {
                // Loop over each package version, which should use a key that is the artifact identifier (groupId:artifactId)
                // and have a value of the artifact version.
                foreach (var packageKvp in packageVersions)
                {
                    string[] groupIdAndArtifactIdSplit = packageKvp.Key.Split(':', 2);
                    if (groupIdAndArtifactIdSplit.Length != 2 || !groupIdAndArtifactIdSplit[0].Equals("com.azure"))
                    {
                        continue;
                    }

                    await ProcessUtil.RunAsync("python", $"set_versions.py --bt client --group-id {groupIdAndArtifactIdSplit[0]} --artifact-id {groupIdAndArtifactIdSplit[1]} --new-version {packageKvp.Value}", workingDirectory: versionsPath);
                }
            }

            await ProcessUtil.RunAsync("python", $"update_versions.py --sr --bt client --ut library", workingDirectory: versionsPath);
        }

        public override async Task<IterationResult> RunAsync(string project, string languageVersion,
            IDictionary<string, string> packageVersions, string testName, string arguments, string context)
        {
            var processArguments = $"-XX:+CrashOnOutOfMemoryError -jar {context} -- {testName} {arguments}";

            var result = await Util.RunAsync("java", processArguments, WorkingDirectory, throwOnError: false);

            // Completed 157,630 operations in a weighted-average of 1.01s (156,225.73 ops/s, 0.000 s/op)
            var match = Regex.Match(result.StandardOutput, @"\((.*) ops/s", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

            var opsPerSecond = -1d;
            if (match.Success)
            {
                opsPerSecond = double.Parse(match.Groups[1].Value);
            }

            return new IterationResult
            {
                OperationsPerSecond = opsPerSecond,
                StandardOutput = result.StandardOutput,
                StandardError = result.StandardError
            };
        }

        public override Task CleanupAsync(string project)
        {
            var projectFile = Path.Combine(WorkingDirectory, project, "pom.xml");

            // Restore backups
            File.Move(PerfCoreProjectFile + ".bak", PerfCoreProjectFile, overwrite: true);
            File.Move(projectFile + ".bak", projectFile, overwrite: true);

            return Task.CompletedTask;
        }
    }
}
