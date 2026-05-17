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
        private readonly string _tspProjectPath;

        public SdkBreakingChangeClassificationTemplate(string sdkBreakingPatternContent, string sdkChanges, string language, string tspProjectPath)
        {
            _sdkBreakingPatternContent = sdkBreakingPatternContent;
            _sdkChanges = sdkChanges;
            _language = language;
            _tspProjectPath = tspProjectPath;
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
            Analyze the following SDK changes and classify each change based on the provided SDK breaking change pattern document.
            Compare each SDK change against the patterns and conditions outlined in the document to determine if it constitutes a breaking change and categorize it accordingly.
            Merge the same SDK changes as one item, e.g. "model 'User' renamed to 'Customer'", and all the breakings related to the model are the same, such as 'Proprty 'user' of model `ClientUpdateResponse` type changed from `User` to `Customer`', they should be merged as one item and classified once.

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
            var breakingReferenceInstruction = "";
            if (_tspProjectPath != null)
            {
               breakingReferenceInstruction = $"- Use the typespec item in the SDK breaking change, such as model, operation. You may read the definitions in the typespec code in directory `{_tspProjectPath}` as reference when you define the sdk breaking change.";
            }
            return $"""
            **CRITICAL: Required Output Format**
        
            You MUST output one block per sdk breaking change item.

            ```
            [<item-id>]
            breaking: <one-line sdk breaking change>
            category: [emitter change | conversion-by design | conversion-need resolve | spec change | unknown]

            [<next-item-id>]
            breaking: <one-line sdk breaking change>
            category: [emitter change | conversion-by design | conversion-need resolve | spec change | unknown]
            ```

            **Rules:**
            - The `[<item-id>]` header refers to the SDK breaking change type
            {breakingReferenceInstruction}
            - category must be exactly one of: emitter change, conversion-by design, conversion-need resolve, spec change, unknown
            - Reason must clearly state which condition triggered the classification
            - For emitter change: the SDK breaking change is caused by a change in the code emitter that generates the client code
            - For conversion-by design: the SDK breaking change is caused by conversion swagger to typespec and it is accepted and does not need resolve, such as common types.
            - For conversion-need resolve: the SDK breaking change is caused by conversion swagger to typespec and need resolve
            - For spec change: the SDK breaking change is caused by a change in the service spec (swagger/typespec)
            - For unknown: the root cause of the SDK breaking change cannot be determined by the provided information
            - Do NOT include Next Action or step-by-step guidance (that is handled separately)
            """;
        }

    }
}
