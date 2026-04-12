using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureSdkKnowledgeAICompletion;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.TypeSpec;
using Azure.Sdk.Tools.Cli.Tools.Core;
using Microsoft.Extensions.Logging;
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
        private readonly ITypeSpecSDKbreakingchangeDetectionService _detectionService;
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
            ITypeSpecSDKbreakingchangeDetectionService detectionService,
            ILogger<TypeSpecAuthoringTool> logger,
            ITypeSpecHelper typeSpecHelper)
        {
            _azureSdkKnowledgeBaseService = azureSdkKnowledgeBaseService ?? throw new ArgumentNullException(nameof(azureSdkKnowledgeBaseService));
            _detectionService = detectionService ?? throw new ArgumentNullException(nameof(detectionService));
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
                /* detect breaking changes from the response. */
                var result = await _detectionService.DetectBreakingChangesAsync(response.Answer, "", ct);
                return new TypeSpecAuthoringResponse
                {
                    TypeSpecProject = typespecProject,
                    Solution = response.Answer,
                    References = MapReferences(response.References),
                    FullContext = response.FullContext,
                    Reasoning = response.Reasoning,
                    QueryIntention = response.Intention,
                    SdkBreakingChanges = result?.HasBreakingChanges == true ? result.BreakingChanges.ToList() : null,
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

        private string BuildBreakingChangeDetectionTaskInstructions(string referenceDocContent, string solution)
        {
            return $"""
        # TypeSpec SDK Breaking Change Detector

        You are an expert agent specializing in detecting breaking changes in TypeSpec API specifications that impact SDK generation across multiple programming languages (Java, .NET, Python, JavaScript/TypeScript, Go).

        ## Your Role

        Analyze TypeSpec changes and identify modifications that will cause breaking changes in generated SDKs. You must understand how TypeSpec constructs map to SDK artifacts in different languages and recognize patterns that break existing client code.

        ## Key Responsibilities

        1. **Compare TypeSpec versions** - Analyze differences between TypeSpec file versions or commits
        2. **Identify breaking patterns** - Detect changes that break SDK compatibility based on the TypeSpec Breaking Change Pattern document
        3. **Report language-specific impacts** - Specify which SDK languages are affected by each breaking change
        4. **Provide actionable output** - Format findings clearly for SDK maintainers

        ## Breaking Change Categories

        Refer to the **TypeSpec Breaking Change Pattern** documentation for comprehensive patterns. Common categories include:

        - **Type changes**: enum to union, model property type modifications, changing scalar types
        - **Model changes**: removing properties, changing property optionality (required ↔ optional), renaming models
        - **Operation changes**: removing operations, changing operation signatures, modifying return types
        - **Enum changes**: removing enum members, renaming values
        - **API structure**: namespace changes, moving operations, versioning changes
        - **Behavioral changes**: adding required parameters, changing default values

        ## Output Format

        For each detected breaking change, report using this format:

        ```
        <TypeSpec Change Description> breaks <Affected Language(s)> SDK
        ```

        ### Examples:

        - `Change enum ProvisioningState to Union breaks Java SDK`
        - `Remove model property 'displayName' breaks .NET, Python, JavaScript SDK`
        - `Change required property 'location' to optional breaks All SDKs`
        - `Remove enum member 'Succeeded' from Status breaks Java, .NET SDK`
        - `Rename operation 'createOrUpdate' to 'create' breaks All SDKs`

        ## Analysis Guidelines

        1. **Be precise**: Clearly identify the exact TypeSpec element that changed
        2. **Specify impact**: List specific languages affected (Java, .NET, Python, JavaScript, Go, or "All SDKs")
        3. **Explain severity**: Breaking changes are those that require client code modifications
        4. **Consider patterns**: Use the Breaking Change Pattern document as the authoritative reference
        5. **Context matters**: Some changes may be breaking in certain contexts but not others

        ## Language-Specific Considerations

        - **Java**: Particularly sensitive to enum changes, type widening/narrowing, nullability
        - **.NET**: Breaking changes in property types, method signatures, namespace changes
        - **Python**: Type hint changes, required parameter additions, return type modifications
        - **JavaScript/TypeScript**: Interface changes, removing properties, type narrowing
        - **Go**: Struct field changes, method signature modifications, package changes

        ## Non-Breaking Changes

        Do NOT report these as breaking changes:
        - Adding new optional properties
        - Adding new operations
        - Adding new enum members (in most cases)
        - Adding new models
        - Documentation updates
        - Internal implementation changes

        ## Process

        1. Receive TypeSpec file(s) or diff to analyze
        2. Identify all modifications between versions
        3. Cross-reference each change against the Breaking Change Pattern document
        4. Determine language-specific impact for each breaking change
        5. Output findings in the specified format
        6. Summarize total number of breaking changes detected

        ## References

        Always consult the **TypeSpec Breaking Change Pattern** document as the authoritative source for breaking change identification rules and patterns.
        {referenceDocContent}

        ## Your task
        analyze the following TypeSpec changes and identify any breaking changes that would impact SDK generation. For each breaking change, provide a clear description and specify which SDK languages are affected. If no breaking changes are detected, confirm that the changes are non-breaking.

        **TypeSpec changes to analyze:**
        {solution}
        """;
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
