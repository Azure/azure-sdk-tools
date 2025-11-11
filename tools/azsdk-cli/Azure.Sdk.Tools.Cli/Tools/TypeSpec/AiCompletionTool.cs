using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json.Serialization;
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
        private readonly Argument<string> _questionArgument = new("question") {
            Description = "The question to ask the AI agent"
        };
        public AiCompletionTool(
            IAiCompletionService aiCompletionService,
            ILogger<AiCompletionTool> logger)
        {
            _aiCompletionService = aiCompletionService ?? throw new ArgumentNullException(nameof(aiCompletionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override Command GetCommand()
        {
            var command = new Command("ai-completion", "Query the Azure SDK QA Bot AI agent for answers about TypeSpec, Azure SDK, and API guidelines")
            {
                _questionArgument
            };

            return command;
        }

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var question = parseResult.GetValue(_questionArgument);

            if (string.IsNullOrWhiteSpace(question))
            {
                _logger.LogError("Question cannot be empty");
                return new DefaultCommandResponse() { ResponseError = "Question cannot be empty" };
            }

            try
            {
                _logger.LogInformation("Querying AI agent via CLI with question: {Question}", question);

                var response = await QueryAzureSDKDocumentation(
                  question,
                  ct: ct
                );

                if (!response.IsSuccessful)
                {
                    _logger.LogError("AI query failed: {Error}", response.ResponseError);
                    return new DefaultCommandResponse() { ResponseError = $"AI query failed: {response.ResponseError}" };
                }
                _logger.LogDebug("AI response: {Response}", response.ToString());
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query AI agent via CLI");
                return new DefaultCommandResponse() { ResponseError = $"Failed to query AI agent: {ex.Message}" };
            }
        }

        [McpServerTool(Name = "azsdk_ai_qa_completion")]
        [Description(@"Query the Azure SDK QA Bot AI agent for answers about TypeSpec, Azure SDK, and API guidelines.
            Pass in a `question` to get an AI-generated response with references.
            Returns an answer with supporting references and documentation links.")]
        public async Task<AiCompletionToolResponse> QueryAzureSDKDocumentation(
            [Description("The question to ask the AI agent")]
            string question,
            CancellationToken ct = default)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(question))
                {
                    return new AiCompletionToolResponse
                    {
                        ResponseError = "Question cannot be empty"
                    };
                }

                _logger.LogInformation("Querying AI agent with question: {Question}", question);

                // Build request
                var request = new CompletionRequest
                {
                    TenantId = TenantId.AzureTypespecAuthoring,
                    Message = new Message
                    {
                        Role = Role.User,
                        Content = question
                    }
                };

                // Call the service
                var response = await _aiCompletionService.SendCompletionRequestAsync(
                    request, ct);

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
                return new AiCompletionToolResponse
                {
                    ResponseError = "Query was cancelled"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying AI agent");
                return new AiCompletionToolResponse
                {
                    ResponseError = $"Failed to query AI agent: {ex.Message}"
                };
            }
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

    // Response models for the MCP tool
    public class AiCompletionToolResponse : CommandResponse
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

        protected override string Format()
        {
            if (!IsSuccessful || !string.IsNullOrEmpty(ResponseError))
            {
                return string.Empty;
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
