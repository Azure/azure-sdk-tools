using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AiCompletion;
using Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.TypeSpec
{
    /// <summary>
    /// Generate a solution or execution plan to define and update TypeSpec-based API specifications for Azure services.
    /// It connects to an AI agent that can answer questions about TypeSpec, Azure SDK guidelines, and API best practices.
    /// </summary>
    [McpServerToolType, Description("This type contains the tool to generate solutions or execution plans for TypeSpec‑related tasks, such as defining and updating TypeSpec‑based API specifications for an Azure service.")]
    public class TypeSpecAuthoringTool : MCPTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.TypeSpec];

        private readonly IAzureKnowledgeBaseService _azureKnowledgeBaseService;
        private readonly ILogger<TypeSpecAuthoringTool> _logger;
        /* the maximum number of characters allowed in a reference snippet */
        private const int MaxReferenceContentLength = 500;
        private const string TypeSpecAuthoringToolCommandName = "retrieve-solution";

        private readonly Option<string> _requestOption = new("--request")
        {
            Description = "The request to authoring",
            Required = true,
        };

        private readonly Option<string> _additionalInformationOption = new("--additional-information")
        {
            Description = "The additional information to consider for the TypeSpec project.",
            Required = false,
        };
        public TypeSpecAuthoringTool(
            IAzureKnowledgeBaseService azureKnowledgeBaseService,
            ILogger<TypeSpecAuthoringTool> logger)
        {
            _azureKnowledgeBaseService = azureKnowledgeBaseService ?? throw new ArgumentNullException(nameof(azureKnowledgeBaseService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override Command GetCommand()
        {
            var command = new Command(TypeSpecAuthoringToolCommandName, "Guide the user to define and update TypeSpec based API spec for an azure service.")
            {
                _requestOption,
                _additionalInformationOption
            };

            return command;
        }

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var request = parseResult.GetValue(_requestOption);

            if (string.IsNullOrWhiteSpace(request))
            {
                _logger.LogError("Request cannot be empty");
                return new DefaultCommandResponse() { ResponseError = "Request cannot be empty" };
            }

            try
            {
                _logger.LogInformation("Authoring with question: {Request}", request);

                var response = await AuthoringWithAzureSDKDocumentation(
                  request,
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


        [McpServerTool(Name = "azsdk_typespec_retrieve_solution")]
        [Description(@"Generate solutions or execution plans for TypeSpec‑related tasks, such as defining and updating TypeSpec‑based API specifications for an Azure service.
Pass in a `request` to get an AI-generated response with references.
Returns an answer with supporting references and documentation links
")]
        public async Task<TypeSpecAuthoringResponse> AuthoringWithAzureSDKDocumentation(
            [Description("The request to ask the AI agent")]
            string request,
            [Description("Additional information to consider for the TypeSpec project")]
            string additionalInformation = null,
            CancellationToken ct = default)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(request))
                {
                    return new TypeSpecAuthoringResponse
                    {
                        ResponseError = "Request cannot be empty"
                    };
                }

                _logger.LogInformation("Authoring with request: {Request}", request);

                // Build request
                var completionRequest = new CompletionRequest
                {
                    TenantId = TenantId.AzureTypespecAuthoring,
                    Message = new Message
                    {
                        Role = Role.User,
                        Content = request,
                    }
                };

                if (!string.IsNullOrWhiteSpace(additionalInformation))
                {
                    completionRequest.AdditionalInfos.Add(new AdditionalInfo
                    {
                        Type = AdditionalInfoType.Text,
                        Content = additionalInformation
                    });
                }

                // Call the service
                var response = await _azureKnowledgeBaseService.SendCompletionRequestAsync(
                    completionRequest, ct);

                _logger.LogInformation("Received response with ID: {Id}, HasResult: {HasResult}",
                    response.Id, response.HasResult);

                return new TypeSpecAuthoringResponse
                {
                    IsSuccessful = response.HasResult,
                    Solution = response.Answer,
                    References = MapReferences(response.References),
                    FullContext = response.FullContext,
                    ReasoningProgress = response.ReasoningProgress,
                    QueryIntention = response.Intention != null ? new QueryIntention
                    {
                        Question = response.Intention.Question,
                        Category = response.Intention.Category,
                        SpecType = response.Intention.SpecType,
                        Scope = response.Intention.Scope?.ToString()
                    } : null
                };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogWarning("AI query was cancelled");
                return new TypeSpecAuthoringResponse
                {
                    ResponseError = "Query was cancelled"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying AI agent");
                return new TypeSpecAuthoringResponse
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
                /* truncate snippet if too long */
                Snippet = r.Content.Length > MaxReferenceContentLength
                    ? r.Content.Substring(0, MaxReferenceContentLength - 3) + "..."
                    : r.Content
            }).ToList();
        }
    }
}
