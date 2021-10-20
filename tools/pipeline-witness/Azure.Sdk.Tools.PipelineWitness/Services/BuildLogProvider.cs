using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace Azure.Sdk.Tools.PipelineWitness.Services
{
    public class BuildLogProvider
    {
        private readonly ILogger<BuildLogProvider> logger;
        private readonly IMemoryCache cache;
        private readonly VssConnection vssConnection;

        public BuildLogProvider(ILogger<BuildLogProvider> logger, IMemoryCache cache, VssConnection vssConnection)
        {
            this.logger = logger;
            this.cache = cache;
            this.vssConnection = vssConnection;
        }

        protected BuildLogProvider()
        {
        }

        public virtual async Task<IReadOnlyList<string>> GetTimelineRecordLogsAsync(Build build, TimelineRecord record)
        {
            if (record.Log == null)
            {
                return null;
            }

            var cacheKey = $"{build.Id}:{record.Log.Id}";

            logger.LogTrace("Getting logs for {CacheKey} from cache", cacheKey);
            var lines = await this.cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                logger.LogTrace("Cache miss for {CacheKey}, falling back to rest api", cacheKey);
                var buildHttpClient = vssConnection.GetClient<BuildHttpClient>();
                var response = await buildHttpClient.GetBuildLogLinesAsync(build.Project.Id, build.Id, record.Log.Id);
                var characterCount = response.Sum(x => x.Length);
                entry.Priority = CacheItemPriority.Low;
                entry.Size =characterCount + 4;

                logger.LogTrace("Caching {CharacterCount} characters in {LineCount} lines for {CacheKey}", characterCount, response.Count, cacheKey);
                return response;
            });

            return lines;
        }
    }
}
