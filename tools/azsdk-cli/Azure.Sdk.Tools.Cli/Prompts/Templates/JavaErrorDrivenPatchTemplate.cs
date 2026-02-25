// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Error-driven template for Java customization patching.
/// Grounded in autorest.java customization framework capabilities it applies safe structural patches based on build errors.
/// </summary>
public class JavaErrorDrivenPatchTemplate(
    string buildError,
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

        ## BUILD ERRORS
        ```
        {{buildError}}
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
        - Read the generated file(s) to confirm current method signatures.
        - Identify what changed (new parameters, renamed methods, etc.).

        ### Step 3 — Apply safe patches
        Apply patches ONLY when you can determine the CORRECT value with certainty:
        - Method gained a new parameter → add the parameter with proper forwarding
        - Method was renamed → update the method call
        - Return type changed → update the cast/assignment

        ### Step 4 — Return summary
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

        ### 2. SAFE PATCHES ONLY
        Apply patches only when the fix is obvious and correct.
        **DO NOT**:
        - Pass `null` for a new parameter whose correct value is unknown
        - Remove or comment out method calls to suppress errors
        - Add placeholder/dummy values
        - Guess at correct values

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
