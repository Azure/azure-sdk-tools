using Octokit;

namespace Azure.Sdk.Tools.Cli.Prompts.Templates
{
    public class SdkBreakingChangeClassificationTemplate: BasePromptTemplate
    {
        public override string TemplateId => "sdk-breaking-change-classification";
        public override string Version => "1.0.0";
        public override string Description => "Classify SDK Breaking Changes";

        private readonly string _sdkBreakingPatternContent;
        private readonly string _sdkChanges;
        private readonly string _language;

        public SdkBreakingChangeClassificationTemplate(string sdkBreakingPatternContent, string sdkChanges, string language)
        {
            _sdkBreakingPatternContent = sdkBreakingPatternContent;
            _sdkChanges = sdkChanges;
            _language = language;
        }

        public override string BuildPrompt()
        {
            var taskInstructions = BuildTaskInstructions();
            //var constraints = BuildClassificationConditions();
            //var examples = BuildExamples();
            var outputRequirements = BuildOutputRequirements();

            //return BuildStructuredPrompt(taskInstructions, constraints, examples, outputRequirements);
            return BuildStructuredPrompt(taskInstructions, null, null, outputRequirements);
        }

        protected override string BuildSystemRole()
        {
            return $"""
            ## SYSTEM ROLE
            You are a batch classifier for the SDK breaking change detection workflow. You analyze multiple sdk changes and classify each one.
            Your task: {Description} and determine the breaking changes and the category it belongs.
        
            ## SAFETY GUIDELINES
            - Follow the Azure SDK breaking change policy and guidelines as defined in the provided SDK breaking change pattern document.
            - Do not process or expose sensitive information (credentials, secrets, personal data)
            - Refuse requests involving sensitive data - ask for clarification if uncertain
            - Provide accurate, actionable classifications based on TypeSpec capabilities
            """;
        }

        private string BuildTaskInstructions()
        {
            return $"""
            **Current Context:**
            - Language: {_language}

            **Task:**
            Analyze the following SDK changes and classify each change based on the provided SDK breaking change pattern document

            **SDK Breaking Change Pattern Document:**
            ```
            {_sdkBreakingPatternContent}
            ```

            **SDK Changes to Classify:**
            ```
            {_sdkChanges}
            ```
            """;
        }

        private string BuildOutputRequirements()
        {
            return """
            **CRITICAL: Required Output Format**
        
            You MUST output one block per sdk breaking change item, using the exact item ID in square brackets as a header.

            ```
            [<item-id>]
            breaking: <one-line sdk breaking change>
            category: [emitter change | conversion-by design | conversion-need resolve | spec change | unknown]

            [<next-item-id>]
            breaking: <one-line sdk breaking change>
            category: [emitter change | conversion-by design | conversion-need resolve | spec change | unknown]
            ```

            **Rules:**
            - The `[<item-id>]` header MUST match the exact ID from each feedback item
            - category must be exactly one of: TSP_APPLICABLE, CODE_CUSTOMIZATION, SUCCESS, or REQUIRES_MANUAL_INTERVENTION
            - Reason must clearly state which condition triggered the classification
            - For TSP_APPLICABLE: mention which TypeSpec decorator(s) can address the feedback
            - For CODE_CUSTOMIZATION: explain what code changes are needed with specific repair instructions in the Reason
            - For REQUIRES_MANUAL_INTERVENTION: explain why no TypeSpec decorator or automated patch applies
            - For SUCCESS: explain why the feedback is non-actionable or already resolved
            - Do NOT include Next Action or step-by-step guidance (that is handled separately)
            - Output ALL items — every single item ID must appear in your response
            - Do NOT add any text before or after the classification blocks
            """;
        }

    }
}
