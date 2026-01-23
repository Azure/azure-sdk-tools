#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Configuration
{
    /// <summary>
    /// Configuration service for MCP-specific settings loaded from Azure App Configuration.
    /// </summary>
    public class McpConfiguration
    {
        private readonly IConfiguration _configuration;
        private const string McpConfigPrefix = "microsoft/mcp";

        public McpConfiguration(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Gets the mapping of server labels to their corresponding GitHub team mentions.
        /// </summary>
        public Dictionary<string, string> GetServerTeamMappings()
        {
            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            string? configValue = _configuration[$"{McpConfigPrefix}:ServerTeamMappings"];
            
            if (string.IsNullOrEmpty(configValue))
            {
                Console.WriteLine("ServerTeamMappings not found in configuration. Using empty mappings.");
                return mappings;
            }

            var pairs = configValue.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var keyValue = pair.Split('=', 2);
                if (keyValue.Length == 2)
                {
                    var serverLabel = keyValue[0].Trim();
                    var teamMention = keyValue[1].Trim();
                    mappings[serverLabel] = teamMention;
                }
            }

            Console.WriteLine($"Loaded {mappings.Count} server team mappings from configuration.");
            return mappings;
        }

        /// <summary>
        /// Gets the team mention for a specific server label. Returns null if no mapping exists.
        /// </summary>
        public string? GetTeamMentionForServerLabel(string serverLabel)
        {
            var mappings = GetServerTeamMappings();
            return mappings.TryGetValue(serverLabel, out string? team) ? team : null;
        }
    }
}
