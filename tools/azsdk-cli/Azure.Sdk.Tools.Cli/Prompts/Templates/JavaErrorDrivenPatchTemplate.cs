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
    string customizationRoot,
    List<string> customizationFiles,
    string? documentationContent = null,
    string? documentationFallback = null) : BasePromptTemplate
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
        
        ## JAVA CUSTOMIZATION CONTEXT
        Java SDK customizations use the autorest customization framework.
        {BuildDocumentationSection()}
        
        ## SCOPE LIMITATIONS
        You may ONLY perform these types of fixes:
        - Remove duplicate field/method definitions (customization adds something TypeSpec now generates)
        - Update method/class name references after TypeSpec renames
        - Fix references to methods that no longer exist or were renamed
        - Add missing import statements
        
        You may NOT:
        - Add new customization logic
        - Restructure the customization architecture
        - Add error handling or new features
        
        ## FILE STRUCTURE
        - Package Path (ReadFile base): {packagePath}
        - Customization Root: {customizationRoot}
        - Customization Files (use ReadFile to examine):
        {fileList}
        
        ## WORKFLOW
        1. **Parse the error**: Identify the exact issue from the build error
        2. **Read relevant files**: Use ReadFile to examine:
           - The customization file(s) that might need fixing
           - The generated code referenced in the error (to find new names after renames)
        3. **Locate the fix**: Find the exact string in customization that needs updating
        4. **Apply fix**: Use ClientCustomizationCodePatch tool with exact replacement
        5. **Return result**: true if fix applied, false if unable to fix
        
        ## KEY INSIGHT: TypeSpec Renames
        When you see "cannot find symbol" or "method not found" errors:
        - The TypeSpec may have renamed the property/method
        - The customization references old names like `getMethodsByName("setUrlSource")`
        - Use ReadFile to look at the GENERATED code to discover the NEW name
        - Then update the customization to use the new method name
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
        
        **CRITICAL: STOP AFTER SUCCESS**
        - After a ClientCustomizationCodePatch returns Success=true, IMMEDIATELY return true
        - DO NOT try additional patches - you will corrupt the file
        - If a patch fails with "Old content not found", the file may have changed - read it again or stop
        
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
        
        **Example 1: Customization References Renamed Method**
        Build error: `[ERROR] ... cannot find symbol ... method getMethodsByName("setUrlSource")`
        
        Step 1 - Read customization: ReadFile("customization/src/main/java/.../MyCustomizations.java")
        Discovery: Customization has `clazz.getMethodsByName("setUrlSource")` 
        
        Step 2 - Read generated code: ReadFile("src/main/java/com/azure/.../AnalyzeDocumentOptions.java")
        Discovery: File shows `setSourceUrl` method exists (TypeSpec renamed urlSource → sourceUrl)
        
        Step 3 - Fix customization to use new method name:
        ```
        ClientCustomizationCodePatch(
            FilePath: 'MyCustomizations.java',
            OldContent: 'clazz.getMethodsByName("setUrlSource")',
            NewContent: 'clazz.getMethodsByName("setSourceUrl")'
        )
        ```
        
        **Example 2: Customization Adds Field That's Now Generated**
        Build error: `[ERROR] ... variable operationId is already defined`
        
        Analysis: TypeSpec now generates `operationId`, but customization also adds it via
        `clazz.addField(...)` or similar. Remove the duplicate from customization.
        
        ```
        ClientCustomizationCodePatch(
            FilePath: 'MyCustomizations.java',
            OldContent: '.addField("operationId"...',
            NewContent: '// Removed: operationId now generated by TypeSpec'
        )
        ```
        
        **Example 3: Error in Generated Code - NOT Fixable**
        Build error: `[ERROR] src/main/java/.../SomeGeneratedFile.java:[100,5] incompatible types`
        
        Analysis: Error is in GENERATED code (src/main/java/...), not customization.
        This tool CANNOT patch generated files - only customization files.
        
        Action: Return false immediately. This is an emitter/regeneration issue.
        
        **Example 4: Customization References Non-Existent Class**
        Build error: `[ERROR] ... cannot find symbol ... class OldClassName`
        
        Step 1 - Read customization to find the reference
        Step 2 - Search generated code to find what the class was renamed to
        Step 3 - Update the class name reference in customization
        """;
    }

    private string BuildDocumentationSection()
    {
        if (string.IsNullOrEmpty(documentationContent))
        {
            // Use provided fallback from language service
            return documentationFallback ?? "";
        }
        
        return $"""
        
        ### AUTOREST JAVA CUSTOMIZATION DOCUMENTATION
        The following is the official documentation for the Java customization framework:
        
        {documentationContent}
        
        ### END OF DOCUMENTATION
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
