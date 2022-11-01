using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;
using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIViewWeb.Repositories
{
    public class DevopsArtifactRepository : IDevopsArtifactRepository
    {
        static readonly HttpClient _devopsClient = new();
        private readonly IConfiguration _configuration;

        private readonly string _devopsAccessToken;
        private readonly string _hostUrl;

        private IMemoryCache _pipelineNameCache;
        public DevopsArtifactRepository(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, IMemoryCache cache)
        {
            _configuration = configuration;
            _devopsAccessToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "", _configuration["Azure-Devops-PAT"])));
            _hostUrl = _configuration["APIVIew-Host-Url"];
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
            //Create dictionary of all required parametes to run tools - generate-<language>-apireview pipeline in azure devops
            var reviewDetailsDict = new Dictionary<string, string> { { "Reviews", reviewDetails }, { "APIViewUrl", _hostUrl }, { "StorageContainerUrl", originalStorageUrl } };
            var devOpsCreds = new VssBasicCredential("nobody", _configuration["Azure-Devops-PAT"]);
            var devOpsConnection = new VssConnection(new Uri($"https://dev.azure.com/azure-sdk/"), devOpsCreds);
            string projectName = _configuration["Azure-Devops-internal-project"] ?? "internal";

            BuildHttpClient buildClient = await devOpsConnection.GetClientAsync<BuildHttpClient>();
            var projectClient = await devOpsConnection.GetClientAsync<ProjectHttpClient>();
            int definitionId = await GetPipelineId(pipelineName, buildClient, projectName);
            if (definitionId == 0)
            {
                throw new Exception(string.Format("Azure Devops pipeline is not found with name {0}. Please recheck and ensure pipeline exists with this name", pipelineName));
            }
            
            var definition = await buildClient.GetDefinitionAsync(projectName, definitionId);            
            var project = await projectClient.GetProject(projectName);
            await buildClient.QueueBuildAsync(new Build()
            {
                Definition = definition,
                Project = project,
                Parameters = JsonConvert.SerializeObject(reviewDetailsDict)
            });
        }

        private async Task<int> GetPipelineId(string pipelineName, BuildHttpClient client, string projectName)
        {          
            var pipelines = await client.GetFullDefinitionsAsync2(project: projectName);
            if (pipelines != null)
            {
                return pipelines.Single(p => p.Name == pipelineName).Id;
            }
            return 0;
        }
    }
}
