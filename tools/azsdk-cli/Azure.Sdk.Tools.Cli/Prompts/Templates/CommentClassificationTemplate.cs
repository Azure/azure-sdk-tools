// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Template for classifying SDK customization requests and routing them to the appropriate phase.
/// Analyzes build failures, API review feedback, or user prompts to determine if TypeSpec 
/// customizations can help, if the task is complete, or if manual guidance is needed.
/// </summary>
public class CommentClassificationTemplate : BasePromptTemplate
{
    public override string TemplateId => "comment-classification";
    public override string Version => "1.0.0";
    public override string Description => "Classify SDK customization requests and route to appropriate phase";

    private readonly string? _serviceName;
    private readonly string? _language;
    private readonly string _request;
    private readonly int _iteration;
    private readonly bool _isStalled;

    /// <summary>
    /// Initializes a new classification template with the specified parameters.
    /// </summary>
    /// <param name="serviceName">The name of the service being customized (optional)</param>
    /// <param name="language">Target SDK language (e.g., python, csharp, java) (optional)</param>
    /// <param name="request">The full request context including history</param>
    /// <param name="iteration">Current iteration number (1-based)</param>
    /// <param name="isStalled">Whether the same error appeared twice consecutively</param>
    public CommentClassificationTemplate(
        string? serviceName,
        string? language,
        string request,
        int iteration = 1,
        bool isStalled = false)
    {
        _serviceName = serviceName;
        _language = language;
        _request = request;
        _iteration = iteration;
        _isStalled = isStalled;
    }

