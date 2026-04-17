// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;

namespace SearchIndexCreator.RepositoryIndexConfigs
{
    public static class RepositoryIndexConfigFactory
    {
        /// <summary>
        /// Creates the appropriate repository index configuration based on the repository name.
        /// </summary>
        /// <param name="repoName">The repository name from configuration.</param>
        /// <param name="config">The application configuration.</param>
        /// <returns>The corresponding IRepositoryIndexConfig implementation.</returns>
        public static IRepositoryIndexConfig Create(string? repoName, IConfiguration? config = null)
        {

            if (string.Equals(repoName, "mcp", StringComparison.OrdinalIgnoreCase))
            {
                if (config == null) {
                    throw new ArgumentNullException(nameof(config), "IConfiguration is required for MCP repository.");
                }
                    return new McpRepositoryIndexConfig(config);
            }

            // Default to Azure SDK config
            return new AzureSdkRepositoryIndexConfig();
        }
    }
}
