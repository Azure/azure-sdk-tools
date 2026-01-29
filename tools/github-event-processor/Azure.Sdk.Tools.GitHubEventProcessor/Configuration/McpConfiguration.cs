#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;

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
        /// Gets the mapping of server labels to their corresponding GitHub team mentions.
        /// </summary>
        public Dictionary<string, string> GetServerTeamMappings()
        {
            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            string? configValue = Configuration[$"{McpConstants.McpConfigPrefix}:ServerTeamMappings"];
            
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
