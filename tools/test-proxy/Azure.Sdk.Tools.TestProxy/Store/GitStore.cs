using System.IO;
using Azure.Sdk.Tools.TestProxy.Common;
using System.Net;
using System;

namespace Azure.Sdk.Tools.TestProxy.Store
{
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
    }
}
