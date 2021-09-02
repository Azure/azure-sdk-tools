using Azure.Sdk.Tools.PerfAutomation.Models;
using System;
using System.Collections.Generic;
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
    // 4. ./bootstrap-vcpkg
    // 5. vcpkg install curl LibXml2
    public class Cpp : LanguageBase
    {
        private const string _buildDirectory = "build";

        protected override Language Language => Language.Cpp;

        public override async Task<(string output, string error, string context)> SetupAsync(
            string project, string languageVersion, IDictionary<string, string> packageVersions)
        {
            var buildDirectory = Path.Combine(WorkingDirectory, _buildDirectory);

            Util.DeleteIfExists(buildDirectory);
            Directory.CreateDirectory(buildDirectory);

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            // Windows and Linux require different arguments to build Release config
            var additionalGenerateArguments = Util.IsWindows ? String.Empty : "-DCMAKE_BUILD_TYPE=Release";
            var additionalBuildArguments = Util.IsWindows ? "--config MinSizeRel" : String.Empty;

            await Util.RunAsync(
                "cmake", $"-DBUILD_TESTING=ON -DBUILD_PERFORMANCE_TESTS=ON {additionalGenerateArguments} ..",
                buildDirectory, outputBuilder, errorBuilder);

            var result = await Util.RunAsync(
                "cmake", $"--build . --parallel {Environment.ProcessorCount} {additionalBuildArguments} --target {project}",
                buildDirectory, outputBuilder, errorBuilder);

            // Find path to perf test executable
            var exeFileName = Util.IsWindows ? $"{project}.exe" : project;
            var exe = Directory.GetFiles(buildDirectory, exeFileName, SearchOption.AllDirectories).Single();

            return (result.StandardOutput, result.StandardError, exe);
        }

        public override async Task<IterationResult> RunAsync(string project, string languageVersion,
            IDictionary<string, string> packageVersions, string testName, string arguments, string context)
        {
            var perfExe = context;

            var result = await Util.RunAsync(perfExe, $"{testName} {arguments}", WorkingDirectory);

            // Completed 54 operations in a weighted-average of 1s (52.766473 ops/s, 0.0189514 s/op)
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
            var buildDirectory = Path.Combine(WorkingDirectory, _buildDirectory);
            Util.DeleteIfExists(buildDirectory);
            return Task.CompletedTask;
        }
    }
}
