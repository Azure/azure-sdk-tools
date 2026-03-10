// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Error-driven template for Python customization patching.
/// Targets _patch.py files that override generated code via the __all__ export list.
/// </summary>
public class PythonErrorDrivenPatchTemplate(
    string buildContext,
    string packagePath,
    string customizationRoot,
    List<string> readFilePaths,
    List<string> patchFilePaths) : BasePromptTemplate
{
    public override string TemplateId => "python-error-driven-patch";
    public override string Version => "1.0.0";
    public override string Description =>
        "Analyze pylint/mypy errors and apply safe patches to Python _patch.py customization files";

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
        You are a worker for the Python Azure SDK customization repair pipeline.
        Your job is to apply SAFE patches to fix pylint/mypy errors in `_patch.py` customization files.

        Python SDKs use `_patch.py` files to override generated classes and functions. Any symbol
        listed in `__all__` in a `_patch.py` replaces the corresponding generated symbol at import time.

        ## LINT/TYPE ERRORS
        ```
        {{buildContext}}
        ```

        ## INPUT STRUCTURE
        - Package root: `{{packagePath}}`
        - Customization root: `{{customizationRoot}}`
        - _patch.py files to inspect:
        {{readFileList}}
        {{patchFileList}}

        ## TOOLS & FILE PATHS
        Three tools are available. They use DIFFERENT base directories:

        **GrepSearch** — resolves paths relative to the package root: `{{packagePath}}`
        - Search for text patterns across files without reading them in full.
        - Returns matching lines with file paths and line numbers.
        - Use `path: "."` to search all files, or a specific relative path to narrow scope.

        **ReadFile** — resolves paths relative to the package root: `{{packagePath}}`
        - Generated code: e.g. `azure/ai/<package>/models/_models.py`
        - Customization code: use the ReadFile paths listed above.
        - Supports `startLine`/`endLine` to read specific sections of large files.

        **CodePatchTool** — resolves paths relative to the customization root: `{{customizationRoot}}`
        - Use ONLY the CodePatchTool paths listed above.

        ## WORKFLOW

        ### Step 1 — Parse errors
        Extract each pylint/mypy error: file path, line, error code, message.

        ### Step 2 — Find and read relevant code
        - **Use GrepSearch first** to find the failing symbol (e.g., the old name) across the
          customization files. This tells you exactly which lines reference it.
        - Then use **ReadFile with startLine/endLine** to read ~20 lines around each match
          to understand the surrounding context.
        - Also read the generated file(s) referenced in errors to confirm current signatures/names.

        ### Step 3 — Apply safe patches
        Apply patches only when the correct fix is certain.

        After determining the fix for each error, fix ALL occurrences of the affected symbol across
        the file — imports, `__all__`, type annotations, and any other uses. Do not assume pylint
        will report every affected line; cascading errors may be hidden until earlier ones are resolved.

        ### Step 4 — Return summary
        Briefly describe each fix applied, or return empty string if none.
        """;
    }

    private string BuildTaskConstraints() => """
        ## CONSTRAINTS

        ### 1. _patch.py FILES ONLY
        Patch ONLY the `_patch.py` files provided. Never modify generated files.

        ### 2. SAFE PATCHES ONLY
        **DO NOT**:
        - Add `# type: ignore` or `# noqa` to suppress errors (unless it's the only safe option)
        - Remove symbols from `__all__` just to silence errors
        - Introduce placeholder values or bare `...` bodies for missing implementations

        ### 3. SURGICAL PATCHING — MINIMUM SCOPE
        The `CodePatchTool` uses exact text replacement:
        - `StartLine`/`EndLine`: line range of the segment to modify (use the narrowest range that contains OldText)
        - `OldText`: The **minimum** text that uniquely identifies what to replace. Target just the specific token, name, or expression being changed — do NOT copy whole lines or blocks when only a symbol needs renaming.
        - `NewText`: Only the replacement for exactly what OldText contains — nothing more.

        **IMPORTANT**: The line number prefix shown by ReadFile (e.g. `5: `) is a display artifact — it is NOT part of the file content. Do NOT include it in `OldText` or `NewText`.

        **Example**: To rename `NumberField` to `NumField` on line 7:
          - `StartLine=7`, `EndLine=7`, `OldText="NumberField"`, `NewText="NumField"` ✓
          - NOT: `OldText="7: x = NumberField(...)"`, `NewText="7: x = NumField(...)"` ✗

        If `CodePatchTool` reports "OldText not found", re-read the file to get the exact current content and retry with the correct text.
        If `CodePatchTool` reports "multiple matches", narrow the `StartLine`/`EndLine` range or add one line of surrounding context to `OldText`.

        ### 4. READ FIRST, PATCH SECOND
        Always read the file with `ReadFile` (which returns line numbers) before patching for every patch applied.
        """;

    private string BuildOutputRequirements() => """
        ## OUTPUT
        - If patches were applied: describe each fix concisely
        - If no patches could be applied: return empty string ""
        """;
}
