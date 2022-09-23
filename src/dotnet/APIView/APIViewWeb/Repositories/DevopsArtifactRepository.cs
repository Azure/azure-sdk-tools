using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIViewWeb.Repositories
{
    public class DevopsArtifactRepository
    {
        static readonly HttpClient _devopsClient = new();
        private readonly IConfiguration _configuration;

        private readonly string _devopsAccessToken;
        private readonly string _pipeline_run_rest;
        private readonly string _hostUrl;
        private readonly string _listPipelineApi;

        private IMemoryCache _pipelineNameCache;
        public DevopsArtifactRepository(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, IMemoryCache cache)
        {
            _configuration = configuration;
            _devopsAccessToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "", _configuration["Azure-Devops-PAT"])));
            _pipeline_run_rest = _configuration["Azure-Devops-Run-Ripeline-Rest"];
            _hostUrl = _configuration["APIVIew-Host-Url"];
            _listPipelineApi = _configuration["devops-pipeline-list-api"] ?? "https://dev.azure.com/azure-sdk/internal/_apis/pipelines?api-version=6.0-preview.1";
            _pipelineNameCache = cache;
        }

        public async Task<Stream> DownloadPackageArtifact(string repoName, string buildId, string artifactName, string filePath, string project, string format= "file")
        {
            var downloadUrl = await GetDownloadArtifactUrl(repoName, buildId, artifactName, project);
            if (!string.IsNullOrEmpty(downloadUrl))
            {
                if(!string.IsNullOrEmpty(filePath))
                {
                    if (!filePath.StartsWith("/"))
                    {
                        filePath = "/" + filePath;
                    }
                    downloadUrl = downloadUrl.Split("?")[0] + "?format=" + format + "&subPath=" + filePath;
                }
                
                SetDevopsClientHeaders();
                var downloadResp = await _devopsClient.GetAsync(downloadUrl);
                downloadResp.EnsureSuccessStatusCode();
                return await downloadResp.Content.ReadAsStreamAsync();
            }
            return null;
        }

        private void SetDevopsClientHeaders()
        {
            _devopsClient.DefaultRequestHeaders.Accept.Clear();
            _devopsClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            _devopsClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", _devopsAccessToken);
        }

        private async Task<string> GetDownloadArtifactUrl(string repoName, string buildId, string artifactName, string project)
        {
            var artifactGetReq = GetArtifactRestAPIForRepo(repoName).Replace("{buildId}", buildId).Replace("{artifactName}", artifactName).Replace("{project}", project);
            SetDevopsClientHeaders();
            var response = await _devopsClient.GetAsync(artifactGetReq);
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
            var downloadArtifactRestApi = _configuration["download-artifact-rest-api-for-" + repoName];
            if (downloadArtifactRestApi == null)
            {
                downloadArtifactRestApi = _configuration["download-artifact-rest-api"];
            }
            return downloadArtifactRestApi;
        }

        public async Task RunPipeline(string pipelineName, string reviewDetails, string originalStorageUrl)
        {
            SetDevopsClientHeaders();
            //Create dictionary of all required parametes to run tools - generate-<language>-apireview pipeline in azure devops
            var reviewDetailsDict = new Dictionary<string, string> { { "Reviews", reviewDetails }, { "APIViewUrl", _hostUrl }, { "StorageContainerUrl", originalStorageUrl } };
            var pipelineParams = new Dictionary<string, Dictionary<string, string>> { { "templateParameters", reviewDetailsDict } };
            var stringContent = new StringContent(JsonSerializer.Serialize(pipelineParams), Encoding.UTF8, "application/json");
            var pipelineApiURl = string.Format(_pipeline_run_rest, await GetPipelineId(pipelineName));
            var response = await _devopsClient.PostAsync(pipelineApiURl, stringContent);
            response.EnsureSuccessStatusCode();
        }

        private async Task<string> GetPipelineId(string pipelineName)
        {
            string pipelineId = null;
            if(!_pipelineNameCache.TryGetValue(pipelineName, out pipelineId))
            {
                await FetchPipelineDetails();
            }

            _pipelineNameCache.TryGetValue(pipelineName, out pipelineId);
            if(string.IsNullOrEmpty(pipelineId))
            {
                throw new Exception(string.Format("Azure Devops pipeline is not found in internal project with name {0}. Please recheck and ensure pipeline exists with this name", pipelineName));
            }

            return pipelineId;
        }

        private async Task FetchPipelineDetails()
        {
            SetDevopsClientHeaders();
            var response = await _devopsClient.GetAsync(_listPipelineApi);
            response.EnsureSuccessStatusCode();
            var pipelineListDetails = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if(pipelineListDetails != null)
            {

            }
        }
    }
}
