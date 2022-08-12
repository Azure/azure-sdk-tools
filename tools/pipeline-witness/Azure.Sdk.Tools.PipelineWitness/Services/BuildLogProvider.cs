using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;

namespace Azure.Sdk.Tools.PipelineWitness.Services
{
    public class BuildLogProvider
    {
        private readonly ILogger<BuildLogProvider> logger;
        private readonly DevOpsClient devOpsClient;

        public BuildLogProvider(ILogger<BuildLogProvider> logger, DevOpsClient devOpsClient)
        {
            this.logger = logger;
            this.devOpsClient = devOpsClient;
        }

        public virtual async Task<IReadOnlyList<string>> GetLogLinesAsync(Build build, int logId)
        {
            logger.LogTrace("Getting logs for build {BuildId}, log {LogId} from rest api", build.Id, logId);

            var response = await this.devOpsClient.GetBuildLogLinesAsync(build.Project.Id, build.Id, logId);

            logger.LogTrace("Received {CharacterCount} characters in {LineCount} lines for build {BuildId}, log {LogId}", response.Sum(x => x.Length), response.Count, build.Id, logId);

            return response;
        }

        public virtual async Task<Stream> GetLogStreamAsync(Guid project, int buildId, int logId)
        {
            logger.LogTrace("Getting logs for build {BuildId}, log {LogId} from rest api", buildId, logId);

            var stream = await this.devOpsClient.GetBuildLogAsync(project, buildId, logId);

            return stream;
        }
    }
}
