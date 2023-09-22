using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Models.OpenSourcePortal;
using Newtonsoft.Json;

namespace Azure.Sdk.Tools.NotificationConfiguration.Helpers
{
    public class GitHubToAADConverter
    {
        /// <summary>
        /// GitHubToAadConverter constructor for generating new token, and initialize http client.
        /// </summary>
        /// <param name="credential">The aad token auth class.</param>
        /// <param name="logger">Logger</param>
        public GitHubToAADConverter(
            ClientSecretCredential credential,
            ILogger<GitHubToAADConverter> logger)
        {
            this.logger = logger;
            var opsAuthToken = "";
            try
            {
                // This is aad scope of opensource rest API.
                string[] scopes = new string[]
                {
                    "api://2789159d-8d8b-4d13-b90b-ca29c1707afd/.default"
                };
                opsAuthToken = credential.GetToken(new TokenRequestContext(scopes)).Token;
            }
            catch (Exception ex)
            {
                logger.LogError("Failed to generate aad token. " + ex.Message);
            }
            client = new HttpClient();
            client.DefaultRequestHeaders.Add("content_type", "application/json");
            client.DefaultRequestHeaders.Add("api-version", "2019-10-01");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {opsAuthToken}");
        }

        private readonly HttpClient client;
        private readonly ILogger<GitHubToAADConverter> logger;

        /// <summary>
        /// Get the user principal name from github. User principal name is in format of ms email.
        /// </summary>
        /// <param name="githubUserName">github user name</param>
        /// <returns>Aad user principal name</returns>
        public string GetUserPrincipalNameFromGithub(string githubUserName)
        {
            return GetUserPrincipalNameFromGithubAsync(githubUserName).Result;
        }

        public async Task<string> GetUserPrincipalNameFromGithubAsync(string githubUserName)
        {
            try
            {
                var responseJsonString = await client.GetStringAsync($"https://repos.opensource.microsoft.com/api/people/links/github/{githubUserName}");
                dynamic contentJson = JsonConvert.DeserializeObject(responseJsonString);
                return contentJson.aad.userPrincipalName;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogWarning("Github username {Username} not found", githubUserName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
            }

            return null;
        }

        public async Task<UserLink[]> GetPeopleLinksAsync()
        {
            try
            {
                logger.LogInformation("Calling GET https://repos.opensource.microsoft.com/api/people/links");
                var responseJsonString = await client.GetStringAsync($"https://repos.opensource.microsoft.com/api/people/links");
                var allLinks = JsonConvert.DeserializeObject<UserLink[]>(responseJsonString);

                return allLinks;
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
            }

            return null;
        }
    }
}
