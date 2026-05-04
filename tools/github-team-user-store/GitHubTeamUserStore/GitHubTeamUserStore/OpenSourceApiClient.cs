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
        private static readonly TokenRequestContext OpenSourceApiTokenRequestContext = new([ProductAndTeamConstants.OpenSourceApiScope]);
        private static readonly string[] UnsupportedPaginationHeaders = ["Link", "x-ms-continuation", "x-next-page"];
        private static readonly HttpClient SharedHttpClient = new HttpClient();
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

            var publicMembers = await GetFromOpenSourceApi<List<OpenSourcePublicMember>>(
                $"organizations/{Uri.EscapeDataString(orgName)}/public_memberships");

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

        public async Task<IReadOnlyList<(string Name, string Slug)>> GetAzureChildTeams(string teamSlug)
        {
            if (string.IsNullOrWhiteSpace(teamSlug))
            {
                throw new ArgumentException("teamSlug cannot be null or whitespace", nameof(teamSlug));
            }

            var childTeams = await GetFromOpenSourceApi<List<OpenSourceChildTeam>>(
                $"organizations/{ProductAndTeamConstants.Azure}/teams/{Uri.EscapeDataString(teamSlug)}/children");

            List<(string Name, string Slug)> results = new List<(string Name, string Slug)>(childTeams.Count);
            foreach (var childTeam in childTeams)
            {
                if (string.IsNullOrWhiteSpace(childTeam.Name) || string.IsNullOrWhiteSpace(childTeam.Slug))
                {
                    throw new InvalidOperationException($"Open Source API returned a child team without a valid name and slug for '{teamSlug}'.");
                }

                results.Add((childTeam.Name, childTeam.Slug));
            }

            return results;
        }

        public async Task<IReadOnlyList<string>> GetAzureTeamMembers(string teamSlug)
        {
            if (string.IsNullOrWhiteSpace(teamSlug))
            {
                throw new ArgumentException("teamSlug cannot be null or whitespace", nameof(teamSlug));
            }

            var teamMembers = await GetFromOpenSourceApi<List<OpenSourceTeamMember>>(
                $"organizations/{ProductAndTeamConstants.Azure}/teams/{Uri.EscapeDataString(teamSlug)}/members");

            List<string> memberLogins = new List<string>(teamMembers.Count);
            foreach (var teamMember in teamMembers)
            {
                if (string.IsNullOrWhiteSpace(teamMember.Login))
                {
                    throw new InvalidOperationException($"Open Source API returned a team member without a login for '{teamSlug}'.");
                }

                memberLogins.Add(teamMember.Login);
            }

            return memberLogins;
        }

        public async Task<HashSet<string>> GetAzureRepositoryLabels(string repositoryName)
        {
            if (string.IsNullOrWhiteSpace(repositoryName))
            {
                throw new ArgumentException("repositoryName cannot be null or whitespace", nameof(repositoryName));
            }

            var repositoryLabels = await GetFromOpenSourceApi<List<OpenSourceRepositoryLabel>>(
                $"organizations/{ProductAndTeamConstants.Azure}/repositories/{Uri.EscapeDataString(repositoryName)}/issues/labels");

            HashSet<string> labelNames = new HashSet<string>();
            foreach (var repositoryLabel in repositoryLabels)
            {
                if (string.IsNullOrWhiteSpace(repositoryLabel.Name))
                {
                    throw new InvalidOperationException($"Open Source API returned a repository label without a name for '{repositoryName}'.");
                }

                labelNames.Add(repositoryLabel.Name);
            }

            Console.WriteLine($"number of labels in {repositoryName}={labelNames.Count}");
            return labelNames;
        }

        private async Task<T> GetFromOpenSourceApi<T>(string relativePath)
        {
            AccessToken accessToken = await _credential.GetTokenAsync(OpenSourceApiTokenRequestContext, CancellationToken.None);

            using HttpRequestMessage request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{ProductAndTeamConstants.OpenSourceApiBaseUrl}/{relativePath}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
            request.Headers.Add("api-version", ProductAndTeamConstants.OpenSourceApiVersion);

            using HttpResponseMessage response = await SharedHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            EnsureNoUnsupportedPagination(response, relativePath);

            await using Stream responseStream = await response.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<T>(responseStream)
                ?? throw new InvalidOperationException($"Open Source API returned an empty payload for '{relativePath}'.");
        }

        private static void EnsureNoUnsupportedPagination(HttpResponseMessage response, string relativePath)
        {
            foreach (string header in UnsupportedPaginationHeaders)
            {
                if (response.Headers.Contains(header) || response.Content.Headers.Contains(header))
                {
                    throw new InvalidOperationException($"Open Source API returned unsupported pagination header '{header}' for '{relativePath}'.");
                }
            }

            if (response.Content.Headers.Contains("Content-Range"))
            {
                throw new InvalidOperationException($"Open Source API returned unsupported pagination header 'Content-Range' for '{relativePath}'.");
            }
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

        private sealed class OpenSourceChildTeam
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = null!;

            [JsonPropertyName("slug")]
            public string Slug { get; set; } = null!;
        }

        private sealed class OpenSourceTeamMember
        {
            [JsonPropertyName("login")]
            public string Login { get; set; } = null!;
        }

        private sealed class OpenSourceRepositoryLabel
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = null!;
        }
    }
}
