using Microsoft.Extensions.Configuration;

namespace ConfigurationService
{
    public class Configuration
    {
        public IConfiguration _config;

        public Configuration(IConfiguration config) =>
            _config = config;

        public RepositoryConfiguration GetForRepository(string repository) =>
            new(_config, repository);

        public RepositoryConfiguration GetDefault() =>
            new(_config);
    }
}
