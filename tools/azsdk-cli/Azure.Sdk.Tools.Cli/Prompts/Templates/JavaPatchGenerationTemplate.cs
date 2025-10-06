// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Prompts;

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Template for Java customization patch generation prompts.
/// This template guides AI to analyze API changes and create patches for customization code.
/// </summary>
public class JavaPatchGenerationTemplate : BasePromptTemplate
{
    public override string TemplateId => "java-patch-generation";
    public override string Version => "1.0.0";
    public override string Description => "Find API changes and create patches for customization code";

    private readonly string _oldGeneratedCode;
    private readonly string _newGeneratedCode;
    private readonly string _customizationContent;
    private readonly string _customizationRoot;
    private readonly string _commitSha;

    /// <summary>
    /// Initializes a new Java patch generation template with the specified parameters.
    /// </summary>
    /// <param name="oldGeneratedCode">The previous version of generated code</param>
    /// <param name="newGeneratedCode">The new version of generated code</param>
    /// <param name="customizationContent">The customization code that needs updates</param>
    /// <param name="customizationRoot">Root path for customization files</param>
    /// <param name="commitSha">The commit SHA from TypeSpec changes</param>
    public JavaPatchGenerationTemplate(
        string oldGeneratedCode,
        string newGeneratedCode, 
        string customizationContent,
        string customizationRoot,
        string commitSha)
    {
        _oldGeneratedCode = oldGeneratedCode;
        _newGeneratedCode = newGeneratedCode;
        _customizationContent = customizationContent;
        _customizationRoot = customizationRoot;
        _commitSha = commitSha ?? string.Empty;
    }

    /// <summary>
    /// Builds the complete Java patch generation prompt using the configured parameters.
    /// </summary>
    /// <returns>Complete structured prompt for patch generation</returns>
    public string BuildPrompt()
    {
        var taskInstructions = BuildTaskInstructions();
        var constraints = BuildTaskConstraints();
        var examples = BuildExamples();
        var outputRequirements = BuildOutputRequirements();

        return BuildStructuredPrompt(taskInstructions, constraints, examples, outputRequirements);
    }

    private string BuildTaskInstructions()
    {
        var tspChangeContext = !string.IsNullOrEmpty(_commitSha) 
            ? $"## TSP CHANGE CONTEXT:\nTypeSpec changes: https://github.com/Azure/azure-rest-api-specs/commit/{_commitSha}\n**Chain Reasoning**: Can trace through TSP change → generated code → customization impact\n\n" 
            : "";

        return $"""
        {tspChangeContext}TypeSpec generated new Java client code. You need to update customization files to match the new API.
        
        ## OLD Generated Code:
        ```java
        {_oldGeneratedCode}
        ```
        
        ## NEW Generated Code:
        ```java
        {_newGeneratedCode}
        ```
        
        ## Customization Code (needs updates):
        ```java
        {_customizationContent}
        ```
        
        ## What you need to do:
        1. **Compare OLD vs NEW**: Find what changed (method names, parameter names, class names)
        2. **Find in customization**: Look for the OLD names in the customization code
        3. **Create patches**: For each OLD name found, create a patch to update it to the NEW name
        4. **Reference TSP context**: Use the commit link above to understand the root cause of changes
        
        ## Common changes to look for:
        - Parameter name changes: `getParameterByName("oldName")` → `getParameterByName("newName")`
        - Method name changes: `oldMethodName()` → `newMethodName()`
        - Class name changes: `OldClassName` → `NewClassName`
        - Import statement changes: `import com.azure.OldClass` → `import com.azure.NewClass`
        """;
    }

    private string BuildTaskConstraints()
    {
        return """
        **CRITICAL Requirements:**
        - Only create patches for things ACTUALLY found in the customization code
        - Use EXACT file paths with double backslashes for Windows
        - Include enough context in OldContent/NewContent to uniquely identify the location
        - If no changes are needed, return empty array: Exit({"Result": []})
        - The OLD and NEW code ARE different - you MUST find what changed!
        
        **Safety Guidelines:**
        - Do not modify import statements unless the class name actually changed
        - Ensure patches maintain code functionality and correctness
        - Verify that replacement strings are exact matches to avoid unintended changes
        """;
    }

    private string BuildExamples()
    {
        var sampleFilePath = Path.Combine(_customizationRoot, "DocumentIntelligenceCustomizations.java").Replace("\\", "\\\\");
        
        return $$"""
        **Example Scenario:**
        If OLD code had `beginAnalyzeDocument(analyzeRequest)` and NEW code has `beginAnalyzeDocument(analyzeDocumentRequest)`,
        and customization has `getParameterByName("analyzeRequest")`, create this patch:
        
        ```json
        {
          "FilePath": "{{sampleFilePath}}",
          "Description": "Update parameter name from analyzeRequest to analyzeDocumentRequest",
          "OldContent": "getParameterByName(\"analyzeRequest\")",
          "NewContent": "getParameterByName(\"analyzeDocumentRequest\")"
        }
        ```
        
        **Response Process:**
        1. First, tell me what differences you found between OLD and NEW code
        2. Then, tell me which of those differences exist in the customization code  
        3. Finally, call Exit({"Result": [array of patches]})
        """;
    }

    private string BuildOutputRequirements()
    {
        return """
        **Exit Format:**
        ```json
        {"Result": [{"FilePath": "full\\\\path", "Description": "what changed", "OldContent": "exact old text", "NewContent": "exact new text"}]}
        ```
        
        **Output Structure:**
        - FilePath: Complete absolute path with double backslashes for Windows
        - Description: Clear explanation of what is being changed
        - OldContent: Exact text to be replaced (with sufficient context)
        - NewContent: Exact replacement text (with sufficient context)
        
        **Quality Standards:**
        - Each patch must be precise and unambiguous
        - Include surrounding code context to ensure unique matching
        - Validate that all identified changes are actually present in customization code
        """;
    }
}