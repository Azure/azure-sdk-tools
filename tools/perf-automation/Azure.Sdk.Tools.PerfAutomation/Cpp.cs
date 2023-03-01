using Azure.Sdk.Tools.PerfAutomation.Models;
using Microsoft.Crank.Agent;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PerfAutomation
{
    // Prerequisites
    // 1. Set VCPKG env vars
    //    Windows: set VCPKG_ROOT=C:\Git\vcpkg & set VCPKG_DEFAULT_TRIPLET=x64-windows-static
    //    Linux: export VCPKG_ROOT=/home/user/vcpkg && export VCPKG_DEFAULT_TRIPLET=x64-linux
    // 2. git clone https://github.com/microsoft/vcpkg
    // 3. cd vcpkg
    // 4. ./bootstrap-vcpkg.sh
    // 5. ./vcpkg install curl LibXml2 openssl
    public class Cpp : LanguageBase
    {
        public class UtilEventArgs : EventArgs
        {
            public UtilEventArgs(string methodName, string[] methodParams, bool isWindows)
            {
                this.MethodName = methodName;
                this.Params = methodParams;
                this.IsWindows = isWindows;
            }

            public string MethodName { get; set; }
            public string[] Params { get; set; } = null;
            public bool IsWindows { get; set; }
        }

        private const string _buildDirectory = "build";
        private const string _vcpkgFile = "vcpkg.json";
        public bool IsTest { get; set; } = false;
        public bool IsWindows { get; set; } = Util.IsWindows;
        public int ProcessorCount { get; set; } = Environment.ProcessorCount;
        protected override Language Language => Language.Cpp;
        public event EventHandler<UtilEventArgs> UtilMethodCall;

        public override async Task<(string output, string error, object context)> SetupAsync(
            string project,
            string languageVersion,
            string primaryPackage,
            IDictionary<string, string> packageVersions,
            bool debug)
        {
            var buildDirectory = Path.Combine(WorkingDirectory, _buildDirectory);

            if (IsTest)
            {
                UtilMethodCall(this, new UtilEventArgs("DeleteIfExists", new string[] { buildDirectory }, false));
                UtilMethodCall(this, new UtilEventArgs("CreateDirectory", new string[] { buildDirectory }, false));
            }
            else
            {
                Util.DeleteIfExists(buildDirectory);
                Directory.CreateDirectory(buildDirectory);
            }

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();
            if (IsTest)
            {
                UtilMethodCall(this, new UtilEventArgs("UpdatePackageVersions", new string[] { "packageVersions" }, false));
            }
            else
            {
                await UpdatePackageVersions(packageVersions);
            }

            // Windows and Linux require different arguments to build Release config
            var additionalGenerateArguments = IsWindows ? "-DDISABLE_AZURE_CORE_OPENTELEMETRY=ON" : (debug ? "-DCMAKE_BUILD_TYPE=Debug" : "-DCMAKE_BUILD_TYPE=Release");
            var additionalBuildArguments = IsWindows ? (debug ? "--config Debug" : "--config MinSizeRel") : String.Empty;

            if (IsTest)
            {
                outputBuilder.Append("output");
                errorBuilder.Append("error");
                UtilMethodCall(this, new UtilEventArgs(
                    "RunAsync1",
                    new string[]
                    {
                        "cmake",
                        $"-DBUILD_TESTING=ON -DBUILD_PERFORMANCE_TESTS=ON {additionalGenerateArguments} ..",
                        buildDirectory,
                        outputBuilder.ToString(),
                        errorBuilder.ToString()
                    },
                    IsWindows));

                UtilMethodCall(this, new UtilEventArgs(
                    "RunAsync2",
                    new string[]
                    {
                        "cmake",
                        $"--build . --parallel {this.ProcessorCount} {additionalBuildArguments} --target {project}",
                        buildDirectory,
                        outputBuilder.ToString(),
                        errorBuilder.ToString()
                    },
                    IsWindows));
                return (outputBuilder.ToString(), errorBuilder.ToString(), "exe");
            }
            else
            {
                await Util.RunAsync(
                    "cmake", $"-DBUILD_TESTING=ON -DBUILD_PERFORMANCE_TESTS=ON {additionalGenerateArguments} ..",
                    buildDirectory, outputBuilder: outputBuilder, errorBuilder: errorBuilder);

                var result = await Util.RunAsync(
                    "cmake", $"--build . --parallel {Environment.ProcessorCount} {additionalBuildArguments} --target {project}",
                    buildDirectory, outputBuilder: outputBuilder, errorBuilder: errorBuilder);

                // Find path to perf test executable
                var exeFileName = Util.IsWindows ? $"{project}.exe" : project;
                var exe = Directory.GetFiles(buildDirectory, exeFileName, SearchOption.AllDirectories).Single();

                return (result.StandardOutput, result.StandardError, exe);
            }
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
            string perfExe = (string)context;
            var profiledExe = perfExe;

            if (profile)
            {
                if (IsWindows)
                {
                    throw new InvalidOperationException("Profiling available on linux alone at the moment");
                }

                profiledExe = perfExe;
                perfExe = "valgrind";

            }

            string finalParams = $"{testName} {arguments}";
            if (profile)
            {
                finalParams = $"{profilerOptions} {profiledExe} {finalParams}";
            }
            ProcessResult result = new ProcessResult(0, String.Empty, String.Empty);
            if (IsTest)
            {
                UtilMethodCall(this, new UtilEventArgs(
                    "RunAsync",
                    new string[] {
                        perfExe,
                        finalParams,
                        WorkingDirectory},
                    IsWindows));
                result = new ProcessResult(0, "output (2.0 ops/s, 1.0 s/op)", "error");
            }
            else
            {
                result = await Util.RunAsync(perfExe, finalParams, WorkingDirectory);
            }
            IDictionary<string, string> reportedVersions = new Dictionary<string, string>();

            // Completed 54 operations in a weighted-average of 1s (52.766473 ops/s, 0.0189514 s/op)
            var match = Regex.Match(result.StandardOutput, @"\((.*) ops/s", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

            var opsPerSecond = -1d;
            if (match.Success)
            {
                opsPerSecond = double.Parse(match.Groups[1].Value);
            }

            foreach (var key in packageVersions.Keys)
            {
                var packageMatch = Regex.Match(result.StandardOutput, @$"{key.ToUpper()} VERSION ?.*");
                if (packageMatch.Success)
                {
                    var version = packageMatch.Captures[0].Value.Split(' ');

                    if (version.Length > 0)
                    {
                        reportedVersions.Add(key, version[version.Length - 1]);
                    }
                }
            }

            return new IterationResult
            {
                OperationsPerSecond = opsPerSecond,
                StandardOutput = result.StandardOutput,
                StandardError = result.StandardError,
                PackageVersions = reportedVersions
            };
        }

        public override async Task CleanupAsync(string project)
        {
            var fullVcpkgPath = Path.Combine(WorkingDirectory, _vcpkgFile);
            var buildDirectory = Path.Combine(WorkingDirectory, _buildDirectory);
            Util.DeleteIfExists(buildDirectory);
            //cleanup the vcpkg file by restoring from git
            await Util.RunAsync("git", $"checkout -- {fullVcpkgPath}", WorkingDirectory);
            return;
        }

        private async Task<bool> UpdatePackageVersions(IDictionary<string, string> packageVersions)
        {
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();
            var fullVcpkgPath = Path.Combine(WorkingDirectory, _vcpkgFile);
            bool updated = false;
            VcpkgDefinition document;

            // make sure we have the latest version of vcpkg declaration file before attempting any changes.
            var result = await Util.RunAsync("git", $"checkout -- {fullVcpkgPath}", WorkingDirectory, outputBuilder: outputBuilder, errorBuilder: errorBuilder);

            if (result.ExitCode != 0)
            {
                throw new Exception($"Unable to git checkout main version of {fullVcpkgPath}.{Environment.NewLine}Output: {outputBuilder.ToString()} {Environment.NewLine}Error: {errorBuilder.ToString()}");
            }

            using (StreamReader r = new StreamReader(fullVcpkgPath))
            {
                string json = r.ReadToEnd();
                document = JsonConvert.DeserializeObject<VcpkgDefinition>(json);
                Console.WriteLine($"Original {fullVcpkgPath} {Environment.NewLine}{json}");
            }

            foreach (var package in packageVersions.Keys)
            {
                var packageVersion = packageVersions[package];
                var envName = $"VCPKG-{package.ToUpper()}";
                // we don't need to make any updates we want the latest version
                if (String.Compare(packageVersion, "source", true) == 0)
                {
                    Environment.SetEnvironmentVariable(envName, null);
                    continue;
                }
                bool found = false;

                foreach (VcpkgDependency dependency in document.Dependencies)
                {
                    if (dependency.Name == package)
                    {
                        dependency.VersionGt = packageVersion;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    VcpkgDependency vcpkgDependency = new VcpkgDependency();
                    vcpkgDependency.Name = package;
                    vcpkgDependency.VersionGt = packageVersion;
                    document.Dependencies.Add(vcpkgDependency);
                }

                if (document.Overrides == null)
                {
                    document.Overrides = new List<VcpkgDependency>();
                }

                found = false;

                foreach (VcpkgDependency overrideEntry in document.Overrides)
                {
                    if (overrideEntry.Name == package)
                    {
                        overrideEntry.Version = packageVersion;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    VcpkgDependency overrideEntry = new VcpkgDependency();
                    overrideEntry.Name = package;
                    overrideEntry.Version = packageVersion;
                    document.Overrides.Add(overrideEntry);
                }
                updated = true;
                Environment.SetEnvironmentVariable(envName, packageVersion);
            }

            if (updated)
            {
                using (StreamWriter writer = new StreamWriter(fullVcpkgPath))
                {
                    string serializedDocument = JsonConvert.SerializeObject(document, Formatting.Indented);
                    Console.WriteLine($"Updated {fullVcpkgPath}{Environment.NewLine}{serializedDocument}");
                    writer.Write(serializedDocument);
                }
            }

            return updated;
        }
    }
}
