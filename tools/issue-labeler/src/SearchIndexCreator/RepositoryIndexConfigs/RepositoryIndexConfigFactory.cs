// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace SearchIndexCreator.RepositoryIndexConfigs
{
    /// <summary>
    /// Factory for creating repository index configurations based on repository name.
    /// </summary>
    public static class RepositoryIndexConfigFactory
    {
        /// <summary>
        /// Creates the appropriate repository index configuration based on the repository name.
        /// </summary>
        /// <param name="repoName">The repository name from configuration.</param>
        /// <returns>The appropriate IRepositoryIndexConfig implementation.</returns>
        public static IRepositoryIndexConfig Create(string? repoName)
        {
            if (string.Equals(repoName, "mcp", StringComparison.OrdinalIgnoreCase))
            {
                return new McpRepositoryIndexConfig();
            }

            // Default to Azure SDK config
            return new AzureSdkRepositoryIndexConfig();
        }
    }
}
