using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.PerfAutomation.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public class Go : LanguageBase
    {
        protected override Language Language => Language.Go;

        private static readonly Regex _opsRegex =
            new Regex(@"\(([-\d\.,]+)\s+ops/s", RegexOptions.IgnoreCase | RegexOptions.RightToLeft | RegexOptions.Compiled);

        public override async Task<(string output, string error, object context)> SetupAsync(
            string project,
            string languageVersion,
            string primaryPackage,
            IDictionary<string, string> packageVersions,
            bool debug)
        {
            var projectDirectory = Path.Combine(WorkingDirectory, project);

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            var goModPath = Path.Combine(projectDirectory, "go.mod");
            var goSumPath = Path.Combine(projectDirectory, "go.sum");

            BackupFile(goModPath);
            BackupFile(goSumPath);

            // Drop any existing replace directives for all requested packages to avoid stale state.
            foreach (var pkg in packageVersions.Keys)
            {
                await DropReplaceAsync(projectDirectory, pkg, outputBuilder, errorBuilder);
            }

            foreach (var kvp in packageVersions)
            {
                var packageName = kvp.Key;
                var packageVersion = kvp.Value;

                if (string.Equals(packageVersion, Program.PackageVersionSource, StringComparison.OrdinalIgnoreCase))
                {
                    var localPath = ResolveSourcePath(packageName);
                    await AddReplaceAsync(projectDirectory, packageName, localPath, outputBuilder, errorBuilder);
                }
                else
                {
                    // Ensure published version resolution for this package.
                    await Util.RunAsync(
                        "go",
                        $"get {packageName}@{packageVersion}",
                        projectDirectory,
                        outputBuilder: outputBuilder,
                        errorBuilder: errorBuilder);
                }
            }

            await Util.RunAsync(
                "go",
                "mod tidy",
                projectDirectory,
                outputBuilder: outputBuilder,
                errorBuilder: errorBuilder);

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

            // Go perf runners in this scenario are CLI test apps.
            var processResult = await Util.RunAsync(
                "go",
                $"run . {testName} {arguments}",
                projectDirectory,
                outputBuilder: outputBuilder,
                errorBuilder: errorBuilder,
                throwOnError: false);

            // Capture combined output for parsing and return.
            var combinedOutput = (processResult.StandardOutput ?? string.Empty) + Environment.NewLine + (processResult.StandardError ?? string.Empty);
            var opsPerSecond = ParseOpsPerSecond(combinedOutput);

            var runtimePackageVersions = await GetRuntimePackageVersionsAsync(projectDirectory);

            return new IterationResult
            {
                PackageVersions = runtimePackageVersions,
                OperationsPerSecond = opsPerSecond,
                StandardOutput = processResult.StandardOutput ?? outputBuilder.ToString(),
                StandardError = processResult.StandardError ?? errorBuilder.ToString()
            };
        }

        public override async Task CleanupAsync(string project)
        {
            var projectDirectory = Path.Combine(WorkingDirectory, project);

            RestoreBackup(Path.Combine(projectDirectory, "go.mod"));
            RestoreBackup(Path.Combine(projectDirectory, "go.sum"));

            // Best-effort cleanup to ensure module graph is consistent after restore.
            try
            {
                await Util.RunAsync(
                    "go",
                    "mod tidy",
                    projectDirectory,
                    throwOnError: false);
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }

        public override IDictionary<string, string> FilterRuntimePackageVersions(IDictionary<string, string> runtimePackageVersions)
        {
            // Keep Azure packages and a minimal runtime signal.
            return runtimePackageVersions?
                .Where(kvp =>
                    kvp.Key.StartsWith("github.com/Azure/", StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.Equals("go", StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.StartsWith("golang.org/x/", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private static double ParseOpsPerSecond(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return -1d;
            }

            var match = _opsRegex.Match(output);
            if (!match.Success)
            {
                return -1d;
            }

            var raw = match.Groups[1].Value.Replace(",", string.Empty).Trim();

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            return -1d;
        }

        private async Task<IDictionary<string, string>> GetRuntimePackageVersionsAsync(string projectDirectory)
        {
            var runtimePackageVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var result = await Util.RunAsync(
                    "go",
                    "list -m all",
                    projectDirectory,
                    throwOnError: false);

                var lines = (result.StandardOutput ?? string.Empty)
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    // Formats:
                    // module version
                    // module version => replacement
                    // module => replacement (workspace/local edge cases)
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                    {
                        continue;
                    }

                    var module = parts[0];
                    var version = parts[1];

                    if (parts.Length > 2 && parts[2] == "=>")
                    {
                        version = string.Join(" ", parts.Skip(1));
                    }

                    runtimePackageVersions[module] = version;
                }
            }
            catch
            {
                // Best effort: return what we have.
            }

            return runtimePackageVersions;
        }

        private string ResolveSourcePath(string packageName)
        {
            const string repoPrefix = "github.com/Azure/azure-sdk-for-go/";
            if (!packageName.StartsWith(repoPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Cannot resolve source path for package '{packageName}'. Expected prefix '{repoPrefix}'.");
            }

            var relative = packageName.Substring(repoPrefix.Length).Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(WorkingDirectory, relative);
        }

        private static async Task DropReplaceAsync(
            string projectDirectory,
            string packageName,
            StringBuilder outputBuilder,
            StringBuilder errorBuilder)
        {
            await Util.RunAsync(
                "go",
                $"mod edit -dropreplace={packageName}",
                projectDirectory,
                outputBuilder: outputBuilder,
                errorBuilder: errorBuilder,
                throwOnError: false);
        }

        private static async Task AddReplaceAsync(
            string projectDirectory,
            string packageName,
            string localPath,
            StringBuilder outputBuilder,
            StringBuilder errorBuilder)
        {
            await Util.RunAsync(
                "go",
                $"mod edit -replace={packageName}={localPath}",
                projectDirectory,
                outputBuilder: outputBuilder,
                errorBuilder: errorBuilder);
        }

        private static void BackupFile(string path)
        {
            if (File.Exists(path))
            {
                File.Copy(path, path + ".bak", overwrite: true);
            }
        }

        private static void RestoreBackup(string path)
        {
            var backup = path + ".bak";
            if (File.Exists(backup))
            {
                File.Move(backup, path, overwrite: true);
            }
        }
    }
}