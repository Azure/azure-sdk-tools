// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Error-driven template for Java customization patch generation.
/// Grounded in autorest.java customization framework capabilities and client.tsp decorator reference.
/// Issues fixable via client.tsp decorators are redirected there; only structural breakages are patched.
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
    public override string Version => "2.0.0";
    public override string Description => "Analyze build error and apply targeted fix to Java customization code";

    public override string BuildPrompt() =>
        BuildStructuredPrompt(BuildTaskInstructions(), BuildTaskConstraints(), BuildExamples(), BuildOutputRequirements());

    private string BuildTaskInstructions()
    {
        var fileList = string.Join("\n", customizationFiles.Select(f => $"  - {f}"));
        
        return $$"""
        ## BUILD ERROR TO FIX
        ```
        {{buildError}}
        ```
        
        ## YOUR TASK
        Analyze the build error above. Determine whether it can be fixed via TypeSpec
        customization (client.tsp) or requires a code patch to Java customization files.
        
        - If the issue is fixable via client.tsp → Return false with guidance on which
          decorator to use. Do NOT patch the code.
        - If the issue is a structural breakage in customization code that TypeSpec cannot
          express → Apply a targeted code patch.
        
        ## WHAT client.tsp CAN FIX (use decorators, NOT code patches)
        The following issues should ALWAYS be resolved in client.tsp, not by patching code:
        
        | Issue | client.tsp Decorator | Example |
        |-------|---------------------|---------|
        | Type/method/property renamed | `@@clientName` | `@@clientName(NewName, "OldName", "java")` |
        | Operation/type visibility changed | `@@access` | `@@access(MyOp, Access.internal, "java")` |
        | Operation moved to wrong client | `@@clientLocation` | `@@clientLocation(MyService.op, TargetClient)` |
        | Method signature needs simplification | `@@override` | Define custom op, `@@override(original, custom)` |
        | Type in wrong namespace/package | `@@clientNamespace` | `@@clientNamespace(MyType, "new.package")` |
        | Client structure needs reorganization | `@client` / `@operationGroup` | Define new client interfaces |
        | Client initialization params | `@@clientInitialization` | Add params to client constructor |
        | Property type needs to change | `@@alternateType` | `@@alternateType(Foo.date, string, "java")` |
        | Operation excluded/included per language | `@@scope` | `@@scope(Foo.create, "!java")` |
        
        ## WHAT JAVA CODE CUSTOMIZATIONS DO
        Java SDK customizations extend `com.azure.autorest.customization.Customization` and
        override `customize(LibraryCustomization, Logger)`. They use JavaParser to manipulate
        generated code AST. The framework supports:
        
        - Change class/method modifiers (e.g., make a method package-private to hide it)
        - Replace method bodies (e.g., set a convenience overload's delegation logic)
        - Change method return types
        - Change class super types
        - Add/remove annotations (e.g., `@Deprecated`, `@Immutable`)
        - Add fields, getters, setters, methods to generated classes
        - Rename enum members
        - Set javadoc comments
        - Remove files (e.g., remove generated samples replaced by custom ones)
        
        Common pattern: A customization creates a convenience overload that delegates to a
        generated method using `StaticJavaParser.parseBlock(...)` with hardcoded positional
        arguments. Example from DocumentIntelligence SDK:
        ```java
        methodDeclaration.setBody(StaticJavaParser.parseBlock(
            "{ return this.beginAnalyzeDocument(modelId, analyzeDocumentOptions, "
            + "analyzeDocumentOptions.getPages(), analyzeDocumentOptions.getLocale(), "
            + "...); }"));
        ```
        
        When TypeSpec changes the generated method's parameter list, these hardcoded calls break.
        {{BuildDocumentationSection()}}
        
        ## WHAT THIS TOOL CAN FIX (structural breakages only)
        After confirming the issue is NOT fixable via client.tsp, you may patch these:
        
        1. **Positional argument mismatch**: A `StaticJavaParser.parseBlock(...)` body calls
           a generated method with hardcoded args, but the method's parameter list changed
           (parameter added, removed, reordered, or type changed).
           → Read the generated method to find the new signature, update the call site.
        
        2. **Missing getter/setter**: A `StaticJavaParser.parseBlock(...)` body calls
           `options.getSomeField()` but that getter no longer exists because the field was
           removed from the model (not renamed — renames go to client.tsp via `@@clientName`).
           → Remove or replace the reference in the hardcoded body.
        
        3. **Duplicate definition**: Customization adds a field/method via `addField()`/
           `addMethod()` that TypeSpec now generates directly.
           → Remove the duplicate `addField()`/`addMethod()` call from customization.
        
        4. **Missing import in customization file**: A type was moved to a different package.
           → Update the import statement in the customization Java file.
        
        5. **Method lookup failure**: Customization calls `getMethodsByName("oldName")` but
           the generated method was renamed by TypeSpec. This silently produces wrong behavior
           (the convenience wrapper is never created) rather than a compile error.
           → If a method was renamed, fix via client.tsp `@@clientName`, not here.
        
        ## FILE STRUCTURE
        - Package Path: {{packagePath}}
        - Customization Root: {{customizationRoot}}
        
        **For ReadFile tool**: Use paths relative to Package Path (listed below)
        **For ClientCustomizationCodePatch tool**: Use just the filename (e.g., "MyCustomizations.java")
        
        Customization Files (full paths for ReadFile):
        {{fileList}}
        
        ## WORKFLOW
        1. **Parse the error**: Identify the exact class, method, and error type
        2. **Classify — is this a client.tsp issue?**
           - "cannot find symbol" for a renamed class/method/type → client.tsp `@@clientName`
           - Visibility/access error → client.tsp `@@access`
           - Wrong client/operation group → client.tsp `@@clientLocation`
           - If YES → Return false with the specific decorator recommendation
        3. **Read relevant files**: Read customization file(s) AND the generated code referenced
           in the error to understand exactly what changed
        4. **Locate the breakage**: Find the exact `StaticJavaParser.parseBlock(...)` or
           framework call that references the changed generated code
        5. **Apply fix**: Use ClientCustomizationCodePatch with just the FILENAME
        6. **STOP**: After a successful patch, immediately return true
        """;
    }

    private static string BuildTaskConstraints()
    {
        return """
        ## CRITICAL CONSTRAINTS
        
        ### Constraint 1: client.tsp First
        BEFORE attempting ANY code patch, determine if the issue can be resolved by
        updating client.tsp with the appropriate decorator. If it can, return false
        immediately and include the specific decorator recommendation in your response.
        
        Quick reference for the most common redirects:
        - "cannot find symbol" for a class/type → Was it renamed? → `@@clientName(NewType, "OldType", "java")`
        - "cannot find symbol" for a method → Was it renamed? → `@@clientName(newMethod, "OldMethod", "java")`
        - Visibility/access issue → `@@access(MyOp, Access.internal, "java")`
        - Method has wrong parameters in generated code → `@@override(original, customSignature)`
        - Type in wrong package → `@@clientNamespace(MyType, "correct.package")`
        
        ### Constraint 2: Customization Files Only
        The ClientCustomizationCodePatch tool ONLY works on files in the customization
        directory (files listed in the CUSTOMIZATION FILES section above).
        
        You CANNOT patch generated files (src/main/java/...). If the error is in generated
        code, check if:
        - The root cause is in a customization file → Fix the customization
        - The root cause is a TypeSpec change → Return false (regeneration or client.tsp fix)
        
        ### Constraint 3: Scope Check
        Before applying any patch, verify ALL of the following:
        - [ ] The fix is to a CUSTOMIZATION file (not generated code)
        - [ ] The issue is NOT fixable via client.tsp
        - [ ] The fix is structural (updating existing code, NOT adding new logic)
        - [ ] The change is < 20 lines
        - [ ] You have read both the customization file AND the generated code to confirm
        
        If ANY check fails → Return false immediately.
        
        ### Constraint 4: Safety
        - Use exact string matching for replacements — include enough context lines to
          uniquely identify the location
        - Preserve code formatting and indentation exactly
        - If uncertain about the fix → Return false (let human review)
        
        ### Constraint 5: Stop After Success
        After a ClientCustomizationCodePatch returns Success=true, IMMEDIATELY return true.
        Do NOT try additional patches. If a patch fails with "Old content not found",
        the file content may have changed — read it again or return false.
        
        ### Common Error Patterns and Classification
        
        | Error Pattern | Classification | Action |
        |--------------|---------------|--------|
        | "method X cannot be applied to given types" | Structural breakage (arg count/type mismatch in `parseBlock` body) | PATCH: Update the positional call site |
        | "cannot find symbol" — class/type renamed | client.tsp issue | RETURN FALSE: Recommend `@@clientName` |
        | "cannot find symbol" — getter for removed field | Structural breakage (field no longer exists) | PATCH: Remove/replace the getter call |
        | "cannot find symbol" — getter for renamed field | client.tsp issue | RETURN FALSE: Recommend `@@clientName` |
        | "variable X is already defined" | Structural breakage (duplicate) | PATCH: Remove duplicate from customization |
        | Error in src/main/java/... with no customization cause | Emitter/regeneration issue | RETURN FALSE: Not patchable |
        | "incompatible types" in customization | Structural breakage (return type changed) | PATCH: Update the type reference |
        """;
    }

    private static string BuildExamples()
    {
        return """
        ## EXAMPLE SCENARIOS
        
        ### Example 1: Positional Argument Mismatch (FIXABLE — structural breakage)
        
        **Error**: `method beginAnalyzeDocument in class DocumentIntelligenceClient cannot be applied to given types; required: String,AnalyzeDocumentOptions,String,String,StringIndexType,List,List,ContentFormat,List,AnalyzeOutputOption; found: String,AnalyzeDocumentOptions,String,String,StringIndexType,List,List,ContentFormat,List`
        
        **Root cause**: The customization has a convenience overload that delegates to the
        generated method using `StaticJavaParser.parseBlock(...)` with positional args. TypeSpec
        added a new parameter (`AnalyzeOutputOption`) at the end, so the hardcoded call site
        no longer matches.
        
        **How to find it**: Read the customization file and look for `setBody(StaticJavaParser.parseBlock(...))` 
        calls that reference `beginAnalyzeDocument`. The body will have a hardcoded call like:
        ```java
        "{ return this.beginAnalyzeDocument(modelId, analyzeDocumentOptions, "
        + "analyzeDocumentOptions.getPages(), analyzeDocumentOptions.getLocale(), "
        + "analyzeDocumentOptions.getStringIndexType(), ... ); }"
        ```
        
        **Fix**: Read the generated method signature to identify the new parameter and its
        position, then update the `parseBlock` string to include it:
        ```
        ClientCustomizationCodePatch(
            FilePath: 'DocumentIntelligenceCustomizations.java',
            OldContent: 'analyzeDocumentOptions.getOutputContentFormat(), analyzeDocumentOptions.getOutput()); }',
            NewContent: 'analyzeDocumentOptions.getOutputContentFormat(), analyzeDocumentOptions.getOutput(), analyzeDocumentOptions.getNewParam()); }'
        )
        ```
        
        ### Example 2: Duplicate Field Definition (FIXABLE — structural breakage)
        
        **Error**: `variable operationId is already defined in class AnalyzeOperationDetails`
        
        **Root cause**: The customization adds a field via `clazz.addField("String", "operationId", ...)` 
        but TypeSpec now generates that field directly.
        
        **Fix**: Remove the `addField` call from the customization since TypeSpec handles it:
        ```
        ClientCustomizationCodePatch(
            FilePath: 'DocumentIntelligenceCustomizations.java',
            OldContent: 'clazz.addField("String", "operationId", Modifier.Keyword.PRIVATE);',
            NewContent: '// Removed: operationId is now generated by TypeSpec'
        )
        ```
        
        ### Example 3: Renamed Type (NOT FIXABLE — use client.tsp)
        
        **Error**: `cannot find symbol: class AnalyzeBatchRequest`
        
        **Analysis**: TypeSpec renamed `AnalyzeBatchRequest` to `AnalyzeBatchDocumentsOptions`.
        The customization references the old name. This is NOT a code fix — renaming should be
        handled in client.tsp so ALL references (generated code, samples, tests) are consistent.
        
        **Action**: Return false. Recommend adding to client.tsp:
        ```typespec
        @@clientName(AnalyzeBatchDocumentsOptions, "AnalyzeBatchRequest", "java")
        ```
        
        ### Example 4: Renamed Method (NOT FIXABLE — use client.tsp)
        
        **Error**: Build succeeds but convenience overload is silently missing because
        `getMethodsByName("analyzeDocument")` returns empty — the method was renamed to
        `analyzeDocumentFromStream`.
        
        **Analysis**: The customization uses `getMethodsByName("analyzeDocument")` but the
        generated method was renamed. Even though this won't cause a compile error (it
        silently skips), the fix belongs in client.tsp, not in code.
        
        **Action**: Return false. Recommend adding to client.tsp:
        ```typespec
        @@clientName(analyzeDocumentFromStream, "analyzeDocument", "java")
        ```
        
        ### Example 5: Error in Generated Code (NOT FIXABLE)
        
        **Error**: `[ERROR] src/main/java/com/azure/ai/documentintelligence/models/SomeModel.java:[50,5] incompatible types`
        
        **Analysis**: Error is in generated code (src/main/java/...), not in a customization
        file. This tool can only patch customization files.
        
        **Action**: Return false. This is an emitter or TypeSpec issue.
        
        ### Example 6: Visibility/Access Change (NOT FIXABLE — use client.tsp)
        
        **Error**: `method setUrlSource(String) has private access in AnalyzeDocumentOptions`
        
        **Analysis**: The customization calls `setUrlSource(...)` but TypeSpec changed the
        method's visibility. The visibility should be controlled in client.tsp.
        
        **Action**: Return false. Recommend adding to client.tsp:
        ```typespec
        @@access(AnalyzeDocumentOptions.setUrlSource, Access.public, "java")
        ```
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
        - Read customization files to find the `StaticJavaParser.parseBlock(...)` or
          `customizeAst(...)` call that references the broken generated code
        - Read generated code (src/main/java/...) to understand what changed
        - Use full paths from the customization file list
        
        **ClientCustomizationCodePatch tool:**
        - FilePath: just the filename (e.g., "MyCustomizations.java"), NOT full path
        - OldContent: exact text to replace — include enough surrounding context for uniqueness
        - NewContent: replacement text preserving formatting
        
        ## DECISION FLOW
        
        ```
        Build error received
              │
              ▼
        Is this a rename? (class, method, type, or property name changed)
              │
         ┌────┴────┐
         Yes       No
         │         │
         ▼         ▼
        Return    Is this a visibility/access issue?
        false         │
        (recommend   ┌┴──┐
        @@clientName) Yes  No
                  │    │
                  ▼    ▼
                Return Is error in generated code (src/main/java/...)?
                false       │
                (recommend ┌┴──┐
                @@access)  Yes  No
                           │    │
                           ▼    ▼
                         Return Is error in customization code with
                         false  a structural root cause?
                         (emitter    │
                          issue) ┌───┴───┐
                                 Yes     No
                                 │       │
                                 ▼       ▼
                              Apply    Return
                              patch    false
        ```
        
        ## RETURN VALUES
        - **Return true**: Structural fix applied successfully via ClientCustomizationCodePatch
        - **Return false**: Unable to fix. Include reason:
          - "Rename issue — recommend @@clientName(NewName, 'OldName', 'java') in client.tsp"
          - "Access issue — recommend @@access(Type.member, Access.public, 'java') in client.tsp"
          - "Error in generated code — emitter/regeneration issue, not patchable"
          - "Uncertain about fix — needs human review"
        
        It is BETTER to return false with a clear recommendation than to apply an incorrect fix.
        """;
    }
}
