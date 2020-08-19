using Azure.Data.AppConfiguration;
using Azure.Security.KeyVault.Secrets;
using GitHubIssues;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GitHubIssues.Services.Configuration
{
    public class ConfigurationService : IConfigurationService
    { 
        public ConfigurationService(ConfigurationClient configurationClient, SecretClient secretClient, ILogger<ConfigurationService> logger)
        {
            this.logger = logger;
            this.configurationClient = configurationClient;
            this.secretClient = secretClient;
        }

        private ILogger<ConfigurationService> logger;
        private ConfigurationClient configurationClient;
        private SecretClient secretClient;

        private IEnumerable<RepositoryConfiguration> ParseRepositories(string repositories)
        {
            logger.LogInformation("Parsing repository configuration setting.");
            string[] repos = repositories.Split(';');
            logger.LogInformation("Repository configuration had {segmentCount} segments.", repos.Length);

            foreach (var repo in repos)
            {
                logger.LogInformation("Parsing repository configuration with value: {repoConfiguration}", repo);
                yield return RepositoryConfiguration.Create(repo);
            }
        }

        public async Task<IEnumerable<RepositoryConfiguration>> GetRepositoryConfigurationsAsync()
        {
            logger.LogInformation("Reading githubissues/repositories from Azure AppConfiguration");
            ConfigurationSetting setting = await configurationClient.GetConfigurationSettingAsync("githubissues/repositories");
            logger.LogInformation("Read githubissues/repositories from Azure AppConfiguration, value was: {setting}", setting.Value);

            var repositories = new List<RepositoryConfiguration>(ParseRepositories(setting.Value));
            return repositories;
        }

        public async Task<string> GetFromAddressAsync()
        {
            logger.LogInformation("Reading githubissues/from-address from Azure AppConfiguration");
            ConfigurationSetting setting = await configurationClient.GetConfigurationSettingAsync("githubissues/from-address");
            logger.LogInformation("Read githubissues/from-address from Azure AppConfiguration, value was: {setting}", setting.Value);
            return setting.Value;
        }

        public async Task<string> GetSendGridTokenAsync()
        {
            logger.LogInformation("Reading sendgrid-token from Azure KeyVault");
            KeyVaultSecret secret = await secretClient.GetSecretAsync("sendgrid-token");
            logger.LogInformation("Read sendgrid-token from Azure KeyVault");
            return secret.Value;
        }

        public async Task<string> GetGitHubPersonalAccessTokenAsync()
        {
            logger.LogInformation("Reading github-token from Azure KeyVault");
            KeyVaultSecret secret = await secretClient.GetSecretAsync("github-token");
            logger.LogInformation("Read github-token from Azure KeyVault");
            return secret.Value;
        }
    }
}
