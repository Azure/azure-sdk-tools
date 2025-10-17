using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using APIView.Identity;
using APIViewWeb.Models;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.Account
{
    public static class AuthenticationValidator
    {
        #region Azure Identity Validation

        public static bool IsValidAzureIdentity(ClaimsPrincipal user)
        {
            if (!IsAuthenticated(user))
            {
                return false;
            }

            return HasRequiredAzureIdentityClaims(user) && IsValidAzureAuthentication(user);
        }

        private static bool IsValidAzureAuthentication(ClaimsPrincipal user)
        {
            return HasValidAuthenticationType(user) || HasValidIdentityProvider(user);
        }

        private static bool HasRequiredAzureIdentityClaims(ClaimsPrincipal user)
        {
            Claim oidClaim = user.FindFirst("oid") ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier");
            return oidClaim != null;
        }

        private static bool HasValidAuthenticationType(ClaimsPrincipal user)
        {
            var authType = user.Identity.AuthenticationType;
            return authType == "Bearer" || authType == "AuthenticationTypes.Federation";
        }

        private static bool HasValidIdentityProvider(ClaimsPrincipal user)
        {
            Claim identityProviderClaim = user.FindFirst("idp");
            return identityProviderClaim?.Value?.Contains("login.microsoftonline.com") == true;
        }

        #endregion

        #region GitHub Organization Validation

        public static bool HasOrganizationAccess(ClaimsPrincipal user, string[] requiredOrganizations)
        {
            if (!IsAuthenticated(user))
            {
                return false;
            }

            string[] userOrganizations = GetUserOrganizations(user);
            return userOrganizations.Any(userOrg =>
                requiredOrganizations.Contains(userOrg, StringComparer.OrdinalIgnoreCase));
        }

        public static bool HasAnyOrganizationAccess(ClaimsPrincipal user)
        {
            return IsAuthenticated(user) && GetUserOrganizations(user).Length > 0;
        }

        public static string[] GetUserOrganizations(ClaimsPrincipal user)
        {
            if (!IsAuthenticated(user))
            {
                return [];
            }

            Claim orgClaim = user.FindFirst(ClaimConstants.Orgs);
            return orgClaim?.Value?.Split(',') ?? [];
        }

        #endregion

        #region Combined Authentication Methods
        
        public static bool HasOrganizationOrAzureAuthenticationAccess(ClaimsPrincipal user, string[] requiredOrganizations)
        {
            if (!IsAuthenticated(user))
            {
                return false;
            }

            return IsValidAzureIdentity(user) || HasOrganizationAccess(user, requiredOrganizations);
        }
        
        public static string GetAuthenticationMethod(ClaimsPrincipal user)
        {
            if (IsValidAzureIdentity(user))
            {
                return "ManagedIdentity";
            }

            if (HasAnyOrganizationAccess(user))
            {
                return "GitHubOrganization";
            }

            return IsAuthenticated(user) ? "Other" : "Anonymous";
        }
        #endregion

        #region GitHub Token Validation

        private static readonly string[] gitHubTokenPrefixes = [
            "ghp_",         // Personal Access Token
            "ghs_",         // App Installation Token  
            "github_pat_",  // Fine-grained PAT
            "gho_",         // OAuth App Token
            "ghu_",         // User-to-Server Token
            "ghr_"          // Server-to-Server Token
        ];

        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        public static bool IsGitHubToken(string token) =>
            !string.IsNullOrEmpty(token) && gitHubTokenPrefixes.Any(token.StartsWith);

        public static async Task<ClaimsPrincipal> ValidateGitHubTokenAsync(string token, HttpClient httpClient, ILogger logger = null)
        {
            if (!IsGitHubToken(token)) return null;

            try
            {
                SetupHttpClient(httpClient, token);

                GithubUser user = await GetGitHubUserAsync(httpClient, logger);
                if (user == null) { return null; }

                string[] organizations = await GetUserOrganizationsAsync(httpClient, logger);
                return CreateGitHubClaimsPrincipal(user, organizations);
            }
            catch
            {
                return null;
            }
        }

        private static void SetupHttpClient(HttpClient httpClient, string token)
        {
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "APIView-GitHub-Token-Validator");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }

        private static async Task<GithubUser> GetGitHubUserAsync(HttpClient httpClient, ILogger logger = null)
        {
            HttpResponseMessage response = await httpClient.GetAsync("https://api.github.com/user");
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                logger?.LogWarning("GitHub API /user request failed. Status: {StatusCode}, Content: {ErrorContent}", 
                    response.StatusCode, errorContent);
                return null;
            }

            string json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<GithubUser>(json, jsonOptions);
        }

        private static async Task<string[]> GetUserOrganizationsAsync(HttpClient httpClient, ILogger logger = null)
        {
            var response = await httpClient.GetAsync("https://api.github.com/user/orgs");
            if (!response.IsSuccessStatusCode) 
            { 
                logger?.LogWarning($"GitHub organization API call failed with status code: {response.StatusCode}");
                return []; 
            }

            string json = await response.Content.ReadAsStringAsync();
            GitHubOrganization[] orgs = JsonSerializer.Deserialize<GitHubOrganization[]>(json, jsonOptions);
            return orgs?.Select(o => o.Login).ToArray() ?? [];
        }

        public static ClaimsPrincipal CreateGitHubClaimsPrincipal(GithubUser user, string[] organizations)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Name ?? user.Login),
                new(ClaimConstants.Login, user.Login),
                new(ClaimConstants.Url, user.HtmlUrl ?? ""),
                new(ClaimConstants.Avatar, user.AvatarUrl ?? ""),
                new(ClaimTypes.AuthenticationMethod, "GitHubToken")
            };

            if (organizations.Length > 0)
            {
                claims.Add(new Claim(ClaimConstants.Orgs, string.Join(",", organizations)));
            }

            return new ClaimsPrincipal(new ClaimsIdentity(claims, "GitHubToken"));
        }

        #endregion

        private static bool IsAuthenticated(ClaimsPrincipal user)
        {
            return user?.Identity?.IsAuthenticated == true;
        }
    }
}
