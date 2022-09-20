using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeOwnersParser;

namespace Azure.Sdk.Tools.NotificationConfiguration
{
    /// <summary>
    /// Interface for interacting with GitHub
    /// </summary>
    public class GitHubService
    {
        private static HttpClient httpClient = new HttpClient();
        private static ConcurrentDictionary<string, List<CodeOwnerEntry>> codeownersFileCache = new ConcurrentDictionary<string, List<CodeOwnerEntry>>();

        private readonly ILogger<GitHubService> logger;

        /// <summary>
        /// Creates a new GitHubService
        /// </summary>
        /// <param name="logger">Logger</param>
        public GitHubService(ILogger<GitHubService> logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Looks for CODEOWNERS in the main branch of the given repo URL using cache
        /// </summary>
        /// <param name="repoUrl">GitHub repository URL</param>
        /// <returns>Contents fo the located CODEOWNERS file</returns>
        public async Task<List<CodeOwnerEntry>> GetCodeownersFile(Uri repoUrl)
        {
            List<CodeOwnerEntry> result;
            if (codeownersFileCache.TryGetValue(repoUrl.ToString(), out result))
            {
                return result;
            }

            result = await GetCodeownersFileImpl(repoUrl);
            codeownersFileCache.TryAdd(repoUrl.ToString(), result);
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
                logger.LogInformation("Retrieved CODEOWNERS file URL = {0}", codeOwnersUrl);
                return CodeOwnersFile.ParseContent(await result.Content.ReadAsStringAsync());
            }

            logger.LogWarning("Could not retrieve CODEOWNERS file URL = {0} ResponseCode = {1}", codeOwnersUrl, result.StatusCode);
            return default;
        }

    }
}
