using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using GitHubTeamUserStore.Constants;

namespace GitHubTeamUserStore
{
    public class OpenSourceApiClient
    {
        private readonly TokenCredential _credential;

        public OpenSourceApiClient()
        {
            _credential = CreateOpenSourceApiCredential();
        }

        /// <summary>
        /// Fetch the public memberships for the given organization from repos.opensource.microsoft.com.
        /// </summary>
        public async Task<HashSet<string>> GetPublicOrgMembers(string orgName)
        {
            if (string.IsNullOrWhiteSpace(orgName))
            {
                throw new ArgumentException("orgName cannot be null or whitespace", nameof(orgName));
            }

            AccessToken accessToken = await _credential.GetTokenAsync(
                new TokenRequestContext([ProductAndTeamConstants.OpenSourceApiScope]),
                CancellationToken.None);

            using HttpClient client = new HttpClient();
            using HttpRequestMessage request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{ProductAndTeamConstants.OpenSourceApiBaseUrl}/organizations/{orgName}/public_memberships");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
            request.Headers.Add("api-version", ProductAndTeamConstants.OpenSourceApiVersion);

            using HttpResponseMessage response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            await using Stream responseStream = await response.Content.ReadAsStreamAsync();
            var publicMembers = await JsonSerializer.DeserializeAsync<List<OpenSourcePublicMember>>(responseStream)
                ?? throw new InvalidOperationException("Open Source API returned an empty payload.");

            HashSet<string> memberLogins = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var publicMember in publicMembers)
            {
                if (string.IsNullOrWhiteSpace(publicMember.Login))
                {
                    throw new InvalidOperationException("Open Source API returned a public membership entry without a login.");
                }

                memberLogins.Add(publicMember.Login);
            }

            if (memberLogins.Count == 0)
            {
                throw new InvalidOperationException("Open Source API returned no public member logins.");
            }

            return memberLogins;
        }

        private static TokenCredential CreateOpenSourceApiCredential()
        {
            return IsRunningInAzureDevOps()
                ? CreateAzureDevOpsCredential()
                : CreateLocalCredential();
        }

        private static TokenCredential CreateAzureDevOpsCredential()
        {
            string azureSubscriptionTenant = Environment.GetEnvironmentVariable("AZURESUBSCRIPTION_TENANT_ID");
            string azureSubscriptionClient = Environment.GetEnvironmentVariable("AZURESUBSCRIPTION_CLIENT_ID");
            string azureServiceConnection = Environment.GetEnvironmentVariable("AZURESUBSCRIPTION_SERVICE_CONNECTION_ID");
            string accessToken = Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN");

            if (string.IsNullOrEmpty(azureSubscriptionTenant) ||
                string.IsNullOrEmpty(azureSubscriptionClient) ||
                string.IsNullOrEmpty(azureServiceConnection) ||
                string.IsNullOrEmpty(accessToken))
            {
                return new ChainedTokenCredential(
                    new WorkloadIdentityCredential(),
                    new AzureCliCredential());
            }

            return new ChainedTokenCredential(
                new AzurePipelinesCredential(azureSubscriptionClient, azureSubscriptionTenant, azureServiceConnection, accessToken),
                new WorkloadIdentityCredential(),
                new AzureCliCredential());
        }

        private static TokenCredential CreateLocalCredential()
        {
            string managedIdentityClientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
            TokenCredential managedIdentityCredential = string.IsNullOrWhiteSpace(managedIdentityClientId)
                ? new ManagedIdentityCredential(new ManagedIdentityCredentialOptions())
                : new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(managedIdentityClientId));

            return new ChainedTokenCredential(
                new AzureCliCredential(),
                new AzurePowerShellCredential(),
                new AzureDeveloperCliCredential(),
                new VisualStudioCredential(),
                managedIdentityCredential);
        }

        private static bool IsRunningInAzureDevOps()
        {
            return Environment.GetEnvironmentVariable("SYSTEM_TEAMPROJECTID") != null;
        }

        private sealed class OpenSourcePublicMember
        {
            [JsonPropertyName("login")]
            public string Login { get; set; } = null!;
        }
    }
}
