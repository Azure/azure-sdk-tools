using Azure.Data.AppConfiguration;
using Azure.Security.KeyVault.Secrets;
using GitHubIssues;
using Microsoft.Extensions.Azure;
using Octokit;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GitHubIssues.Services.Configuration
{
    public class ConfigurationService : IConfigurationService
    { 
        public ConfigurationService(ConfigurationClient configurationClient, SecretClient secretClient)
        {
            this.configurationClient = configurationClient;
            this.secretClient = secretClient;
        }

        private ConfigurationClient configurationClient;
        private SecretClient secretClient;

        private IEnumerable<RepositoryConfiguration> ParseRepositories(string repositories)
        {
            string[] repos = repositories.Split(';');

            foreach (var repo in repos)
            {
                yield return RepositoryConfiguration.Create(repo);
            }
        }

        public async Task<IEnumerable<RepositoryConfiguration>> GetRepositoryConfigurationsAsync()
        {
            ConfigurationSetting setting = await configurationClient.GetConfigurationSettingAsync("githubissues/repositories");
            var repositories = new List<RepositoryConfiguration>(ParseRepositories(setting.Value));
            return repositories;
        }

        public async Task<string> GetFromAddressAsync()
        {
            ConfigurationSetting setting = await configurationClient.GetConfigurationSettingAsync("githubissues/from-address");
            return setting.Value;
        }

        public async Task<string> GetSendGridTokenAsync()
        {
            KeyVaultSecret secret = await secretClient.GetSecretAsync("sendgrid-token");
            return secret.Value;
        }

        public async Task<string> GetGitHubPersonalAccessTokenAsync()
        {
            KeyVaultSecret secret = await secretClient.GetSecretAsync("github-token");
            return secret.Value;
        }
    }
}
