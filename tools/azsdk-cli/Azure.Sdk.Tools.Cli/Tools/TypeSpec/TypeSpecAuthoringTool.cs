using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureSdkKnowledgeAICompletion;
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

        private readonly IAzureSdkKnowledgeBaseService _azureSdkKnowledgeBaseService;
        private readonly ILogger<TypeSpecAuthoringTool> _logger;
        private readonly ITypeSpecHelper _typeSpecHelper;
        /* the maximum number of characters allowed in a reference snippet */
        private const int MaxReferenceContentLength = 500;
        private const string TypeSpecAuthoringToolCommandName = "generate-authoring-plan";

        private readonly Option<string> _requestOption = new("--request")
        {
            Description = "The TypeSpec‑related task or user request sent to an AI agent to produce a proposed solution or execution plan with references.",
            Required = true,
        };

        private readonly Option<string> _additionalInformationOption = new("--additional-information")
        {
            Description = "The additional information to consider for the TypeSpec project.",
            Required = false,
        };

        private readonly Option<string> _typeSpecProjectPathOption = new("--typespec-project")
        {
            Description = "The root path of the TypeSpec project",
            Required = false,
        };

        public TypeSpecAuthoringTool(
            IAzureSdkKnowledgeBaseService azureSdkKnowledgeBaseService,
            ILogger<TypeSpecAuthoringTool> logger,
            ITypeSpecHelper typeSpecHelper)
        {
            _azureSdkKnowledgeBaseService = azureSdkKnowledgeBaseService ?? throw new ArgumentNullException(nameof(azureSdkKnowledgeBaseService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _typeSpecHelper = typeSpecHelper ?? throw new ArgumentNullException(nameof(typeSpecHelper));
        }

        protected override Command GetCommand()
        {
            var command = new Command(TypeSpecAuthoringToolCommandName, "Generate a solution or execution plan for defining and updating a TypeSpec-based API specification for an Azure service.")
            {
                _requestOption,
                _additionalInformationOption,
                _typeSpecProjectPathOption,
            };

            return command;
        }

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var request = parseResult.GetValue(_requestOption);
            var additionalInformation = parseResult.GetValue(_additionalInformationOption);
            var typespecProjectRootPath = parseResult.GetValue(_typeSpecProjectPathOption);

            if (string.IsNullOrWhiteSpace(request))
            {
                _logger.LogError("Request cannot be empty");
                return new DefaultCommandResponse() { ResponseError = "Request cannot be empty" };
            }

            try
            {
                _logger.LogInformation("Authoring with question: {Request}", request);

                var response = await GenerateTypeSpecAuthoringPlan(
                  request,
                  additionalInformation,
                  typespecProjectRootPath,
                  ct: ct
                );

                if (response.OperationStatus == Status.Failed || !string.IsNullOrEmpty(response.ResponseError))
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


        [McpServerTool(Name = "azsdk_typespec_generate_authoring_plan")]
        [Description(@"Generate solutions or execution plans for TypeSpec‑related tasks, such as defining and updating TypeSpec‑based API specifications for an Azure service.
This tool applies to all tasks involving **TypeSpec**:
- Writing new TypeSpec definitions: service, api version, resource, models, operations
- Editing or refactoring existing TypeSpec files, editing api version, service, resource, models, operations, or properties.
- Versioning evolution:
  - Make a **preview** API **stable (GA)**.
  - Replace an existing **preview** with a **new preview**.
  - Replace a **preview** with a **stable**
  - Replacing a preview API with a stable API and a new preview API.
  - **Add** a preview or **add** a stable API version.
- Resolving TypeSpec-related issues
Pass in a `request` to get an AI-generated response with references.
Returns an answer with supporting references and documentation links
")]
        public async Task<TypeSpecAuthoringResponse> GenerateTypeSpecAuthoringPlan(
            [Description("The request to ask the AI agent")]
            string request,
            [Description("Additional information to consider for the TypeSpec project")]
            string additionalInformation = null,
            [Description("The root path of the TypeSpec project")]
            string typeSpecProjectRootPath = null,
            CancellationToken ct = default)
        {
            var typespecProject = _typeSpecHelper.GetTypeSpecProjectRelativePath(typeSpecProjectRootPath);
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(request))
                {
                    return new TypeSpecAuthoringResponse
                    {
                        TypeSpecProject = typespecProject,
                        ResponseError = "Request cannot be empty"
                    };
                }

                _logger.LogInformation("Authoring with request: {Request}", request);

                // Build request
                var completionRequest = new CompletionRequest
                {
                    AzureSdkKnowledgeServiceTenant = AzureSdkKnowledgeServiceTenant.AzureTypespecAuthoring,
                    Message = new Message
                    {
                        Role = Role.User,
                        Content = request,
                    },
                    WithAgenticSearch = false, // For authoring, disable agentic search
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
                var response = await _azureSdkKnowledgeBaseService.SendCompletionRequestAsync(
                    completionRequest, ct);

                _logger.LogInformation("Received response with ID: {Id}, HasResult: {HasResult}",
                    response.Id, response.HasResult);

                if (!response.HasResult)
                {
                    return new TypeSpecAuthoringResponse
                    {
                        TypeSpecProject = typespecProject,
                        ResponseError = "Did not receive a result from knowledge base service."
                    };
                }
                return new TypeSpecAuthoringResponse
                {
                    TypeSpecProject = typespecProject,
                    Solution = response.Answer,
                    References = MapReferences(response.References),
                    FullContext = response.FullContext,
                    Reasoning = response.Reasoning,
                    QueryIntention = response.Intention,
                };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogWarning("AI query was cancelled");
                return new TypeSpecAuthoringResponse
                {
                    TypeSpecProject = typespecProject,
                    ResponseError = "Query was cancelled"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying AI agent");
                return new TypeSpecAuthoringResponse
                {
                    TypeSpecProject = typespecProject,
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
