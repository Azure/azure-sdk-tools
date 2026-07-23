// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Template for classifying user prompts into Azure SDK workflow categories
/// and extracting metadata while sanitizing PII.
/// </summary>
public class UserPromptClassificationTemplate : BasePromptTemplate
{
    public override string TemplateId => "user-prompt-classification";
    public override string Version => "1.0.0";
    public override string Description => "Classify user prompts into Azure SDK workflow categories and sanitize PII";

    private readonly string _userPrompt;

    public UserPromptClassificationTemplate(string userPrompt)
    {
        _userPrompt = userPrompt;
    }

    public override string BuildPrompt()
    {
        var taskInstructions = BuildTaskInstructions();
        var constraints = BuildConstraints();
        var examples = BuildExamples();
        var outputRequirements = BuildOutputRequirements();

        return BuildStructuredPrompt(taskInstructions, constraints, examples, outputRequirements);
    }

    protected override string BuildSystemRole()
    {
        return """
        ## SYSTEM ROLE
        You are a prompt classifier and PII sanitizer for the Azure SDK engineering system.
        Your task: analyze user prompts to determine their intent category and extract relevant metadata,
        while removing any personally identifiable information.

        ## SAFETY GUIDELINES
        - NEVER include any personally identifiable information (PII) in your output
        - PII includes: names, email addresses, IP addresses, phone numbers, file paths containing usernames,
          Azure subscription IDs, tenant IDs, client secrets, API keys, access tokens, connection strings,
          URLs with embedded credentials, and any other sensitive data
        - Replace PII with generic placeholders (e.g., "[USER]", "[EMAIL]", "[SUBSCRIPTION_ID]")
        - Do not process or expose credentials or secrets
        - Provide accurate classifications based on the prompt content
        """;
    }

    private string BuildTaskInstructions()
    {
        return $"""
        Analyze the following user prompt and perform these tasks:

        1. **Sanitize**: Remove all PII from the prompt and create a brief sanitized summary (max 200 characters)
           that captures the intent without any sensitive information.

        2. **Classify**: Determine which single category best matches the user's intent from the allowed categories.

        3. **Extract Metadata**: If the prompt mentions any of the following, extract them:
           - **language**: The SDK language (e.g., Python, .NET, Java, JavaScript, Go)
           - **package_name**: The SDK package name (e.g., azure-storage-blob, Azure.Storage.Blobs)
           - **typespec_project**: The TypeSpec project path or name

        **User Prompt to Analyze:**
        <prompt>
        {_userPrompt}
        </prompt>
        """;
    }

    private static string BuildConstraints()
    {
        return """
        **Allowed Categories (pick exactly one):**
        - `typespec_authoring_or_update` — User wants to create, author, or update a TypeSpec specification
        - `typespec_customization` — User wants to customize TypeSpec-generated SDK output (client.tsp, decorators, overrides)
        - `typespec_validation` -  user wants to validate or compile TypeSpec
        - `sdk_generation` — User wants to generate SDK code from a TypeSpec specification
        - `sdk_build_and_test` — User wants to build, compile, or run tests for an SDK package
        - `release_planning` — User wants to create or manage a release plan for an SDK package
        - `sdk_release` — User wants to release or publish an SDK package
        - `changelog_and_metadata_update` — User wants to update changelog, version, or package metadata
        - `fix_build_failure` — User is troubleshooting or fixing a build failure or compilation error
        - `analyze_pipeline_error` — User wants to analyze or debug a CI/CD pipeline failure
        - `sdk_validations` — User wants to run or fix SDK validation checks (APIView, breaking changes, linting)
        - `apiview_request` - USer wants to check APIview approval status, get apireview link or get api review comments.

        **Classification Rules:**
        - If the prompt is ambiguous, choose the category that best matches the primary intent
        - If the prompt doesn't clearly match any category, choose the closest match based on keywords and context
        - Consider the full context of the prompt, not just individual keywords
        """;
    }

    private static string BuildExamples()
    {
        return """
        **Example 1:**
        Prompt: "Generate the Python SDK for azure-rest-api-specs/specification/contosowidgetmanager"
        ```json
        {
          "category": "sdk_generation",
          "prompt_summary": "Generate Python SDK for contosowidgetmanager service",
          "language": "Python",
          "package_name": null,
          "typespec_project": "specification/contosowidgetmanager"
        }
        ```

        **Example 2:**
        Prompt: "The CI pipeline for azure-sdk-for-java PR #12345 is failing with a compilation error"
        ```json
        {
          "category": "analyze_pipeline_error",
          "prompt_summary": "CI pipeline failing with compilation error on Java SDK PR",
          "language": "Java",
          "package_name": null,
          "typespec_project": null
        }
        ```

        **Example 3:**
        Prompt: "I need to rename the client in the .NET SDK Azure.ResourceManager.Contoso using @@clientName decorator"
        ```json
        {
          "category": "typespec_customization",
          "prompt_summary": "Rename client in .NET SDK using clientName decorator",
          "language": ".NET",
          "package_name": "Azure.ResourceManager.Contoso",
          "typespec_project": null
        }
        ```
        """;
    }

    private static string BuildOutputRequirements()
    {
        return """
        **CRITICAL: Required Output Format**

        You MUST output a single valid JSON object with exactly these fields:
        ```json
        {
          "category": "<one of the allowed category identifiers>",
          "prompt_summary": "<sanitized summary of the prompt, max 200 chars, NO PII>",
          "language": "<SDK language if mentioned, otherwise null>",
          "package_name": "<package name if mentioned, otherwise null>",
          "typespec_project": "<TypeSpec project path/name if mentioned, otherwise null>"
        }
        ```

        **Rules:**
        - Output ONLY the JSON object, no other text
        - `category` must be exactly one of the allowed category identifiers listed above
        - `prompt_summary` must be a concise, PII-free summary of the user's intent
        - `language`, `package_name`, `typespec_project` should be null if not mentioned in the prompt
        - Do NOT include any PII in any field
        - Do NOT wrap the JSON in code fences or add explanation
        """;
    }
}
