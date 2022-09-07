using System.IO;
using System.Net;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Linq;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Console;

namespace Azure.Sdk.Tools.TestProxy.Store
{
    public class DirectoryEvaluation
    {
        public bool IsRoot;
        public bool IsGitRoot;
        public bool AssetsJsonPresent;
    }

    /// <summary>
    /// This class provides an abtraction for dealing with git assets that are stored in an external repository. An "assets.json" within a repo folder is used to inform targeting.
    /// </summary>
    public class GitStore : IAssetsStore
    {
        private HttpClient httpClient = new HttpClient();
        private IConsoleWrapper _consoleWrapper;
        public GitProcessHandler GitHandler = new GitProcessHandler();
        public string DefaultBranch = "main";
        public string FileName = "assets.json";
        public static readonly string EnvironmentVariableName = "PROXY_GIT_TOKEN";

        public GitStore() 
        {
            _consoleWrapper = new ConsoleWrapper();
        }

        public GitStore(IConsoleWrapper consoleWrapper)
        {
            _consoleWrapper = consoleWrapper;
        }

        public GitStore(GitProcessHandler processHandler) {
            GitHandler = processHandler;
        }

        #region push, reset, restore, and other asset repo implementations
        /// <summary>
        /// Given a config, locate the cloned assets.
        /// </summary>
        /// <param name="pathToAssetsJson"></param>
        /// <returns></returns>
        public async Task<string> GetPath(string pathToAssetsJson)
        {
            var config = await ParseConfigurationFile(pathToAssetsJson);
            return config.AssetsRepoLocation;
        }

        /// <summary>
        /// Pushes a set of changed files to the assets repo. Honors configuration of assets.json passed into it.
        /// </summary>
        /// <param name="pathToAssetsJson"></param>
        /// <returns></returns>
        public async Task Push(string pathToAssetsJson) {
            var config = await ParseConfigurationFile(pathToAssetsJson);
            var pendingChanges = DetectPendingChanges(config);
            var onLatestSHA = true;

            if (pendingChanges.Length > 0)
            {
                if(!GitHandler.TryRun($"rev-parse origin/{config.AssetsRepoBranch}", config, out CommandResult result))
                {
                    // if we have a nonzero exit code, only code 128 is acceptable.
                    if (result.ExitCode != 128)
                    {
                        throw GenerateInvokeException(result);
                    }
                }
                else
                {
                    var retrievedSHA = result.StdOut.Trim();
                    if (retrievedSHA != config.SHA)
                    {
                        onLatestSHA = false;
                    }
                }

                if (onLatestSHA)
                {
                    try
                    {
                        GitHandler.Run($"branch {config.AssetsRepoBranch}", config);
                        GitHandler.Run($"checkout {config.AssetsRepoBranch}", config);
                        GitHandler.Run($"add -A .", config);
                        GitHandler.Run($"commit -m \"Automatic asset update from test-proxy.\"", config);
                        GitHandler.Run($"push origin {config.AssetsRepoBranch}", config);
                    }
                    catch(GitProcessException e)
                    {
                        throw GenerateInvokeException(e.Result);
                    }
                }
                else
                {
                    try
                    {
                        GitHandler.Run($"stash", config);
                        GitHandler.Run($"fetch origin {config.AssetsRepoBranch}", config);
                        GitHandler.Run($"checkout {config.AssetsRepoBranch}", config);
                        GitHandler.Run($"stash pop", config);
                        GitHandler.Run($"add -A .", config);
                        GitHandler.Run($"commit -m \"Automatic asset update from test-proxy.\"", config);
                        GitHandler.Run($"push origin {config.AssetsRepoBranch}", config);
                    }
                    catch (GitProcessException e)
                    {
                        throw GenerateInvokeException(e.Result);
                    }
                }
            }
        }

        /// <summary>
        /// Restores a set of recordings from the assets repo. Honors configuration of assets.json passed into it.
        /// </summary>
        /// <param name="pathToAssetsJson"></param>
        /// <returns></returns>
        public async Task Restore(string pathToAssetsJson) {
            var config = await ParseConfigurationFile(pathToAssetsJson);
            var initialized = config.IsAssetsRepoInitialized();

            if (!initialized)
            {
                InitializeAssetsRepo(config);
            }

            CheckoutRepoAtConfig(config);
        }

