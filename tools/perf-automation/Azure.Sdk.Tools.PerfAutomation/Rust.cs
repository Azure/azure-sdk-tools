﻿using Azure.Sdk.Tools.PerfAutomation.Models;
using Microsoft.Crank.Agent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Azure.Sdk.Tools.PerfAutomation
{
    namespace Models.Rust
    {
        //{ "sampling_mode":"Linear","iters":[216428.0, 432856.0, 649284.0, 865712.0, 1082140.0, 1298568.0, 1514996.0, 1731424.0, 1947852.0, 2164280.0],"times":[89284700.0, 182949300.0, 279225800.0, 370553800.0, 459758900.0, 544908000.0, 647300900.0, 757810300.0, 831844100.0, 926051100.0]}
        struct Samples
        {
            public string SamplingMode { get; set; }
            public double[] Iters { get; set; }
            public double[] Times { get; set; }
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
        private const string _cargoName = "cargo";
        private string _resultsDirectory = Path.Combine("target", "criterion");
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
            // just make sure we have the target directory clened up of previous results in case of a test issue in a previous test / run
            _targetResultsDirectory = await CleanupAsync(project);
            return (String.Empty, String.Empty, String.Empty);
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
            // set up the params for cargo
            string finalParams = $"bench -- \"{testName}\"";
            ProcessResult result = new ProcessResult(0, String.Empty, String.Empty);
            if (IsTest)
            {
                UtilMethodCall(this, new UtilEventArgs(
                    "RunAsync",
                    new string[] {
                        _cargoName,
                        finalParams,
                        WorkingDirectory}));
                result = new ProcessResult(0, "cargo bench result", "error");
            }
            else
            {
                result = await Util.RunAsync(_cargoName, finalParams, WorkingDirectory);
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
            var results = ParseFromJsonFile(Path.Combine(_targetResultsDirectory, testName, "new\\sample.json"));
            double nanos = 0.0;
            // the timings are in nanos, also we have to divide the time by the number of iterations to get the time for one operation
            for (int i = 0; i < results.Iters.Length; i++)
            {
                nanos = (nanos * i + (results.Times[i] / results.Iters[i])) / (i + 1);
            }
            // once we have the timing of one operation( in nano seconds) divinde one sec in nanos by the time to get ops/sec
            return Math.Pow(10, 9) / nanos;
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
