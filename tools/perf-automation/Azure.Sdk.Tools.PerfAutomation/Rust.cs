using Azure.Sdk.Tools.PerfAutomation.Models;
using Microsoft.Crank.Agent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Azure.Sdk.Tools.PerfAutomation
{
    namespace Models.Rust
    {
        //{ "sampling_mode":"Linear","iters":[216428.0, 432856.0, 649284.0, 865712.0, 1082140.0, 1298568.0, 1514996.0, 1731424.0, 1947852.0, 2164280.0],"times":[89284700.0, 182949300.0, 279225800.0, 370553800.0, 459758900.0, 544908000.0, 647300900.0, 757810300.0, 831844100.0, 926051100.0]}
        struct Samples
        {
            [JsonPropertyName("test_name")]
            public string TestName { get; set; }

            [JsonPropertyName("operations_per_second")]
            public double OperationsPerSecond { get; set; }

            [JsonPropertyName("average_cpu_use")]
            public double? AverageCpuUse { get; set; }

            [JsonPropertyName("average_memory_use")]
            public long? AverageMemoryUse { get; set; }
        }
    }

    public class Rust : LanguageBase
    {
        public class UtilEventArgs : EventArgs
        {
            public UtilEventArgs(string methodName, string[] methodParams)
            {
                this.MethodName = methodName;
                this.Params = methodParams;
            }

            public string MethodName { get; set; }
            public string[] Params { get; set; } = null;
        }

        private const string _sdkDirectory = "sdk";
        private string _resultsDirectory = "testResults";
        private string _executablePath = "";
        private string _targetResultsDirectory;
        public bool IsTest { get; set; } = false;
        public bool IsWindows { get; set; } = Util.IsWindows;
        protected override Language Language => Language.Rust;
        public event EventHandler<UtilEventArgs> UtilMethodCall;

        public override async Task<(string output, string error, object context)> SetupAsync(
            string project,
            string languageVersion,
            string primaryPackage,
            IDictionary<string, string> packageVersions,
            bool debug)
        {

            // just make sure we have the target directory cleaned up of previous results in case of a test issue in a previous test / run
            _targetResultsDirectory = await CleanupAsync(project);
            Directory.CreateDirectory(_targetResultsDirectory);

            string flavor = debug ? "debug" : "release";
            string testCommand = $"--{flavor} --package {primaryPackage} --bench perf ";
            var result = await Util.RunAsync("cargo", $"build {testCommand} --message-format=json", WorkingDirectory, log: false);
            // Look for errors in the build.
            if (result.ExitCode != 0)
            {
                return (result.StandardOutput, result.StandardError, null);
            }

            Console.WriteLine("Parsing build output to find test executables...");
            string[] json_elements = result.StandardOutput.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            string[] executables = new string[0];
            foreach (string element in json_elements)
            {
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(element))
                    {
                        if (doc.RootElement.TryGetProperty("executable", out JsonElement executableElement) && executableElement.ValueKind == JsonValueKind.String)
                        {
                            Console.WriteLine($"Found executable: {executableElement.GetString()}");
                            executables = executables.Append(executableElement.GetString()).ToArray();
                        }
                    }
                }
                catch (JsonException e)
                {
                    Console.WriteLine($"Exception {e} parsing line as JSON, skipping line.");
                    // Ignore lines that are not valid JSON
                }
            }
            if (executables.Length != 1)
            {
                Console.WriteLine($"Found {executables.Length} test executables after building the project.");
                foreach (var exe in executables)
                {
                    Console.WriteLine($"Executable: {exe}");
                }
                throw new Exception($"No test executables were found after building the project");
            }
            _executablePath = executables[0];

            return (result.ToString(), result.ToString(), null);
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
            // The Rust SDK uses environment variables to determine if the tests should run in playback or live mode.
            // If --test-proxy is in the command line arguments, set AZURE_TEST_MODE environment variable to "playback" to indicate that we are going to use the test proxy.
            // Otherwise set test mode to `live`.
            if (arguments.Contains("test-proxy"))
            {
                Environment.SetEnvironmentVariable("AZURE_TEST_MODE", "playback");
            }
            else
            {
                Environment.SetEnvironmentVariable("AZURE_TEST_MODE", "live");
            }
            ProcessResult result = null;
            string testParams = $" --test-results {_targetResultsDirectory}/{testName}-results.json \"{testName}\" {arguments}";
            if (_executablePath != String.Empty)
            {
                if (IsTest)
                {
                    UtilMethodCall(this, new UtilEventArgs(
                        "RunAsync",
                        new string[] {
                        _executablePath,
                        testParams,
                        WorkingDirectory}));
                    result = new ProcessResult(0, "cargo bench result", "error");
                }
                else
                {
                    result = await Util.RunAsync(_executablePath, testParams, WorkingDirectory);
                }
            }
            else
            {
                throw new Exception("No test executable found to run the benchmark.");
            }

            if (result.ExitCode != 0)
            {
                throw new Exception($"Error running the benchmark test {testName} in project {project}. Error: {result.StandardError}");
            }

            //parse the samples file for the test and calculate the ops per second
            var opsPerSecond = ExtractOpsPerSecond(testName);

            return new IterationResult
            {
                OperationsPerSecond = opsPerSecond,
                StandardOutput = result.StandardOutput,
                StandardError = result.StandardError,
                PackageVersions = packageVersions
            };
        }

        public override async Task<string> CleanupAsync(string project)
        {
            // we need to find the folder that contains SDK  directory. SDK is a sibling of target
            var currentDirectory = WorkingDirectory;
            bool sdkFolderFound = false;
            while (!sdkFolderFound)
            {
                DirectoryInfo directory = new DirectoryInfo(Path.Combine(currentDirectory, _sdkDirectory));
                if (directory.Exists)
                {
                    sdkFolderFound = true;
                }
                else
                {
                    currentDirectory = (new DirectoryInfo(currentDirectory)).Parent.FullName;
                }
            }
            // now that we have the path to the target directory wwe cand determine the results directory .../target/criterion
            var resultsDirectory = Path.Combine(currentDirectory, _resultsDirectory);

            if (IsTest)
            {
                UtilMethodCall(this, new UtilEventArgs("DeleteIfExists", new string[] { resultsDirectory }));
            }
            else
            {
                Util.DeleteIfExists(resultsDirectory);
            }
            await Task.Delay(0);
            // return the results directory so we can use it later on and avoid the discovery
            return resultsDirectory;
        }

        private double ExtractOpsPerSecond(string testName)
        {
            // the results are in the target directory under target/criterion/<testName>/new
            var results = ParseFromJsonFile(Path.Combine(_targetResultsDirectory, $"{testName}-results.json"));

            return results.OperationsPerSecond;
        }

        private Models.Rust.Samples ParseFromJsonFile(string filePath)
        {
            // open the file 
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"The file '{filePath}' does not exist.");
            }

            var jsonContent = File.ReadAllText(filePath);
            // deserialize in a Samples struct
            return JsonSerializer.Deserialize<Models.Rust.Samples>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true // Allows case-insensitive matching of JSON property names
            });
        }
    }
}
