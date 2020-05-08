using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Octokit;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Azure.Sdk.Tools.CheckEnforcer.Configuration
{
    public class RepositoryConfigurationProvider : IRepositoryConfigurationProvider
    {
        public RepositoryConfigurationProvider(IGitHubClientProvider gitHubClientProvider)
        {
            this.gitHubClientProvider = gitHubClientProvider;
        }

        private IGitHubClientProvider gitHubClientProvider;

        private ConcurrentDictionary<string, RepositoryConfigurationCacheEntry> cachedConfigurations = new ConcurrentDictionary<string, RepositoryConfigurationCacheEntry>();

        private const int RepositoryConfigurationCacheDurationInSeconds = 60;

        public async Task<IRepositoryConfiguration> GetRepositoryConfigurationAsync(long installationId, long repositoryId, string pullRequestSha, CancellationToken cancellationToken)
        {
            var cacheKey = $"{installationId}/{repositoryId}"; // HACK: Config is global across all branches at the moment.
            
            try
            {
                cachedConfigurations.TryGetValue(cacheKey, out RepositoryConfigurationCacheEntry cacheEntry);
                
                if (cacheEntry?.Fetched.AddSeconds(RepositoryConfigurationCacheDurationInSeconds) > DateTimeOffset.UtcNow)
                {
                    return cacheEntry.Configuration;
                }

                var client = await gitHubClientProvider.GetInstallationClientAsync(installationId, cancellationToken);
                var searchResults = await client.Repository.Content.GetAllContents(repositoryId, "eng/CHECKENFORCER");
                var configurationFile = searchResults.Single();
                ThrowIfInvalidFormat(configurationFile);

                var builder = new DeserializerBuilder().Build();
                var configuration = builder.Deserialize<RepositoryConfiguration>(configurationFile.Content);

                cacheEntry = new RepositoryConfigurationCacheEntry(DateTimeOffset.UtcNow, configuration);
                cachedConfigurations.TryAdd(cacheKey, cacheEntry);

                return configuration;
            }
            catch (NotFoundException) // OK, we just disable if it isn't configured.
            {
                var configuration =  new RepositoryConfiguration()
                {
                    IsEnabled = false
                };

                var cacheEntry = new RepositoryConfigurationCacheEntry(DateTimeOffset.UtcNow, configuration);
                cachedConfigurations.TryAdd(cacheKey, cacheEntry);

                return configuration;
            }
        }

        private static void ThrowIfInvalidFormat(RepositoryContent configurationFile)
        {
            // TODO: This is gross. I want to look more closely at the YamlDotNet API
            //       to see if there is a way I can parse this file once and then do
            //       deserialization of a document. At the moment I'm parsing the string
            //       twice. I suspect that I'm just not grokking the API properly yet.

            var stream = new StringReader(configurationFile.Content);
            var yaml = new YamlStream();
            yaml.Load(stream);

            var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;
            var formatScalar = (YamlScalarNode)mapping.Children[new YamlScalarNode("format")];

            if (formatScalar.Value != "v0.1-alpha")
            {
                throw new CheckEnforcerConfigurationException("The value for the 'format' was not valid. Try v0.1-alpha.");
            }
        }
    }
}
