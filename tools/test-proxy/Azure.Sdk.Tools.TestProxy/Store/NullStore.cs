using System.IO;
using Azure.Sdk.Tools.TestProxy.Common;
using System.Net;

namespace Azure.Sdk.Tools.TestProxy.Store
{
    public class NullStore : IAssetsStore
    {
        public void Push(string assetsJsonPath, string contextPath) {}

        public void Restore(string assetsJsonPath, string contextPath) {}

        public void Reset(string assetsJsonPath, string contextPath) {}

        public AssetsConfiguration ParseConfigurationFile(string assetsJsonPath)
        {
            if (!File.Exists(assetsJsonPath)) {
                throw new HttpException(HttpStatusCode.BadRequest, $"The provided assets json path of \"{assetsJsonPath}\" does not exist.");
            }

            return new AssetsConfiguration();
        }
    }
}
