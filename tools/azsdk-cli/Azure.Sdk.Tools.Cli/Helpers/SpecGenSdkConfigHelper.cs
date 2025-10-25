// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.CommandLine.Parsing;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    /// <summary>
    /// Interface for accessing and processing Azure SDK repository configuration from swagger_to_sdk_config.json files.
    /// Supports reading build configurations, processing command templates, and navigating JSON configuration structures.
    /// </summary>
    public interface ISpecGenSdkConfigHelper
    {
        // Config value retrieval methods
        Task<T> GetConfigValueFromRepoAsync<T>(string repositoryRoot, string jsonPath);
        Task<(ConfigContentType type, string value)> GetBuildConfigurationAsync(string repositoryRoot);
        Task<(ConfigContentType type, string value)> GetConfigurationAsync(string repositoryRoot, ConfigType configType);
        
        // Command processing methods
        string SubstituteCommandVariables(string command, Dictionary<string, string> variables);
        string[] ParseCommand(string command);
    }

    /// <summary>
    /// Helper class for reading and processing configuration from the standardized "swagger_to_sdk_config.json" file 
    /// located at "eng/swagger_to_sdk_config.json" in Azure SDK repositories. This configuration file defines build 
    /// scripts, commands, and package options used during SDK generation from OpenAPI specifications.
    /// 
    /// Provides functionality to:
    /// - Extract build configuration (commands or script paths) for SDK compilation
    /// - Substitute template variables in build commands (e.g., {packagePath})
    /// - Parse command strings into executable components
    /// - Navigate JSON configuration paths to retrieve specific values
    /// 
    /// Configuration schema reference: https://github.com/Azure/azure-sdk-tools/blob/main/tools/spec-gen-sdk/src/types/SwaggerToSdkConfigSchema.json
    /// </summary>
    public class SpecGenSdkConfigHelper : ISpecGenSdkConfigHelper
    {
        // Constants
        private const string BuildCommandJsonPath = "packageOptions/buildScript/command";
        private const string BuildScriptPathJsonPath = "packageOptions/buildScript/path";
        private const string UpdateChangelogCommandJsonPath = "packageOptions/updateChangelogScript/command";
        private const string UpdateChangelogScriptPathJsonPath = "packageOptions/updateChangelogScript/path";
        private const string UpdateVersionCommandJsonPath = "packageOptions/updateVersionScript/command";
        private const string UpdateVersionScriptPathJsonPath = "packageOptions/updateVersionScript/path";
        private const string UpdateMetadataCommandJsonPath = "packageOptions/updateMetadataScript/command";
        private const string UpdateMetadataScriptPathJsonPath = "packageOptions/updateMetadataScript/path";
        private const string UpdateCiCommandJsonPath = "packageOptions/updateCiScript/command";
        private const string UpdateCiScriptPathJsonPath = "packageOptions/updateCiScript/path";
        private const string SpecToSdkConfigPath = "eng/swagger_to_sdk_config.json";

        private readonly ILogger<SpecGenSdkConfigHelper> _logger;

        public SpecGenSdkConfigHelper(ILogger<SpecGenSdkConfigHelper> logger)
        {
            this._logger = logger;
        }

        // Gets a configuration value from the swagger_to_sdk_config.json file
        public async Task<T> GetConfigValueFromRepoAsync<T>(string repositoryRoot, string jsonPath)
        {
            var specToSdkConfigFilePath = Path.Combine(repositoryRoot, SpecToSdkConfigPath);

            _logger.LogInformation($"Reading configuration from: {specToSdkConfigFilePath} at path: {jsonPath}");

            if (!File.Exists(specToSdkConfigFilePath))
            {
                throw new FileNotFoundException($"Configuration file not found at: {specToSdkConfigFilePath}");
            }

            try
            {
                // Read and parse the configuration file
                var configContent = await File.ReadAllTextAsync(specToSdkConfigFilePath);
                using var configJson = JsonDocument.Parse(configContent);

                // Use helper method to navigate JSON path
                var (found, element) = TryGetJsonElementByPath(configJson.RootElement, jsonPath);
                if (!found)
                {
                    throw new InvalidOperationException($"Property not found at JSON path '{jsonPath}' in configuration file {specToSdkConfigFilePath}.");
                }

                // Deserialize to the requested type
                var value = JsonSerializer.Deserialize<T>(element.GetRawText());
                if (value == null)
                {
                    throw new InvalidOperationException($"Failed to deserialize value at JSON path '{jsonPath}' to type {typeof(T).Name}.");
                }

                _logger.LogDebug("Retrieved config value from {JsonPath} as {Type}", jsonPath, typeof(T).Name);
                return value;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Error parsing JSON configuration: {ex.Message}", ex);
            }
        }

        // Get build configuration (either command or script path)
        public async Task<(ConfigContentType type, string value)> GetBuildConfigurationAsync(string repositoryRoot)
        {
            return await GetConfigurationAsync(repositoryRoot, ConfigType.Build);
        }

        // Get configuration for a specific type (either command or script path)
        public async Task<(ConfigContentType type, string value)> GetConfigurationAsync(string repositoryRoot, ConfigType configType)
        {
            var (commandPath, scriptPath) = GetConfigPaths(configType);
            
            // Try command first
            try
            {
                var command = await GetConfigValueFromRepoAsync<string>(repositoryRoot, commandPath);
                if (!string.IsNullOrEmpty(command))
                {
                    _logger.LogDebug("Found {ConfigType} command configuration", configType);
                    return (ConfigContentType.Command, command);
                }
            }
            catch (InvalidOperationException)
            {
                // Command not found, continue to try path
                _logger.LogDebug("No {ConfigType} command found, trying script path", configType);
            }

            // Try path
            try
            {
                var path = await GetConfigValueFromRepoAsync<string>(repositoryRoot, scriptPath);
                if (!string.IsNullOrEmpty(path))
                {
                    _logger.LogDebug("Found {ConfigType} script path configuration", configType);
                    return (ConfigContentType.ScriptPath, path);
                }
            }
            catch (InvalidOperationException)
            {
                // Path not found either
                _logger.LogError("No {ConfigType} configuration found", configType);
            }

            throw new InvalidOperationException($"Neither '{commandPath}' nor '{scriptPath}' found in configuration for {configType}.");
        }

        // Substitute template variables in command strings
        public string SubstituteCommandVariables(string command, Dictionary<string, string> variables)
        {
            if (string.IsNullOrEmpty(command))
            {
                return command;
            }

            if (variables == null || variables.Count == 0)
            {
                return command;
            }

            var result = command;
            
            // Replace variables in the format {variableName}
            foreach (var variable in variables)
            {
                var placeholder = $"{{{variable.Key}}}";
                result = result.Replace(placeholder, variable.Value, StringComparison.OrdinalIgnoreCase);
            }

            _logger.LogDebug("Command after variable substitution: {Result}", result);
            return result;
        }

        // Parse command string into command and arguments
        public string[] ParseCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return Array.Empty<string>();
            }

            // Use System.CommandLine.CommandLineParser to split respecting quotes
            var tokens = CommandLineParser.SplitCommandLine(command).ToArray();

            _logger.LogDebug("Parsed command into {Count} parts: {Parts}", tokens.Length, string.Join(", ", tokens));
            return tokens;
        }

        // Get the configuration paths for a specific config type
        private (string commandPath, string scriptPath) GetConfigPaths(ConfigType configType)
        {
            return configType switch
            {
                ConfigType.Build => (BuildCommandJsonPath, BuildScriptPathJsonPath),
                ConfigType.UpdateChangelog => (UpdateChangelogCommandJsonPath, UpdateChangelogScriptPathJsonPath),
                ConfigType.UpdateVersion => (UpdateVersionCommandJsonPath, UpdateVersionScriptPathJsonPath),
                ConfigType.UpdateMetadata => (UpdateMetadataCommandJsonPath, UpdateMetadataScriptPathJsonPath),
                ConfigType.UpdateCi => (UpdateCiCommandJsonPath, UpdateCiScriptPathJsonPath),
                _ => throw new ArgumentException($"Unsupported config type: {configType}")
            };
        }

        // Try to get a JSON element by its path
        private (bool found, JsonElement element) TryGetJsonElementByPath(JsonElement root, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return (false, default);
            }

            var pathParts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            JsonElement current = root;

            foreach (var part in pathParts)
            {
                if (!current.TryGetProperty(part, out current))
                {
                    return (false, default);
                }
            }

            return (true, current);
        }
    }

    /// <summary>
    /// Represents different content types for configuration values.
    /// </summary>
    public enum ConfigContentType
    {
        Command,
        ScriptPath
    }

    /// <summary>
    /// Represents different types of configuration that can be retrieved from swagger_to_sdk_config.json
    /// </summary>
    public enum ConfigType
    {
        Build,
        UpdateChangelog,
        UpdateVersion,
        UpdateMetadata,
        UpdateCi
    }
}
