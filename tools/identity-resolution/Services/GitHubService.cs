using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using System.IO;

namespace Azure.Sdk.Tools.NotificationConfiguration
{
    /// <summary>
    /// Interface for interacting with GitHub
    /// </summary>
    public class GitHubService
    {

        private static readonly ConcurrentDictionary<string, List<CodeownersEntry>>
            codeownersFileCache = new();

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
        public List<CodeownersEntry> GetCodeownersFileEntries(Uri repoUrl)
        {
            List<CodeownersEntry> result;
            if (codeownersFileCache.TryGetValue(repoUrl.ToString(), out result))
            {
                return result;
            }

            result = GetCodeownersFileImpl(repoUrl);
            codeownersFileCache.TryAdd(repoUrl.ToString(), result);
            return result;
        }

        /// <summary>
        /// Looks for CODEOWNERS in the main branch of the given repo URL
        /// </summary>
        /// <param name="repoUrl"></param>
        /// <returns></returns>
        private List<CodeownersEntry> GetCodeownersFileImpl(Uri repoUrl)
        {
            // Gets the repo path from the URL
            var relevantPathParts = repoUrl.Segments.Skip(1).Take(2);
            var repoPath = string.Join("", relevantPathParts);

            var codeOwnersUrl = $"https://raw.githubusercontent.com/{repoPath}/main/.github/CODEOWNERS";

            try
            {
                this.logger.LogInformation("Parsing CodeownersEntries from CODEOWNERS file URL = {0}", codeOwnersUrl);
                return CodeownersParser.ParseCodeownersFile(codeOwnersUrl);
            }
            // Thrown by FileHelpers if there was the codeOwnersUrl doesn't point to a valid URL or local file.
            catch (ArgumentException)
            {
                this.logger.LogWarning("Unable to retrieve contents from codeOwnersUrl {0}. Please ensure that the file exists.", codeOwnersUrl);
            }
            // Thrown by FileHelpers if the codeOwnersUrl was good but was unable to be fetched with retries.
            // This is the condition where GitHub is having issues.
            catch (FileLoadException fle)
            {
                this.logger.LogWarning("{0}", fle.Message);
            }

            return null;
        }

    }
}