        /// <summary>
        /// Resets a cloned assets repository to the default contained within the assets.json targeted commit. This
        /// function should only be called by the user as the server will only use Restore.
        /// </summary>
        /// <param name="pathToAssetsJson"></param>
        /// <returns></returns>
        public async Task Reset(string pathToAssetsJson) 
        {
            var config = await ParseConfigurationFile(pathToAssetsJson);
            var initialized = config.IsAssetsRepoInitialized();
            var allowReset = false;

            if (!initialized)
            {
                InitializeAssetsRepo(config);
            }

            var pendingChanges = DetectPendingChanges(config);

            if (pendingChanges.Length > 0)
            {
                _consoleWrapper.WriteLine("There are pending git chances, are you sure you want to reset? [Y|N]");
                while (true)
                {
                    string response = _consoleWrapper.ReadLine();
                    response = response.ToLowerInvariant();
                    if (response.Equals("y"))
                    {
                        allowReset = true;
                        break;
                    }
                    else if (response.Equals("n"))
                    {
                        allowReset = false;
                        break;
                    }
                    else
                    {
                        _consoleWrapper.WriteLine("Please answer [Y|N]");
                    }
                }
            }

            if (allowReset)
            {
                try
                {
                    GitHandler.Run("checkout *", config);
                    GitHandler.Run("clean -xdf", config);
                }
                catch(GitProcessException e)
                {
                    throw GenerateInvokeException(e.Result);
                }

                if (!string.IsNullOrWhiteSpace(config.SHA))
                {
                    CheckoutRepoAtConfig(config);
                }
            }
        }

        /// <summary>
        /// Given a CommandResult, generate an HttpException.
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public HttpException GenerateInvokeException(CommandResult result)
        {
            var message = $"Invocation of \"git {result.Arguments}\" had a non-zero exit code {result.ExitCode}.\nStdOut: {result.StdOut}\nStdErr: {result.StdErr}\n";

            return new HttpException(HttpStatusCode.InternalServerError, message);
        }

        /// <summary>
        /// Checks an asset repository for pending changes. Equivalent of "git status --porcelain".
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public string[] DetectPendingChanges(GitAssetsConfiguration config)
        {
            if (!GitHandler.TryRun("status --porcelain", config, out var diffResult))
            {
                throw GenerateInvokeException(diffResult);
            }

            if (!string.IsNullOrWhiteSpace(diffResult.StdOut))
            {
                // Normally, we'd use Environment.NewLine here but this doesn't work on Windows since its NewLine is \r\n and
                // Git's NewLine is just \n
                var individualResults = diffResult.StdOut.Split("\n").Select(x => x.Trim()).ToArray();
                return individualResults;
            }

            return new string[] {};
        }

        /// <summary>
        /// Given a configuration, set the sparse-checkout directory for the config, then attempt checkout of the targeted SHA.
        /// </summary>
        /// <param name="config"></param>
        public void CheckoutRepoAtConfig(GitAssetsConfiguration config)
        {
            var checkoutPaths = ResolveCheckoutPaths(config);

            try
            {
                // Set non-cone mode otherwise path filters will not work in git >= 2.37.0
                // See https://github.blog/2022-06-27-highlights-from-git-2-37/#tidbits
                GitHandler.Run($"sparse-checkout set --no-cone {checkoutPaths}", config);
                // The -c advice.detachedHead=false removes the verbose detatched head state
                // warning that happens when syncing sparse-checkout to a particular SHA
                GitHandler.Run($"-c advice.detachedHead=false checkout {config.SHA}", config);
            }
            catch(GitProcessException e)
            {
                throw GenerateInvokeException(e.Result);
            }
        }

        public string GetCloneUrl(GitAssetsConfiguration config)
        {
            var gitToken = Environment.GetEnvironmentVariable(EnvironmentVariableName);

            if (!string.IsNullOrWhiteSpace(gitToken)){
                return $"https://{gitToken}@github.com/{config.AssetsRepo}";
            }

            return $"https://github.com/{config.AssetsRepo}";
        }

        /// <summary>
        /// Initializes an asset repo for a given configuration. This includes creating the target repo directory, cloning, and taking care of initial restore operations.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="forceInit"></param>
        /// <returns></returns>
        public bool InitializeAssetsRepo(GitAssetsConfiguration config, bool forceInit = false)
        {
            var assetRepo = config.AssetsRepoLocation;
            var initialized = config.IsAssetsRepoInitialized();
            var workCompleted = false;

            if (forceInit)
            {
                DirectoryHelper.DeleteGitDirectory(assetRepo);
                Directory.CreateDirectory(assetRepo);
                initialized = false;
            }

            if (!initialized)
            {
                try
                {
                    // The -c core.longpaths=true is basically for Windows and is a noop for other platforms
                    GitHandler.Run($"clone -c core.longpaths=true --no-checkout --filter=tree:0 {GetCloneUrl(config)} .", config);
                    GitHandler.Run($"sparse-checkout init", config);
                }
                catch(GitProcessException e)
                {
                    throw GenerateInvokeException(e.Result);
                }

                CheckoutRepoAtConfig(config);
                workCompleted = true;
            }

            return workCompleted;
        }

        /// <summary>
        /// Evaluates an assets configuration and returns the correct sparse checkout path.
        /// </summary>
        /// <param name="config"></param>
        /// <returns>A relative path for use within the assets repo.</returns>
        /// <exception cref="NotImplementedException"></exception>
        public string ResolveCheckoutPaths(GitAssetsConfiguration config)
        {
            var combinedPath = Path.Join(config.AssetsRepoPrefixPath ?? String.Empty, config.AssetsJsonRelativeLocation).Replace("\\", "/");

            if (combinedPath.ToLower() == FileName)
            {
                return "./";
            }
            else
            {
                return combinedPath.Substring(0, combinedPath.Length - (FileName.Length + 1));
            }
        }
        #endregion

