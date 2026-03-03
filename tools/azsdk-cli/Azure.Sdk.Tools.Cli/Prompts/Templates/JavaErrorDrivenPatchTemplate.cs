// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Error-driven template for Java customization patching.
/// Grounded in autorest.java customization framework capabilities it applies safe structural patches based on build errors.
/// </summary>
public class JavaErrorDrivenPatchTemplate(
    string buildContext,
    string packagePath,
    string customizationRoot,
    List<string> customizationFiles,
    List<string> patchFilePaths) : BasePromptTemplate
{
    public override string TemplateId => "java-error-driven-patch";
    public override string Version => "1.0.0";
    public override string Description =>
        "Analyze build errors and apply safe structural patches to customization files";

    public override string BuildPrompt() =>
        BuildStructuredPrompt(
            BuildTaskInstructions(),
            BuildTaskConstraints(),
            BuildOutputRequirements());

    private string BuildTaskInstructions()
    {
        var readFileList = string.Join("\n", customizationFiles.Select(f => $"  - ReadFile path: `{f}`"));
        var patchFileList = string.Join("\n", patchFilePaths.Select(f => $"  - PatchTool path: `{f}`"));

        return $$"""
        ## CONTEXT
        You are a worker for the Java Azure SDK customization repair pipeline.
        Your job is to apply SAFE structural patches to fix build errors in customization files.

        ## BUILD CONTEXT
        ```
        {{buildContext}}
        ```

        ## INPUT STRUCTURE
        - Package path: {{packagePath}}
        - Customization root: {{customizationRoot}}
        - Customization files:
        {{readFileList}}
        {{patchFileList}}

        ## TOOLS & FILE PATHS
        Two tools are available. They use DIFFERENT base directories:

        **ReadFile** — resolves paths relative to the package path: `{{packagePath}}`
        - Generated code: `src/main/java/com/azure/.../<ClassName>.java`
        - Customization code: `customization/src/main/java/.../<ClassName>.java`

        **ClientCustomizationCodePatch** — resolves paths relative to the customization root: `{{customizationRoot}}`
        - Use ONLY the filename or path relative to the customization root.
        - Example: if customization root is `.../customization/src/main/java`, use just `DocumentIntelligenceCustomizations.java`.

        ## WORKFLOW

        ### Step 1 — Parse errors
        - Extract each compiler error: failing symbol, file, line, error type.
        - Identify the root cause (e.g., method signature changed, parameter added).

        ### Step 2 — Read relevant files
        - Read the customization file(s) referenced in errors.
        - Read the generated file(s) that the build errors reference to see the CURRENT
          method signatures, parameter names, types, and Javadoc. These are READ-ONLY context.
        - Identify what changed (new parameters, removed parameters, renamed methods, etc.).

        ### Step 3 — Determine the correct fix
        For each error, determine the semantically correct fix:

        **Method gained a new parameter:**
        1. Read the generated method signature to identify the new parameter's name and type.
        2. Check if the Options class (e.g., `AnalyzeDocumentOptions`) already has a getter
           for this parameter (look in both the generated code and the customization code).
        3. If no getter exists, add a getter (and setter) to the Options class in the
           customization file. Name it to match the parameter (e.g., parameter `outputFormat`
           → `getOutputFormat()` / `setOutputFormat()`).
        4. In the method body string, pass `options.getOutputFormat()` at the correct
           argument position — NEVER pass `null`.

        **Parameter removed from generated method:**
        - Remove the extra argument from the method body string.
        - Remove the corresponding getter/setter from the Options class customization.

        **Method/type renamed:**
        - Update all references (method calls, type names, imports).

        **Return type changed:**
        - Update the customization to match the new return type.

        ### Step 4 — Apply safe patches
        Apply patches ONLY when you can determine the CORRECT value with certainty.

        ### Step 5 — Return summary
        If you applied patches, return a brief summary of what was fixed.
        If no patches could be applied, return empty string.

        """;
    }

    private string BuildTaskConstraints()
    {
        return """
        ## CONSTRAINTS

        ### 1. CUSTOMIZATION FILES ONLY
        You may patch ONLY the customization files provided. Never patch generated code.

        ### 2. SAFE PATCHES ONLY — SEMANTIC CORRECTNESS
        Apply patches only when the fix is obvious and correct.
        **DO NOT**:
        - Pass `null` for a new parameter — this compiles but silently drops functionality,
          which is WORSE than a build failure. Always thread the actual value via an Options
          class getter (add the getter if it doesn't exist).
        - Remove or comment out method calls to suppress errors
        - Add placeholder/dummy values (empty strings, 0, false, etc.)
        - Guess at correct values without reading the generated source

        **ALWAYS**:
        - Read the generated file to understand what a new parameter does before patching.
        - Match the getter/setter name to the parameter name from the generated method.
        - Place the new argument at the correct position in the method call string.

        ### 3. SURGICAL PATCHING
        The ClientCustomizationCodePatch tool uses **surgical text replacement**:
        - `StartLine`/`EndLine`: The line range containing the text to modify
        - `OldText`: The EXACT text fragment to find (can span multiple lines)
        - `NewText`: The replacement text

        **Example**: To add a parameter to a method call:
        ```
        StartLine: 100
        EndLine: 105
        OldText: "getOutput())); }"
        NewText: "getOutput(), options.getPriority())); }"
        ```

        This surgically replaces ONLY that text, preserving all surrounding syntax.

        ### 4. READ FIRST, PATCH SECOND
        - Always read the customization file and relevant generated file before patching.
        - ReadFile returns line-numbered output. Use these line numbers for StartLine/EndLine.
        - For OldText, copy the EXACT text from the file (you can span multiple lines).

        ### 5. NO DUPLICATE PATCHES
        - Each OldText can only be replaced once per file.
        - If a patch is rejected, STOP and return.

        """;
    }

    private string BuildOutputRequirements()
    {
        return """
        ## OUTPUT
        Return a brief summary of what you did:
        - If patches were applied: describe each fix
        - If no patches could be applied: return empty string ""

        Keep it concise.
        """;
    }
}
