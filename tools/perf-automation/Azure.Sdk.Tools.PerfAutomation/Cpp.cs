using Azure.Sdk.Tools.PerfAutomation.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PerfAutomation
{
    // Prerequisites
    // set VCPKG_ROOT=C:\Git\vcpkg& set VCPKG_DEFAULT_TRIPLET=x64-windows
    public class Cpp : LanguageBase
    {
        private const string _buildDirectory = "build-perf";

        protected override Language Language => Language.Cpp;

        public override async Task<(string output, string error, string context)> SetupAsync(
            string project, string languageVersion, IDictionary<string, string> packageVersions)
        {
            //cmake --build . --config Release -j <num-cores>
            //sdk\storage\azure-storage-blobs\test\perf\Release\azure-storage-blobs-perf DownloadBlob

            var buildDirectory = Path.Combine(WorkingDirectory, _buildDirectory);

            Util.DeleteIfExists(buildDirectory);
            Directory.CreateDirectory(buildDirectory);

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            await Util.RunAsync(
                "cmake", "-DBUILD_TESTING=ON -DBUILD_PERFORMANCE_TESTS=ON ..",
                buildDirectory, outputBuilder, errorBuilder);

            var result = await Util.RunAsync(
                "cmake", $"--build . --config Release --parallel {Environment.ProcessorCount} --target {project}",
                buildDirectory, outputBuilder, errorBuilder);

            /*
            azure-storage-blobs-perf.vcxproj -> C:\Git\cpp\build-perf\sdk\storage\azure-storage-blobs\test\perf\Release\azure-storage-blobs-perf.exe
            */
            var buildMatch = Regex.Match(result.StandardOutput, $@"-> (.*{project}\.exe)", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);
            var exe = buildMatch.Groups[1].Value;

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
