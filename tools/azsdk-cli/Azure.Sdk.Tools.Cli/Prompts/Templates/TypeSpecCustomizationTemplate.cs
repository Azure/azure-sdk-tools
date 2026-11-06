// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Template for TypeSpec client.tsp customization prompts.
/// This template guides AI to apply customizations to client.tsp files based on build errors, user requests, or API feedback.
/// </summary>
public class TypeSpecCustomizationTemplate : BasePromptTemplate
{
    public override string TemplateId => "typespec-customization";
    public override string Version => "1.0.1";
    public override string Description => "Apply TypeSpec client.tsp customizations";

    private readonly string customizationRequest;
    private readonly string typespecProjectPath;
    private readonly string referenceDocContent;

    /// <summary>
    /// Initializes a new TypeSpec customization template with the specified parameters.
    /// </summary>
    /// <param name="customizationRequest">The customization request (build error, user prompt, API feedback, etc.)</param>
    /// <param name="typespecProjectPath">Path to the TypeSpec project (contains tspconfig.yaml)</param>
    /// <param name="referenceDocContent">Content of the customizing-client-tsp.md reference document</param>
    public TypeSpecCustomizationTemplate(
        string customizationRequest,
        string typespecProjectPath,
        string referenceDocContent)
    {
        this.customizationRequest = customizationRequest;
        this.typespecProjectPath = typespecProjectPath;
        this.referenceDocContent = referenceDocContent;
    }

    /// <summary>
    /// Builds the complete TypeSpec customization prompt using the configured parameters.
    /// </summary>
    /// <returns>Complete structured prompt for TypeSpec client.tsp customization</returns>
    public override string BuildPrompt()
    {
        var taskInstructions = BuildTaskInstructions(referenceDocContent);
        var constraints = BuildTaskConstraints();
        var outputRequirements = BuildOutputRequirements();

        // Examples are included in the reference doc, so pass null
        return BuildStructuredPrompt(taskInstructions, constraints, examples: null, outputRequirements);
    }

    private string BuildTaskInstructions(string referenceDocContent)
    {
        return $"""
        You are applying TypeSpec client customizations to a client.tsp file.

        **TypeSpec Project Path:** {typespecProjectPath}

        **Working Directory for Tools:**
        All file operations use RELATIVE paths from the TypeSpec project directory above.
        - To read client.tsp, use: ReadFile("client.tsp")
        - To read main.tsp, use: ReadFile("main.tsp")
        - To read files in subdirectories, use: ReadFile("connections/models.tsp")
        - Do NOT use absolute paths or paths relative to the repository root
        - The WriteFile and CompileTypeSpec tools also use relative paths from this directory

        **Customization Request:**
        {customizationRequest}

        **Reference Documentation:**
        The following is the complete reference for TypeSpec client customizations. Use this as your guide for applying changes:

        {referenceDocContent}

        **Your Tasks:**
        1. Analyze the customization request to understand what changes are needed
        2. Read the COMPLETE existing client.tsp through end-of-file and retain its contents as the baseline; read relevant TypeSpec files (main.tsp, tspconfig.yaml) as needed
        3. Apply customizations incrementally to client.tsp only
        4. Compile after each change to verify it works
        5. If a change fails compilation, rollback and try an alternative approach if any. If no alternative approach is possible, explain why the requested change cannot be applied
        6. Continue until all requested customizations are successfully applied
        """;
    }

    private static string BuildTaskConstraints()
    {
        return """
        **CRITICAL Requirements:**
        - Only write to the client.tsp file - do not modify main.tsp or other TypeSpec files
        - Read any files as needed to understand the existing structure
        - Apply changes incrementally, one logical change at a time
        - Compile after each change to verify correctness
        - If a change causes compilation errors, rollback and try a different approach if any
        - If compilation succeeds but warnings remain, attempt to reduce or resolve the warnings when feasible (warnings are not fatal)
        - Follow the patterns and best practices from the reference documentation

        **Preserve Existing Content:**
        - WriteFile REPLACES the entire file; it does not append or patch. Every call must include the COMPLETE updated client.tsp, including all unchanged content through end-of-file
        - If a read is truncated or incomplete, use ReadFile with consecutive startLine/endLine ranges until end-of-file; never treat a preview or excerpt as the whole file
        - Preserve existing imports, namespaces, declarations, decorators, comments, and language scopes unless the request requires changing them. Do not remove, reorder, reformat, or simplify unrelated content
        - Never replace existing content with a template, summary, ellipsis, or "unchanged code" placeholder. The basic template is only for a client.tsp confirmed not to exist
        - If you cannot obtain or safely write the complete file, stop and report the limitation without writing partial content
        - Before writing, compare the proposed content with the baseline: every deletion or modification must be required by the request. Preserve all unrelated customizations, both for the target language and for every other language
        - After each write, read back the COMPLETE file and compare with the baseline, including the original trailing content. Compilation success alone does not prove existing customizations were preserved
        - If unrelated content is lost, restore it before proceeding. Rollback must undo only your latest change, preserving pre-existing user edits and earlier successful customizations

        **File Structure:**
        - client.tsp: Contains all client customizations (this is the ONLY file you should modify)
        - main.tsp: Service definition (read-only reference)
        - tspconfig.yaml: Compiler configuration (read-only reference)
        """;
    }

    private static string BuildOutputRequirements()
    {
        return """
        **Workflow:**
        1. Read and retain the complete existing client.tsp as the baseline; read relevant files to understand current state
        2. Plan the changes needed based on the customization request
        3. Apply each change incrementally:
           a. Write the complete client.tsp with only the required change, then read it back and verify all unrelated content is preserved
           b. Compile the TypeSpec project
           c. If compilation succeeds, proceed to next change
           d. If compilation fails, rollback and try alternative approach
        4. After changes are applied and compilation succeeds, review any compiler warnings and attempt reasonable fixes without sacrificing required customizations
        5. Finish only when compilation succeeds AND comparison with the baseline confirms no unrelated content was changed or lost

        **Final Output:**
        Provide a summary of all successfully applied changes, including:
        - What customizations were applied
        - Whether the complete file was checked and existing unrelated customizations were preserved
        - Any issues encountered and how they were resolved
        - Any warnings that could not be addressed and why
        - Any requested changes that could not be applied and why
        """;
    }
}
