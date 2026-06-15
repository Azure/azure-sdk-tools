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
            Your task: {Description} and determine the breaking changes and the category it belongs. You MUST go through each sdk breaking change item in `sdk changes ### Breaking Changes` and evaluate it exactly once. After evaluating all items, merge related breakings into one classified SDK breaking change only when they share the same root cause, then provide the resolution if any in the matched pattern.
            Each classified SDK breaking change must be output as one separate block. If an sdk breaking change item does not match any provided SDK breaking change patterns, report the original sdk breaking change.

            ## MERGING RULES
            When a root SDK element (e.g. a model, enum, or operation) is renamed or restructured, all downstream breaking changes caused by that same root change MUST be merged into a single item. Examples:
            - If model 'User' is renamed to 'Customer', every breaking change that references a property, parameter, or return type changing from 'User' to 'Customer' is part of the same root change and must be reported as one merged item.
            - If an enum is renamed, all usages of that enum across different models or operations are part of the same root change and must be merged.
            - If an operation signature changes (e.g. parameter added or removed), all related cascading breakings (overloads, convenience methods) are part of the same root change and must be merged.
            Do NOT create separate items for each occurrence of a breaking change that shares the same root cause.

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
            Compare each SDK change against the patterns and conditions outlined in the document to determine if it constitutes a breaking change and categorize it accordingly. And retrieve the resolution if any in the matched pattern.
            First pass requirement: iterate through every item in input `sdk changes ### Breaking Changes` and determine its root cause candidate.
            Merge SDK changes that have the same root cause into one item, e.g. "model 'User' renamed to 'Customer'", and include every related breaking caused by that root change (direct and cascading) in that same merged item.
            This includes all related changes across models, properties, enums, operations, parameters, return types, overloads, convenience methods, and serialization shape changes that are triggered by the same root cause.
            Do not omit related breakings, and do not split one root cause into multiple classified items.
            Keep each classified SDK breaking change atomic: one block must represent exactly one root SDK change.

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
            var breakingReferenceInstruction = "- Use the TypeSpec element name (model, operation, enum, or property) as the target in the breaking change. For example: `[Model Foo renamed]`, `[Operation Bar parameter removed]`, `[Enum Baz value removed]`.";
            if (_tspProjectPath != null)
            {
               breakingReferenceInstruction += $" Read the TypeSpec definitions in directory `{_tspProjectPath}` to obtain the canonical TypeSpec element names used in each breaking change description.";
            }
            return $"""
            **CRITICAL: Required Output Format**
        
            You MUST output one block per classified sdk breaking change item (one merged root-cause item per block).

            ```
            [<item-id>]
            breaking: <one-line sdk breaking change>
            category: [emitter change | conversion-by design | conversion-need resolve | spec change | unknown]
            resolution: <one-line mitigation resolution for this sdk breaking change, optional>
            originBreaks:
            - <exact original breaking change #1 from sdk changes ### Breaking Changes>
            - <exact original breaking change #2 from sdk changes ### Breaking Changes>
            - ...
            ```

            [<next-item-id>]
            breaking: <one-line sdk breaking change>
            category: [emitter change | conversion-by design | conversion-need resolve | spec change | unknown]
            resolution: <one-line mitigation resolution for this sdk breaking change, optional>
            originBreaks:
            - <exact original breaking change #1 from sdk changes ### Breaking Changes>
            - <exact original breaking change #2 from sdk changes ### Breaking Changes>
            - ...
            ```

            **Rules:**
            - The `[<item-id>]` header refers to the SDK breaking change type.
            {breakingReferenceInstruction}
            - One classified SDK breaking change must map to one separate output block.
            - One classified SDK breaking change may be caused by several original breakings from input `sdk changes ### Breaking Changes`.
            - Every input breaking item must be processed; do not skip any input item.
            - Merge is conditional: only merge items that clearly share the same root cause and same classified breaking intent.
            - Do NOT split one classified SDK breaking change into multiple blocks.
            - The `breaking` line must describe exactly one classified SDK breaking change (one root change only).
            - If a candidate `breaking` sentence contains two root changes joined by words like `and`, commas, or multiple rename clauses, split into separate blocks.
            - Example split rule: `Model ResourceInfo renamed to Resource, and ResourceInfoList renamed to ResourceList` MUST be two blocks: one for `ResourceInfo -> Resource`, one for `ResourceInfoList -> ResourceList`.
            - category must be exactly one of: emitter change, conversion-by design, conversion-need resolve, spec change, unknown
            - `originBreaks` is REQUIRED and must be a bullet list (`- ...`) containing ALL related original breakings for that merged root-cause item from input `sdk changes ### Breaking Changes`.
            - If a merged item is caused by N related original breakings, `originBreaks` must contain exactly N bullets for that item (not 1 unless N = 1).
            - `originBreaks` items must use the exact original text from input (no paraphrasing), with no duplicates and no missing related entries.
            - If the `breaking` summary says words like `affecting`, `including`, `across`, or `all references`, `originBreaks` must list multiple bullets whenever multiple matching input breakings exist.
            - Before finalizing output, run a coverage check: every original breaking in `sdk changes ### Breaking Changes` must appear in exactly one item's `originBreaks`.
            - Count check is REQUIRED: the total number of bullets across all `originBreaks` lists must be exactly equal to the number of items in input `sdk changes ### Breaking Changes`.
            - If the counts do not match, revise classification and `originBreaks` until they match exactly.
            - Never output a merged item with only one `originBreaks` bullet when multiple related breakings for the same root cause are present in input.
            - Invalid pattern: `breaking` claims broad impact but `originBreaks` has only one bullet while more related input items exist.
            - Valid pattern: each merged root-cause block contains every matching original input breaking as separate bullets in `originBreaks`.
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
