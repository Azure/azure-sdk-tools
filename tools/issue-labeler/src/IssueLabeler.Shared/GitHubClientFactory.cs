using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using GitHubJwt;
using Microsoft.Extensions.Configuration;
using Octokit;

namespace IssueLabeler.Shared
{
    public sealed class GitHubClientFactory
    {
        private readonly IConfiguration _configuration;

        public GitHubClientFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<GitHubClient> CreateAsync()
        {
            // See: https://octokitnet.readthedocs.io/en/latest/github-apps/ for details.

            string localDevPAT = _configuration["GitHubDeveloperPAT"];
            if (localDevPAT != null)
            {
                return new GitHubClient(new ProductHeaderValue("GHNotif"))
                {
                    Credentials = new Credentials(localDevPAT)
                };
            }
            else
            {
                var appId = Convert.ToInt32(_configuration["GitHubAppId"]);
                SecretClient secretClient = new SecretClient(new Uri(_configuration["KeyVaultUri"]), new DefaultAzureCredential());
                KeyVaultSecret secret = await secretClient.GetSecretAsync(_configuration["AppSecretName"]).ConfigureAwait(false);
                string privateKey = secret.Value;


                var privateKeySource = new PlainStringPrivateKeySource(privateKey);
                var generator = new GitHubJwtFactory(
                    privateKeySource,
                    new GitHubJwtFactoryOptions
                    {
                        AppIntegrationId = appId,
                        ExpirationSeconds = 8 * 60 // 600 is apparently too high
                });
                var token = generator.CreateEncodedJwtToken();

                var client = CreateForToken(token, AuthenticationType.Bearer);
                await client.GitHubApps.GetAllInstallationsForCurrent();
                var installationTokenResult = await client.GitHubApps.CreateInstallationToken(long.Parse(_configuration["InstallationId"]));

                return CreateForToken(installationTokenResult.Token, AuthenticationType.Oauth);
            }
        }

        private static GitHubClient CreateForToken(string token, AuthenticationType authenticationType)
        {
            var productInformation = new ProductHeaderValue("issuelabelertemplate");
            var client = new GitHubClient(productInformation)
            {
                Credentials = new Credentials(token, authenticationType)
            };
            return client;
        }

        public sealed class PlainStringPrivateKeySource : IPrivateKeySource
        {
            private readonly string _key;

            public PlainStringPrivateKeySource(string key)
            {
                _key = key;
            }

            public TextReader GetPrivateKeyReader()
            {
                return new StringReader(_key);
            }
        }
    }
}