        #region code repo interactions
        /// <summary>
        /// Parses a configuration assets.json into a strongly typed representation of the same. A GitAssetConfiguration is used to describe work throughout the GitStore.
        /// </summary>
        /// <param name="assetsJsonPath"></param>
        /// <returns></returns>
        /// <exception cref="HttpException"></exception>
        public async Task<GitAssetsConfiguration> ParseConfigurationFile(string assetsJsonPath)
        {
            if (!File.Exists(assetsJsonPath) && !Directory.Exists(assetsJsonPath))
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"The provided {FileName} path of \"{assetsJsonPath}\" does not exist.");
            }

            var pathToAssets = ResolveAssetsJson(assetsJsonPath);
            var assetsContent = await File.ReadAllTextAsync(pathToAssets);

            if (string.IsNullOrWhiteSpace(assetsContent) || assetsContent.Trim() == "{}")
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"The provided {FileName} at \"{assetsJsonPath}\" did not have valid json present.");
            }

            try
            {
                var assetConfig = JsonSerializer.Deserialize<GitAssetsConfiguration>(assetsContent, options: new JsonSerializerOptions() { AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip });

                if (string.IsNullOrWhiteSpace(assetConfig.AssetsRepo))
                {
                    throw new HttpException(HttpStatusCode.BadRequest, $"Unable to utilize the {FileName} present at \"{assetsJsonPath}. It must contain value for the key \"AssetsRepo\" to be considered a valid {FileName}.");
                }

                var repoRoot = AscendToRepoRoot(pathToAssets);

                assetConfig.AssetsJsonLocation = pathToAssets;
                assetConfig.AssetsJsonRelativeLocation = Path.GetRelativePath(repoRoot, pathToAssets);
                assetConfig.RepoRoot = repoRoot;
                assetConfig.AssetsFileName = FileName;

                return assetConfig;
            }
            catch (Exception e)
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"Unable to parse {FileName} content at \"{assetsJsonPath}\". Exception: {e.Message}");
            }
        }

        /// <summary>
        /// Reaches out to a git repo and resolves the name of the default branch.
        /// </summary>
        /// <param name="config">A valid and populated GitAssetsConfiguration generated from a assets.json.</param>
        /// <returns>The default branch</returns>
        public async Task<string> GetDefaultBranch(GitAssetsConfiguration config)
        {
            var token = Environment.GetEnvironmentVariable(EnvironmentVariableName);

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

        /// <summary>
        /// Reaches out to the assets repo and confirms presence of auto branch.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public string ResolveCheckoutBranch(GitAssetsConfiguration config)
        {
            GitHandler.TryRun($"rev-parse \"origin/{config.AssetsRepoBranch}\"", config, out var commandResult);

            switch (commandResult.ExitCode)
            {
                case 0:   // there is indeed a branch that exists with that name
                    return config.AssetsRepoBranch;
                case 128: // not a git repo
                    throw new HttpException(HttpStatusCode.BadRequest, commandResult.StdOut); ;
            }

            return DefaultBranch;
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
            if (inputPath.ToLowerInvariant().EndsWith(FileName))
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
                return Path.Join(inputPath, FileName);
            }

            throw new HttpException(HttpStatusCode.BadRequest, $"Unable to locate an {FileName} at or above the targeted directory \"{originalPath}\".");
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

            var assetsJsonLocation = Path.Join(directoryPath, FileName);
            var gitLocation = Path.Join(directoryPath, ".git");

            return new DirectoryEvaluation()
            {
                AssetsJsonPresent = File.Exists(assetsJsonLocation),
                IsGitRoot = File.Exists(gitLocation) || Directory.Exists(gitLocation),
                IsRoot = new DirectoryInfo(directoryPath).Parent == null
            };
        }

        /// <summary>
        /// Do we have a new update for the assets.json? Right now, only the recording SHA is automatically updatable by the test-proxy.
        /// </summary>
        /// <param name="newSha"></param>
        /// <param name="config"></param>
        public async Task UpdateAssetsJson(string newSha, GitAssetsConfiguration config)
        {
            // only do work if the SHAs aren't equivalent
            if (config.SHA != newSha)
            {
                config.SHA = newSha;

                // we deliberately do an extremely stripped down version parse and update here. We do this primarily to maintain
                // any comments left in the assets.json though maintaining attribute ordering is also nice. To do this, we read all the file content, then
                // simply replace the existing SHA value with the new one, then write the content back to the json file.

                var currentSHA = (await ParseConfigurationFile(config.AssetsJsonLocation)).SHA;
                var content = await File.ReadAllTextAsync(config.AssetsJsonLocation);
                content = content.Replace(currentSHA, newSha);

                File.WriteAllText(config.AssetsJsonLocation, content);
            }
        }
        #endregion
    }
}
