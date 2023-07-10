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
        static TelemetryClient _telemetryClient = new(TelemetryConfiguration.CreateDefault());

        private readonly string _aadClientId;
        private readonly string _aadClientSecret;
        private readonly string _aadTenantId;

        public OpenSourceRequestManager(IConfiguration configuration)
        {
            _aadClientId = configuration["opensource-aad-app-id"] ?? "";
            _aadClientSecret = configuration["opensource-aad-client-secret"] ?? "";
            _aadTenantId = configuration["opensource-aad-tenant-id"] ?? "";
        }

        public async Task<OpenSourceUserInfo> GetUserInfo(string githubUserId)
        {
            int retryCount = 0;
            bool authCheckCompleted = false;
            while (!authCheckCompleted && retryCount < 3)
            {
                try
                {
                    retryCount++;
                    var ossClient = new HttpClient();
                    await SetHeaders(ossClient);
                    var response = await ossClient.GetAsync($"https://repos.opensource.microsoft.com/api/people/links/github/{githubUserId}");
                    response.EnsureSuccessStatusCode();
                    var userDetailsJson = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<OpenSourceUserInfo>(userDetailsJson);
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    _telemetryClient.TrackTrace($"GitHub username {githubUserId} is not found");
                    authCheckCompleted = true;
                }
                catch (Exception ex)
                {
                    _telemetryClient.TrackException(ex);
                }

                if(!authCheckCompleted && retryCount < 3)
                {
                    await Task.Delay(2000);
                    _telemetryClient.TrackTrace($"Retrying to check user authorization for user Id {githubUserId}");
                }
            }
            return null;            
        }

        public async Task<bool> IsAuthorizedUser(string githubUserId)
        {
            var resp = await GetUserInfo(githubUserId);
            if (resp == null)
                return false;
            // For now we only need to check if user info is available on MS OSS
            return true;
        }

        private async Task SetHeaders(HttpClient ossClient)
        {
            var clientCredential = new ClientSecretCredential(_aadTenantId, _aadClientId, _aadClientSecret);
            var token =  (await clientCredential.GetTokenAsync(new TokenRequestContext(scopes))).Token;
            ossClient.DefaultRequestHeaders.Add("content_type", "application/json");
            ossClient.DefaultRequestHeaders.Add("api-version", "2019-10-01");
            ossClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }  
}
