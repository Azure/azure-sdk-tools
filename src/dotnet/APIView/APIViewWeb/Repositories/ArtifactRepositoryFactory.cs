using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using System;

namespace APIViewWeb.Repositories
{
    public class ArtifactRepositoryFactory
    {
        private readonly IConfiguration _configuration;
        private readonly TelemetryClient _telemetryClient;

        public ArtifactRepositoryFactory(IConfiguration configuration, TelemetryClient telemetryClient)
        {
            _configuration = configuration;
            _telemetryClient = telemetryClient;
        }

        public IArtifactRepository CreateRepository(string location = "DevOps")
        {
            return location switch
            {
                "GitHub" => new GitHubArtifactRepository(_configuration, _telemetryClient),
                "DevOps" => new DevopsArtifactRepository(_configuration, _telemetryClient),
                _ => throw new ArgumentException("Invalid repository location")
            };
        }
    }
}
