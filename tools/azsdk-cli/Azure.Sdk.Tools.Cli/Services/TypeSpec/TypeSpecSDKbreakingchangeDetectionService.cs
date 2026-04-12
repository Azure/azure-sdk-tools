using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Microsoft.TeamFoundation.TestManagement.WebApi;

namespace Azure.Sdk.Tools.Cli.Services.TypeSpec
{
    public class TypeSpecSDKbreakingchangeDetectionService : ITypeSpecSDKbreakingchangeDetectionService
    {
        private readonly ILogger<TypeSpecSDKbreakingchangeDetectionService> logger;
        private readonly ICopilotAgentRunner copilotAgentRunner;
        public TypeSpecSDKbreakingchangeDetectionService(ILogger<TypeSpecSDKbreakingchangeDetectionService> logger, ICopilotAgentRunner copilotAgentRunner)
        {
            this.logger = logger;
            this.copilotAgentRunner = copilotAgentRunner;
        }
        public async Task<SDKBreakingChangeDetectionResponse> DetectBreakingChangesAsync(string typespecChanges, string? referenceDocPath = null, CancellationToken ct = default)
        {
            // Placeholder implementation - in a real implementation, this would analyze the TypeSpec changes to detect breaking changes
            logger.LogInformation("Analyzing TypeSpec changes for breaking changes...");
            if (!File.Exists(referenceDocPath))
            {
                throw new FileNotFoundException(
                    $"Reference document not found: {referenceDocPath}", referenceDocPath);
            }
            logger.LogInformation("Using reference doc: {RefDoc}", referenceDocPath);

            // Read the reference doc content
            var referenceDocContent = await File.ReadAllTextAsync(referenceDocPath, ct);
            var agent = new CopilotAgent<SDKBreakingChangeDetectionResponse>
            {
                Instructions = this.BuildInstructions(referenceDocContent, typespecChanges),
                //Tools = tools,
                //MaxIterations = maxIterations,
                Model = "claude-opus-4.5"
            };
            var result = await copilotAgentRunner.RunAsync(agent, ct);
            logger.LogInformation("copilot agent completed. hasBreakingChange: {hasBreakingChanges}, Breaking Changes: {breakingChanges}", result.HasBreakingChanges, string.Join("\n", result.BreakingChanges));
            // For demonstration purposes, we'll just return a response indicating no breaking changes were found
            return result;
        }

        private string BuildInstructions(string referenceDocContent, string solution)
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
    }
}
