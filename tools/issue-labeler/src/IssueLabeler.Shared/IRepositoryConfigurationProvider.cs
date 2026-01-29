// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace IssueLabeler.Shared
{
    /// <summary>
    /// Provides repository-specific configuration.
    /// </summary>
    public interface IRepositoryConfigurationProvider
    {
        /// <summary>
        /// Gets the configuration for a specific repository.
        /// </summary>
        RepositoryConfiguration GetForRepository(string repository);

        /// <summary>
        /// Gets the default configuration.
        /// </summary>
        RepositoryConfiguration GetDefault();
    }
}
