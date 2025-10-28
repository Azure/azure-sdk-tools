// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Concurrent;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface IEnvironmentHelper
    {
        /// <summary>
        /// Gets all cached environment variables starting with AZSDKTOOLS_ prefix
        /// </summary>
        /// <returns>Dictionary of environment variable names and values</returns>
        Dictionary<string, string> GetEnvironmentVariables();

        /// <summary>
        /// Gets a boolean environment variable value with default fallback
        /// </summary>
        /// <param name="name">Environment variable name</param>
        /// <param name="defaultValue">Default value if variable is not set or invalid</param>
        /// <returns>Boolean value</returns>
        bool GetBooleanVariable(string name, bool defaultValue = false);

        /// <summary>
        /// Gets a string environment variable value with default fallback
        /// </summary>
        /// <param name="name">Environment variable name</param>
        /// <param name="defaultValue">Default value if variable is not set</param>
        /// <returns>String value</returns>
        string GetStringVariable(string name, string defaultValue = "");
    }

    public class EnvironmentHelper : IEnvironmentHelper
    {
        private const string AZSDKTOOLS_PREFIX = "AZSDKTOOLS_";
        private readonly ILogger<EnvironmentHelper> logger;
        private readonly ConcurrentDictionary<string, string> cachedVariables = new();

        public EnvironmentHelper(ILogger<EnvironmentHelper> logger)
        {
            this.logger = logger;
            CacheEnvironmentVariables();
        }

        /// <summary>
        /// Cache all environment variables starting with AZSDKTOOLS_ prefix
        /// </summary>
        private void CacheEnvironmentVariables()
        {
            try
            {
                var environmentVariables = Environment.GetEnvironmentVariables();
                foreach (var key in environmentVariables.Keys)
                {
                    var variableName = key.ToString();
                    if (variableName != null && variableName.StartsWith(AZSDKTOOLS_PREFIX, StringComparison.OrdinalIgnoreCase))
                    {
                        var value = environmentVariables[key]?.ToString() ?? "";
                        cachedVariables[variableName] = value;
                        logger.LogDebug("Cached environment variable: {VariableName} = {Value}", variableName, value);
                    }
                }
                logger.LogDebug("Cached {Count} AZSDKTOOLS_ environment variables", cachedVariables.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to cache environment variables");
            }
        }

        public Dictionary<string, string> GetEnvironmentVariables()
        {
            return new Dictionary<string, string>(cachedVariables);
        }

        public bool GetBooleanVariable(string name, bool defaultValue = false)
        {
            if (!cachedVariables.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
            {
                logger.LogDebug("Environment variable {VariableName} not found, using default value: {DefaultValue}", name, defaultValue);
                return defaultValue;
            }

            if (bool.TryParse(value, out var boolValue))
            {
                logger.LogDebug("Environment variable {VariableName} parsed as boolean: {Value}", name, boolValue);
                return boolValue;
            }

            // Handle common string representations of true/false
            var normalizedValue = value.Trim().ToLowerInvariant();
            var isTrue = normalizedValue is "1" or "yes" or "on" or "enabled";
            return isTrue;
        }

        public string GetStringVariable(string name, string defaultValue = "")
        {
            if (cachedVariables.TryGetValue(name, out var value))
            {
                logger.LogDebug("Environment variable {VariableName} found: {Value}", name, value);
                return value;
            }

            logger.LogDebug("Environment variable {VariableName} not found, using default value: {DefaultValue}", name, defaultValue);
            return defaultValue;
        }
    }
}
