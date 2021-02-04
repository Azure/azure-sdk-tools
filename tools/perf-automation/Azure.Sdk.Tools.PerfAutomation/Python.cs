using Azure.Sdk.Tools.PerfAutomation.Models;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public class Python : ILanguage
    {
        private const string _env = "env-perf";
        private static readonly string _envBin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "scripts" : "bin";
        private static readonly string _python = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "python" : "python3";

        public async Task<(string output, string error, string context)> SetupAsync(string project, IDictionary<string, string> packageVersions)
        {
            var workingDirectory = Path.Combine(Program.Config.WorkingDirectories[Language.Python], project);
            var env = Path.Combine(workingDirectory, _env);

            if (Directory.Exists(env))
            {
                Directory.Delete(env, recursive: true);
            }

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            // Create venv
            await Util.RunAsync(_python, $"-m venv {_env}", workingDirectory, outputBuilder, errorBuilder);

            var python = Path.Combine(env, _envBin, "python");
            var pip = Path.Combine(env, _envBin, "pip");

            // Upgrade pip
            await Util.RunAsync(python, "-m pip install --upgrade pip", workingDirectory, outputBuilder, errorBuilder);

            // Install dev reqs
            await Util.RunAsync(pip, "install -r dev_requirements.txt", workingDirectory, outputBuilder, errorBuilder);

            // TODO: Support multiple packages if possible.  Maybe by force installing?
            foreach (var v in packageVersions)
            {
                var packageName = v.Key;
                var packageVersion = v.Value;

                if (packageVersion == "master")
                {
                    await Util.RunAsync(pip, "install -e .", workingDirectory, outputBuilder, errorBuilder);
                }
                else
                {
                    await Util.RunAsync(pip, $"install {packageName}=={packageVersion}", workingDirectory, outputBuilder, errorBuilder);
                }
            }

            return (outputBuilder.ToString(), errorBuilder.ToString(), null);
        }

        public async Task<Result> RunAsync(string project, string testName, string arguments, string context)
        {
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            var workingDirectory = Path.Combine(Program.Config.WorkingDirectories[Language.Python], project);
            var env = Path.Combine(workingDirectory, _env);
            var pip = Path.Combine(env, _envBin, "pip");
            var perfstress = Path.Combine(env, _envBin, "perfstress");

            // Dump package versions to std output
            await Util.RunAsync(pip, "freeze", workingDirectory, outputBuilder, errorBuilder);

            var processResult = await Util.RunAsync(
                perfstress,
                $"{testName} {arguments}",
                Path.Combine(workingDirectory, "tests"),
                outputBuilder,
                errorBuilder
            );

            // TODO: Why does Python perf framework write to StdErr instead of StdOut?
            var match = Regex.Match(processResult.StandardError, @"\((.*) ops/s", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);
            var opsPerSecond = double.Parse(match.Groups[1].Value);

            return new Result
            {
                OperationsPerSecond = opsPerSecond,
                StandardOutput = outputBuilder.ToString(),
                StandardError = errorBuilder.ToString()
            };
        }

        public Task CleanupAsync(string project)
        {
            var workingDirectory = Path.Combine(Program.Config.WorkingDirectories[Language.Python], project);
            Directory.Delete(Path.Combine(workingDirectory, _env), recursive: true);
            return Task.CompletedTask;
        }

        /*
        === Warmup ===
        Current         Total           Average
        3103684         3103684         2879624.40

        === Results ===
        Completed 5,735,961 operations in a weighted-average of 2.00s (2,867,847.51 ops/s, 0.000 s/op)

        === Test ===
        Current         Total           Average
        3116721         3116721         2854769.61

        === Results ===
        Completed 5,718,534 operations in a weighted-average of 2.00s (2,858,373.57 ops/s, 0.000 s/op)
        */
    }
}
