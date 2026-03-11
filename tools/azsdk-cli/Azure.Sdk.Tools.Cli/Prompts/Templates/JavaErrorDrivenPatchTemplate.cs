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
        var patchFileList = string.Join("\n", patchFilePaths.Select(f => $"  - CodePatchTool path: `{f}`"));

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
        Three tools are available. They use DIFFERENT base directories:

        **GrepSearch** — resolves paths relative to the package path: `{{packagePath}}`
        - Search for text patterns in files without reading entire files.
        - Returns matching lines with file paths and line numbers.
        - Use `path: "."` to search across all files, or a specific relative path.

        **ReadFile** — resolves paths relative to the package path: `{{packagePath}}`
        - Generated code: `src/main/java/com/azure/.../<ClassName>.java`
        - Customization code: `customization/src/main/java/.../<ClassName>.java`
        - Supports `startLine`/`endLine` parameters to read specific sections.

        **CodePatchTool** — resolves paths relative to the customization root: `{{customizationRoot}}`
        - Use ONLY the filename or path relative to the customization root.
        - Example: if customization root is `.../customization/src/main/java`, use just `DocumentIntelligenceCustomizations.java`.

        ## WORKFLOW

        ### Step 1 — Parse errors
        - Read the Original Request and Classifier Analysis in the BUILD CONTEXT above.
          These tell you WHAT was changed (e.g., a field was renamed) and WHY the build broke.
        - Extract each compiler error: failing symbol, file, line, error type.

        ### Step 2 — Find and read relevant code
        - **Use GrepSearch first** to find the failing symbol (e.g., the old field name) in
          the customization files. This tells you exactly which lines reference it.
        - Then use **ReadFile with startLine/endLine** to read ~20 lines around each match
          to understand the surrounding context.
        - Customization code often injects method bodies into generated files via
          `customizeAst()`/`parseBlock()`. When a build error appears in a generated file,
          the root cause is usually a string literal inside the customization file that
          references a renamed or removed symbol.
        - Also read the generated file(s) referenced in errors (use line ranges around the
          error lines) to confirm current field names and method signatures.

        ### Step 3 — Apply safe patches
        Apply patches ONLY when you can determine the CORRECT value with certainty:
        - Method gained a new parameter → add the parameter with proper forwarding
        - Method was renamed → update the method call
        - Return type changed → update the cast/assignment
        - Field renamed → update `this.oldName` references in string literals passed to
          `parseBlock()` / `parseStatement()` to use `this.newName`.

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
        The CodePatchTool uses **surgical text replacement**:
        - `StartLine`/`EndLine`: The line range containing the text to modify
        - `OldText`: The EXACT text fragment to find (can span multiple lines)
        - `NewText`: The replacement text
        - `PatchDescription`: A brief human-readable summary of the change
          (e.g., "Renamed maxSpeakers to maxSpeakerCount in javadoc comment")

        **Example**: To rename a field reference in a method call:
        ```
        StartLine: 100
        EndLine: 105
        OldText: "getOutput())); }"
        NewText: "getOutput(), options.getPriority())); }"
        PatchDescription: "Added priority parameter to getOutput() call"
        ```

        This surgically replaces ONLY that text, preserving all surrounding syntax.

        ### 4. GREP FIRST, READ RANGES, THEN PATCH
        - Use GrepSearch to locate the failing symbol in customization files.
        - Use ReadFile with startLine/endLine to read context around the matches.
        - Use the line numbers from ReadFile output for StartLine/EndLine in patches.
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
