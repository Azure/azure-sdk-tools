// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Error-driven template for Python customization patch generation.
/// Scope: deterministic, mechanical fixes to _patch.py files only (update references, fix renames).
/// </summary>
public class PythonErrorDrivenPatchTemplate(
    string buildError,
    string packagePath,
    List<string> customizationFiles) : BasePromptTemplate
{
    public override string TemplateId => "python-error-driven-patch";
    public override string Version => "1.0.0";
    public override string Description => "Analyze build/import error and apply targeted fix to Python _patch.py customization code";

    public override string BuildPrompt() =>
        BuildStructuredPrompt(BuildTaskInstructions(), BuildTaskConstraints(), BuildExamples(), BuildOutputRequirements());

    private string BuildTaskInstructions()
    {
        var fileList = string.Join("\n", customizationFiles.Select(f => $"  - {f}"));

        return $"""
        ## BUILD/IMPORT ERROR TO FIX
        ```
        {buildError}
        ```
        
        ## YOUR TASK
        Analyze the error above and apply a targeted fix to the Python customization code in _patch.py files.
        
        ## PYTHON CUSTOMIZATION CONTEXT
        Python SDKs use `_patch.py` files with class inheritance to customize generated code.
        The pattern is:
        1. Import the generated class from the generated module (e.g., `from ._client import FaceClient as FaceClientGenerated`)
        2. Inherit from it and override/wrap methods (e.g., `class FaceClient(FaceClientGenerated):`)
        3. Call `super()._method_name(...)` to delegate to the generated implementation
        4. Export the customized class in `__all__` so it replaces the generated one
        
        Customization files can exist at multiple levels:
        - `azure/package/_patch.py` — client-level customizations
        - `azure/package/aio/_patch.py` — async client customizations
        - `azure/package/models/_patch.py` — model customizations
        - `azure/package/operations/_patch.py` — operation group customizations
        
        See: https://github.com/Azure/autorest.python/blob/main/docs/customizations.md
        
        ## SCOPE LIMITATIONS
        You may ONLY perform these types of fixes:
        - Update method/function name references after TypeSpec renames (e.g., `super()._old_name(...)` → `super()._new_name(...)`)
        - Update class name references (e.g., import renames)
        - Fix broken `super()` calls when the parent method signature changed
        - Update parameter names in delegated calls
        
        You may NOT:
        - Add new customization logic
        - Restructure the customization architecture
        - Add error handling or new features
        - Modify generated code (files outside _patch.py)
        
        ## FILE STRUCTURE
        - Package Path: {packagePath}
        
        **For ReadFile tool**: Use paths relative to Package Path (listed below)
        **For ClientCustomizationCodePatch tool**: Use the same relative path as ReadFile
        
        Customization Files (_patch.py with customizations):
        {fileList}
        
        ## WORKFLOW
        1. **Parse the error**: Identify the exact issue (NameError, AttributeError, ImportError, etc.)
        2. **Read the _patch.py file(s)**: Use ReadFile with paths listed above
           - Find the `super()._method_name(...)` call or import that references the old name
        3. **Read generated code**: Find the corresponding generated file to discover the NEW name
           - For client patches: read the generated `_client.py` in the same directory
           - For model patches: read the generated `_models.py` in the same directory
           - For operation patches: read the generated `_operations.py` in the same directory
           - For async: check `aio/` subdirectory for async versions
        4. **Locate the fix**: Find the exact string in _patch.py that needs updating
        5. **Apply fix**: Use ClientCustomizationCodePatch with the relative path
        6. **Fix ALL affected files**: If both sync and async _patch.py files have the same broken reference, fix BOTH
        7. **Fix ALL broken references**: If the error mentions a missing type/method, search ALL _patch.py files for references to it and fix them all
        
        ## KEY INSIGHT: TypeSpec Renames in Python
        When you see "NameError", "AttributeError", or method not found errors:
        - TypeSpec may have renamed an operation (e.g., `detect_from_url` → `detect_face_from_url`)
        - Python generates snake_case method names with underscore prefix for internal methods
        - The _patch.py calls `super()._old_method_name(...)` but the generated code now has `_new_method_name`
        - Both sync and async _patch.py files may need the same fix
        - Use ReadFile to look at the GENERATED code (_client.py, _operations.py) to discover the NEW name
        """;
    }

    private static string BuildTaskConstraints()
    {
        return """
        ## CRITICAL CONSTRAINTS
        
        **IMPORTANT: You can ONLY patch _patch.py files**
        The ClientCustomizationCodePatch tool ONLY works on _patch.py files.
        You CANNOT patch generated files like _client.py, _operations.py, _models.py.
        
        If the error is in generated code, you must:
        1. Find the ROOT CAUSE in the _patch.py file
        2. Fix the _patch.py so that the import/inheritance works with the new generated code
        3. If there's no _patch.py cause → Return false (regeneration issue)
        
        **Scope Check - Before attempting ANY fix, verify:**
        - Is the fix to a _patch.py file? (not generated code)
        - Is this a mechanical fix? (updating a method/class name reference)
        - Is this <20 lines of change?
        - Am I updating references, NOT adding new logic?
        
        If ANY answer is NO → Return false immediately. Do not attempt the fix.
        
        **Safety Guidelines:**
        - Use exact string matching for replacements
        - Include enough context to uniquely identify the location
        - Preserve code formatting and indentation
        - If uncertain about the fix → Return false (let human review)
        
        **CRITICAL: FIX ALL RELATED ERRORS**
        - The error message may mention ONE broken reference, but the same broken reference may appear in MULTIPLE places
        - Fix ALL occurrences in ALL _patch.py files (sync AND async)
        - After each successful patch, check if other _patch.py files have the SAME broken reference
        - If a patch fails with "Old content not found", read the file again to get the EXACT current content
        - Use the Exit tool to signal completion only after you've fixed all files
        
        **Discovery Process for Renames:**
        When error says "NameError", "AttributeError", or "has no attribute":
        1. READ the generated file in the same directory using ReadFile tool
           - For `_patch.py` → read `_client.py` or `_operations.py` in same folder
           - For `aio/_patch.py` → read `aio/_client.py` or `aio/_operations.py`
        2. SEARCH for similar method/class names to find what it was renamed TO
        3. UPDATE the _patch.py to use the new name
        
        **Python Naming Convention:**
        - TypeSpec operations become snake_case in Python: `detectFromUrl` → `detect_from_url`
        - Internal/generated methods have underscore prefix: `_detect_from_url`
        - When TypeSpec renames `detectFromUrl` → `detectFaceFromUrl`, Python generates `_detect_face_from_url`
        """;
    }

    private static string BuildExamples()
    {
        return """
        ## EXAMPLE SCENARIOS
        
        **Example 1: Method Renamed - super() Call Breaks**
        Error: `NameError: name '_detect_from_url' is not defined`
        
        Step 1 - Read _patch.py: ReadFile("azure/ai/vision/face/aio/_patch.py")
        Discovery: Has `return await super()._detect_from_url(body, url=url, ...)`
        
        Step 2 - Read generated client: ReadFile("azure/ai/vision/face/aio/_client.py")
        Discovery: Method is now `_detect_face_from_url` (TypeSpec renamed detectFromUrl → detectFaceFromUrl)
        
        Step 3 - Fix _patch.py:
        ```
        ClientCustomizationCodePatch(
            FilePath: 'azure/ai/vision/face/aio/_patch.py',
            OldContent: 'return await super()._detect_from_url(',
            NewContent: 'return await super()._detect_face_from_url('
        )
        ```
        
        **Example 2: Import References Old Class Name**
        Error: `ImportError: cannot import name 'OldClient' from '._client'`
        
        Step 1 - Read _patch.py to find the import statement
        Step 2 - Read _client.py to find the new class name
        Step 3 - Update the import and class reference in _patch.py
        
        **Example 3: Parameter Renamed in Generated Method**
        Error: `TypeError: _method() got an unexpected keyword argument 'old_param'`
        
        Step 1 - Read _patch.py to find the super() call with old_param
        Step 2 - Read generated code to find the new parameter name
        Step 3 - Update the parameter name in the super() call
        
        **Example 4: Error in Generated Code - NOT Fixable**
        Error: `SyntaxError in _client.py:100`
        
        Analysis: Error is in GENERATED code (_client.py), not _patch.py.
        This tool CANNOT patch generated files.
        
        Action: Return false immediately. This is an emitter/regeneration issue.
        """;
    }

    private static string BuildOutputRequirements()
    {
        return """
        ## TOOL USAGE
        
        **ReadFile tool:**
        - Use to examine both _patch.py files and generated code
        - Paths relative to package path: `azure/package/_patch.py`, `azure/package/_client.py`
        
        **ClientCustomizationCodePatch tool:**
        - FilePath: relative path to the _patch.py file from package path
        - OldContent: exact text to replace (include context for uniqueness)
        - NewContent: replacement text
        
        ## DECISION FLOW
        
        1. Can I identify the exact error location?
           NO → Return false
           
        2. Is the error caused by a _patch.py referencing something that was renamed?
           NO → Return false
           
        3. Can I find the new name in the generated code?
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
