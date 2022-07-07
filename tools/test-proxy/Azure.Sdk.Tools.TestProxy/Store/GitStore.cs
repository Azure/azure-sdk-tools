using System.IO;
using Azure.Sdk.Tools.TestProxy.Common;
using System.Net;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Azure.Sdk.Tools.TestProxy.Store
{
    public class DirectoryEvaluation
    {
        public bool IsRoot;
        public bool IsGitRoot;
        public bool AssetsJsonPresent;
    }

    public class CommandResult
    {
        public int ReturnCode;
        public string StdErr;
        public string StdOut;

    }

    public class GitStore : IAssetsStore
    {
        public async Task Push(string pathToAssetsJson, string contextPath) {
            var config = await ParseConfigurationFile(pathToAssetsJson);
            var gitCommand = BasicGitInvocation(config.RepoRoot);

            // need to add further arguments
            throw new NotImplementedException();
        }

        public async Task Restore(string pathToAssetsJson, string contextPath) {
            var config = await ParseConfigurationFile(pathToAssetsJson);
            var gitCommand = BasicGitInvocation(config.RepoRoot);

            // need to add further arguments
            throw new NotImplementedException();
        }

        public async Task Reset(string pathToAssetsJson, string contextPath) {
            var config = await ParseConfigurationFile(pathToAssetsJson);
            var gitCommand = BasicGitInvocation(config.RepoRoot);

            // need to add further arguments
            throw new NotImplementedException();
        }

        public async Task<GitAssetsConfiguration> ParseConfigurationFile(string assetsJsonPath)
        {
            if (!File.Exists(assetsJsonPath) && !Directory.Exists(assetsJsonPath)) {
                throw new HttpException(HttpStatusCode.BadRequest, $"The provided assets json path of \"{assetsJsonPath}\" does not exist.");
            }

            var pathToAssets = ResolveAssetsJson(assetsJsonPath);
            var assetsContent = await File.ReadAllTextAsync(pathToAssets);

            if (string.IsNullOrWhiteSpace(assetsContent) || assetsContent.Trim() == "{}")
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"The provided assets json at \"{assetsJsonPath}\" did not have valid json present.");
            }
            
            try
            {
                var assetConfig = JsonSerializer.Deserialize<GitAssetsConfiguration>(assetsContent, options: new JsonSerializerOptions() { AllowTrailingCommas = true });

                if (string.IsNullOrWhiteSpace(assetConfig.AssetsRepo))
                {
                    throw new HttpException(HttpStatusCode.BadRequest, $"Unable to utilize the assets.json present at \"{assetsJsonPath}. It must contain value for the key \"AssetsRepo\" to be considered a valid assets.json.");
                }

                var repoRoot = AscendToRepoRoot(pathToAssets);
                
                assetConfig.AssetsJsonLocation = pathToAssets;
                assetConfig.AssetsJsonRelativeLocation = Path.GetRelativePath(repoRoot, pathToAssets);
                assetConfig.RepoRoot = repoRoot;

                return assetConfig;
            }
            catch (Exception e)
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"Unable to parse assets.json content at \"{assetsJsonPath}\". Exception: {e.Message}");
            }
        }

        public ProcessStartInfo BasicGitInvocation(string workingDirectory)
        {
            var startInfo = new ProcessStartInfo("git")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory,
            };

            startInfo.EnvironmentVariables["PATH"] = Environment.GetEnvironmentVariable("PATH");

            return startInfo;
        }

        public CommandResult RunProcess(ProcessStartInfo processStartInfo)
        {
            Process process = null;

            try
            {
                process = Process.Start(processStartInfo);
                string output = process.StandardOutput.ReadToEnd();
                string errorOutput = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return new CommandResult()
                {
                    ReturnCode = process.ExitCode,
                    StdErr = output,
                    StdOut = errorOutput
                };
            }
            catch (Exception e)
            {
                throw new HttpException(HttpStatusCode.BadRequest, "Unable to locate git command.");
            }
        }


        public string AscendToRepoRoot(string path)
        {
            var originalPath = path.Clone();
            var fileAttributes = File.GetAttributes(path);
            if (!(fileAttributes == FileAttributes.Directory))
            {
                path = Path.GetDirectoryName(path);
            }

            while (true)
            {
                var evaluation = EvaluateDirectory(path);

                if (evaluation.IsGitRoot)
                {
                    return path;
                }
                else if (evaluation.IsRoot)
                {
                    throw new HttpException(HttpStatusCode.BadRequest, $"The target directory \"{originalPath}\" does not exist within a git repository. This is disallowed when utilizing git store.");
                }

                path = Path.GetDirectoryName(path);
            }
        }

        public string ResolveAssetsJson(string inputPath)
        {
            if (inputPath.ToLowerInvariant().EndsWith("assets.json"))
            {
                return inputPath;
            }

            var originalPath = inputPath.Clone();
            var directoryEval = EvaluateDirectory(inputPath);

            while (!directoryEval.IsRoot && !directoryEval.IsGitRoot && !directoryEval.AssetsJsonPresent)
            {
                inputPath = Path.GetDirectoryName(inputPath);
                directoryEval = EvaluateDirectory(inputPath);
            }

            if (directoryEval.AssetsJsonPresent)
            {
                return Path.Join(inputPath, "assets.json");
            }

            throw new HttpException(HttpStatusCode.BadRequest, $"Unable to locate an assets.json at or above the targeted directory \"{originalPath}\".");
        }

        /// <summary>
        /// Evaluates a directory and determines whether it contains an assets json, whether it is a git repo root, and if it is a root folder.
        /// </summary>
        /// <param name="directoryPath">Path to a directory. If given an actual file path, it will use the directory CONTAINING that file as the directory it is evaluating.</param>
        /// <returns></returns>
        public DirectoryEvaluation EvaluateDirectory(string directoryPath)
        {
            var fileAttributes = File.GetAttributes(directoryPath);
            
            if (!(fileAttributes == FileAttributes.Directory))
            {
                directoryPath = Path.GetDirectoryName(directoryPath);
            }

            var assetsJsonLocation = Path.Join(directoryPath, "assets.json");
            var gitLocation = Path.Join(directoryPath, ".git");

            return new DirectoryEvaluation()
            {
                AssetsJsonPresent = File.Exists(assetsJsonLocation),
                IsGitRoot = File.Exists(gitLocation),
                IsRoot = new DirectoryInfo(directoryPath).Parent == null
            };
        }
    }
}