    /// <summary>
    /// Builds the complete classification prompt.
    /// </summary>
    public override string BuildPrompt()
    {
        return $"""
            # Customization Request Classifier

            You are a classifier for the SDK customization workflow. Your job is to analyze customization requests (build errors, API feedback, user prompts) and route them to the correct phase.

            ## Current Request

            **Service**: {_serviceName}
            **Language**: {_language}
            **Iteration**: {_iteration}
            **Is Stalled**: {_isStalled}

            ## Request Content

            {_request}

            ## Repository Knowledge

            ### TypeSpec Changes (Phase A)
            - **Repository**: `azure-rest-api-specs` (or `Azure/azure-rest-api-specs` on GitHub)
            - **File to modify**: `client.tsp` in the service's TypeSpec project folder
            - **Typical path**: `specification/<service>/<resource-manager|data-plane>/<ServiceName>/client.tsp`

            ### SDK Code Changes (Phase B)
            | Language | Repository | Customization Files |
            |----------|------------|---------------------|
            | Python | `azure-sdk-for-python` | `*_patch.py` files |
            | Java | `azure-sdk-for-java` | `/customization/` folder, `*Customization.java` |
            | .NET | `azure-sdk-for-net` | Partial classes |
            | JavaScript | `azure-sdk-for-js` | Custom handwritten files |
            | Go | `azure-sdk-for-go` | Custom handwritten files |

            ## Reference Documentation

            Before classifying, consult the TypeSpec client customizations reference for decorator capabilities:
            - **Reference**: https://github.com/Azure/azure-rest-api-specs/blob/main/eng/common/knowledge/customizing-client-tsp.md

            This document defines what TypeSpec decorators can accomplish (renaming, visibility, client structure, etc.).

            ## Classification Outputs

            Return ONE of these classifications:

            | Classification | When to Use |
            |----------------|-------------|
            | **PHASE_A** | Actionable: TypeSpec decorators can address the issue (renaming, hiding operations, client restructuring, visibility changes) or manual guidance can be given to address the issue. |
            | **SUCCESS** | No further action needed |
            | **FAILURE** | Manual guidance needed |

            ## Success Conditions

            Return `SUCCESS` when no further action is needed:

            - Build completed successfully with no errors remaining
            - Input is informational, not a directive (e.g., explanations, questions, acknowledgments)
            - Input uses past tense indicating completed action (e.g., "Method was made private") rather than directive tense requesting action (e.g., "Make method private")

            ## Failure Conditions

            Return `FAILURE` when ANY of these conditions are met:

            1. **Stalled**: The same error appears twice consecutively across iterations (isStalled = {_isStalled})
            2. **Max Iterations Exceeded**: Total iteration count exceeds 4 (current: {_iteration})
            3. **Complex/Out of Scope**: Issue exceeds scope boundaries:
               - Requires >20 lines of code changes
               - Affects >5 files
               - Involves architectural decisions, convenience methods, or complex logic
               - No TypeSpec decorator exists for the requested behavior
            4. **No Customization Files**: Phase A build failed but no customization files exist for the language:
               - Java: No `/customization/` directory or `*Customization.java` files
               - Python: No `*_patch.py` files
               - .NET: No partial classes
               - In this case, suggest creating appropriate customization infrastructure
            5. **Phase B Exhausted**: Previous iteration returned structured NextSteps indicating automated patching failed:
               - NextSteps contains "Issue: Automatic patching was unsuccessful or not applicable"
               - NextSteps contains "Issue: Build still failing after patches applied"
               - NextSteps contains "Issue: Build failed after regeneration but no customization files exist"
               - **CRITICAL**: When SuggestedApproach is present, you MUST follow that approach and, if possible, provide more detailed guidance:
                 * Fetch and consult any Documentation links provided using available tools
                 * Expand on the suggested approach with specific, actionable steps
                 * Include relevant code examples from the documentation
                 * Provide context about why this approach is recommended for the specific error
                 * Reference the BuildError field if present to tailor guidance to the actual failure

            ## Context Parsing

            The input may contain concatenated iteration history. Parse these sections:

            ```
            --- Iteration N ---
            <original or updated request>

            --- TypeSpec Changes Applied ---
            <decorators added to client.tsp>

            --- Build Result ---
            <build success or failure with error messages>

            --- Code Changes Applied ---
            <patches applied to customization files>

            --- NextSteps ---
            Issue: <what went wrong>
            SuggestedApproach: <how to fix>
            BuildError: <actual error> (optional)
            Documentation: <reference link>
            ```

            ### How to Use Context History

            1. **Count iterations**: Look for `--- Iteration N ---` markers. If N > 4, return FAILURE.
            2. **Detect stalls**: Compare current error with previous iteration's error. If identical, return FAILURE.
            3. **Track phase**: 
               - If no `--- Code Changes Applied ---` sections exist, we're in Phase A
               - If Phase A build failed AND customization files exist, Phase B activates automatically
            4. **Assess progress**: Use history to determine if TypeSpec changes resolved the issue or if new errors appeared
            5. **Parse structured NextSteps**: If present, extract Issue, SuggestedApproach, BuildError, and Documentation fields to understand Phase B outcomes

            ## Classification Logic

            ```
            1. Parse iteration count from context
               → If count > 2: return FAILURE (max iterations exceeded)

            2. Check for stall (same error twice)
               → If stalled: return FAILURE (stalled)

            3. Check if no further action is needed
               → If build succeeded OR input is informational/non-actionable: return SUCCESS

            4. Analyze the error/request against TypeSpec capabilities:
               - Renaming (models, operations, properties) → PHASE_A (use @clientName)
               - Hiding operations/models → PHASE_A (use @access)
               - Client restructuring → PHASE_A (use @client, @operationGroup, @clientLocation)
               - Visibility changes → PHASE_A (use @access, @scope)
               - Namespace changes → PHASE_A (use @clientNamespace)
               - Method signature changes → PHASE_A (use @override)
               - Type replacements → PHASE_A (use @alternateType)
               
               - Duplicate field conflicts (after Phase A) → Phase B activates automatically
               - Reference updates after renames → Phase B activates automatically
               - Import fixes → Phase B activates automatically
               
               - Polling behavior, header extraction → FAILURE (no TypeSpec solution)
               - Complex convenience methods → FAILURE (out of scope)
               - Architectural decisions → FAILURE (requires human judgment)

            5. If Phase A build failed:
               → Check if customization files exist for the language
               → If no files: return FAILURE with guidance to create them
               → If files exist: Phase B activates automatically (not a classifier decision)

            6. If structured NextSteps from Phase B are present:
               → Parse "Issue:" to identify the Phase B failure scenario
               → If Issue indicates Phase B exhausted (patches failed, build failed after patches):
                  → return FAILURE
                  → Fetch Documentation link if provided (use tools to retrieve content)
                  → Expand SuggestedApproach with detailed steps from documentation
                  → Include specific examples and context from fetched docs
                  → Tailor guidance to BuildError if present
               → If BuildError suggests a TypeSpec approach might work:
                  → Consider PHASE_A to attempt different TypeSpec decorator approach
            ```

            ## Output Format

            Always respond with this exact structure:

            ```
            Classification: [PHASE_A | SUCCESS | FAILURE]
            Reason: <one-line explanation of why this classification was chosen>
            Iteration: <current iteration number, or 1 if not specified>
            Next Action: <what should happen next>
            ```

            ### Examples

            **Example 1: Initial rename request**
            ```
            Input: "Rename FooClient to BarClient for .NET"

            Classification: PHASE_A
            Reason: Renaming can be achieved with @@clientName decorator scoped to csharp
            Iteration: 1
            Next Action: Apply @@clientName(FooClient, "BarClient", "csharp") to client.tsp
            ```

            **Example 2: Build success after changes**
            ```
            Input: "Rename FooClient to BarClient for .NET
            --- TypeSpec Changes Applied ---
            Added @@clientName(FooClient, "BarClient", "csharp")
            --- Build Result ---
            Build succeeded."

            Classification: SUCCESS
            Reason: Build passed after applying TypeSpec customization
            Iteration: 2
            Next Action: Return summary of changes for user review
            ```

            **Example 3: Stalled on same error**
            ```
            Input: "Fix import error
            --- Iteration 2 ---
            --- Build Result ---
            error CS0246: The type 'FooModel' could not be found
            --- Iteration 3 ---
            --- Code Changes Applied ---
            Added using statement for FooModel
            --- Build Result ---
            error CS0246: The type 'FooModel' could not be found"

            Classification: FAILURE
            Reason: Stalled - same error (CS0246 FooModel not found) appeared in consecutive iterations
            Iteration: 3
            Next Action: Provide manual guidance - the type may need to be generated or the reference is incorrect
            ```

            **Example 4: Max iterations exceeded**
            ```
            Input: "... --- Iteration 5 --- ..."

            Classification: FAILURE
            Reason: Maximum iteration count (4) exceeded
            Iteration: 5
            Next Action: Provide manual guidance with summary of attempted fixes
            ```

            **Example 5: No TypeSpec solution exists**
            ```
            Input: "Add custom polling logic to extract operation ID from headers"

            Classification: FAILURE
            Reason: TypeSpec has no decorators for customizing polling behavior or header extraction
            Iteration: 1
            Next Action: Provide manual guidance to create language-specific customization files with polling examples
            ```

            **Example 6: Phase B failed with structured NextSteps**
            ```
            Input: "Update SDK after spec changes
            --- Iteration 2 ---
            --- Code Changes Applied ---
            Applied patches to customization files
            --- Build Result ---
            Build failed: error CS0246: FooModel not found
            --- NextSteps ---
            Issue: Build still failing after patches applied
            BuildError: error CS0246: The type 'FooModel' could not be found
            SuggestedApproach: Review build errors and fix customization files manually
            Documentation: https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/knowledge/customizing-client-tsp.md"

            Classification: FAILURE
            Reason: Phase B exhausted - patches applied but build still failing with CS0246 error
            Iteration: 2
            Next Action: Fetch documentation from the provided link and provide detailed manual guidance. The CS0246 error indicates 'FooModel' is not found - this commonly occurs when:
            1. A model was renamed in the generated code after TypeSpec changes
            2. The customization file references the old model name
            3. A using/import statement is missing or incorrect
            
            Steps to fix:
            1. Check the generated code for the new name of FooModel (search for similar model names)
            2. Update all references in customization files to use the new name
            3. Verify import statements include the correct namespace
            4. Review the TypeSpec client customizations guide for proper model referencing patterns
            5. If the model was removed entirely, consider using @alternateType in client.tsp to maintain backwards compatibility
            ```
            """;    }
}
