// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Template for README generation prompts.
/// This template guides AI to generate README files following Azure SDK standards with safety guidelines.
/// </summary>
public class ReadMeGenerationTemplate : BasePromptTemplate
{
    public override string TemplateId => "readme-generation";
    public override string Version => "1.0.0";
    public override string Description => "Generate README files for Azure SDK packages following established standards";

    protected override IEnumerable<string> RequiredParameters => new[] 
    { 
        "template_content", 
        "service_documentation", 
        "package_path" 
    };

    protected override IEnumerable<string> OptionalParameters => new[] 
    { 
        "additional_rules" 
    };

    protected override string BuildTaskInstructions(IPromptContext context)
    {
        var templateContent = context.GetParameter<string>("template_content") ?? "";
        var serviceDocumentation = context.GetParameter<string>("service_documentation") ?? "";
        var packagePath = context.GetParameter<string>("package_path") ?? "";

        return $"""
        We're going to create README files for Azure SDK packages.

        **Parameters:**
        * Service documentation URL: {serviceDocumentation}
        * Package path for documentation links: {packagePath}

        **Template to fill in:**
        ```
        {templateContent}
        ```

        **Your Tasks:**
        1. Use the service documentation URL to create key concepts and introduction content
        2. Use the package path to generate proper documentation links
        3. Fill in all template placeholders with appropriate content
        4. Follow Azure SDK README standards and guidelines
        5. Call the check_readme_tool with the readme contents
        6. Follow any returned suggestions until no further improvements are needed
        7. Provide the final README content
        """;
    }

    protected override string BuildTaskConstraints(IPromptContext context)
    {
        var additionalRules = context.GetParameter<string>("additional_rules") ?? "";

        var constraints = """
        **CRITICAL README Rules:**
        - Do NOT touch the following sections or their subsections: Contributing
        - Do NOT generate sample code - leave sample code sections for manual completion
        - Follow the official README template: https://github.com/Azure/azure-sdk/blob/main/docs/policies/README-TEMPLATE.md
        - Preserve all existing formatting and structure
        - Only fill in template placeholders, don't modify the template structure
        - Use the service documentation for conceptual content, not implementation details

        **Content Guidelines:**
        - Write clear, concise descriptions
        - Focus on what the service does, not how to implement it
        - Use consistent terminology with Azure documentation
        - Ensure all links are valid and properly formatted
        - Include appropriate disclaimers and legal notices
        """;

        if (!string.IsNullOrEmpty(additionalRules))
        {
            constraints += $"\n\n**Additional Rules:**\n{additionalRules}";
        }

        return constraints;
    }

    protected override string BuildOutputRequirements(IPromptContext context)
    {
        return """
        **Output Process:**
        1. First, call check_readme_tool with your initial README content
        2. Address any suggestions or issues returned by the tool
        3. Continue iterating until check_readme_tool returns no further suggestions
        4. Finally, provide the complete, validated README content

        **Final Output Format:**
        - Return only the final README content as plain text
        - Ensure all template placeholders are properly filled
        - Verify all links and references are correct
        - Confirm the content follows Azure SDK standards
        """;
    }

    protected override string BuildExamples(IPromptContext context)
    {
        return """
        **Example Template Placeholder Replacement:**
        
        **Before:**
        ```
        # Azure {service-name} client library for .NET
        {service-description}
        ```
        
        **After:**
        ```
        # Azure Document Intelligence client library for .NET
        Azure Document Intelligence is a cloud service that uses machine learning to analyze text and structured data from documents.
        ```
        
        **Example Documentation Link:**
        ```
        [API reference documentation](https://docs.microsoft.com/dotnet/api/azure.ai.documentintelligence)
        ```
        """;
    }
}
