using System.IO;
using Azure.Sdk.Tools.TestProxy.Common;
using System.Net;

namespace Azure.Sdk.Tools.TestProxy.Store
{
    public class NullStore : IAssetsStore
    {
        public void Save(AssetsConfiguration assetsConfig, string contextPath) {}

        public void Restore(AssetsConfiguration assetsConfig, string contextPath) {}

        public void Reset(AssetsConfiguration assetsConfig, string contextPath) {}

        public object ParseConfigurationFile(string assetsJsonPath)
        {
            if (!File.Exists(assetsJsonPath)) {
                throw new HttpException(HttpStatusCode.BadRequest, $"The provided assets json path of \"{assetsJsonPath}\" does not exist.");
            }

            return new AssetsConfiguration();
        }
    }
}
