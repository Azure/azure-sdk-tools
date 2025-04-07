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
        private IConfigurationRoot  _config;

        public ConfigurationService(IConfigurationRoot config)
        {
            _config = config;
        }

        /// <summary>
        /// Gets the configuration for a specific repository, falling back to the default configuration if the repository section does not exist.
        /// </summary>
        /// <param name="repository">The name of the repository.</param>
        /// <returns>The merged configuration for the specified repository.</returns>
        public IConfigurationRoot GetRepositoryConfiguration(string repository)
        {
            var defaultSection = _config.GetSection("defaults");
            var repoSection = _config.GetSection(repository);

            var configurationBuilder = new ConfigurationBuilder();

            // Add the default section first
            configurationBuilder.AddConfiguration(defaultSection);

            // Check if the repository section exists and is not empty
            if (repoSection.Exists() && repoSection.GetChildren().Any())
            {
                // Add the repository section, which will override the default section where keys overlap
                configurationBuilder.AddConfiguration(repoSection);
            }

            var mergedConfig = configurationBuilder.Build();

            return mergedConfig;
        }

        /// <summary>
        /// Gets the default app configuration.
        /// </summary>
        /// <returns>The default app configuration.</returns>
        public IConfigurationRoot GetDefaultConfiguration()
        {
            // Return the default configuration section
            return _config.GetSection("defaults");
        }
    }
}