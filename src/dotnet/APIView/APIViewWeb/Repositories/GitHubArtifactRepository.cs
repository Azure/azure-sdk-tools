using Polly;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.ApplicationInsights;

namespace APIViewWeb.Repositories
{
    public class GitHubArtifactRepository : IArtifactRepository
    {
        private readonly IConfiguration _configuration;
        private readonly TelemetryClient _telemetryClient;

        public GitHubArtifactRepository(IConfiguration configuration, TelemetryClient telemetryClient)
        {
            _configuration = configuration;
            _telemetryClient = telemetryClient;
        }

        public async Task<Stream> DownloadPackageArtifact(
            string repoName, string buildId, string artifactName, string filePath, string project, string format = "file")
        {
            using (var client = new HttpClient())
            {
                var gitHubToken = _configuration["github-access-token"];
                var artifactUrl = $"https://api.github.com/repos/{repoName}/actions/runs/{buildId}/artifacts";

                var retryPolicy = Policy
                    .Handle<HttpRequestException>()
                    .WaitAndRetryAsync(5, i => TimeSpan.FromSeconds(2));

                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DotNet", "1.0"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", gitHubToken);

                var response = await client.GetStringAsync(artifactUrl);

                _telemetryClient.TrackEvent($"GitHub Artifact Repo Response: {response}");

                var artifacts = JsonDocument.Parse(response).RootElement.GetProperty("artifacts");

                var downloadUrl = artifacts.EnumerateArray()
                                    .FirstOrDefault(artifact => artifact.GetProperty("name").GetString() == artifactName)
                                    .GetProperty("archive_download_url").GetString();

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    throw new Exception(
                        $"Failed to get download url for artifact {artifactName} in build {buildId}.");
                }

                HttpResponseMessage downloadResp = null;
                await retryPolicy.ExecuteAsync(async () =>
                {
                    downloadResp = await client.GetAsync(downloadUrl);
                });

                downloadResp.EnsureSuccessStatusCode();
                return await downloadResp.Content.ReadAsStreamAsync();
            }
        }

        Task IArtifactRepository.RunPipeline(string pipelineName, string reviewDetails, string originalStorageUrl)
        {
            throw new NotImplementedException();
        }
    }
}
