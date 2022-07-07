using System.IO;
using Azure.Sdk.Tools.TestProxy.Common;
using System.Net;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Store
{
    public class NullStore : IAssetsStore
    {
        public Task Push(string assetsJsonPath, string contextPath) { return null; }

        public Task Restore(string assetsJsonPath, string contextPath) { return null; }

        public Task Reset(string assetsJsonPath, string contextPath) { return null; }

        public AssetsConfiguration ParseConfigurationFile(string assetsJsonPath)
        {
            if (!File.Exists(assetsJsonPath)) {
                throw new HttpException(HttpStatusCode.BadRequest, $"The provided assets json path of \"{assetsJsonPath}\" does not exist.");
            }

            return new AssetsConfiguration();
        }
    }
}
