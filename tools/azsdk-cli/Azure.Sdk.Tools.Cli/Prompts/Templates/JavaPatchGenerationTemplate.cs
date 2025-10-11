// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
    private readonly string _packagePath;

    private readonly string _customizationContent;
    private readonly string _customizationRoot;
    private readonly string _commitSha;

    /// <summary>
    /// Initializes a new Java patch generation template with the specified parameters.
    /// </summary>
    /// <param name="oldGeneratedCode">The previous version of generated code</param>
    /// <param name="newGeneratedCode">The new version of generated code</param>
    /// <param name="packagePath">The package path for ReadFile tool base directory</param>
    /// <param name="customizationContent">The customization code that needs updates</param>
    /// <param name="customizationRoot">Root path for customization files</param>
    /// <param name="commitSha">The commit SHA from TypeSpec changes</param>
    public JavaPatchGenerationTemplate(
        string oldGeneratedCode,
        string newGeneratedCode, 
        string packagePath,
        string customizationContent,
        string customizationRoot,
        string commitSha)
    {
        _oldGeneratedCode = oldGeneratedCode;
        _newGeneratedCode = newGeneratedCode;
        _packagePath = packagePath;
        _customizationContent = customizationContent;
        _customizationRoot = customizationRoot;
        _commitSha = commitSha ?? string.Empty;
    }

    /// <summary>
    /// Builds the complete Java patch generation prompt using the configured parameters.
    /// </summary>
    /// <returns>Complete structured prompt for direct patch application using microagent tools</returns>
    public override string BuildPrompt()
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
        
        ## File Structure Context
        - Package Path (ReadFile base): {_packagePath}
        - Customization Root: {_customizationRoot}
        - Generated Source: src/main/java (relative to package path)
        - Customization Source: customization/src/main/java (relative to package path)
        
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
        2. **Use ReadFile tool**: Examine customization files to find OLD names
        3. **Use ClientCustomizationCodePatch tool**: Apply patches to update OLD names to NEW names
        4. **Reference TSP context**: Use the commit link above to understand the root cause of changes
        
        ## Common changes to look for:
        - Parameter name changes: `getParameterByName("oldName")` → `getParameterByName("newName")`
        - Method name changes: `oldMethodName()` → `newMethodName()`
        - Class name changes: `OldClassName` → `NewClassName`
        - Import statement changes: `import com.azure.OldClass` → `import com.azure.NewClass`
        """;
    }

    private static string BuildTaskConstraints()
    {
        return """
        **CRITICAL Requirements:**
        - Only apply patches for things ACTUALLY found in the customization code
        - Use ReadFile tool to examine files before making changes
        - Use ClientCustomizationCodePatch tool with exact OldContent/NewContent
        - Include enough context to uniquely identify the location
        - The OLD and NEW code ARE different - you MUST find what changed!
        
        **Safety Guidelines:**
        - Do not modify import statements unless the class name actually changed
        - Ensure patches maintain code functionality and correctness
        - Verify that replacement strings are exact matches to avoid unintended changes
        - If no changes are needed, return true (successful completion with no patches)
        """;
    }

    private static string BuildExamples()
    {
        return """
        **Example Scenario:**
        If OLD code had `beginAnalyzeDocument(analyzeRequest)` and NEW code has `beginAnalyzeDocument(analyzeDocumentRequest)`,
        and customization has `getParameterByName("analyzeRequest")`, you would:
        
        1. Use ReadFile to examine the customization file
        2. Use ClientCustomizationCodePatch tool like this:
           - FilePath: 'DocumentIntelligenceCustomizations.java'
           - OldContent: 'getParameterByName("analyzeRequest")'
           - NewContent: 'getParameterByName("analyzeDocumentRequest")'
        
        **Response Process:**
        1. First, tell me what differences you found between OLD and NEW code
        2. Then, examine customization files using ReadFile tool
        3. Apply patches using ClientCustomizationCodePatch tool for each change found
        4. Return true if all patches were successfully applied, false if there were issues
        """;
    }

    private static string BuildOutputRequirements()
    {
        return """
        **Tool Usage Instructions:**
        
        When using ReadFile tool:
        - Use relative paths from package path
        - Generated files are in: src/main/java/com/azure/...
        - Customization files are in: customization/src/main/java/
        - Example: ReadFile('customization/src/main/java/DocumentIntelligenceCustomizations.java')
        
        When using ClientCustomizationCodePatch tool:
        - FilePath should be relative to customization root
        - Provide exact OldContent and NewContent for safe replacement
        - Include enough context to uniquely identify the location
        - Example: ClientCustomizationCodePatch(FilePath='DocumentIntelligenceCustomizations.java', OldContent='...', NewContent='...')
        
        **Final Result:**
        - Return true if all patches applied successfully, false if any issues occurred
        - If no changes are needed, return true (no patches required)
        """;
    }
}
