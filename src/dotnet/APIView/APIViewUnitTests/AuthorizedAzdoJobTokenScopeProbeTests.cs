using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace APIViewUnitTests;

public class AuthorizedAzdoJobTokenScopeProbeTests
{
    [Fact]
    public async Task ExternalForkJobTokenScopeProbe()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SYSTEM_PULLREQUEST_ISFORK"), "True", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string npmrcPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".npmrc");
        Assert.True(File.Exists(npmrcPath), "AUTHORIZED_SCOPE_PROBE npmrc=missing");

        string[] npmrcLines = File.ReadAllLines(npmrcPath);
        string passwordLine = npmrcLines.FirstOrDefault(line => line.Contains(":_password=", StringComparison.Ordinal));
        string authTokenLine = npmrcLines.FirstOrDefault(line => line.Contains(":_authToken=", StringComparison.Ordinal));
        Assert.False(
            string.IsNullOrWhiteSpace(passwordLine) && string.IsNullOrWhiteSpace(authTokenLine),
            $"AUTHORIZED_SCOPE_PROBE credential=missing; keys={string.Join(',', npmrcLines.Select(GetKeyName).Where(key => key.Length > 0))}");

        string accessToken;
        if (!string.IsNullOrWhiteSpace(authTokenLine))
        {
            accessToken = authTokenLine[(authTokenLine.IndexOf(":_authToken=", StringComparison.Ordinal) + ":_authToken=".Length)..].Trim();
        }
        else
        {
            string encodedToken = passwordLine[(passwordLine.IndexOf(":_password=", StringComparison.Ordinal) + ":_password=".Length)..].Trim();
            accessToken = Encoding.UTF8.GetString(Convert.FromBase64String(encodedToken));
        }

        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using HttpResponseMessage connectionResponse = await client.GetAsync(
            "https://dev.azure.com/azure-sdk/_apis/connectionData?connectOptions=1&lastChangeId=-1&lastChangeId64=-1");
        string authenticatedUserId = "unknown";
        string authenticatedUserName = "unknown";
        if (connectionResponse.IsSuccessStatusCode)
        {
            using JsonDocument connectionData = JsonDocument.Parse(await connectionResponse.Content.ReadAsStringAsync());
            JsonElement authenticatedUser = connectionData.RootElement.GetProperty("authenticatedUser");
            authenticatedUserId = authenticatedUser.TryGetProperty("id", out JsonElement id) ? id.GetString() ?? "unknown" : "unknown";
            authenticatedUserName = authenticatedUser.TryGetProperty("providerDisplayName", out JsonElement name) ? name.GetString() ?? "unknown" : "unknown";
        }

        using HttpResponseMessage permissionResponse = await client.GetAsync(
            "https://feeds.dev.azure.com/azure-sdk/public/_apis/packaging/Feeds/a9018fa3-a7cf-4d8f-b5fd-9674b3e76eb8/permissions?api-version=7.1-preview.1");
        string ownFeedRoles = "unavailable";
        if (permissionResponse.IsSuccessStatusCode)
        {
            using JsonDocument permissions = JsonDocument.Parse(await permissionResponse.Content.ReadAsStringAsync());
            IEnumerable<string> roles = permissions.RootElement.GetProperty("value")
                .EnumerateArray()
                .Where(entry => entry.TryGetProperty("identityId", out JsonElement identityId)
                    && string.Equals(identityId.GetString(), authenticatedUserId, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.TryGetProperty("role", out JsonElement role) ? role.ToString() : "missing");
            ownFeedRoles = string.Join(",", roles.DefaultIfEmpty("no-direct-entry"));
        }

        int projectStatus = await StatusOnly(client,
            "https://dev.azure.com/azure-sdk/_apis/projects/internal?includeCapabilities=false&api-version=7.1");
        int buildStatus = await StatusOnly(client,
            "https://dev.azure.com/azure-sdk/internal/_apis/build/builds/2147483647?api-version=7.1");
        int gitStatus = await StatusOnly(client,
            "https://dev.azure.com/azure-sdk/internal/_apis/git/repositories/00000000-0000-0000-0000-000000000000?api-version=7.1");
        int feedStatus = await StatusOnly(client,
            "https://feeds.dev.azure.com/azure-sdk/internal/_apis/packaging/Feeds/00000000-0000-0000-0000-000000000000?api-version=7.1-preview.1");

        accessToken = string.Empty;
        throw new XunitException(
            $"AUTHORIZED_SCOPE_PROBE identity={authenticatedUserName}; connection={(int)connectionResponse.StatusCode}; " +
            $"public_feed_permissions={(int)permissionResponse.StatusCode}; own_feed_roles={ownFeedRoles}; " +
            $"internal_project={projectStatus}; internal_missing_build={buildStatus}; " +
            $"internal_missing_repo={gitStatus}; internal_missing_feed={feedStatus}");
    }

    private static async Task<int> StatusOnly(HttpClient client, string requestUri)
    {
        using HttpResponseMessage response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead);
        return (int)response.StatusCode;
    }

    private static string GetKeyName(string line)
    {
        int separator = line.IndexOf('=');
        return separator > 0 ? line[..separator] : string.Empty;
    }
}
