// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Prompts.Templates;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Result of analyzing a user prompt for telemetry purposes.
/// </summary>
public class UserPromptAnalysisResult
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = "unknown";

    [JsonPropertyName("prompt_summary")]
    public string PromptSummary { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("package_name")]
    public string? PackageName { get; set; }

    [JsonPropertyName("typespec_project")]
    public string? TypeSpecProject { get; set; }

    /// <summary>
    /// Indicates whether the prompt analysis completed successfully.
    /// When false, the result should be ignored and not recorded in telemetry.
    /// </summary>
    [JsonIgnore]
    public bool IsSuccessful { get; set; } = true;
}

/// <summary>
/// Processes user prompts to classify intent and sanitize PII for telemetry.
/// </summary>
public interface IUserPromptProcessor
{
    /// <summary>
    /// Analyzes a user prompt to determine its category, extract metadata,
    /// and produce a PII-sanitized summary.
    /// </summary>
    /// <param name="promptBody">The raw user prompt text.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Analysis result with category, sanitized summary, and extracted metadata.</returns>
    Task<UserPromptAnalysisResult> AnalyzePromptAsync(string promptBody, CancellationToken ct = default);
}

/// <summary>
/// Uses the Copilot SDK agent pattern to classify user prompts and sanitize PII.
/// </summary>
public class UserPromptProcessor : IUserPromptProcessor
{
    private static readonly HashSet<string> ValidCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "typespec_authoring_or_update",
        "typespec_customization",
        "typespec_validation",
        "sdk_generation",
        "sdk_build_and_test",
        "release_planning",
        "sdk_release",
        "changelog_and_metadata_update",
        "fix_build_failure",
        "analyze_pipeline_error",
        "sdk_validations",
        "apiview_request"
    };

    private readonly ICopilotAgentRunner _agentRunner;
    private readonly ILogger<UserPromptProcessor> _logger;

    public UserPromptProcessor(
        ICopilotAgentRunner agentRunner,
        ILogger<UserPromptProcessor> logger)
    {
        _agentRunner = agentRunner;
        _logger = logger;
    }

    public async Task<UserPromptAnalysisResult> AnalyzePromptAsync(string promptBody, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(promptBody))
        {
            return new UserPromptAnalysisResult
            {
                Category = "unknown",
                PromptSummary = "Empty prompt",
                IsSuccessful = false
            };
        }

        try
        {
            var template = new UserPromptClassificationTemplate(promptBody);
            var prompt = template.BuildPrompt();

            var result = await _agentRunner.RunAsync(new CopilotAgent<string>
            {
                Instructions = prompt,
                MaxIterations = 3,
                IdleTimeout = TimeSpan.FromMinutes(2)
            }, ct);

            return ParseResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze user prompt via Copilot SDK");
            return new UserPromptAnalysisResult
            {
                Category = "unknown",
                PromptSummary = "Prompt analysis failed",
                IsSuccessful = false
            };
        }
    }

    private UserPromptAnalysisResult ParseResult(string rawResult)
    {
        try
        {
            // Strip markdown code fences if present
            var json = rawResult.Trim();
            if (json.StartsWith("```"))
            {
                var firstNewline = json.IndexOf('\n');
                if (firstNewline >= 0)
                {
                    json = json[(firstNewline + 1)..];
                }
                var lastFence = json.LastIndexOf("```");
                if (lastFence >= 0)
                {
                    json = json[..lastFence];
                }
            }

            var result = JsonSerializer.Deserialize<UserPromptAnalysisResult>(json.Trim());
            if (result == null)
            {
                _logger.LogWarning("Failed to deserialize prompt analysis result, received null");
                return new UserPromptAnalysisResult
                {
                    Category = "unknown",
                    PromptSummary = "Failed to parse analysis result",
                    IsSuccessful = false
                };
            }

            // Validate category
            if (string.IsNullOrWhiteSpace(result.Category))
            {
                _logger.LogWarning("Missing or empty category returned by LLM, defaulting to 'unknown'");
                result.Category = "unknown";
                result.IsSuccessful = false;
            }
            else if (!ValidCategories.Contains(result.Category))
            {
                _logger.LogWarning("Unknown category '{Category}' returned by LLM, defaulting to 'unknown'", result.Category);
                result.Category = "unknown";
            }

            // Truncate summary if needed
            if (result.PromptSummary?.Length > 200)
            {
                result.PromptSummary = result.PromptSummary[..197] + "...";
            }

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse prompt analysis JSON result: {Result}", rawResult);
            return new UserPromptAnalysisResult
            {
                Category = "unknown",
                PromptSummary = "Failed to parse analysis result",
                IsSuccessful = false
            };
        }
    }
}
