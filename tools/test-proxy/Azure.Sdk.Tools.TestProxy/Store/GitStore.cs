using System.IO;
using Azure.Sdk.Tools.TestProxy.Common;
using System.Net;
using System;
using Newtonsoft.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Store
{
    public class DirectoryEvaluation
    {
        public bool IsRoot;
        public bool IsGitRoot;
        public bool AssetsJsonPresent;
    }

    public class GitStore : IAssetsStore
    {
        

        public void Push(string pathToAssetsJson, string contextPath) {
            var config = ParseConfigurationFile(pathToAssetsJson);

            throw new NotImplementedException();
        }

        public void Restore(string pathToAssetsJson, string contextPath) {
            var config = ParseConfigurationFile(pathToAssetsJson);

            throw new NotImplementedException();
        }

        public void Reset(string pathToAssetsJson, string contextPath) {
            var config = ParseConfigurationFile(pathToAssetsJson);

            throw new NotImplementedException();
        }

        public async Task<GitAssetsConfiguration> ParseConfigurationFile(string assetsJsonPath)
        {
            if (!File.Exists(assetsJsonPath)) {
                throw new HttpException(HttpStatusCode.BadRequest, $"The provided assets json path of \"{assetsJsonPath}\" does not exist.");
            }
            JsonDocument assetsContent = null;

            var pathToAssets = ResolveAssetsJson(assetsJsonPath);

            using (FileStream fs = File.OpenRead(assetsJsonPath)) {
                assetsContent = await JsonDocument.ParseAsync(fs, options: new JsonDocumentOptions() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            }

            if (assetsContent == null)
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"The provided assets json at \"{assetsJsonPath}\" did not have valid json present.");
            }

            //if (assetsContent != null)
            //{
            //    var recordingFile = GetProp(key, document.RootElement);

            //    if (recordingFile.Value.ValueKind != JsonValueKind.Undefined)
            //    {
            //        value = recordingFile.Value.GetString();
            //    }
            //    else
            //    {
            //        if (!allowNulls)
            //        {
            //            throw new HttpException(HttpStatusCode.BadRequest, $"Failed attempting to retrieve value from request body. Targeted key was: {key}. Raw body value was {document.RootElement.GetRawText()}.");
            //        }
            //    }
            //}


            return new GitAssetsConfiguration();
        }

        public string ResolveAssetsJson(string inputPath)
        {
            var originalPath = inputPath;
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
