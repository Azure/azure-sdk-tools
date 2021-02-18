using Azure.Sdk.Tools.PerfAutomation.Models;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public class Java : ILanguage
    {
        public async Task<(string output, string error, string context)> SetupAsync(
            string project, string languageVersion, IDictionary<string, string> packageVersions)
        {
            var workingDirectory = Program.Config.WorkingDirectories[Language.Java];
            var projectFile = Path.Combine(workingDirectory, project);

            // Create backup.  Throw if exists, since this shouldn't happen
            File.Copy(projectFile, projectFile + ".bak", overwrite: false);

            var doc = new XmlDocument() { PreserveWhitespace = true };
            doc.Load(projectFile);

            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("mvn", "http://maven.apache.org/POM/4.0.0");

            foreach (var v in packageVersions)
            {
                var packageName = v.Key;
                var packageVersion = v.Value;

                if (packageVersion != Program.PackageVersionSource)
                {
                    var versionNode = doc.SelectSingleNode($"/mvn:project/mvn:dependencies/mvn:dependency[mvn:artifactId='{packageName}']/mvn:version", nsmgr);
                    versionNode.InnerText = packageVersion;
                }
            }

            doc.Save(projectFile);

            string buildFilename;
            var buildArguments = $"package -T1C -am -Dmaven.test.skip=true -Dmaven.javadoc.skip=true --pl {project}";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                buildFilename = "cmd";
                buildArguments = $"/c mvn {buildArguments}";
            }
            else
            {
                buildFilename = "mvn";
            }

            var result = await Util.RunAsync(buildFilename, buildArguments, workingDirectory);

            /*
            [11:27:11.796] [INFO] Building jar: C:\Git\java\sdk\storage\azure-storage-perf\target\azure-storage-perf-1.0.0-beta.1-jar-with-dependencies.jar
            */

            var buildMatch = Regex.Match(result.StandardOutput, @"Building jar: (.*\.jar)", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);
            var jar = buildMatch.Groups[1].Value;

            return (result.StandardOutput, result.StandardError, jar);
        }

        public async Task<IterationResult> RunAsync(string project, string languageVersion, string testName, string arguments, string context)
        {
            var workingDirectory = Program.Config.WorkingDirectories[Language.Java];

            var processArguments = $"-jar {context} -- {testName} {arguments}";

            var result = await Util.RunAsync("java", processArguments, workingDirectory, throwOnError: false);

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

        public Task CleanupAsync(string project)
        {
            var workingDirectory = Program.Config.WorkingDirectories[Language.Java];
            var projectFile = Path.Combine(workingDirectory, project);

            // Restore backup
            File.Move(projectFile + ".bak", projectFile, overwrite: true);

            return Task.CompletedTask;
        }

        /*
        === Warmup ===
        Current         Total           Average
        124293          124293          127608.18
        1879            126172          127618.75

        === Results ===
        Completed 126,172 operations in a weighted-average of 0.99s (127,618.75 ops/s, 0.000 s/op)

        === Test ===
        Current         Total           Average
        157630          157630          156225.73
        0               157630          156225.73

        === Results ===
        Completed 157,630 operations in a weighted-average of 1.01s (156,225.73 ops/s, 0.000 s/op)
        */
    }
}
