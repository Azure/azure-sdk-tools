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
    public class Python : LanguageBase
    {
        private const string _env = "env-perf";
        private static readonly string _envBin = Util.IsWindows ? "scripts" : "bin";

        protected override Language Language => Language.Python;

        private static int profileCount = 0;

        public override async Task<(string output, string error, object context)> SetupAsync(
            string project,
            string languageVersion,
            string primaryPackage,
            IDictionary<string, string> packageVersions,
            bool debug)
        {
            var projectDirectory = Path.Combine(WorkingDirectory, project);
            var env = Path.Combine(projectDirectory, _env);

            Util.DeleteIfExists(env);

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            // On Windows, always use "python".  On Unix-like systems, specify the major and minor versions, e.g "python3.7".
            var systemPython = Util.IsWindows ? "python" : "python" + languageVersion;

            // Create venv
            await Util.RunAsync(systemPython, $"-m venv {_env}", projectDirectory, outputBuilder: outputBuilder, errorBuilder: errorBuilder);

            var python = Path.Combine(env, _envBin, "python");
            var pip = Path.Combine(env, _envBin, "pip");

            // Install test tools
            // await Util.RunAsync(pip, $"install -r {WorkingDirectory}/eng/test_tools.txt", projectDirectory, outputBuilder: outputBuilder, errorBuilder: errorBuilder: errorBuilder);

            // Install dev reqs
            await Util.RunAsync(pip, "install -r dev_requirements.txt", projectDirectory, outputBuilder: outputBuilder, errorBuilder: errorBuilder);

            foreach (var v in packageVersions)
            {
                var packageName = v.Key;
                var packageVersion = v.Value;

                if (packageVersion == Program.PackageVersionSource)
                {
                    if (packageName == primaryPackage)
                    {
                        await Util.RunAsync(pip, "install .", projectDirectory, outputBuilder: outputBuilder, errorBuilder: errorBuilder);
                    }
                    // TODO: Consider installing source versions of non-primary packages.  Would require finding package in source tree.
                    //       So far, this seems unnecessary, since dev-requirements.txt usually includes core.
                }
                else
                {
                    await Util.RunAsync(pip, $"install {packageName}=={packageVersion}", projectDirectory, outputBuilder: outputBuilder, errorBuilder: errorBuilder);
                }
            }

            return (outputBuilder.ToString(), errorBuilder.ToString(), null);
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
            var projectDirectory = Path.Combine(WorkingDirectory, project);

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            var env = Path.Combine(projectDirectory, _env);
            var pip = Path.Combine(env, _envBin, "pip");
            var perfstress = Path.Combine(env, _envBin, "perfstress");

            var pipListResult = await Util.RunAsync(pip, "list", projectDirectory, outputBuilder: outputBuilder, errorBuilder: errorBuilder);
            var runtimePackageVersions = GetRuntimePackageVersions(pipListResult.StandardOutput);

            if (profile)
            {
                var formattedArgs = arguments.Replace(" --", "_").Replace("--", "_").Replace(" ", "-");
                var profileFilename = $"{packageVersions[primaryPackage]}_{testName}_{formattedArgs}_{profileCount++}.pstats";
                var profileOutputPath = Path.GetFullPath(Path.Combine(Util.GetProfileDirectory(WorkingDirectory), profileFilename));

                arguments += $" --profile --profile-path {profileOutputPath}";
            }
            var processResult = await Util.RunAsync(
                perfstress,
                $"{testName} {arguments}",
                Path.Combine(projectDirectory, "tests"),
                outputBuilder: outputBuilder,
                errorBuilder: errorBuilder
            );

            // TODO: Why does Python perf framework write to StdErr instead of StdOut?
            // Completed 5,718,534 operations in a weighted-average of 2.00s (2,858,373.57 ops/s, 0.000 s/op)
            var match = Regex.Match(processResult.StandardError, @"\((.*) ops/s", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);
            double opsPerSecond = -1;
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

        // Package              Version   Editable project location
        // -------------------- --------- -----------------------------------------------
        // azure-common         1.1.28
        // azure-core           1.25.0
        // azure-devtools       1.2.1     /mnt/vss/_work/1/s/tools/azure-devtools/src
        // azure-identity       1.10.0
        // azure-mgmt-core      1.3.1
        // azure-mgmt-keyvault  10.0.0
        // azure-mgmt-resource  21.1.0
        // azure-mgmt-storage   20.0.0    /mnt/vss/_work/1/s/sdk/storage/azure-mgmt-storage
        // azure-sdk-tools      0.0.0     /mnt/vss/_work/1/s/tools/azure-sdk-tools
        // azure-storage-blob   12.14.0b1 /mnt/vss/_work/1/s/sdk/storage/azure-storage-blob
        // azure-storage-common 1.4.0
        public static Dictionary<string, string> GetRuntimePackageVersions(string standardOutput)
        {
            var runtimePackageVersions = new Dictionary<string, string>();

            foreach (var line in standardOutput.ToLines())
            {
                var match = Regex.Match(line, @"^(azure\S*)\s+(\S+)\s*(\S*)$");

                if (match.Success)
                {
                    var name = match.Groups[1].Value;
                    var version = match.Groups[2].Value;
                    var location = match.Groups[3].Value;

                    if (!string.IsNullOrEmpty(location))
                    {
                        version = version + " -> " + location;
                    }

                    runtimePackageVersions.Add(name, version);
                }
            }

            return runtimePackageVersions;
        }

        public override IDictionary<string, string> FilterRuntimePackageVersions(IDictionary<string, string> runtimePackageVersions)
        {
            return runtimePackageVersions?
                .Where(kvp => !kvp.Key.Equals("azure-devtools", StringComparison.OrdinalIgnoreCase) &&
                              !kvp.Key.Equals("azure-sdk-tools", StringComparison.OrdinalIgnoreCase) &&
                              !kvp.Key.Equals("azure-common", StringComparison.OrdinalIgnoreCase) &&
                              !kvp.Key.Equals("azure-storage-common", StringComparison.OrdinalIgnoreCase) &&
                              !kvp.Key.StartsWith("azure-mgmt", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public override Task CleanupAsync(string project)
        {
            var projectDirectory = Path.Combine(WorkingDirectory, project);
            var env = Path.Combine(projectDirectory, _env);
            Util.DeleteIfExists(env);
            return Task.CompletedTask;
        }
    }
}
