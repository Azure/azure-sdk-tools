using Azure.Core;
using Azure.Identity;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;
using Polly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace APIViewWeb.Repositories
{
    public class DevopsArtifactRepository : IDevopsArtifactRepository
    {
        private readonly IConfiguration _configuration;
        private readonly string _hostUrl;
        private readonly TelemetryClient _telemetryClient;

        public DevopsArtifactRepository(IConfiguration configuration, TelemetryClient telemetryClient)
        {
            _configuration = configuration;
            _hostUrl = _configuration["APIVIew-Host-Url"];
            _telemetryClient = telemetryClient;
        }

        public async Task<Stream> DownloadPackageArtifact(string repoName, string buildId, string artifactName, string filePath, string project, string format= "file")
        {
            var downloadUrl = await getDownloadArtifactUrl(buildId, artifactName, project);
            if (string.IsNullOrEmpty(downloadUrl))
            {
                throw new Exception(string.Format("Failed to get download url for artifact {0} in build {1} in project {2}", artifactName, buildId, project));
            }
            
            if(!string.IsNullOrEmpty(filePath))
            {
                if (!filePath.StartsWith("/"))
                {
                    filePath = "/" + filePath;
                }
                downloadUrl = downloadUrl.Split("?")[0] + "?format=" + format + "&subPath=" + filePath;
            }

            HttpResponseMessage downloadResp = await GetFromDevopsAsync(downloadUrl);
            downloadResp.EnsureSuccessStatusCode();
            return await downloadResp.Content.ReadAsStreamAsync();
        }

        private async Task<string> getDownloadArtifactUrl(string buildId, string artifactName, string project)
        {
            var pauseBetweenFailures = TimeSpan.FromSeconds(2);
            var retryPolicy = Policy
                .Handle<HttpRequestException>()
                .WaitAndRetryAsync(5, i => pauseBetweenFailures);

            var connection = await CreateVssConnection();
            var buildClient = connection.GetClient<BuildHttpClient>();
            string url = null;
            await retryPolicy.ExecuteAsync(async () =>
            {
                var artifact = await buildClient.GetArtifactAsync(project, int.Parse(buildId), artifactName);
                url =  artifact?.Resource?.DownloadUrl;
            });

            if (string.IsNullOrEmpty(url))
            {
                throw new Exception(string.Format("Failed to get download url for artifact {0} in build {1} in project {2}", artifactName, buildId, project));
            }
            return url;
        }

        private async Task<VssConnection> CreateVssConnection()
        {
            var accessToken = await getAccessToken();
            var token = new VssAadToken("Bearer", accessToken);
            return new VssConnection(new Uri("https://dev.azure.com/azure-sdk/"), new VssAadCredential(token));
        }

        private async Task<string> getAccessToken()
        {
            // APIView deployed instances uses managed identity to authenticate requests to Azure DevOps.
            // For local testing, VS will use developer credentials to create token
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new TokenRequestContext(VssAadSettings.DefaultScopes);
            var token = await credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);
            return token.Token;
        }

        private async Task<HttpResponseMessage> GetFromDevopsAsync(string request)
        {
            var httpClient = new HttpClient();            
            var accessToken = await getAccessToken();
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var maxRetryAttempts = 10;
            var pauseBetweenFailures = TimeSpan.FromSeconds(2);

            var retryPolicy = Policy
                .Handle<HttpRequestException>()
                .WaitAndRetryAsync(maxRetryAttempts, i => pauseBetweenFailures);

            HttpResponseMessage downloadResp = null;
            await retryPolicy.ExecuteAsync(async () =>
            {
                downloadResp = await httpClient.GetAsync(request);
            });
            return downloadResp;
        }

        public async Task RunPipeline(string pipelineName, string reviewDetails, string originalStorageUrl)
        {
            //Create dictionary of all required parametes to run tools - generate-<language>-apireview pipeline in azure devops
            var reviewDetailsDict = new Dictionary<string, string> { { "Reviews", reviewDetails }, { "APIViewUrl", _hostUrl }, { "StorageContainerUrl", originalStorageUrl } };
            var devOpsConnection = await CreateVssConnection();
            string projectName = _configuration["Azure-Devops-internal-project"] ?? "internal";

            BuildHttpClient buildClient = await devOpsConnection.GetClientAsync<BuildHttpClient>();
            var projectClient = await devOpsConnection.GetClientAsync<ProjectHttpClient>();
            string envName = _configuration["apiview-deployment-environment"];
            string updatedPipelineName = string.IsNullOrEmpty(envName) ? pipelineName : $"{pipelineName}-{envName}";
            int definitionId = await GetPipelineId(updatedPipelineName, buildClient, projectName);
            if (definitionId == 0)
            {
                throw new Exception(string.Format("Azure Devops pipeline is not found with name {0}. Please recheck and ensure pipeline exists with this name", updatedPipelineName));
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
