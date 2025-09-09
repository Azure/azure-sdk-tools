// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface ISdkRepoConfigHelper
    {
        Task<string> GetConfigFilePathForRepoAsync(string repoName);
        Task<SdkRepoConfiguration> GetRepoConfigurationAsync(string repoName);
        
        // Config value retrieval methods
        Task<T> GetConfigValueFromRepoAsync<T>(string repositoryRoot, string repoName, string jsonPath);
        Task<(BuildConfigType type, string value)> GetBuildConfigurationAsync(string repositoryRoot, string repoName);
        
        // Command processing methods
        string SubstituteCommandVariables(string command, Dictionary<string, string> variables);
        string[] ParseCommand(string command);
    }

    public class SdkRepoConfigHelper : ISdkRepoConfigHelper
    {
        // JSON path constants
        private const string BuildCommandJsonPath = "packageOptions/buildScript/command";
        private const string BuildScriptPathJsonPath = "packageOptions/buildScript/path";

        private readonly ILogger<SdkRepoConfigHelper> _logger;
        private readonly string _sdkRepoConfigPath;
        private Dictionary<string, SdkRepoConfiguration>? _cachedConfig;

        public SdkRepoConfigHelper(ILogger<SdkRepoConfigHelper> logger)
        {
            this._logger = logger;
            this._sdkRepoConfigPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "Configuration", 
                "SdkRepoConfig.json");
        }

        public async Task<string> GetConfigFilePathForRepoAsync(string repoName)
        {
            var config = await GetRepoConfigurationAsync(repoName);
            _logger.LogDebug($"config file path:{config.ConfigFilePath}");
            return config.ConfigFilePath;
        }

        public async Task<SdkRepoConfiguration> GetRepoConfigurationAsync(string repoName)
        {
            if (string.IsNullOrEmpty(repoName))
            {
                throw new ArgumentException("Repository name cannot be null or empty", nameof(repoName));
            }

            var config = await LoadConfigAsync();
            
            if (!config.TryGetValue(repoName, out var repoConfig))
            {
                _logger.LogError("No configuration found for repository: {RepoName}", repoName);
                throw new InvalidOperationException($"No configuration found for repository: {repoName}");
            }

            _logger.LogDebug("Retrieved configuration for repository: {RepoName}", repoName);
            return repoConfig;
        }

        // Gets a configuration value from the repository-specific config file
        public async Task<T> GetConfigValueFromRepoAsync<T>(string repositoryRoot, string repoName, string jsonPath)
        {
            // Get the config file path for this specific repository
            var relativeConfigPath = await GetConfigFilePathForRepoAsync(repoName);
            var specToSdkConfigFilePath = Path.Combine(repositoryRoot, relativeConfigPath);

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
        public async Task<(BuildConfigType type, string value)> GetBuildConfigurationAsync(string repositoryRoot, string repoName)
        {
            // Try command first
            try
            {
                var command = await GetConfigValueFromRepoAsync<string>(repositoryRoot, repoName, BuildCommandJsonPath);
                if (!string.IsNullOrEmpty(command))
                {
                    _logger.LogDebug("Found build command configuration for repository: {RepoName}", repoName);
                    return (BuildConfigType.Command, command);
                }
            }
            catch (InvalidOperationException)
            {
                // Command not found, continue to try path
                _logger.LogDebug("No build command found, trying script path for repository: {RepoName}", repoName);
            }

            // Try path
            try
            {
                var path = await GetConfigValueFromRepoAsync<string>(repositoryRoot, repoName, BuildScriptPathJsonPath);
                if (!string.IsNullOrEmpty(path))
                {
                    _logger.LogDebug("Found build script path configuration for repository: {RepoName}", repoName);
                    return (BuildConfigType.ScriptPath, path);
                }
            }
            catch (InvalidOperationException)
            {
                // Path not found either
                _logger.LogError("No build configuration found for repository: {RepoName}", repoName);
            }

            throw new InvalidOperationException($"Neither '{BuildCommandJsonPath}' nor '{BuildScriptPathJsonPath}' found in configuration.");
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

            var parts = new List<string>();
            var inQuotes = false;
            var currentPart = new StringBuilder();

            for (int i = 0; i < command.Length; i++)
            {
                char c = command[i];
                
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (currentPart.Length > 0)
                    {
                        parts.Add(currentPart.ToString());
                        currentPart.Clear();
                    }
                }
                else
                {
                    currentPart.Append(c);
                }
            }

            if (currentPart.Length > 0)
            {
                parts.Add(currentPart.ToString());
            }

            _logger.LogDebug("Parsed command into {Count} parts: {Parts}", parts.Count, string.Join(", ", parts));
            return parts.ToArray();
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

        private async Task<Dictionary<string, SdkRepoConfiguration>> LoadConfigAsync()
        {
            // Use cached config if available
            if (_cachedConfig != null)
            {
                return _cachedConfig;
            }

            try
            {
                if (!File.Exists(_sdkRepoConfigPath))
                {
                    _logger.LogError($"SDK repository configuration file not found at: {_sdkRepoConfigPath}");
                    throw new FileNotFoundException($"SDK repository configuration file not found at: {_sdkRepoConfigPath}");
                }

                _logger.LogDebug($"Loading SDK repository configuration from: {_sdkRepoConfigPath}");
                var content = await File.ReadAllTextAsync(_sdkRepoConfigPath);
                
                // Parse the JSON and filter out properties that start with underscore (comments)
                using var document = JsonDocument.Parse(content);
                var filteredConfig = new Dictionary<string, SdkRepoConfiguration>();
                
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    // Skip properties that start with underscore (like _description, _usage)
                    if (!property.Name.StartsWith("_"))
                    {
                        var config = JsonSerializer.Deserialize<SdkRepoConfiguration>(property.Value.GetRawText());
                        if (config != null)
                        {
                            filteredConfig[property.Name] = config;
                        }
                    }
                }
                
                _cachedConfig = filteredConfig;

                _logger.LogInformation("Successfully loaded configuration for {Count} repositories", _cachedConfig.Count);
                return _cachedConfig;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parsing SDK repository configuration JSON");
                throw new InvalidOperationException($"Error parsing SDK repository configuration: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading SDK repository configuration");
                throw;
            }
        }
    }

    public class SdkRepoConfiguration
    {
        [JsonPropertyName("configFilePath")]
        public string ConfigFilePath { get; set; } = string.Empty;
    }

    public enum BuildConfigType
    {
        Command,
        ScriptPath
    }
}
