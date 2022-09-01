using System.IO;
using System.Net;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Console;

namespace Azure.Sdk.Tools.TestProxy.Store
{
    public class NullStore : IAssetsStore
    {
        public Task Push(string assetsJsonPath) { return null; }

        public Task Restore(string assetsJsonPath) { return null; }

        public Task Reset(string assetsJsonPath) { return null; }

        public AssetsConfiguration ParseConfigurationFile(string assetsJsonPath)
        {
            if (!File.Exists(assetsJsonPath)) {
                throw new HttpException(HttpStatusCode.BadRequest, $"The provided assets json path of \"{assetsJsonPath}\" does not exist.");
            }

            return new AssetsConfiguration();
        }
    }
}
