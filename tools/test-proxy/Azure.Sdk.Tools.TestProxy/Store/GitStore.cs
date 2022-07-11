using System.IO;
using Azure.Sdk.Tools.TestProxy.Common;
using System.Net;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;

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

    // Locating Assets Repo
        // -ResolveAssetsStoreLocation
        // -ResolveAssetRepoLocation
        // -IsAssetsRepoInitialized

    // Interacting with Assets Repo
        // CheckoutRepoAtConfig
        // InitializeAssetsRepo
        // DetectPendingChanges
        // ResetAssetsRepo
        // PushAssetsRepoUpdate

    // Generic "Target to Targeted Git Repo for current config"
        // -GetDefaultBranch
        // -ResolveCheckoutPaths
        // ResolveTargetBranch resolve presence of autobranch
        // UpdateAssetsJson

    // Targeted "get user decision" that can accept user input or no. depending on how it's been called.
        // do we need to add a bit for mode? that way we can either set TRUE for cli interrupt, but FALSE for the server calls

    public class GitStore : IAssetsStore
    {
        private HttpClient httpClient = new HttpClient();
        public string DefaultBranch = "main";


        #region push, reset, restore implementations
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
        #endregion

        #region git process interactions
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
        #endregion

        #region code repo interactions
        /// <summary>
        /// Reaches out to a git repo and resolves the default branch
        /// </summary>
        /// <param name="config">A valid and populated GitAssetsConfiguration generated from a assets.json.</param>
        /// <returns>The default branch</returns>
        public async Task<string> GetDefaultBranch(GitAssetsConfiguration config)
        {
            var gitCommand = BasicGitInvocation(config.RepoRoot);
            var token = Environment.GetEnvironmentVariable("GIT_TOKEN");

            HttpRequestMessage msg = new HttpRequestMessage()
            {
                RequestUri = new Uri($"https://api.github.com/repos/{config.AssetsRepo}"),
                Method = HttpMethod.Get
            };

            if (token != null)
            {
                msg.Headers.Authorization = new AuthenticationHeaderValue("token", token);
                msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                msg.Headers.Add("User-Agent", "Azure-Sdk-Test-Proxy");
            }

            var webResult = await httpClient.SendAsync(msg);

            if (webResult.StatusCode == HttpStatusCode.OK)
            {
                var doc = JsonDocument.Parse(webResult.Content.ReadAsStream(), options: new JsonDocumentOptions() { AllowTrailingCommas = true });
                if (doc.RootElement.TryGetProperty("default_branch", out var result))
                {
                    return result.ToString();
                }
            }

            return DefaultBranch;
        }

        public Task<string> ResolveTargetBranch(GitAssetsConfiguration config)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Used to ascend to the repo root of any given startup path. Unlike ResolveAssetsJson, which implements similar ascension logic, this function returns the repo root, NOT the assets.json.
        /// </summary>
        /// <param name="path"></param>
        /// <returns>An absolute path to the discovered repo root.</returns>
        /// <exception cref="HttpException"></exception>
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

        /// <summary>
        /// Given a startup path, ascend the directory tree until we either reach git root (success) or disk root (failure).
        /// </summary>
        /// <param name="inputPath">A valid directory. If passed an assets json file directly instead of a directory, that value will be returned.</param>
        /// <returns>A path to a file named "assets.json"</returns>
        /// <exception cref="HttpException"></exception>
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

        #endregion

        #region assets repo interactions
        public string ResolveAssetsStoreLocation(GitAssetsConfiguration config, bool autoCreate = true)
        {
            var location = Path.Join(config.RepoRoot, ".assets");
            if (!Directory.Exists(location) && autoCreate)
            {
                Directory.CreateDirectory(location);
            }

            return location;
        }

        public string ResolveAssetRepoLocation(GitAssetsConfiguration config, bool autoCreate = true)
        {
            var assetsStore = ResolveAssetsStoreLocation(config, autoCreate: autoCreate);
            var location = Path.Join(assetsStore, config.RepoRoot.GetHashCode().ToString());
            if (!Directory.Exists(location) && autoCreate)
            {
                Directory.CreateDirectory(location);
            }

            return location;
        }

        public bool IsAssetsRepoInitialized(GitAssetsConfiguration config, bool autoCreate = true)
        {
            var location = Path.Join(ResolveAssetRepoLocation(config, autoCreate: autoCreate), ".git");

            return Directory.Exists(location);
        }

        public string ResolveCheckoutPaths(GitAssetsConfiguration config)
        {
            var assetsRepoPath = ResolveAssetRepoLocation(config);

            var combinedPath = Path.Join(config.AssetsRepoPrefixPath, config.AssetsJsonRelativeLocation);


            throw new NotImplementedException();
        }

        public void UpdateAssetsJson(GitAssetsConfiguration config)
        {

        }
        #endregion
    }
}
