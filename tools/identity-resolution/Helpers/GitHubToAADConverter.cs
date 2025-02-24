using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Models.OpenSourcePortal;
using Newtonsoft.Json;

namespace Azure.Sdk.Tools.NotificationConfiguration.Helpers
{
    /// <summary>
    /// Utility class for converting GitHub usernames to AAD user principal names.
    /// </summary>
    /// <remarks>
    /// A map of GitHub usernames to AAD user principal names is cached in memory to avoid making multiple calls to the
    /// OpenSource portal API. The cache is initialized with the full alias list on the first call to
    /// GetUserPrincipalNameFromGithubAsync.
    /// </remarks>
    public class GitHubToAADConverter
    {
        private readonly TokenCredential credential;
        private readonly ILogger<GitHubToAADConverter> logger;
        private readonly SemaphoreSlim cacheLock = new(1);
        private Dictionary<string, string> lookupCache;

        /// <summary>
        /// GitHubToAadConverter constructor for generating new token, and initialize http client.
        /// </summary>
        /// <param name="credential">The aad token auth class.</param>
        /// <param name="logger">Logger</param>
        public GitHubToAADConverter(TokenCredential credential, ILogger<GitHubToAADConverter> logger)
        {
            this.credential = credential;
            this.logger = logger;

        }

        public async Task<string> GetUserPrincipalNameFromGithubAsync(string gitHubUserName)
        {
            await EnsureCacheExistsAsync();
            
            if (this.lookupCache.TryGetValue(gitHubUserName, out string aadUserPrincipalName))
            {
                return aadUserPrincipalName;
            }
            
            return null;
        }

        public async Task EnsureCacheExistsAsync()
        {
            await this.cacheLock.WaitAsync();
            try
            {
                if (this.lookupCache == null)
                {
                    var peopleLinks = await GetPeopleLinksAsync();
                    this.lookupCache = peopleLinks.ToDictionary(
                        x => x.GitHub.Login,
                        x => x.Aad.UserPrincipalName,
                        StringComparer.OrdinalIgnoreCase);
                }
            }
            finally
            {
                this.cacheLock.Release();
            }
        }

        private async Task<UserLink[]> GetPeopleLinksAsync()
        {
            AccessToken opsAuthToken;

            try
            {
                // This is aad scope of opensource rest API.
                string[] scopes = new [] { "2efaf292-00a0-426c-ba7d-f5d2b214b8fc/.default" };
                opsAuthToken = await credential.GetTokenAsync(new TokenRequestContext(scopes), CancellationToken.None);
            }
            catch (Exception ex)
            {
                this.logger.LogError("Failed to generate aad token. {ExceptionMessage}", ex.Message);
                throw;
            }

            try
            {
                using HttpClient client = new ();
                client.DefaultRequestHeaders.Add("content_type", "application/json");
                client.DefaultRequestHeaders.Add("api-version", "2019-10-01");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {opsAuthToken.Token}");

                this.logger.LogInformation("Calling GET https://repos.opensource.microsoft.com/api/people/links");
                string responseJsonString = await client.GetStringAsync($"https://repos.opensource.microsoft.com/api/people/links");
                return JsonConvert.DeserializeObject<UserLink[]>(responseJsonString);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting people links from opensource.microsoft.com: {ExceptionMessage}", ex.Message);
                throw;
            }
        }
    }
}
