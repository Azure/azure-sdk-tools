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

        private const string _targetDirectory = "target";
        private const string _sdkDirectory = "sdk";
        private const string _cargoName = "cargo";
        private const string _buildCommand = "build";
        private const string _vcpkgFile = "vcpkg.json";
        public bool IsTest { get; set; } = false;
        public bool IsWindows { get; set; } = Util.IsWindows;
        public int ProcessorCount { get; set; } = Environment.ProcessorCount;
        protected override Language Language => Language.Rust;
        public event EventHandler<UtilEventArgs> UtilMethodCall;

        public override async Task<(string output, string error, object context)> SetupAsync(
            string project,
            string languageVersion,
            string primaryPackage,
            IDictionary<string, string> packageVersions,
            bool debug)
        {
            var currentDirectory = WorkingDirectory;
            bool sdkFolderFound = false;
            while(!sdkFolderFound)
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
            var buildDirectory = Path.Combine(currentDirectory, _targetDirectory);
            if (IsTest)
            {
                UtilMethodCall(this, new UtilEventArgs("DeleteIfExists", new string[] { buildDirectory }));
            }
            else
            {
                Util.DeleteIfExists(buildDirectory);
            }

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();
            

            if (IsTest)
            {
                outputBuilder.Append("output");
                errorBuilder.Append("error");
                
                UtilMethodCall(this, new UtilEventArgs(
                    "RunAsync2",
                    new string[]
                    {
                        _cargoName,
                        _buildCommand,
                        buildDirectory,
                        outputBuilder.ToString(),
                        errorBuilder.ToString()
                    }));
                return (outputBuilder.ToString(), errorBuilder.ToString(), String.Empty);
            }
            else
            {
                var result = await Util.RunAsync(
                    _cargoName, _buildCommand,
                    WorkingDirectory, outputBuilder: outputBuilder, errorBuilder: errorBuilder);

                return (result.StandardOutput, result.StandardError, String.Empty);
            }
        }
        public override async Task<IterationResult> RunAsync(
            string project,
            string languageVersion,
            string primaryPackage,
            IDictionary<string, string> packageVersions,
            string testName,
            string testFullName,
            string arguments,
            bool profile,
            string profilerOptions,
            object context)
        {
            return await RunAsync(
                project,
                languageVersion,
                primaryPackage,
                packageVersions,
                testFullName,
                arguments,
                profile,
                profilerOptions,
                context);
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
            string finalParams = $"bench -- {testName}";
            ProcessResult result = new ProcessResult(0, String.Empty, String.Empty);
            if (IsTest)
            {
                UtilMethodCall(this, new UtilEventArgs(
                    "RunAsync",
                    new string[] {
                        _cargoName,
                        finalParams,
                        WorkingDirectory}));
                result = new ProcessResult(0, "output (2.0 ops/s, 1.0 s/op)", "error");
            }
            else
            {
                result = await Util.RunAsync(_cargoName, finalParams, WorkingDirectory);
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
           // var buildDirectory = Path.Combine(WorkingDirectory, _buildDirectory);
            //Util.DeleteIfExists(buildDirectory);
            //cleanup the vcpkg file by restoring from git
            await Util.RunAsync("git", $"checkout -- {fullVcpkgPath}", WorkingDirectory);
            return;
        }

       
    }
}
