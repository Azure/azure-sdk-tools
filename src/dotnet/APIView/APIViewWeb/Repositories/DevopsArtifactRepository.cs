using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIViewWeb.Repositories
{
    public class DevopsArtifactRepository
    {
        static readonly HttpClient devopsClient = new();
        private readonly IConfiguration _configuration;

        public DevopsArtifactRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<Stream> DownloadPackageArtifact(string repoName, string buildId, string artifactName, string filePath)
        {
            var downloadUrl = await GetDownloadArtifactUrl(repoName, buildId, artifactName);
            if (!string.IsNullOrEmpty(downloadUrl))
            {
                downloadUrl = downloadUrl.Split("?")[0] + "?format=file&subPath=" + filePath;
                var downloadResp = await devopsClient.GetAsync(downloadUrl);
                downloadResp.EnsureSuccessStatusCode();
                return await downloadResp.Content.ReadAsStreamAsync();
            }
            return null;
        }

        private async Task<string> GetDownloadArtifactUrl(string repoName, string buildId, string artifactName)
        {
            var artifactGetReq = GetArtifactRestAPIForRepo(repoName).Replace("{buildId}", buildId).Replace("{artifactName}", artifactName);
            var response = await devopsClient.GetAsync(artifactGetReq);
            response.EnsureSuccessStatusCode();
            var buildResource = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (buildResource == null)
            {
                return null;
            }
            return buildResource.RootElement.GetProperty("resource").GetProperty("downloadUrl").GetString();
        }

        private string GetArtifactRestAPIForRepo(string repoName)
        {
            var downloadARtifactRestApi = _configuration["download-artifact-rest-api-for-" + repoName];
            if (downloadARtifactRestApi == null)
            {
                downloadARtifactRestApi = _configuration["download-artifact-rest-api"];
            }
            return downloadARtifactRestApi;
        }
    }
}
