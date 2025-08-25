using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models.AiCompletion;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.TypeSpec
{
    /// <summary>
    /// This tool provides AI-powered completion functionality for querying Azure SDK and TypeSpec documentation.
    /// It connects to an AI agent that can answer questions about TypeSpec, Azure SDK guidelines, and API best practices.
    /// </summary>
    [McpServerToolType, Description("AI-powered tool for querying Azure SDK and TypeSpec documentation, guidelines, and best practices.")]
    public class AiCompletionTool : MCPTool
    {
        private readonly IAiCompletionService _aiCompletionService;
        private readonly ILogger<AiCompletionTool> _logger;

        // Command line options and arguments
        private readonly Argument<string> _questionArgument = new("question", "The question to ask the AI agent");
        private readonly Option<string[]> _sourcesOption = new("--sources", "Documentation sources to search (e.g., typespec_docs, azure_api_guidelines)") { AllowMultipleArgumentsPerToken = true };
        private readonly Option<int> _topKOption = new("--top-k", () => 10, "Number of documents to search (1-100)");
        private readonly Option<bool> _includeContextOption = new("--include-context", () => false, "Include full search context in the response");
        private readonly Option<string> _endpointOption = new("--endpoint", "Override the AI completion endpoint");
        private readonly Option<string> _apiKeyOption = new("--api-key", "Override the API key");

        public AiCompletionTool(
            IAiCompletionService aiCompletionService,
            ILogger<AiCompletionTool> logger)
        {
            _aiCompletionService = aiCompletionService ?? throw new ArgumentNullException(nameof(aiCompletionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override Command GetCommand()
        {
            var command = new Command("ai-completion", "Query the Azure SDK QA Bot AI agent for answers about TypeSpec, Azure SDK, and API guidelines");

            command.AddArgument(_questionArgument);
            command.AddOption(_sourcesOption);
            command.AddOption(_topKOption);
            command.AddOption(_includeContextOption);
            command.AddOption(_endpointOption);
            command.AddOption(_apiKeyOption);

            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });

            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var question = ctx.ParseResult.GetValueForArgument(_questionArgument);
            var sources = ctx.ParseResult.GetValueForOption(_sourcesOption)?.ToList();
            var topK = ctx.ParseResult.GetValueForOption(_topKOption);
            var includeContext = ctx.ParseResult.GetValueForOption(_includeContextOption);
            var endpoint = ctx.ParseResult.GetValueForOption(_endpointOption);
            var apiKey = ctx.ParseResult.GetValueForOption(_apiKeyOption);

            if (string.IsNullOrWhiteSpace(question))
            {
                _logger.LogError("Question cannot be empty");
                Console.WriteLine("Error: Question cannot be empty");
                SetFailure();
                return;
            }

            try
            {
                _logger.LogInformation("Querying AI agent via CLI with question: {Question}", question);

                var response = await QueryAzureSDKDocumentation(
                  question,
                  sources,
                  conversationHistory: null,
                  includeFullContext: includeContext,
                  topK: topK,
                  ct: ct);

                if (response.IsSuccessful)
                {
                    Console.WriteLine(response.ToString());
                }
                else
                {
                    _logger.LogError("AI query failed: {Error}", response.ResponseError);
                    Console.WriteLine($"Error: {response.ResponseError}");
                    SetFailure();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query AI agent via CLI");
                Console.WriteLine($"Error: {ex.Message}");
                SetFailure();
            }
        }

        [McpServerTool(Name = "azsdk_ai_qa_completion")]
        [Description(@"Query the Azure SDK QA Bot AI agent for answers about TypeSpec, Azure SDK, and API guidelines.
            Pass in a `question` to get an AI-generated response with references.
            Optionally specify `sources` to limit which documentation sources to search. Valid sources include:
            - 'typespec_docs': TypeSpec documentation
            - 'typespec_azure_docs': TypeSpec Azure documentation  
            - 'azure_rest_api_specs_wiki': Azure REST API specifications wiki
            - 'azure_sdk_for_python_docs': Azure SDK for Python documentation
            - 'azure_api_guidelines': Azure API guidelines
            - 'azure_resource_manager_rpc': Azure Resource Manager RPC documentation
            - 'azure_sdk_guidelines': Azure SDK guidelines
            - 'typespec_azure_http_specs': TypeSpec Azure HTTP specifications
            - 'typespec_http_specs': TypeSpec HTTP specifications
            Optionally include `conversationHistory` for context-aware responses.
            Optionally set `includeFullContext` to get the full search context used.
            Optionally specify `topK` to control how many documents to search (default: 10).
            Returns an answer with supporting references and documentation links.")]
        public async Task<AiCompletionToolResponse> QueryAzureSDKDocumentation(
            [Description("The question to ask the AI agent")]
            string question,
            [Description("List of documentation sources to search (optional)")]
            List<string>? sources = null,
            [Description("Previous conversation messages for context (optional)")]
            List<MessageInput>? conversationHistory = null,
            [Description("Whether to include full search context in the response")]
            bool includeFullContext = false,
            [Description("Number of documents to search (1-100, default: 10)")]
            int topK = 10,
            CancellationToken ct = default)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(question))
                {
                    SetFailure();
                    return new AiCompletionToolResponse
                    {
                        ResponseError = "Question cannot be empty"
                    };
                }

                if (topK < 1 || topK > 100)
                {
                    SetFailure();
                    return new AiCompletionToolResponse
                    {
                        ResponseError = "TopK must be between 1 and 100"
                    };
                }

                _logger.LogInformation("Querying AI agent with question: {Question}", question);

                // Build request
                var request = new CompletionRequest
                {
                    TenantId = "azure_sdk_qa_bot",
                    Message = new Message
                    {
                        Role = Role.User,
                        Content = question
                    },
                    WithFullContext = includeFullContext,
                    TopK = topK
                };

                // Map sources if provided
                if (sources?.Any() == true)
                {
                    request.Sources = MapSources(sources);
                    if (request.Sources.Count == 0)
                    {
                        _logger.LogWarning("No valid sources found in the provided list");
                    }
                    else
                    {
                        _logger.LogDebug("Using sources: {Sources}", string.Join(", ", sources.Where(s => request.Sources.Any(rs => rs.ToString().Contains(s)))));
                    }
                }

                // Map conversation history if provided
                if (conversationHistory?.Any() == true)
                {
                    request.History = MapHistory(conversationHistory);
                    _logger.LogDebug("Including {Count} messages in conversation history", conversationHistory.Count);
                }

                // Call the service
                var response = await _aiCompletionService.SendCompletionRequestAsync(
                    request, null, null, ct);

                _logger.LogInformation("Received response with ID: {Id}, HasResult: {HasResult}",
                    response.Id, response.HasResult);

                return new AiCompletionToolResponse
                {
                    IsSuccessful = response.HasResult,
                    Answer = response.Answer,
                    References = MapReferences(response.References),
                    FullContext = response.FullContext,
                    ReasoningProgress = response.ReasoningProgress,
                    QueryIntension = response.Intension != null ? new QueryIntension
                    {
                        Question = response.Intension.Question,
                        Category = response.Intension.Category,
                        SpecType = response.Intension.SpecType,
                        Scope = response.Intension.Scope?.ToString()
                    } : null
                };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogWarning("AI query was cancelled");
                SetFailure();
                return new AiCompletionToolResponse
                {
                    ResponseError = "Query was cancelled"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying AI agent");
                SetFailure();
                return new AiCompletionToolResponse
                {
                    ResponseError = $"Failed to query AI agent: {ex.Message}"
                };
            }
        }

        [McpServerTool(Name = "azsdk_ai_qa_completion_with_context")]
        [Description(@"Query the Azure SDK QA Bot with additional context like links or images.
            Use this when you need to provide specific documentation links or code snippets as context.
            Pass in `additionalContext` as a list of links or images that should be considered.
            All other parameters are the same as the main query tool.")]
        public async Task<AiCompletionToolResponse> QueryWithAdditionalContext(
            [Description("The question to ask the AI agent")]
            string question,
            [Description("Additional context items (links or images) to include")]
            List<AdditionalContextInput> additionalContext,
            [Description("List of documentation sources to search (optional)")]
            List<string>? sources = null,
            [Description("Previous conversation messages for context (optional)")]
            List<MessageInput>? conversationHistory = null,
            [Description("Whether to include full search context in the response")]
            bool includeFullContext = false,
            [Description("Number of documents to search (1-100, default: 10)")]
            int topK = 10,
            CancellationToken ct = default)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(question))
                {
                    SetFailure();
                    return new AiCompletionToolResponse
                    {
                        ResponseError = "Question cannot be empty"
                    };
                }

                if (additionalContext == null || !additionalContext.Any())
                {
                    SetFailure();
                    return new AiCompletionToolResponse
                    {
                        ResponseError = "Additional context is required for this method"
                    };
                }

                _logger.LogInformation("Querying AI agent with question and {Count} additional context items",
                    additionalContext.Count);

                // Build request
                var request = new CompletionRequest
                {
                    TenantId = "azure_sdk_qa_bot",
                    Message = new Message
                    {
                        Role = Role.User,
                        Content = question
                    },
                    WithFullContext = includeFullContext,
                    TopK = topK
                };

                // Map additional context
                request.AdditionalInfos = additionalContext.Select(ac => new AdditionalInfo
                {
                    Type = ac.Type?.ToLowerInvariant() == "image" ? AdditionalInfoType.Image : AdditionalInfoType.Link,
                    Content = ac.Content ?? string.Empty,
                    Link = ac.Link ?? string.Empty
                }).ToList();

                // Map sources if provided
                if (sources?.Any() == true)
                {
                    request.Sources = MapSources(sources);
                }

                // Map conversation history if provided
                if (conversationHistory?.Any() == true)
                {
                    request.History = MapHistory(conversationHistory);
                }

                // Call the service
                var response = await _aiCompletionService.SendCompletionRequestAsync(
                    request, null, null, ct);

                return new AiCompletionToolResponse
                {
                    IsSuccessful = response.HasResult,
                    Answer = response.Answer,
                    References = MapReferences(response.References),
                    FullContext = response.FullContext,
                    ReasoningProgress = response.ReasoningProgress,
                    QueryIntension = response.Intension != null ? new QueryIntension
                    {
                        Question = response.Intension.Question,
                        Category = response.Intension.Category,
                        SpecType = response.Intension.SpecType,
                        Scope = response.Intension.Scope?.ToString()
                    } : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying AI agent with additional context");
                SetFailure();
                return new AiCompletionToolResponse
                {
                    ResponseError = $"Failed to query AI agent: {ex.Message}"
                };
            }
        }

        private List<Source> MapSources(List<string> sources)
        {
            var mapped = new List<Source>();
            var sourceMapping = new Dictionary<string, Source>(StringComparer.OrdinalIgnoreCase)
            {
                ["typespec_docs"] = Source.TypeSpec,
                ["typespec_azure_docs"] = Source.TypeSpecAzure,
                ["azure_rest_api_specs_wiki"] = Source.AzureRestAPISpec,
                ["azure_sdk_for_python_docs"] = Source.AzureSDKForPython,
                ["azure_sdk_for_python_wiki"] = Source.AzureSDKForPythonWiki,
                ["static_typespec_qa"] = Source.TypeSpecQA,
                ["azure_api_guidelines"] = Source.AzureAPIGuidelines,
                ["azure_resource_manager_rpc"] = Source.AzureResourceManagerRPC,
                ["static_typespec_migration_docs"] = Source.TypeSpecMigration,
                ["azure-sdk-docs-eng"] = Source.AzureSDKDocsEng,
                ["azure-sdk-guidelines"] = Source.AzureSDKGuidelines,
                ["typespec_azure_http_specs"] = Source.TypeSpecAzureHttpSpecs,
                ["typespec_http_specs"] = Source.TypeSpecHttpSpecs
            };

            foreach (var source in sources)
            {
                if (sourceMapping.TryGetValue(source, out var enumSource))
                {
                    mapped.Add(enumSource);
                }
                else
                {
                    _logger.LogWarning("Unknown source: {Source}", source);
                }
            }

            return mapped;
        }

        private List<Message> MapHistory(List<MessageInput> history)
        {
            return history.Select(h => new Message
            {
                Role = h.Role?.ToLowerInvariant() switch
                {
                    "user" => Role.User,
                    "assistant" => Role.Assistant,
                    "system" => Role.System,
                    _ => Role.User
                },
                Content = h.Content ?? string.Empty
            }).ToList();
        }

        private List<DocumentReference> MapReferences(List<Reference>? references)
        {
            if (references == null)
            {
                return new();
            }

            return references.Select(r => new DocumentReference
            {
                Title = r.Title,
                Source = r.Source,
                Link = r.Link,
                Snippet = r.Content.Length > 500
                    ? r.Content.Substring(0, 497) + "..."
                    : r.Content
            }).ToList();
        }
    }

    // Input models for the MCP tool
    public class MessageInput
    {
        [Description("The role of the message (user, assistant, or system)")]
        public string Role { get; set; } = "user";

        [Description("The content of the message")]
        [Required]
        public string Content { get; set; } = string.Empty;
    }

    public class AdditionalContextInput
    {
        [Description("The type of context (link or image)")]
        public string Type { get; set; } = "link";

        [Description("Description or content of the context item")]
        [Required]
        public string Content { get; set; } = string.Empty;

        [Description("URL link to the context item")]
        [Required]
        [Url]
        public string Link { get; set; } = string.Empty;
    }

    // Response models for the MCP tool
    public class AiCompletionToolResponse : Azure.Sdk.Tools.Cli.Models.Response
    {
        [JsonPropertyName("is_successful")]
        public bool IsSuccessful { get; set; }

        [JsonPropertyName("answer")]
        public string Answer { get; set; } = string.Empty;

        [JsonPropertyName("references")]
        public List<DocumentReference> References { get; set; } = new();

        [JsonPropertyName("full_context")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FullContext { get; set; }

        [JsonPropertyName("reasoning_progress")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ReasoningProgress { get; set; }

        [JsonPropertyName("query_intension")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public QueryIntension? QueryIntension { get; set; }

        public override string ToString()
        {
            if (!IsSuccessful || !string.IsNullOrEmpty(ResponseError))
            {
                return ToString(string.Empty);
            }

            var result = new StringBuilder();
            result.AppendLine($"**Answer:** {Answer}");

            if (References.Any())
            {
                result.AppendLine("\n**References:**");
                foreach (var reference in References)
                {
                    result.AppendLine($"- **{reference.Title}** ({reference.Source})");
                    result.AppendLine($"  {reference.Link}");
                    if (!string.IsNullOrEmpty(reference.Snippet))
                    {
                        result.AppendLine($"  Snippet: {reference.Snippet}");
                    }
                    result.AppendLine();
                }
            }

            if (QueryIntension != null)
            {
                result.AppendLine($"\n**Query Analysis:**");
                result.AppendLine($"- Category: {QueryIntension.Category}");
                if (!string.IsNullOrEmpty(QueryIntension.SpecType))
                {
                    result.AppendLine($"- Spec Type: {QueryIntension.SpecType}");
                }
                if (!string.IsNullOrEmpty(QueryIntension.Scope))
                {
                    result.AppendLine($"- Scope: {QueryIntension.Scope}");
                }
            }

            return result.ToString();
        }
    }

    public class DocumentReference
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("link")]
        public string Link { get; set; } = string.Empty;

        [JsonPropertyName("snippet")]
        public string Snippet { get; set; } = string.Empty;
    }

    public class QueryIntension
    {
        [JsonPropertyName("question")]
        public string Question { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("spec_type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SpecType { get; set; }

        [JsonPropertyName("scope")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Scope { get; set; }
    }
}
