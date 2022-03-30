using System;
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
        private readonly ILogger<BuildLogProvider> logger;
        private readonly VssConnection vssConnection;

        public BuildLogProvider(ILogger<BuildLogProvider> logger, VssConnection vssConnection)
        {
            this.logger = logger;
            this.vssConnection = vssConnection;
        }

        public virtual async Task<IReadOnlyList<string>> GetLogLinesAsync(Build build, int logId)
        {
            logger.LogTrace("Getting logs for build {BuildId}, log {LogId} from rest api", build.Id, logId);

            var buildHttpClient = vssConnection.GetClient<BuildHttpClient>();
            var response = await buildHttpClient.GetBuildLogLinesAsync(build.Project.Id, build.Id, logId);

            logger.LogTrace("Received {CharacterCount} characters in {LineCount} lines for build {BuildId}, log {LogId}", response.Sum(x => x.Length), response.Count, build.Id, logId);

            return response;
        }

        public virtual async Task<Stream> GetLogStreamAsync(string projectName, int buildId, int logId)
        {
            logger.LogTrace("Getting logs for build {BuildId}, log {LogId} from rest api", buildId, logId);

            var buildHttpClient = vssConnection.GetClient<BuildHttpClient>();
            var stream = await buildHttpClient.GetBuildLogAsync(projectName, buildId, logId);

            return stream;
        }
    }
}
