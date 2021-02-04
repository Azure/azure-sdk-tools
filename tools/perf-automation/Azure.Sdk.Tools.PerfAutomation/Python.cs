//using Azure.Sdk.Tools.PerfAutomation.Models;
//using System.Collections.Generic;
//using System.IO;
//using System.Runtime.InteropServices;
//using System.Text;
//using System.Text.RegularExpressions;
//using System.Threading.Tasks;

//namespace Azure.Sdk.Tools.PerfAutomation
//{
//    static class Python
//    {
//        private const string _env = "env-perf";
//        private static readonly string _envBin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "scripts" : "bin";
//        private static readonly string _python = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "python" : "python3";

//        private static async Task SetPackageVersions(string workingDirectory, IDictionary<string, string> packageVersions,
//            StringBuilder outputBuilder, StringBuilder errorBuilder)
//        {
//            var env = Path.Combine(workingDirectory, _env);

//            if (Directory.Exists(env))
//            {
//                Directory.Delete(env, recursive: true);
//            }

//            // Create venv
//            await Util.RunAsync(_python, $"-m venv {_env}", workingDirectory, outputBuilder, errorBuilder);

//            var python = Path.Combine(env, _envBin, "python");
//            var pip = Path.Combine(env, _envBin, "pip");

//            // Upgrade pip
//            await Util.RunAsync(python, "-m pip install --upgrade pip", workingDirectory, outputBuilder, errorBuilder);

//            // Install dev reqs
//            await Util.RunAsync(pip, "install -r dev_requirements.txt", workingDirectory, outputBuilder, errorBuilder);

//            // TODO: Support multiple packages if possible.  Maybe by force installing?
//            foreach (var v in packageVersions)
//            {
//                var packageName = v.Key;
//                var packageVersion = v.Value;

//                if (packageVersion == "master")
//                {
//                    await Util.RunAsync(pip, "install -e .", workingDirectory, outputBuilder, errorBuilder);
//                }
//                else
//                {
//                    await Util.RunAsync(pip, $"install {packageName}=={packageVersion}", workingDirectory, outputBuilder, errorBuilder);
//                }
//            }
//        }

//        private static void UnsetPackageVersions(string workingDirectory)
//        {
//            Directory.Delete(Path.Combine(workingDirectory, _env), recursive: true);
//        }

//        public static async Task<Result> RunAsync(
//            LanguageSettingsOld languageSettings, string arguments, IDictionary<string, string> packageVersions)
//        {
//            var errorBuilder = new StringBuilder();
//            var outputBuilder = new StringBuilder();

//            var workingDirectory = Path.Combine(Program.Config.WorkingDirectories[LanguageName.Python], languageSettings.Project);
//            var env = Path.Combine(workingDirectory, _env);
//            var pip = Path.Combine(env, _envBin, "pip");
//            var perfstress = Path.Combine(env, _envBin, "perfstress");

//            try
//            {
//                await SetPackageVersions(workingDirectory, packageVersions, errorBuilder, outputBuilder);

//                // Dump package versions to std output
//                await Util.RunAsync(pip, "freeze", workingDirectory, outputBuilder, errorBuilder);

//                var result = await Util.RunAsync(
//                    perfstress,
//                    $"{languageSettings.TestName} {arguments} {languageSettings.AdditionalArguments}",
//                    Path.Combine(workingDirectory, "tests"),
//                    outputBuilder,
//                    errorBuilder
//                );

//                // TODO: Why does Python perf framework write to StdErr instead of StdOut?
//                var match = Regex.Match(result.StandardError, @"\((.*) ops/s", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);
//                var opsPerSecond = double.Parse(match.Groups[1].Value);

//                return new Result
//                {
//                    OperationsPerSecond = opsPerSecond,
//                    StandardOutput = outputBuilder.ToString(),
//                    StandardError = errorBuilder.ToString()
//                };
//            }
//            finally
//            {
//                UnsetPackageVersions(workingDirectory);
//            }
//        }

//        /*
//        === Warmup ===
//        Current         Total           Average
//        3103684         3103684         2879624.40

//        === Results ===
//        Completed 5,735,961 operations in a weighted-average of 2.00s (2,867,847.51 ops/s, 0.000 s/op)

//        === Test ===
//        Current         Total           Average
//        3116721         3116721         2854769.61

//        === Results ===
//        Completed 5,718,534 operations in a weighted-average of 2.00s (2,858,373.57 ops/s, 0.000 s/op)
//        */
//    }
//}
