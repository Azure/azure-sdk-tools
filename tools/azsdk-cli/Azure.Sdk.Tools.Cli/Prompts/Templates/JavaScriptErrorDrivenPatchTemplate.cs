// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Error-driven template for JavaScript/TypeScript customization patching.
/// Targets files in the <c>src/</c> directory of packages that use the <c>generated/</c> folder
/// customization pattern. Handles TypeScript compiler errors and merge conflict markers
/// left by <c>dev-tool customization apply</c>.
/// </summary>
public class JavaScriptErrorDrivenPatchTemplate(
    string buildContext,
    string packagePath,
    string customizationRoot,
    List<string> readFilePaths,
    List<string> patchFilePaths) : BasePromptTemplate
{
    public override string TemplateId => "javascript-error-driven-patch";
    public override string Version => "1.0.0";
    public override string Description =>
        "Analyze TypeScript build errors and merge conflicts, then apply safe patches to JavaScript SDK customization files";

    public override string BuildPrompt() =>
        BuildStructuredPrompt(
            BuildTaskInstructions(),
            BuildTaskConstraints(),
            BuildOutputRequirements());

    private string BuildTaskInstructions()
    {
        var readFileList = string.Join("\n", readFilePaths.Select(f => $"  - ReadFile path: `{f}`"));
        var patchFileList = string.Join("\n", patchFilePaths.Select(f => $"  - CodePatchTool path: `{f}`"));

        return $$"""
        ## CONTEXT
        You are a worker for the JavaScript/TypeScript Azure SDK customization repair pipeline.
        Your job is to apply SAFE patches to fix TypeScript build errors and merge conflicts in
        customization files under the `src/` directory.

        JavaScript Azure SDKs use a `generated/` folder pattern for customization:
        - `generated/` contains the latest auto-generated TypeScript code (baseline).
        - `src/` contains the customized version of the code that is built and shipped.
        - After regeneration, `dev-tool customization apply` performs a 3-way merge of new
          generated code with existing customizations. Merge conflicts may remain in `src/`.

        ## BUILD CONTEXT
        ```
        {{buildContext}}
        ```

        ## INPUT STRUCTURE
        - Package root: `{{packagePath}}`
        - Customization root (src/): `{{customizationRoot}}`
        - Customization files to inspect:
        {{readFileList}}
        {{patchFileList}}

        ## TOOLS & FILE PATHS
        Three tools are available. They use DIFFERENT base directories:

        **GrepSearch** — resolves paths relative to the package root: `{{packagePath}}`
        - Search for text patterns across files without reading them in full.
        - Returns matching lines with file paths and line numbers.
        - Use `path: "."` to search all files, or a specific relative path to narrow scope.

        **ReadFile** — resolves paths relative to the package root: `{{packagePath}}`
        - Generated code (baseline): `generated/<filename>.ts`
        - Customized code: `src/<filename>.ts`
        - Supports `startLine`/`endLine` to read specific sections of large files.

        **CodePatchTool** — resolves paths relative to the customization root: `{{customizationRoot}}`
        - Use ONLY the CodePatchTool paths listed above.

        ## WORKFLOW

        ### Step 1 — Identify problem type
        Determine what kind of issues are present:
        - **Merge conflicts**: Look for `<<<<<<<`, `=======`, `>>>>>>>` markers in `src/` files.
        - **TypeScript compiler errors**: Parse `TS2xxx` error codes from the build output.
        - **Both**: Address merge conflicts first, then compiler errors.

        ### Step 2 — Resolve merge conflicts (if any)
        For each file with merge conflict markers:
        1. Use **GrepSearch** to find `<<<<<<<` markers in `src/` files.
        2. Use **ReadFile** with startLine/endLine to read the full conflict block (from `<<<<<<<`
           through `=======` to `>>>>>>>`).
        3. Read the corresponding file in `generated/` to understand the new generated API.
        4. Resolve the conflict by choosing the version that:
           - Uses the new generated API signatures and types (from the `>>>>>>>` / generated side).
           - Preserves the customized behavior and logic (from the `<<<<<<<` / customized side).
           - Removes ALL conflict markers (`<<<<<<<`, `=======`, `>>>>>>>`).
        5. Apply the resolution using **CodePatchTool**.

        ### Step 3 — Fix TypeScript compiler errors
        For each TypeScript error:
        1. Parse the error: file path, line, TS error code, message.
        2. Use **GrepSearch** to find the failing symbol in `src/` files.
        3. Use **ReadFile** to read context around the error (~20 lines).
        4. Read the corresponding `generated/` file to see current signatures/types.
        5. Apply safe patches:
           - Renamed types/interfaces → update references in `src/`.
           - Changed method signatures → update parameter lists.
           - New required parameters → add with proper forwarding.
           - Changed return types → update assignments and type annotations.
           - Removed exports → update imports and re-exports.

        ### Step 4 — Return summary
        Briefly describe each fix applied, or return empty string if none.
        """;
    }

    private string BuildTaskConstraints() => """
        ## CONSTRAINTS

        ### 1. src/ FILES ONLY — ERROR-RELATED LINES ONLY
        Patch ONLY the `src/` files provided. Never modify files in `generated/`.
        Only fix the specific symbols, imports, and references that are **directly named in or related to
        build errors or merge conflicts**. Do NOT reformat, rewrite, or "clean up" any other code —
        even if it looks incorrect or could be improved. Leave all unrelated lines exactly as-is.

        ### 2. SAFE PATCHES ONLY
        **DO NOT**:
        - Add `// @ts-ignore` or `as any` casts to suppress errors.
        - Remove exports or function bodies to silence errors.
        - Introduce placeholder values or stub implementations.
        - Guess at correct types when they cannot be determined from context.

        ### 3. MERGE CONFLICT RESOLUTION
        When resolving merge conflicts:
        - Always remove ALL three conflict markers (`<<<<<<<`, `=======`, `>>>>>>>`).
        - Prefer the new generated API (types, signatures) from the generated side.
        - Preserve customized behavior (logic, helper functions, re-exports) from the customized side.
        - If unsure which side to keep, prefer the generated side and note it in the summary.

        ### 4. SURGICAL PATCHING — MINIMUM SCOPE
        The `CodePatchTool` uses exact text replacement:
        - `StartLine`/`EndLine`: line range of the segment to modify (use the narrowest range).
        - `OldText`: The **minimum** text that uniquely identifies what to replace.
        - `NewText`: Only the replacement for exactly what OldText contains — nothing more.

        **IMPORTANT**: The line number prefix shown by ReadFile (e.g. `5: `) is a display artifact —
        it is NOT part of the file content. Do NOT include it in `OldText` or `NewText`.

        If `CodePatchTool` reports "OldText not found": re-run GrepSearch to check if the symbol is
        still present. If not found, it was already fixed by a previous patch — skip it.

        ### 5. READ FIRST, PATCH SECOND
        Always read the file with `ReadFile` (which returns line numbers) before patching.
        After each patch, use GrepSearch to verify the fix and get fresh line numbers before
        the next patch (line numbers shift after each edit).
        """;

    private string BuildOutputRequirements() => """
        ## OUTPUT
        - If patches were applied: describe each fix concisely
        - If no patches could be applied: return empty string ""
        """;
}
