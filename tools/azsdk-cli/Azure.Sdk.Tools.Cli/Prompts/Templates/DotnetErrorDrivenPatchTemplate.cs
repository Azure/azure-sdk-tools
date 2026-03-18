// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Error-driven template for .NET customization patching.
/// Grounded in Azure SDK for .NET partial-class customization patterns, it applies safe structural patches based on build errors.
/// </summary>
public class DotnetErrorDrivenPatchTemplate(
    string buildContext,
    string packagePath,
    string customizationRoot,
    List<string> customizationFiles,
    List<string> patchFilePaths) : BasePromptTemplate
{
    public override string TemplateId => "dotnet-error-driven-patch";
    public override string Version => "1.0.0";
    public override string Description =>
        "Analyze C# build errors and apply safe structural patches to customization files";

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
        You are a worker for the .NET Azure SDK customization repair pipeline.
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

        ## .NET CUSTOMIZATION PATTERN
        In azure-sdk-for-net, generated code lives in a `Generated/` folder under `src/`.
        Customizations are **partial classes** defined in `.cs` files OUTSIDE the `Generated/` folder.
        These partial classes extend the generated types to add or override behavior.

        Common customization patterns:
        - Partial class extending a generated model or client class
        - Adding convenience methods or properties
        - Overriding serialization behavior
        - Adding custom constructors or factory methods

        When generated code changes (e.g. a property is renamed, a method signature changes),
        the customization partial classes may reference stale names and fail to compile.

        ## TOOLS & FILE PATHS
        The following tools are available. They use DIFFERENT base directories:

        **GrepSearch** — resolves paths relative to the package path: `{{packagePath}}`
        - Search for text patterns in files without reading entire files.
        - Returns matching lines with file paths and line numbers.
        - Use `path: "."` to search across all files, or a specific relative path.

        **ReadFile** — resolves paths relative to the package path: `{{packagePath}}`
        - Generated code: `src/Generated/<ClassName>.cs`
        - Customization code: `src/<ClassName>.cs` or `src/Customized/<ClassName>.cs`
        - Supports `startLine`/`endLine` parameters to read specific sections.

        **CodePatchTool** — resolves paths relative to the customization root: `{{customizationRoot}}`
        - Use ONLY the filename or path relative to the customization root.
        - Example: if customization root is `.../src`, use just `WidgetClientExtensions.cs`
          or `Customized/WidgetClient.cs`.

        **RenameFile** — resolves paths relative to the customization root: `{{customizationRoot}}`
        - Renames a customization file. Use when a class has been renamed and the file name
          should match the new class name.
        - Parameters: `oldFilePath` (current relative path), `newFilePath` (new relative path).
        - Example: `RenameFile oldFilePath: "OldClient.cs" newFilePath: "NewClient.cs"`

        ## WORKFLOW

        ### Step 1 — Parse errors
        - Read the Original Request and Classifier Analysis in the BUILD CONTEXT above.
          These tell you WHAT was changed (e.g., a property was renamed) and WHY the build broke.
        - Extract each compiler error: failing symbol, file, line, error code (e.g. CS0117, CS0246).

        ### Step 2 — Find and read relevant code
        - **Use GrepSearch first** to find the failing symbol (e.g., the old property name) in
          the customization files. This tells you exactly which lines reference it.
        - Then use **ReadFile with startLine/endLine** to read ~20 lines around each match
          to understand the surrounding context.
        - Customization code uses partial classes that reference members from generated types.
          When a build error appears in a customization file, the root cause is usually a
          reference to a renamed or removed member from the generated type.
        - Also read the generated file(s) referenced in errors (in the `Generated/` folder)
          to confirm current property names, method signatures, and type definitions.

        ### Step 3 — Apply safe patches
        Apply patches ONLY when you can determine the CORRECT value with certainty:
        - Property was renamed → update all references in partial class
        - Method signature changed → update method calls with new parameters
        - Type was renamed → update type references and using directives
        - Namespace changed → update using statements
        - Method was removed → update to use the replacement method if one exists

        ### Step 3b — Rename files when classes are renamed
        When a class or type is renamed and you patch the partial class declaration to use
        the new class name, you MUST ALSO rename the customization file to match the new class name.
        - In .NET, the convention is that the file name matches the class name (e.g., `WidgetClient.cs`
          contains `partial class WidgetClient`).
        - Use the **RenameFile** tool to rename the file AFTER applying the code patch.
        - Example: if you rename `partial class OldClient` to `partial class NewClient`,
          also call `RenameFile oldFilePath: "OldClient.cs" newFilePath: "NewClient.cs"`.
        - **CRITICAL**: When renaming a class, ONLY patch the class declaration line itself
          (e.g., `partial class OldName` → `partial class NewName`). Do NOT modify, delete, or
          rewrite any method bodies, properties, or other members inside the class. Errors about
          missing members (CS0103) are cascading errors that resolve once the class name matches
          the generated partial class again.

        ### Step 4 — Return summary
        If you applied patches, return a brief summary of what was fixed.
        If no patches could be applied, return empty string.

        """;
    }

    private string BuildTaskConstraints()
    {
        return """

        ### 1. CUSTOMIZATION FILES ONLY
        You may patch ONLY the customization files provided. Never patch generated code in the `Generated/` folder.

        ### 2. SAFE PATCHES ONLY
        Apply patches only when the fix is obvious and correct.
        **DO NOT**:
        - Pass `null` for a new parameter whose correct value is unknown
        - Remove or comment out method calls to suppress errors
        - Add placeholder/dummy values
        - Guess at correct values
        - Remove `partial` keyword or change class hierarchy
        - Delete or rewrite existing method bodies

        ### 3. SURGICAL PATCHING
        The CodePatchTool uses **surgical text replacement**:
        - `StartLine`/`EndLine`: The line range containing the text to modify
        - `OldText`: The EXACT text fragment to find (can span multiple lines)
        - `NewText`: The replacement text
        - `PatchDescription`: A brief human-readable summary of the change
          (e.g., "Renamed MaxSpeakers to MaxSpeakerCount in partial class extension")

        **Example**: To rename a property reference in a partial class:
        ```
        StartLine: 15
        EndLine: 15
        OldText: "response.Value.OldPropertyName"
        NewText: "response.Value.NewPropertyName"
        PatchDescription: "Updated property reference from OldPropertyName to NewPropertyName"
        ```

        This surgically replaces ONLY that text, preserving all surrounding syntax.

        **IMPORTANT**: Never replace large blocks of code (e.g., entire method bodies or whole file content).
        Each patch should target the smallest possible text fragment. If you need to rename a class,
        patch ONLY the class declaration line — do NOT touch method bodies inside the class.

        ### 4. GREP FIRST, READ RANGES, THEN PATCH
        - Use GrepSearch to locate the failing symbol in customization files.
        - Use ReadFile with startLine/endLine to read context around the matches.
        - Use the line numbers from ReadFile output for StartLine/EndLine in patches.
        - For OldText, copy the EXACT text from the file (you can span multiple lines).

        ### 5. NO DUPLICATE PATCHES
        - Each OldText can only be replaced once per file.
        - If a patch is rejected, STOP and return.

        ### 6. IGNORE CASCADING ERRORS FROM CLASS RENAMES
        When a partial class name no longer matches its generated counterpart, the compiler will report
        multiple cascading errors for members that actually exist on the generated type (e.g., CS0103
        for `ClientDiagnostics`, method names, properties). These errors are **NOT real** — they will
        resolve automatically once the partial class name is corrected to match the generated class.
        **DO NOT** try to fix these cascading errors individually (e.g., by removing method calls,
        rewriting method bodies, or adding new members). Just rename the class declaration and the file.

        ### 7. IGNORE ANALYZER RULES
        Ignore analyzer warnings and errors with codes like `AZC0007`, `SA1517`, or any `AZC*`/`SA*`
        prefixed codes. These are style/design analyzers, not compilation errors. Do NOT add constructors,
        reformat code, or make any changes to satisfy analyzer rules.

        """;
    }

    private string BuildOutputRequirements()
    {
        return """
        Return a brief summary of what you did:
        - If patches were applied: describe each fix
        - If no patches could be applied: return empty string ""

        Keep it concise.
        """;
    }

}
