using System;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Azure.Sdk.Tools.PipelineWitness.Configuration;

public class PostConfigureSettings : IPostConfigureOptions<PipelineWitnessSettings>
{
    private readonly ILogger logger;

    public PostConfigureSettings(ILogger<PostConfigureSettings> logger)
    {
        this.logger = logger;
    }

    public void PostConfigure(string name, PipelineWitnessSettings options)
    {
        if (options.GitHubRepositories == null || options.GitHubRepositories.Length == 0)
        {
            options.GitHubRepositories = [];

            if (string.IsNullOrEmpty(options.GitHubRepositoriesSource))
            {
                this.logger.LogWarning("No GitHubRepositories or GitHubRepositoriesSource configured");
                return;
            }

            try
            {
                this.logger.LogInformation("Replacing settings property GitHubRepositories with values from {Source}", options.GitHubRepositoriesSource);
                using var client = new HttpClient();

                options.GitHubRepositories = client.GetFromJsonAsync<string[]>(options.GitHubRepositoriesSource)
                    .ConfigureAwait(true)
                    .GetAwaiter()
                    .GetResult();

                this.logger.LogInformation("Loaded {Count} repositories from {Source}", options.GitHubRepositories.Length, options.GitHubRepositoriesSource);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error loading repository list from source");
                return;
            }
        }
    }
}
