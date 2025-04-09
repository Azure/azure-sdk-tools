using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace IssueLabelerService
{
    public class ConfigurationService
    {
        public IConfiguration _config;

        public ConfigurationService(IConfiguration config) =>
            _config = config;

        public RepositoryConfiguration GetForRepository(string repository) =>
            new(_config, repository);

        public RepositoryConfiguration GetDefault() =>
            new(_config);
    }
}
