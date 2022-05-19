using System.IO;
using Azure.Sdk.Tools.TestProxy.Common;
using System.Net;

namespace Azure.Sdk.Tools.TestProxy.Store
{
    public class GitStore : IAssetsStore
    {
        public void Save(string pathToAssetsJson, string contextPath) {
            var config = ParseConfigurationFile(pathToAssetsJson);
        }

        public void Restore(string pathToAssetsJson, string contextPath) {
            var config = ParseConfigurationFile(pathToAssetsJson);
        }

        public void Reset(string pathToAssetsJson, string contextPath) {
            var config = ParseConfigurationFile(pathToAssetsJson);
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
