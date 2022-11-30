using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using APIViewWeb.Models;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace APIViewWeb.Managers
{
    public class OpenSourceRequestManager : IOpenSourceRequestManager
    {
        static readonly string _ossApiUrl = "https://repos.opensource.microsoft.com/api/people/links/github/{GithubUser}?api-version=2019-10-01";
        static readonly HttpClient _ossClient = new();
        static readonly HttpClient _tokenClient = new();
        static TelemetryClient _telemetryClient = new(TelemetryConfiguration.CreateDefault());

        private readonly string _aadClientId;
        private readonly string _aadClientSecret;
        private readonly string _aadTenantId;
        private readonly string _tokenApiUrl;

        private DateTime _tokenExpiry;
        private string _token;
    
        public OpenSourceRequestManager(IConfiguration configuration)
        {
            _aadClientId = configuration["opensource-aad-app-id"] ?? "";
            _aadClientSecret = configuration["opensource-aad-client-secret"] ?? "";
            _aadTenantId = configuration["opensource-aad-tenant-id"] ?? "";
            _tokenApiUrl = $"https://login.microsoftonline.com/{_aadTenantId}/oauth2/token";
        }

        public async Task<OpenSourceUserInfo> GetUserInfo(string githubUserId)
        {
            await SetHeaders();
            int counter = 0;
            bool retryStatus = true;

            while(retryStatus && counter <= 5)
            {
                var resp = await _ossClient.GetAsync(_ossApiUrl.Replace("{GithubUser}", githubUserId));
                if(resp.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = resp.Headers.RetryAfter?.ToString() ?? "60";
                    _telemetryClient.TrackTrace($"Download request from devops artifact is throttled. Retry After: {retryAfter}, Retry count: {counter}");
                    await Task.Delay(int.Parse(retryAfter) * 1000);
                    counter++;
                }
                else if (resp.StatusCode == HttpStatusCode.OK)
                {
                    var content = await resp.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<OpenSourceUserInfo>(content);
                }
                else
                {
                    retryStatus = false;
                    _telemetryClient.TrackTrace("Failed to get user info from Microsoft Open source management");
                }
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
            _ossClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        
        private async Task<string> GetAccessToken()
        {
            // Reuse access token if not expired
            if (DateTime.Compare(DateTime.Now.AddSeconds(100), _tokenExpiry) >= 0 || _token == null)
            {
                var body = new Dictionary<string, string>
                {
                    {"grant_type", "client_credentials"},
                    {"client_id", _aadClientId},
                    {"client_secret", _aadClientSecret},
                    { "resource", "api://repos.opensource.microsoft.com/audience/7e04aa67" }
                };

                var tokenResp = await _tokenClient.PostAsync(_tokenApiUrl, new FormUrlEncodedContent(body));
                tokenResp.EnsureSuccessStatusCode();
                var content = await tokenResp.Content.ReadAsStringAsync();
                var token = JsonConvert.DeserializeObject<Token>(content);
                _tokenExpiry = DateTime.Now.AddSeconds(token.ExpiresIn);
                _token = token.AccessToken;
            }

            return _token;
         }
    }

    public class Token
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }
    }
    
}
