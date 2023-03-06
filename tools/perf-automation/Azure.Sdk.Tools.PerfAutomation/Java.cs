using Azure.Sdk.Tools.PerfAutomation.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        private static int profileCount = 0;

        private static readonly Dictionary<string, string> _buildEnvironment = new Dictionary<string, string>()
        {
            // Prevents error "InvocationTargetException: Java heap space" in azure-storage-file-datalake when compiling azure-storage-perf
            { "MAVEN_OPTS", "-Xmx1024m" },
        };

        public override async Task<(string output, string error, object context)> SetupAsync(
            string project,
            string languageVersion,
            string primaryPackage,
            IDictionary<string, string> packageVersions, 
            bool debug )
        {
            var projectFile = Path.Combine(WorkingDirectory, project, "pom.xml");

            SourceVersions ??= LoadSourceVersions();

            UpdatePackageVersions(PerfCoreProjectFile, packageVersions, SourceVersions);
            UpdatePackageVersions(projectFile, packageVersions, SourceVersions);

            var result = await Util.RunAsync(
                "mvn",
                "clean install -T 2C -am" +
                " -Denforcer.skip=true -DskipTests=true -Dmaven.javadoc.skip=true -Dcodesnippet.skip=true " +
                " -Dspotbugs.skip=true -Dcheckstyle.skip=true -Drevapi.skip=true" +
                $" --no-transfer-progress --pl {project}",
                WorkingDirectory,
                environmentVariables: _buildEnvironment
            );

            /*
            [11:27:11.796] [INFO] Building jar: C:\Git\java\sdk\storage\azure-storage-perf\target\azure-storage-perf-1.0.0-beta.1-jar-with-dependencies.jar
            */

            var buildMatch = Regex.Match(result.StandardOutput, @"Building jar: (.*with-dependencies\.jar)",
                RegexOptions.IgnoreCase | RegexOptions.RightToLeft);
            var jar = buildMatch.Groups[1].Value;

            return (result.StandardOutput, result.StandardError, jar);
        }

        private static void UpdatePackageVersions(
            string projectFile,
            IDictionary<string, string> packageVersions,
            IDictionary<string, string> sourceVersions)
        {
            // Create backup.  Throw if exists, since this shouldn't happen
            File.Copy(projectFile, projectFile + ".bak", overwrite: false);

            var doc = new XmlDocument() { PreserveWhitespace = true };
            doc.Load(projectFile);

            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("mvn", "http://maven.apache.org/POM/4.0.0");

            foreach (var v in packageVersions)
            {
                var nameParts = v.Key.Split(':');
                var groupId = nameParts[0];
                var artifactId = nameParts[1];
                var packageVersion = v.Value;

                if (packageVersion == Program.PackageVersionSource)
                {
                    if (!sourceVersions.TryGetValue(v.Key, out packageVersion))
                    {
                        continue;
                    }
                }

                var versionNode = doc.SelectSingleNode(
                    $"/mvn:project/mvn:dependencies/mvn:dependency[mvn:groupId='{groupId}' and mvn:artifactId='{artifactId}']/mvn:version",
                    nsmgr);

                if (versionNode != null)
                {
                    versionNode.InnerText = packageVersion;
                }
            }

            Console.WriteLine(doc.OuterXml);

            doc.Save(projectFile);
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
            var jarFile = (string)context;

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            var dependencyListResult = await Util.RunAsync("mvn", $"dependency:list --no-transfer-progress --pl {project}", WorkingDirectory,
                outputBuilder: outputBuilder, errorBuilder: errorBuilder);
            var runtimePackageVersions = GetRuntimePackageVersions(dependencyListResult.StandardOutput);

            var profilingConfig = "";
            if (profile)
            {
                var profileOutputPath = Path.GetFullPath(Path.Combine(Util.GetProfileDirectory(WorkingDirectory), $"{testName}_{profileCount++}.jfr"));
                profilingConfig = $"-XX:StartFlightRecording=filename={profileOutputPath},maxsize=1gb";

                // If Java 8 is the version of Java being used add '-XX:+UnlockCommercialFeatures' as that is required to run Java Flight Recording in Java 8.
                // Don't add '-XX:+UnlockCommercialFeatures' if it is any other version as this causes the JVM to crash on an unrecognized VM options.
                if (int.TryParse(languageVersion, out var res) && res == 8) 
                {
                    profilingConfig = "-XX:+UnlockCommercialFeatures " + profilingConfig;
                }

                var jfrConfigurationFile = Path.Combine(WorkingDirectory, "eng", "PerfAutomation.jfc");
                if (File.Exists(jfrConfigurationFile))
                {
                    profilingConfig += $",settings={jfrConfigurationFile}";
                }
            }

            var processArguments = $"-XX:+CrashOnOutOfMemoryError {profilingConfig} -jar {jarFile} -- {testName} {arguments}";

            var result = await Util.RunAsync("java", processArguments, WorkingDirectory, throwOnError: false,
                outputBuilder: outputBuilder, errorBuilder: errorBuilder);

            // Completed 157,630 operations in a weighted-average of 1.01s (156,225.73 ops/s, 0.000 s/op)
            var match = Regex.Match(result.StandardOutput, @"\((.*) ops/s", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

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
                StandardError = errorBuilder.ToString()
            };
        }

        // [08:13:01.622] [INFO] The following files have been resolved:
        // [08:13:01.622] [INFO]    io.projectreactor:reactor-core:jar:3.4.22:compile
        // [08:13:01.622] [INFO]    io.netty:netty-codec-http:jar:4.1.79.Final:compile
        // [08:13:01.622] [INFO]    com.azure:azure-core-http-okhttp:jar:1.11.2:compile
        // [08:13:01.623] [INFO]    io.netty:netty-tcnative-boringssl-static:jar:linux-x86_64:2.0.53.Final:compile
        // ...
        // [08:13:01.624] [INFO]    org.jetbrains:annotations:jar:13.0:compile
        // [08:13:01.624] [INFO] 
        // [08:13:01.624] [INFO] ------------------------------------------------------------------------
        // [08:13:01.625] [INFO] BUILD SUCCESS
        // [08:13:01.625] [INFO] ------------------------------------------------------------------------
        public static Dictionary<string, string> GetRuntimePackageVersions(string standardOutput)
        {
            var runtimePackageVersions = new Dictionary<string, string>();

            var versionLines = standardOutput.ToLines()
                .SkipWhile(s => !s.Contains("the following files have been resolved", StringComparison.OrdinalIgnoreCase))
                .Skip(1)
                .TakeWhile(s => !s.Trim().EndsWith("[INFO]", StringComparison.OrdinalIgnoreCase));

            foreach (var line in versionLines)
            {
                var versionInfo = Regex.Replace(line, @"^.*\[INFO\]\s+", string.Empty);
                var versionParts = versionInfo.Split(':');

                string groupId = null;
                string artifactId = null;
                string version = null;

                if (versionParts.Length == 5)
                {
                    // io.projectreactor:reactor-core:jar:3.4.22:compile
                    groupId = versionParts[0];
                    artifactId = versionParts[1];
                    version = versionParts[3];
                }
                else if (versionParts.Length == 6)
                {
                    // io.netty:netty-tcnative-boringssl-static:jar:linux-x86_64:2.0.53.Final:compile
                    groupId = versionParts[0];
                    artifactId = versionParts[1];
                    version = versionParts[4];
                }
                else
                {
                    // Skip non-matching lines
                    continue;
                }

                if (groupId.StartsWith("com.azure", StringComparison.OrdinalIgnoreCase) ||
                    groupId.StartsWith("io.projectreactor", StringComparison.OrdinalIgnoreCase))
                {
                    runtimePackageVersions[$"{groupId}:{artifactId}"] = version;
                }
            }

            return runtimePackageVersions;
        }

        public override IDictionary<string, string> FilterRuntimePackageVersions(IDictionary<string, string> runtimePackageVersions)
        {
            return runtimePackageVersions?
                .Where(kvp => !kvp.Key.Equals("com.azure:perf-test-core", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
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
                if (string.IsNullOrEmpty(trimmedLine) ||
                    trimmedLine.StartsWith('#') ||
                    trimmedLine.StartsWith("beta_", StringComparison.Ordinal) ||
                    trimmedLine.StartsWith("unreleased_", StringComparison.Ordinal))
                {
                    continue;
                }

                var splitVersionLine = trimmedLine.Split(';');
                sourceVersions.Add(splitVersionLine[0], splitVersionLine[2]);
            }

            return sourceVersions;
        }
    }
}
