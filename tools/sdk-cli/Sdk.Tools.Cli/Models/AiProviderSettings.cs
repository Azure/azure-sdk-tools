// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Sdk.Tools.Cli.Models;

/// <summary>
/// Configuration for AI provider selection.
/// </summary>
public class AiProviderSettings
{
    /// <summary>
    /// Default model for GitHub Copilot provider.
    /// </summary>
    public const string DefaultCopilotModel = "claude-sonnet-4.5";
    
    /// <summary>
    /// Default model for OpenAI-compatible provider.
    /// </summary>
    public const string DefaultOpenAiModel = "gpt-5.2";
    
    /// <summary>
    /// Use custom OpenAI-compatible endpoint instead of GitHub Copilot.
    /// Set via --use-openai flag or SDK_CLI_USE_OPENAI=true environment variable.
    /// </summary>
    public bool UseOpenAi { get; set; }
    
    /// <summary>
    /// Custom OpenAI-compatible API endpoint (optional).
    /// Set via OPENAI_ENDPOINT environment variable.
    /// Defaults to https://api.openai.com/v1 if not set.
    /// </summary>
    public string? Endpoint { get; set; }
    
    /// <summary>
    /// API key for the OpenAI-compatible endpoint.
    /// Set via OPENAI_API_KEY environment variable.
    /// </summary>
    public string? ApiKey { get; set; }
    
    /// <summary>
    /// Model to use. Defaults to <see cref="DefaultCopilotModel"/> for Copilot, 
    /// <see cref="DefaultOpenAiModel"/> for OpenAI.
    /// Set via SDK_CLI_MODEL environment variable.
    /// </summary>
    public string? Model { get; set; }
    
    /// <summary>
    /// Enable debug logging of AI requests/responses.
    /// Set via SDK_CLI_DEBUG=true environment variable.
    /// </summary>
    public bool DebugEnabled { get; set; }
    
    /// <summary>
    /// Directory for debug log files.
    /// Set via SDK_CLI_DEBUG_DIR environment variable.
    /// Defaults to ~/.sdk-cli/debug
    /// </summary>
    public string? DebugDirectory { get; set; }
    
    /// <summary>
    /// Get the default model for the current provider.
    /// </summary>
    public string DefaultModel => UseOpenAi ? DefaultOpenAiModel : DefaultCopilotModel;
    
    /// <summary>
    /// Load settings from environment variables.
    /// Uses standard OpenAI environment variable names:
    /// - OPENAI_API_KEY: API key (required for OpenAI mode)
    /// - OPENAI_ENDPOINT: Custom endpoint (optional, defaults to api.openai.com)
    /// - SDK_CLI_USE_OPENAI: Set to "true" to enable OpenAI mode
    /// - SDK_CLI_MODEL: Override the default model
    /// - SDK_CLI_DEBUG: Set to "true" to enable debug logging
    /// - SDK_CLI_DEBUG_DIR: Directory for debug logs
    /// </summary>
    public static AiProviderSettings FromEnvironment()
    {
        var useOpenAi = Environment.GetEnvironmentVariable("SDK_CLI_USE_OPENAI");
        var debug = Environment.GetEnvironmentVariable("SDK_CLI_DEBUG");
        
        return new AiProviderSettings
        {
            UseOpenAi = string.Equals(useOpenAi, "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(useOpenAi, "1", StringComparison.OrdinalIgnoreCase),
            Endpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT"),
            ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
            Model = Environment.GetEnvironmentVariable("SDK_CLI_MODEL"),
            DebugEnabled = string.Equals(debug, "true", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(debug, "1", StringComparison.OrdinalIgnoreCase),
            DebugDirectory = Environment.GetEnvironmentVariable("SDK_CLI_DEBUG_DIR")
        };
    }
    
    /// <summary>
    /// Get the effective model name.
    /// </summary>
    public string GetModel(string? overrideModel = null)
    {
        if (!string.IsNullOrEmpty(overrideModel)) return overrideModel;
        if (!string.IsNullOrEmpty(Model)) return Model;
        return DefaultModel;
    }
    
    /// <summary>
    /// Get the effective endpoint. Returns null if using default OpenAI endpoint.
    /// </summary>
    public Uri? GetEndpoint()
    {
        if (!string.IsNullOrEmpty(Endpoint))
            return new Uri(Endpoint);
        return null; // Use default OpenAI endpoint
    }
}
