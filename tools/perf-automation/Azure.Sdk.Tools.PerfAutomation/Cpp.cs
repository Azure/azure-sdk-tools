using Azure.Sdk.Tools.PerfAutomation.Models;
using Microsoft.Crank.Agent;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
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
        private const string _buildDirectory = "build";

        protected override Language Language => Language.Cpp;

        public override async Task<(string output, string error, object context)> SetupAsync(
            string project, string languageVersion, string primaryPackage, IDictionary<string, string> packageVersions)
        {
            foreach (var key in packageVersions.Keys)
            {
                var gitResult = UpdatePackageVersion(key, packageVersions[key]);

                if (gitResult.Result.ExitCode != 0)
                {
                    throw new KeyNotFoundException($"Unable to find version {packageVersions[key]} for package {key}");
                }

                break;
            }

            var buildDirectory = Path.Combine(WorkingDirectory, _buildDirectory);

            Util.DeleteIfExists(buildDirectory);
            Directory.CreateDirectory(buildDirectory);

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            // Windows and Linux require different arguments to build Release config
            var additionalGenerateArguments = Util.IsWindows ? "-DDISABLE_AZURE_CORE_OPENTELEMETRY=ON" : "-DCMAKE_BUILD_TYPE=Release";
            var additionalBuildArguments = Util.IsWindows ? "--config MinSizeRel" : String.Empty;

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

        public override async Task<IterationResult> RunAsync(string project, string languageVersion,
            string primaryPackage, IDictionary<string, string> packageVersions, string testName, string arguments, object context)
        {
            var perfExe = (string)context;

            var result = await Util.RunAsync(perfExe, $"{testName} {arguments}", WorkingDirectory);

            // Completed 54 operations in a weighted-average of 1s (52.766473 ops/s, 0.0189514 s/op)
            var match = Regex.Match(result.StandardOutput, @"\((.*) ops/s", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

            var opsPerSecond = -1d;
            if (match.Success)
            {
                opsPerSecond = double.Parse(match.Groups[1].Value);
            }

            await Util.RunAsync(
                "git", $"checkout main",
                WorkingDirectory);

            return new IterationResult
            {
                OperationsPerSecond = opsPerSecond,
                StandardOutput = result.StandardOutput,
                StandardError = result.StandardError
            };
        }

        public override async Task CleanupAsync(string project)
        {
            var buildDirectory = Path.Combine(WorkingDirectory, _buildDirectory);
            Util.DeleteIfExists(buildDirectory);
            await Util.RunAsync(
                "git", $"checkout main",
                WorkingDirectory);
            return;
        }

        private async Task<ProcessResult> UpdatePackageVersion(string project, string packageVersion)
        {
            string gitTag = ComposeGitTag(project, packageVersion);
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();
            ProcessResult result;
            if (String.Compare(packageVersion, "source", true) == 0)
            {
                result = await Util.RunAsync("git",
                     $"checkout main",
                     WorkingDirectory,
                     outputBuilder: outputBuilder,
                     errorBuilder: errorBuilder);
            }
            else
            {
                // first try to checkout the branch, might have been created already 
                result = await Util.RunAsync("git",
                    $"checkout {gitTag}",
                    WorkingDirectory,
                    outputBuilder: outputBuilder,
                    errorBuilder: errorBuilder);

                // this is the first time thus a branch needs to be created
                if (result.ExitCode != 0)
                {
                    result = await Util.RunAsync("git",
                        $"checkout tags/{gitTag} -b {gitTag}",
                        WorkingDirectory,
                        outputBuilder: outputBuilder,
                        errorBuilder: errorBuilder);
                }
            }

            return result;
        }

        private string ComposeGitTag(string project, string packageVersion)
        {
            StringBuilder tag = new StringBuilder(project.Length + packageVersion.Length);

            string[] parts = project.Split('-', StringSplitOptions.RemoveEmptyEntries);
            tag.Append(String.Join('-', parts.Take(parts.Length - 1).ToArray<string>()));
            tag.Append('_');
            tag.Append(packageVersion);

            return tag.ToString();
        }
    }
}
