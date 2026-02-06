// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

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
        Task<(SpecGenSdkConfigContentType type, string value)> GetConfigurationAsync(string repositoryRoot, SpecGenSdkConfigType configType);

        // Command processing methods
        string SubstituteCommandVariables(string command, Dictionary<string, string> variables);
        string[] ParseCommand(string command);

        // Process creation methods
        ProcessOptions? CreateProcessOptions(
            SpecGenSdkConfigContentType configType,
            string configValue,
            string sdkRepoRoot,
            string workingDirectory,
            Dictionary<string, string> parameters,
            int timeoutMinutes = 5);

        // Process execution methods
        Task<PackageOperationResponse> ExecuteProcessAsync(
            ProcessOptions processOptions,
            CancellationToken ct,
            PackageInfo? packageInfo = null,
            string successMessage = "Process completed successfully.",
            string[]? nextSteps = null);
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
        private const string UpdateChangelogContentCommandJsonPath = "packageOptions/updateChangelogContentScript/command";
        private const string UpdateChangelogContentScriptPathJsonPath = "packageOptions/updateChangelogContentScript/path";
        private const string UpdateVersionCommandJsonPath = "packageOptions/updateVersionScript/command";
        private const string UpdateVersionScriptPathJsonPath = "packageOptions/updateVersionScript/path";
        private const string UpdateMetadataCommandJsonPath = "packageOptions/updateMetadataScript/command";
        private const string UpdateMetadataScriptPathJsonPath = "packageOptions/updateMetadataScript/path";
        private const string SpecToSdkConfigPath = "eng/swagger_to_sdk_config.json";

        private readonly ILogger<SpecGenSdkConfigHelper> _logger;
        private readonly IProcessHelper _processHelper;

        public SpecGenSdkConfigHelper(ILogger<SpecGenSdkConfigHelper> logger, IProcessHelper processHelper)
        {
            this._logger = logger;
            this._processHelper = processHelper;
        }

        // Gets a configuration value from the swagger_to_sdk_config.json file
        public async Task<T> GetConfigValueFromRepoAsync<T>(string repositoryRoot, string jsonPath)
        {
            var specToSdkConfigFilePath = Path.Combine(repositoryRoot, SpecToSdkConfigPath);

            _logger.LogInformation("Reading configuration from: {specToSdkConfigFilePath} at path: {jsonPath}", specToSdkConfigFilePath, jsonPath);

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

        // Get configuration for a specific type (either command or script path)
        public async Task<(SpecGenSdkConfigContentType type, string value)> GetConfigurationAsync(string repositoryRoot, SpecGenSdkConfigType configType)
        {
            var (commandPath, scriptPath) = GetConfigPaths(configType);
            
            // Try command first
            try
            {
                var command = await GetConfigValueFromRepoAsync<string>(repositoryRoot, commandPath);
                if (!string.IsNullOrEmpty(command))
                {
                    _logger.LogDebug("Found {ConfigType} command configuration", configType);
                    return (SpecGenSdkConfigContentType.Command, command);
                }
            }
            catch (InvalidOperationException ex)
            {
                // Command not found, continue to try path
                _logger.LogDebug("No {configType} configuration found, trying script path. Error: {errorMessage}", configType, ex.Message);
            }

            // Try path
            try
            {
                var path = await GetConfigValueFromRepoAsync<string>(repositoryRoot, scriptPath);
                if (!string.IsNullOrEmpty(path))
                {
                    _logger.LogDebug("Found {ConfigType} script path configuration", configType);
                    return (SpecGenSdkConfigContentType.ScriptPath, path);
                }
            }
            catch (InvalidOperationException ex)
            {
                // Path not found either
                _logger.LogDebug("No {configType} configuration found. Error: {errorMessage}", configType, ex.Message);
            }

            _logger.LogWarning("Neither '{commandPath}' nor '{scriptPath}' found in configuration for {configType}", commandPath, scriptPath, configType);
            return (SpecGenSdkConfigContentType.Unknown, string.Empty);
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
                var value = variable.Value;
                
                // Check if value needs quoting - contains special characters that require quoting in shell commands
                // Including: spaces, ampersand, pipe, semicolon, less than, greater than, parentheses, backtick, dollar sign, single quote
                bool needsQuoting = value.IndexOfAny(new[] { ' ', '&', '|', ';', '<', '>', '(', ')', '`', '$', '\'' }) >= 0;
                
                if (needsQuoting)
                {
                    // Use regex to find placeholder and capture path continuation
                    // Path continuation stops at common delimiters: whitespace, quotes, =, ;, ,
                    var pattern = Regex.Escape(placeholder) + @"([^\s""=;,]*)";
                    var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                    
                    result = regex.Replace(result, match =>
                    {
                        // Extract the path continuation from the match
                        string pathContinuation = match.Groups[1].Value;
                        // Build the quoted replacement including the continuation
                        return $"\"{value}{pathContinuation}\"";
                    });
                }
                else
                {
                    // No quoting needed, simple replacement
                    result = result.Replace(placeholder, value, StringComparison.OrdinalIgnoreCase);
                }
            }

            _logger.LogDebug("Command after variable substitution: {result}", result);
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
        private (string commandPath, string scriptPath) GetConfigPaths(SpecGenSdkConfigType configType)
        {
            return configType switch
            {
                SpecGenSdkConfigType.Build => (BuildCommandJsonPath, BuildScriptPathJsonPath),
                SpecGenSdkConfigType.UpdateChangelogContent => (UpdateChangelogContentCommandJsonPath, UpdateChangelogContentScriptPathJsonPath),
                SpecGenSdkConfigType.UpdateVersion => (UpdateVersionCommandJsonPath, UpdateVersionScriptPathJsonPath),
                SpecGenSdkConfigType.UpdateMetadata => (UpdateMetadataCommandJsonPath, UpdateMetadataScriptPathJsonPath),
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

        /// <summary>
        /// Creates ProcessOptions for the specified configuration.
        /// </summary>
        public ProcessOptions? CreateProcessOptions(
            SpecGenSdkConfigContentType configType,
            string configValue,
            string sdkRepoRoot,
            string workingDirectory,
            Dictionary<string, string> parameters,
            int timeoutMinutes = 5)
        {
            if (configType == SpecGenSdkConfigContentType.Command)
            {
                return CreateCommandProcessOptions(configValue, workingDirectory, parameters, timeoutMinutes);
            }
            else if (configType == SpecGenSdkConfigContentType.ScriptPath)
            {
                return CreateScriptProcessOptions(sdkRepoRoot, configValue, workingDirectory, parameters, timeoutMinutes);
            }

            _logger.LogWarning("Unsupported configuration content type: {ConfigContentType}, configValue: {ConfigValue}", configType, configValue);
            return null;
        }

        /// <summary>
        /// Executes a process using the provided ProcessOptions.
        /// </summary>
        public async Task<PackageOperationResponse> ExecuteProcessAsync(
            ProcessOptions processOptions,
            CancellationToken ct,
            PackageInfo? packageInfo = null,
            string successMessage = "Process completed successfully.",
            string[]? nextSteps = null)
        {
            try
            {
                var result = await _processHelper.Run(processOptions, ct);
                var trimmedOutput = (result.Output ?? string.Empty).Trim();

                if (result.ExitCode != 0)
                {
                    return PackageOperationResponse.CreateFailure(
                        $"Process failed with exit code {result.ExitCode}. Output:\n{trimmedOutput}",
                        packageInfo);
                }

                return PackageOperationResponse.CreateSuccess($"{successMessage}", packageInfo, nextSteps);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while executing process");
                return PackageOperationResponse.CreateFailure($"An error occurred: {ex.Message}", packageInfo);
            }
        }

        /// <summary>
        /// Creates ProcessOptions for command-based execution.
        /// </summary>
        private ProcessOptions? CreateCommandProcessOptions(
            string configValue,
            string workingDirectory,
            Dictionary<string, string> variables,
            int timeoutMinutes)
        {
            var substitutedCommand = SubstituteCommandVariables(configValue, variables);
            var commandParts = ParseCommand(substitutedCommand);

            if (commandParts.Length == 0)
            {
                _logger.LogWarning("No command parts found after parsing: {Command}", substitutedCommand);
                return null;
            }

            var options = new ProcessOptions(
                commandParts[0], 
                commandParts.Skip(1).ToArray(),
                logOutputStream: true,
                workingDirectory: workingDirectory,
                timeout: TimeSpan.FromMinutes(timeoutMinutes)
            );

            _logger.LogDebug("Created command process options: {Command} {Args}", options.Command, string.Join(" ", options.Args));
            return options;
        }

        /// <summary>
        /// Creates ProcessOptions for script-based execution.
        /// </summary>
        private ProcessOptions? CreateScriptProcessOptions(
            string sdkRepoRoot,
            string configValue,
            string workingDirectory,
            Dictionary<string, string> variables,
            int timeoutMinutes)
        {
            var fullScriptPath = Path.Combine(sdkRepoRoot, configValue);

            if (!File.Exists(fullScriptPath))
            {
                _logger.LogWarning("Script not found at: {FullScriptPath}", fullScriptPath);
                return null;
            }

            _logger.LogInformation("Executing PowerShell script: {FullScriptPath}", fullScriptPath);

            // Convert dictionary to PowerShell parameter array
            var scriptArgs = new List<string>();
            foreach (var param in variables)
            {
                scriptArgs.Add($"-{param.Key}");
                scriptArgs.Add(param.Value);
            }

            return new PowershellOptions(
                fullScriptPath,
                scriptArgs.ToArray(),
                logOutputStream: true,
                workingDirectory: workingDirectory,
                timeout: TimeSpan.FromMinutes(timeoutMinutes)
            );
        }
    }

    /// <summary>
    /// Represents different content types for configuration values.
    /// </summary>
    public enum SpecGenSdkConfigContentType
    {
        Unknown,
        Command,
        ScriptPath
    }

    /// <summary>
    /// Represents different types of configuration that can be retrieved from swagger_to_sdk_config.json
    /// </summary>
    public enum SpecGenSdkConfigType
    {
        Build,
        UpdateChangelogContent,
        UpdateVersion,
        UpdateMetadata
    }
}
