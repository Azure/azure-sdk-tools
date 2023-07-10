using System.IO;
using System.Net;
using System.Threading.Tasks;
using Azure.Sdk.tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Console;

namespace Azure.Sdk.Tools.TestProxy.Store
{
    public class NullStore : IAssetsStore
    {
        public Task Push(string pathToAssetsJson) { return null; }

        public Task<string> Restore(string pathToAssetsJson) { return null; }

        public Task Reset(string assetsJspathToAssetsJsononPath) { return null; }

        public AssetsConfiguration ParseConfigurationFile(string pathToAssetsJson)
        {
            if (!File.Exists(pathToAssetsJson)) {
                throw new HttpException(HttpStatusCode.BadRequest, $"The provided assets json path of \"{pathToAssetsJson}\" does not exist.");
            }

            return new AssetsConfiguration();
        }

        public Task<NormalizedString> GetPath(string pathToAssetsJson) { return null; }
    }
}
