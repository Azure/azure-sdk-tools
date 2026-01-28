// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.Extensions.Configuration;

namespace Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;

/// <summary>
/// Helper class for classifying feedback items using LLM-powered classification.
/// Determines if feedback is actionable and provides next steps.
/// </summary>
public class FeedbackClassifier
{
    private readonly IMicroagentHostService _microagentHost;
    private readonly ILogger<FeedbackClassifier> _logger;
    private readonly string _model;

    public const int MaxIterations = 4;

    public FeedbackClassifier(
        IMicroagentHostService microagentHost,
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        _microagentHost = microagentHost;
        _model = configuration["AZURE_OPENAI_MODEL"] ?? "gpt-4o";
        _logger = loggerFactory.CreateLogger<FeedbackClassifier>();
    }

    /// <summary>
    /// Classifies feedback using the classification template with documentation fetching.
    /// </summary>
    /// <param name="context">The orchestration context containing feedback and iteration history</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Classification result with actionability status and next steps</returns>
    public async Task<ClassificationResult> ClassifyAsync(OrchestrationContext context, CancellationToken ct)
    {
        var prompt = new CommentClassificationTemplate(
            null, // serviceName - not available from APIView
            context.Language,
            context.ToClassifierInput(),
            context.Iteration,
            context.IsStalled()
        ).BuildPrompt();

        var result = await _microagentHost.RunAgentToCompletion(new Microagent<ClassificationResult>
        {
            Instructions = prompt,
            Model = _model,
            MaxToolCalls = 5, // Allow multiple tool calls to fetch documentation
            Tools = [
                AgentTool<FetchDocumentationInput, FetchDocumentationOutput>.FromFunc(
                    name: "fetch_documentation",
                    description: "Fetch documentation from a URL to provide detailed guidance. Use this when a Documentation link is provided in the NextSteps section.",
                    invokeHandler: async (input, cancellationToken) =>
                    {
                        try
                        {
                            // Transform GitHub web URLs to raw content URLs
                            var url = input.Url;
                            if (url.Contains("github.com") && url.Contains("/blob/"))
                            {
                                url = url.Replace("github.com", "raw.githubusercontent.com")
                                         .Replace("/blob/", "/refs/heads/");
                            }
                            
                            _logger.LogInformation("Fetching documentation from: {Url}", url);
                            
                            // Use HttpClient to fetch the page
                            using var httpClient = new HttpClient();
                            httpClient.DefaultRequestHeaders.Add("User-Agent", "Azure-SDK-Tools");
                            
                            var response = await httpClient.GetAsync(url, cancellationToken);
                            response.EnsureSuccessStatusCode();
                            
                            var content = await response.Content.ReadAsStringAsync(cancellationToken);
                            
                            // Return first 10000 characters to avoid token limits
                            var truncatedContent = content.Length > 10000 
                                ? content.Substring(0, 10000) + "\n\n[Content truncated...]" 
                                : content;
                            
                            return new FetchDocumentationOutput(truncatedContent);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to fetch documentation from {Url}", input.Url);
                            return new FetchDocumentationOutput($"Failed to fetch documentation: {ex.Message}");
                        }
                    }
                )
            ]
        }, ct);

        return result;
    }

    private record FetchDocumentationInput(string Url);
    private record FetchDocumentationOutput(string Content);

    public class ClassificationResult
    {
        public string Classification { get; set; } = "FAILURE";
        public string Reason { get; set; } = "";
        public int Iteration { get; set; } = 1;
        [JsonPropertyName("Next Action")]
        public string? NextAction { get; set; }
    }
}
