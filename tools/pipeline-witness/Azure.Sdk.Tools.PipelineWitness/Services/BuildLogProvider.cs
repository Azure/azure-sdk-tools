using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace Azure.Sdk.Tools.PipelineWitness.Services
{
    public class BuildLogProvider
    {
        private readonly ILogger<BuildLogProvider> _logger;
        private readonly VssConnection _vssConnection;

        public BuildLogProvider(ILogger<BuildLogProvider> logger, VssConnection vssConnection)
        {
            _logger = logger;
            _vssConnection = vssConnection;
        }

        public async Task<IReadOnlyList<string>> GetLogLinesAsync(Build build, int logId)
        {
            _logger.LogTrace("Getting logs for build {BuildId}, log {LogId} from rest api", build.Id, logId);

            var buildHttpClient = _vssConnection.GetClient<BuildHttpClient>();
            var response = await buildHttpClient.GetBuildLogLinesAsync(build.Project.Id, build.Id, logId);

            _logger.LogTrace("Received {CharacterCount} characters in {LineCount} lines for build {BuildId}, log {LogId}", response.Sum(x => x.Length), response.Count, build.Id, logId);

            return response;
        }

        public async Task<Stream> GetLogStreamAsync(string projectName, int buildId, int logId)
        {
            _logger.LogTrace("Getting logs for build {BuildId}, log {LogId} from rest api", buildId, logId);

            var buildHttpClient = _vssConnection.GetClient<BuildHttpClient>();
            var stream = await buildHttpClient.GetBuildLogAsync(projectName, buildId, logId);

            return stream;
        }
    }
}
