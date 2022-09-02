using Azure.Sdk.Tools.PerfAutomation.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public class Java : LanguageBase
    {
        private IDictionary<string, string> SourceVersions;

        protected override Language Language => Language.Java;

        private string PerfCoreProjectFile => Path.Combine(WorkingDirectory, "common", "perf-test-core", "pom.xml");
        private string VersionFile => Path.Combine(WorkingDirectory, "eng", "versioning", "version_client.txt");

        private static readonly Dictionary<string, string> _buildEnvironment = new Dictionary<string, string>()
        {
            // Prevents error "InvocationTargetException: Java heap space" in azure-storage-file-datalake when compiling azure-storage-perf
            { "MAVEN_OPTS", "-Xmx1024m" },
        };

        public override async Task<(string output, string error, string context)> SetupAsync(
            string project, string languageVersion, IDictionary<string, string> packageVersions)
        {
            var projectFile = Path.Combine(WorkingDirectory, project, "pom.xml");

            SourceVersions ??= LoadSourceVersions();

            UpdatePackageVersions(PerfCoreProjectFile, packageVersions, SourceVersions);
            UpdatePackageVersions(projectFile, packageVersions, SourceVersions);

            var result = await Util.RunAsync("mvn", $"clean package -T1C -am -Denforcer.skip=true -DskipTests=true -Dmaven.javadoc.skip=true --no-transfer-progress --pl {project}",
                WorkingDirectory, environmentVariables: _buildEnvironment);

            /*
            [11:27:11.796] [INFO] Building jar: C:\Git\java\sdk\storage\azure-storage-perf\target\azure-storage-perf-1.0.0-beta.1-jar-with-dependencies.jar
            */

            var buildMatch = Regex.Match(result.StandardOutput, @"Building jar: (.*with-dependencies\.jar)", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);
            var jar = buildMatch.Groups[1].Value;

            return (result.StandardOutput, result.StandardError, jar);
        }

        private static void UpdatePackageVersions(string projectFile, IDictionary<string, string> packageVersions, IDictionary<string, string> sourceVersions)
        {
            // Create backup.  Throw if exists, since this shouldn't happen
            File.Copy(projectFile, projectFile + ".bak", overwrite: false);

            var doc = new XmlDocument() { PreserveWhitespace = true };
            doc.Load(projectFile);

            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("mvn", "http://maven.apache.org/POM/4.0.0");

            foreach (var v in packageVersions)
            {
                var groupdIdAndArtifactId = v.Key;
                var splitGroupdIdAndArtifactId = groupdIdAndArtifactId.Split(':');
                var packageVersion = v.Value;

                if (packageVersion == Program.PackageVersionSource)
                {
                    if (!sourceVersions.TryGetValue(groupdIdAndArtifactId, out packageVersion)) continue;
                }

                var dependencies = doc.SelectNodes("/mvn:project/mvn:dependencies/mvn:dependency");

                for (var i = 0; i < dependencies.Count; i++)
                {
                    var dependency = dependencies[i];
                    var groupId = dependency.SelectSingleNode("groupId").InnerText;
                    var artifactId = dependency.SelectSingleNode("artifactId").InnerText;

                    if (string.Equals(groupId, groupdIdAndArtifactId[0]) && string.Equals(artifactId, groupdIdAndArtifactId[1]))
                    {
                        dependency.SelectSingleNode("version").InnerText = packageVersion;
                    }
                }
            }

            Console.WriteLine(doc.OuterXml);

            doc.Save(projectFile);
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

        private IDictionary<string, string> LoadSourceVersions()
        {
            var sourceVersions = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(VersionFile))
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;
                if (trimmedLine.StartsWith('#') || trimmedLine.StartsWith("beta_") || trimmedLine.StartsWith("unreleased_")) continue;

                var splitVersionLine = trimmedLine.Split(';');
                sourceVersions.Add(splitVersionLine[0], splitVersionLine[2]);
            }

            return sourceVersions;
        }
    }
}
