#nullable enable
using System;
using System.Collections.Generic;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using Microsoft.Extensions.Configuration;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Configuration
{
    public class McpConfiguration
    {
        private readonly IConfiguration Configuration;

        public McpConfiguration(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// Gets the primary label prefixes (e.g. "server-") from configuration.
        /// Config key: "microsoft/mcp:McpPrimaryLabelPrefixes", semicolon-delimited.
        /// </summary>
        public IReadOnlyList<string> GetPrimaryLabelPrefixes()
        {
            string? configValue = Configuration[$"{McpConstants.McpConfigPrefix}:{McpConstants.McpServerLabelPrefix}"];
            if (string.IsNullOrEmpty(configValue))
            {
                return Array.Empty<string>();
            }
            return configValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        /// <summary>
        /// Gets the secondary label prefixes (e.g. "tools-;remote-mcp;packages-") from configuration.
        /// Config key: "microsoft/mcp:McpSecondaryLabelPrefixes", semicolon-delimited.
        /// </summary>
        public IReadOnlyList<string> GetSecondaryLabelPrefixes()
        {
            string? configValue = Configuration[$"{McpConstants.McpConfigPrefix}:{McpConstants.McpToolLabelPrefix}"];
            if (string.IsNullOrEmpty(configValue))
            {
                return Array.Empty<string>();
            }
            return configValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}