// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Core;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.Graph;
using System.Text.Json;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface IUserHelper
    {
        public Task<string> GetUserEmail(CancellationToken ct);
        public Task<UserProfile> GetUserProfile(string githubUserName, CancellationToken ct);
    }

    public class UserHelper(IAzureService azureService, ILogger<UserHelper> logger) : IUserHelper
    {
        private readonly string[]  scopes = ["https://graph.microsoft.com/.default"];
        private readonly string openSourceScope = "2efaf292-00a0-426c-ba7d-f5d2b214b8fc/.default";

        public async Task<string> GetUserEmail(CancellationToken ct)
        {
            var graphClient = new GraphServiceClient(azureService.GetCredential(), scopes);
            var user = await graphClient.Me.GetAsync(cancellationToken: ct);
            if (user == null)
            {
                throw new InvalidOperationException("User not found.");
            }
            return user.UserPrincipalName ?? user.Mail;
        }

        public async Task<UserProfile> GetUserProfile(string githubUserName, CancellationToken ct)
        {
            AccessToken opsAuthToken;

            try
            {
                // This is aad scope of opensource rest API.
                string[] openSourceScopes = [openSourceScope];
                opsAuthToken = await azureService.GetCredential().GetTokenAsync(new TokenRequestContext(openSourceScopes), ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate aad token.");
                throw;
            }

            try
            {
                using HttpClient client = new();
                client.DefaultRequestHeaders.Add("content_type", "application/json");
                client.DefaultRequestHeaders.Add("api-version", "2019-10-01");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {opsAuthToken.Token}");

                var encodedUserName = Uri.EscapeDataString(githubUserName);
                var requestUrl = $"https://repos.opensource.microsoft.com/api/people/links/github/{encodedUserName}";
                logger.LogInformation("Calling GET {requestUrl}", requestUrl);
                string responseJsonString = await client.GetStringAsync(requestUrl, ct);
                return JsonSerializer.Deserialize<UserProfile>(responseJsonString)
                    ?? throw new InvalidOperationException("User profile response could not be parsed.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting people links from opensource.microsoft.com");
                throw;
            }
        }
    }
}
