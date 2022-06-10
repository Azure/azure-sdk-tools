using System.IO;
using Azure.Sdk.Tools.TestProxy.Common;
using System.Net;
using System;

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

        public GitAssetsConfiguration ParseConfigurationFile(string assetsJsonPath)
        {
            if (!File.Exists(assetsJsonPath)) {
                throw new HttpException(HttpStatusCode.BadRequest, $"The provided assets json path of \"{assetsJsonPath}\" does not exist.");
            }

            return new GitAssetsConfiguration();
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
