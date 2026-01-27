// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Error-driven template for Java customization patch generation.
/// Scope: deterministic, mechanical fixes only (remove duplicates, update references).
/// </summary>
public class JavaErrorDrivenPatchTemplate(
    string buildError,
    string packagePath,
    string customizationContent,
    string customizationRoot,
    List<string> customizationFiles) : BasePromptTemplate
{
    public override string TemplateId => "java-error-driven-patch";
    public override string Version => "1.0.0";
    public override string Description => "Analyze build error and apply targeted fix to Java customization code";

    public override string BuildPrompt() =>
        BuildStructuredPrompt(BuildTaskInstructions(), BuildTaskConstraints(), BuildExamples(), BuildOutputRequirements());

    private string BuildTaskInstructions()
    {
        var fileList = string.Join("\n", customizationFiles.Select(f => $"  - {f}"));
        
        return $"""
        ## BUILD ERROR TO FIX
        ```
        {buildError}
        ```
        
        ## YOUR TASK
        Analyze the build error above and apply a targeted fix to the customization code.
        
        ## SCOPE LIMITATIONS
        You may ONLY perform these types of fixes:
        - Remove duplicate field/method definitions
        - Update references after TypeSpec renames (e.g., method name changes)
        - Add missing import statements
        - Fix reserved keyword conflicts
        
        You may NOT:
        - Add new methods or functionality
        - Restructure code architecture
        - Change method visibility (use TypeSpec @access instead)
        - Add error handling or new logic
        
        ## FILE STRUCTURE
        - Package Path (ReadFile base): {packagePath}
        - Customization Root: {customizationRoot}
        - Customization Files:
        {fileList}
        
        ## CUSTOMIZATION CODE (to fix):
        ```java
        {customizationContent}
        ```
        
        ## WORKFLOW
        1. **Parse the error**: Identify the exact issue (duplicate field, undefined reference, etc.)
        2. **Investigate the cause**: 
           - Use ReadFile to examine the generated code referenced in the error
           - Look for what the NEW name/field/method is (TypeSpec may have renamed it)
           - Compare with what the customization is looking for
        3. **Locate the fix**: Find the exact string in customization that needs updating
        4. **Apply fix**: Use ClientCustomizationCodePatch tool with exact replacement
        5. **Return result**: true if fix applied, false if unable to fix
        
        ## KEY INSIGHT: TypeSpec Renames
        When you see "cannot find symbol" or "method not found" errors:
        - The TypeSpec may have renamed the property/method
        - Use ReadFile to look at the GENERATED code to discover the NEW name
        - Example: If customization looks for `setUrlSource` but error says not found,
          read the generated file to see if there's a `setSourceUrl` method instead
        - Then update the customization to use the new name
        """;
    }

    private static string BuildTaskConstraints()
    {
        return """
        ## CRITICAL CONSTRAINTS
        
        **IMPORTANT: You can ONLY patch customization files**
        The ClientCustomizationCodePatch tool ONLY works on files in the customization directory.
        You CANNOT patch files in src/main/java/... - the tool will fail with "Old content not found".
        
        If the build error is in generated code (src/main/java/...), you must:
        1. Find the ROOT CAUSE in the customization file
        2. Fix the customization so that regeneration produces correct code
        3. If there's no customization cause → Return false (regeneration issue)
        
        **Scope Check - Before attempting ANY fix, verify:**
        - Is the fix to a CUSTOMIZATION file? (not generated code)
        - Is this a mechanical fix? (removing duplicate, updating reference, adding import)
        - Is this <20 lines of change?
        - Am I removing/updating code, NOT adding new logic?
        
        If ANY answer is NO → Return false immediately. Do not attempt the fix.
        
        **Safety Guidelines:**
        - Use exact string matching for replacements
        - Include enough context to uniquely identify the location
        - Preserve code formatting and indentation
        - If uncertain about the fix → Return false (let human review)
        - After applying a successful patch, EXIT - do not try more patches
        
        **Discovery Process for Renames:**
        When error says "cannot find symbol" or "method not found":
        1. READ the generated file mentioned in the error using ReadFile tool
        2. SEARCH for similar method/field names to find what it was renamed TO
        3. UPDATE the customization to use the new name (NOT the generated file!)
        
        **Common Patterns:**
        
        1. DUPLICATE FIELD: Error says "variable X is already defined"
           → Remove the duplicate definition from customization file
           
        2. METHOD/FIELD NOT FOUND: Error says "cannot find symbol" or method not found
           → Use ReadFile to look at generated code and find the NEW name
           → Update customization to reference the new name
           
        3. MISSING IMPORT: Error says "cannot find symbol" for a type
           → Add the missing import statement to customization
           
        4. ERROR IN GENERATED CODE WITH NO CUSTOMIZATION CAUSE
           → Return false immediately - this is a regeneration/emitter issue
        """;
    }

    private static string BuildExamples()
    {
        return """
        ## EXAMPLE SCENARIOS
        
        **Example 1: Duplicate Field**
        Build error: `[ERROR] DocumentIntelligenceCustomizations.java:[178,20] variable operationId is already defined`
        
        Analysis: TypeSpec now generates `operationId`, but customization also adds it.
        Fix: Remove the duplicate field injection from customization.
        
        ```
        ClientCustomizationCodePatch(
            FilePath: 'DocumentIntelligenceCustomizations.java',
            OldContent: 'clazz.addField("String", "operationId", Modifier.Keyword.PRIVATE);',
            NewContent: '// Removed: operationId now generated by TypeSpec'
        )
        ```
        
        **Example 2: TypeSpec Rename - Method Name Changed**
        Build error: `[ERROR] ... Expected method setUrlSource not found`
        
        Step 1 - Investigate: ReadFile("src/main/java/com/azure/ai/.../AnalyzeDocumentOptions.java")
        Discovery: File shows `setSourceUrl` method exists (not `setUrlSource`)
        
        Step 2 - Understand: TypeSpec renamed `urlSource` → `sourceUrl`, so method is now `setSourceUrl`
        
        Step 3 - Fix: Update customization to use new method name
        
        ```
        ClientCustomizationCodePatch(
            FilePath: 'DocumentIntelligenceCustomizations.java',
            OldContent: 'clazz.getMethodsByName("setUrlSource")',
            NewContent: 'clazz.getMethodsByName("setSourceUrl")'
        )
        ```
        
        **Example 3: Out of Scope - Error in Generated Code, No Customization Cause**
        Build error: `[ERROR] SomeGeneratedFile.java:[100,5] incompatible types`
        
        Analysis: Error is in generated code (src/main/java/...) and examining the customization
        shows no code that could cause this error.
        
        Action: Return false IMMEDIATELY - do not try to patch the generated file!
        The ClientCustomizationCodePatch tool CANNOT modify generated files.
        This is a regeneration/emitter issue.
        
        **Example 4: Multiple Errors - Fix What You Can, Then Exit**
        Build errors show:
        1. Customization error: method name reference wrong
        2. Generated code error: internal inconsistency in generated file
        
        Action: 
        1. Fix the customization error with ClientCustomizationCodePatch
        2. After successful patch, EXIT with true
        3. DO NOT try to fix the generated code error - let regeneration handle it
        """;
    }

    private static string BuildOutputRequirements()
    {
        return """
        ## TOOL USAGE
        
        **ReadFile tool:**
        - Use to examine generated code if needed to understand the change
        - Relative paths from package path: `src/main/java/com/azure/...`
        
        **ClientCustomizationCodePatch tool:**
        - FilePath: relative to customization root
        - OldContent: exact text to replace (include context for uniqueness)
        - NewContent: replacement text
        
        ## DECISION FLOW
        
        1. Can I identify the exact error location? 
           NO → Return false
           
        2. Is the fix mechanical (remove/update, not add)?
           NO → Return false
           
        3. Am I confident the fix is correct?
           NO → Return false
           
        4. Apply the fix with ClientCustomizationCodePatch
        
        5. Return true if patch succeeded, false otherwise
        
        ## FINAL RESULT
        - Return true: Fix was applied successfully
        - Return false: Unable to fix (out of scope, uncertain, or error applying patch)
        
        It is BETTER to return false than to apply an incorrect fix.
        """;
    }
}
