using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeOwnersParser;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.PipelineOwnersExtractor
{
    /// <summary>
    /// Interface for interacting with GitHub
    /// </summary>
    public class GitHubService
    {
        private static readonly HttpClient httpClient = new HttpClient();

        private readonly ILogger<GitHubService> logger;
        private readonly ConcurrentDictionary<string, List<CodeOwnerEntry>> codeOwnersFileCache;

        /// <summary>
        /// Creates a new GitHubService
        /// </summary>
        /// <param name="logger">Logger</param>
        public GitHubService(ILogger<GitHubService> logger)
        {
            this.logger = logger;
            this.codeOwnersFileCache = new ConcurrentDictionary<string, List<CodeOwnerEntry>>();
        }

        /// <summary>
        /// Looks for CODEOWNERS in the main branch of the given repo URL using cache
        /// </summary>
        /// <param name="repoUrl">GitHub repository URL</param>
        /// <returns>Contents fo the located CODEOWNERS file</returns>
        public async Task<List<CodeOwnerEntry>> GetCodeOwnersFile(Uri repoUrl)
        {
            List<CodeOwnerEntry> result;
            if (codeOwnersFileCache.TryGetValue(repoUrl.ToString(), out result))
            {
                return result;
            }

            result = await GetCodeownersFileImpl(repoUrl);
            codeOwnersFileCache.TryAdd(repoUrl.ToString(), result);
            return result;
        }

        /// <summary>
        /// Looks for CODEOWNERS in the main branch of the given repo URL
        /// </summary>
        /// <param name="repoUrl"></param>
        /// <returns></returns>
        private async Task<List<CodeOwnerEntry>> GetCodeownersFileImpl(Uri repoUrl)
        {
            // Gets the repo path from the URL
            var relevantPathParts = repoUrl.Segments.Skip(1).Take(2);
            var repoPath = string.Join("", relevantPathParts);

            var codeOwnersUrl = $"https://raw.githubusercontent.com/{repoPath}/main/.github/CODEOWNERS";
            var result = await httpClient.GetAsync(codeOwnersUrl);
            if (result.IsSuccessStatusCode)
            {
                this.logger.LogInformation("Retrieved CODEOWNERS file URL = {0}", codeOwnersUrl);
                return CodeOwnersFile.ParseContent(await result.Content.ReadAsStringAsync());
            }

            this.logger.LogWarning("Could not retrieve CODEOWNERS file URL = {0} ResponseCode = {1}", codeOwnersUrl, result.StatusCode);
            return default;
        }

    }
}
