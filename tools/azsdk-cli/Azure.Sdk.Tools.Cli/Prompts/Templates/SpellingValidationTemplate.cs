// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Template for spelling validation and correction prompts.
/// This template guides AI to automatically fix spelling issues by either correcting typos or adding legitimate terms.
/// </summary>
public class SpellingValidationTemplate : BasePromptTemplate
{
    public override string TemplateId => "spelling-validation";
    public override string Version => "1.0.0";
    public override string Description => "Automated spelling validation and correction for Azure SDK repositories";


    protected override IEnumerable<string> RequiredParameters => new[] { "cspell_output" };
    protected override IEnumerable<string> OptionalParameters => new[] { "repository_context", "additional_rules" };

    protected override string BuildTaskInstructions(IPromptContext context)
    {
        var cspellOutput = context.GetParameter<string>("cspell_output") ?? "";
        var repositoryContext = context.GetParameter<string>("repository_context") ?? "Azure SDK repository";
        
        return $"""
        You are an automated spelling assistant for an {repositoryContext}.
        
        Analyze cspell lint output and fix spelling issues.
        
        **Your Tasks:**
        1. Analyze each reported spelling issue from the cspell output
        2. For each issue, either:
           - Fix the typo by correcting spelling in the source file
           - Add legitimate technical terms/product names/proper nouns to cspell.json
        3. Apply fixes by reading files, making corrections, and writing them back
        4. Update cspell.json 'words' array with legitimate words (never remove existing words)
        
        **cspell lint output to analyze:**
        ```
        {cspellOutput}
        ```
        """;
    }

    protected override string BuildTaskConstraints(IPromptContext context)
    {
        var additionalRules = context.GetParameter<string>("additional_rules") ?? "";
        
        var constraints = """
        **Spelling Correction Guidelines:**
        - Fix obvious typos in comments, documentation, and non-code text
        - Add technical terms, API names, product names, acronyms to cspell.json
        - Preserve exact casing and formatting
        - NEVER modify code logic or structure, only spelling
        - When in doubt, add the word to cspell.json rather than changing it
        
        **File Handling:**
        - Read the entire file before making changes
        - Make precise changes only to the misspelled words
        - Preserve all formatting, indentation, and line endings
        - Validate that changes don't break syntax
        
        **cspell.json Updates:**
        - Add words to the 'words' array in alphabetical order
        - Never remove existing words from the dictionary
        - Group related technical terms together when possible
        """;

        if (!string.IsNullOrEmpty(additionalRules))
        {
            constraints += $"\n\n**Additional Rules:**\n{additionalRules}";
        }

        return constraints;
    }

    protected override string BuildOutputRequirements(IPromptContext context)
    {
        return """
        **Required Output Format:**
        
        Return a structured summary of operations performed:
        
        ```json
        {
            "summary": "Brief description of actions taken",
            "files_modified": [
                {
                    "file_path": "path/to/file",
                    "changes": [
                        {
                            "line": 42,
                            "old_text": "misspelled word",
                            "new_text": "corrected word",
                            "reason": "Fixed typo"
                        }
                    ]
                }
            ],
            "dictionary_additions": [
                {
                    "word": "technical-term",
                    "reason": "Legitimate Azure service name"
                }
            ],
            "skipped_items": [
                {
                    "word": "ambiguous-term",
                    "reason": "Unclear if typo or legitimate term"
                }
            ]
        }
        ```
        
        **Validation:**
        - Ensure all file paths are accurate
        - Verify line numbers match the actual changes
        - Provide clear reasoning for each decision
        """;
    }

    protected override string BuildExamples(IPromptContext context)
    {
        return """
        **Example 1: Fixing a typo**
        ```
        cspell error: "recieve" in README.md line 15
        Action: Change "recieve" to "receive" in the file
        Reason: Common spelling error
        ```
        
        **Example 2: Adding legitimate term**
        ```
        cspell error: "DocumentIntelligence" in API documentation
        Action: Add "DocumentIntelligence" to cspell.json
        Reason: Official Azure service name
        ```
        
        **Example 3: Technical acronym**
        ```
        cspell error: "SDK" in multiple files
        Action: Add "SDK" to cspell.json
        Reason: Standard software development acronym
        ```
        """;
    }
}
