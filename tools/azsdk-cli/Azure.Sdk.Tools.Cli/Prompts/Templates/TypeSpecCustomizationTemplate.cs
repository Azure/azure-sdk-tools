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
    public override string Version => "1.0.0";
    public override string Description => "Apply TypeSpec client.tsp customizations";

    private readonly string customizationRequest;
    private readonly string typespecProjectPath;
    private readonly string referenceDocPath;

    /// <summary>
    /// Initializes a new TypeSpec customization template with the specified parameters.
    /// </summary>
    /// <param name="customizationRequest">The customization request (build error, user prompt, API feedback, etc.)</param>
    /// <param name="typespecProjectPath">Path to the TypeSpec project (contains tspconfig.yaml)</param>
    /// <param name="referenceDocPath">Path to the customizing-client-tsp.md reference document</param>
    public TypeSpecCustomizationTemplate(
        string customizationRequest,
        string typespecProjectPath,
        string referenceDocPath)
    {
        this.customizationRequest = customizationRequest;
        this.typespecProjectPath = typespecProjectPath;
        this.referenceDocPath = referenceDocPath;
    }

    /// <summary>
    /// Builds the complete TypeSpec customization prompt using the configured parameters.
    /// </summary>
    /// <returns>Complete structured prompt for TypeSpec client.tsp customization</returns>
    public override string BuildPrompt()
    {
        var referenceDocContent = File.ReadAllText(referenceDocPath);
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
        2. Read the existing client.tsp file and any relevant TypeSpec files (main.tsp, tspconfig.yaml) as needed
        3. Apply customizations incrementally to client.tsp only
        4. Compile after each change to verify it works
        5. If a change fails compilation, rollback and try an alternative approach
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
        - If a change causes compilation errors, rollback and try a different approach
        - If compilation succeeds but warnings remain, attempt to reduce or resolve the warnings when feasible (warnings are not fatal)
        - Follow the patterns and best practices from the reference documentation

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
        1. Read existing client.tsp and relevant files to understand current state
        2. Plan the changes needed based on the customization request
        3. Apply each change incrementally:
           a. Modify client.tsp with the change
           b. Compile the TypeSpec project
           c. If compilation succeeds, proceed to next change
           d. If compilation fails, rollback and try alternative approach
        4. After changes compile, review any compiler warnings and attempt reasonable fixes without sacrificing required customizations
        5. Continue until all changes are successfully applied

        **Final Output:**
        Provide a summary of all successfully applied changes, including:
        - What customizations were applied
        - Any issues encountered and how they were resolved
        - Any warnings that could not be addressed and why
        - Any requested changes that could not be applied and why
        """;
    }
}
