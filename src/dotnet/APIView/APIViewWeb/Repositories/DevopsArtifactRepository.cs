using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIViewWeb.Repositories
{
    public class DevopsArtifactRepository
    {
        static readonly HttpClient _devopsClient = new();
        private readonly IConfiguration _configuration;

        private readonly string _devopsAccessToken;
        private readonly string _organization;
        private readonly string _hostUrl;
        private readonly string _project;


        public DevopsArtifactRepository(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = configuration;
            _devopsAccessToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "", _configuration["Azure-Devops-PAT"])));
            _organization = _configuration["Azure-Devops-Org"];
            _hostUrl = _configuration["APIVIew-Host-Url"];
            _project = _configuration["Azure-Devops-Project"];
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
            var credential = new VssBasicCredential("nobody", _devopsAccessToken);
            var connection = new VssConnection(new Uri(_organization), credential);
            var buildClient = connection.GetClient<BuildHttpClient>();
            var projectClient = connection.GetClient<ProjectHttpClient>();

            var project = await projectClient.GetProject(_project);
            Console.WriteLine(project.Name);
            var definitions = await buildClient.GetDefinitionsAsync(pipelineName);
            if(definitions == null || definitions.Count == 0)
            {
                throw new InvalidOperationException("Pipeline not found with name " + pipelineName));
            }
            var definition = definitions.First();
            Console.WriteLine(definition.Name);

            var build = new Build()
            {
                Definition = definition,
                Project = project
            };

            var dict = new Dictionary<string, string> { { "Reviews", reviewDetails }, { "APIViewUrl", _hostUrl }, { "StorageContainerUrl", originalStorageUrl } };
            build.Parameters = JsonConvert.SerializeObject(dict);
            buildClient.QueueBuildAsync(build).Wait();
        }
    }
}
