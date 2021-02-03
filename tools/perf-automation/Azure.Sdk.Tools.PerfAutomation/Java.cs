using Azure.Sdk.Tools.PerfAutomation.Models;
using Microsoft.Crank.Agent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace Azure.Sdk.Tools.PerfAutomation
{
    static class Java
    {
        private static void SetPackageVersions(string projectFile, IDictionary<string, string> packageVersions)
        {
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

                if (packageVersion == "master")
                {
                    continue;
                }
                else
                {
                    var versionNode = doc.SelectSingleNode($"/mvn:project/mvn:dependencies/mvn:dependency[mvn:artifactId='{packageName}']/mvn:version", nsmgr);
                    versionNode.InnerText = packageVersion;
                }
            }

            doc.Save(projectFile);
        }

        private static void UnsetPackageVersions(string projectFile)
        {
            // Restore backup
            File.Move(projectFile + ".bak", projectFile, overwrite: true);
        }

        public static async Task<Result> RunAsync(
            LanguageSettings languageSettings, string arguments, IDictionary<string, string> packageVersions)
        {
            var workingDirectory = Program.Config.WorkingDirectories[Language.Java];
            var projectFile = Path.Combine(workingDirectory, languageSettings.Project);

            try
            {
                SetPackageVersions(projectFile, packageVersions);

                string buildFilename;
                var buildArguments = $"package -T1C -am -Dmaven.test.skip=true -Dmaven.javadoc.skip=true --pl {languageSettings.Project}";
                
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    buildFilename = "cmd";
                    buildArguments = $"/c mvn {buildArguments}";
                }
                else
                {
                    buildFilename = "mvn";
                }

                var buildResult = await ProcessUtil.RunAsync(
                    buildFilename,
                    buildArguments,
                    workingDirectory: workingDirectory,
                    log: true,
                    captureOutput: true,
                    captureError: true
                );

                /*
                [11:27:11.796] [INFO] Building jar: C:\Git\java\sdk\storage\azure-storage-perf\target\azure-storage-perf-1.0.0-beta.1-jar-with-dependencies.jar
                */

                var buildMatch = Regex.Match(buildResult.StandardOutput, @"Building jar: (.*\.jar)", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);
                var jar = buildMatch.Groups[1].Value;

                var processArguments = $"-jar {jar} -- {languageSettings.TestName} {arguments} {languageSettings.AdditionalArguments}";

                var result = await ProcessUtil.RunAsync(
                    "java",
                    processArguments,
                    workingDirectory: workingDirectory,
                    log: true,
                    captureOutput: true,
                    captureError: true
                );

                var match = Regex.Match(result.StandardOutput, @"\((.*) ops/s", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);
                var opsPerSecond = double.Parse(match.Groups[1].Value);

                return new Result
                {
                    OperationsPerSecond = opsPerSecond,
                    StandardError = buildResult.StandardError + Environment.NewLine + result.StandardError,
                    StandardOutput = buildResult.StandardOutput + Environment.NewLine + result.StandardOutput,
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
