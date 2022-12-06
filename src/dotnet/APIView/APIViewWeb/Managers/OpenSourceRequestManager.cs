using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using APIViewWeb.Models;
using Azure.Core;
using Azure.Identity;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octokit;

namespace APIViewWeb.Managers
{
    public class OpenSourceRequestManager : IOpenSourceRequestManager
    {
        static readonly string[] scopes = new string[] { "api://2789159d-8d8b-4d13-b90b-ca29c1707afd/.default" };
        static readonly HttpClient _ossClient = new();
        static TelemetryClient _telemetryClient = new(TelemetryConfiguration.CreateDefault());

        private readonly ClientSecretCredential _clientCredential;
        private DateTime _tokenExpiry;
        private string _token;
    
        public OpenSourceRequestManager(IConfiguration configuration)
        {
            var aadClientId = configuration["opensource-aad-app-id"] ?? "";
            var aadClientSecret = configuration["opensource-aad-client-secret"] ?? "";
            var aadTenantId = configuration["opensource-aad-tenant-id"] ?? "";
            _clientCredential = new ClientSecretCredential(aadTenantId, aadClientId, aadClientSecret);
        }

        public async Task<OpenSourceUserInfo> GetUserInfo(string githubUserId)
        {            
            try
            {
                await SetHeaders();
                var response = await _ossClient.GetAsync($"https://repos.opensource.microsoft.com/api/people/links/github/{githubUserId}");
                response.EnsureSuccessStatusCode();
                var userDetailsJson = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<OpenSourceUserInfo>(userDetailsJson);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _telemetryClient.TrackTrace($"Github username {githubUserId} is not found");
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
            }
            return null;            
        }

        public async Task<bool> IsAuthorizedUser(string githubUserId)
        {
            var resp = await GetUserInfo(githubUserId);
            if (resp == null)
                return false;
            // For now we only need to check if user info is availableon MS OSS
            return true;
        }

        private async Task SetHeaders()
        {
            var token = await GetAccessToken();
            _ossClient.DefaultRequestHeaders.Add("content_type", "application/json");
            _ossClient.DefaultRequestHeaders.Add("api-version", "2019-10-01");
            _ossClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        
        private async Task<string> GetAccessToken()
        {
            if (DateTime.Compare(DateTime.Now.AddSeconds(100), _tokenExpiry) >= 0 || _token == null)
            {
                var token = await _clientCredential.GetTokenAsync(new TokenRequestContext(scopes));
                _token = token.Token;
                _tokenExpiry = token.ExpiresOn.DateTime;
            }
            return _token;
         }
    }  
}
