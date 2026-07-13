// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Prompts.Templates
{
    public class SdkBreakingChangeClassificationTemplate : BasePromptTemplate
    {
        public override string TemplateId => "sdk-breaking-change-classification";
        public override string Version => "1.0.0";
        public override string Description => "Classify SDK Breaking Changes";

        private readonly string _sdkBreakingPatternContent;
        private readonly string _sdkChanges;
        private readonly string _language;
        private readonly string? _tspProjectPath;

        public SdkBreakingChangeClassificationTemplate(string sdkBreakingPatternContent, string sdkChanges, string language, string? tspProjectPath)
        {
            _sdkBreakingPatternContent = sdkBreakingPatternContent;
            _sdkChanges = sdkChanges;
            _language = language;
            _tspProjectPath = tspProjectPath;
        }

        public override string BuildPrompt()
        {
            var taskInstructions = BuildTaskInstructions();
            var outputRequirements = BuildOutputRequirements();

            return BuildStructuredPrompt(taskInstructions, null, null, outputRequirements);
        }

        protected override string BuildSystemRole()
        {
            return $"""
            ## SYSTEM ROLE
            You are a batch classifier for the SDK breaking change detection workflow. You analyze multiple SDK changes and classify each one.
            Your task is to {Description} by determining the breaking changes and their categories. You MUST review each SDK breaking change item in `sdk changes ### Breaking Changes` exactly once. After reviewing all items, merge related breakings into one classified SDK breaking change only when they clearly share the same root cause, then provide the matched resolution when one exists.
            Each classified SDK breaking change must be output as one separate block. If an SDK breaking change item does not match any provided SDK breaking change pattern, keep the original breaking change text unchanged.

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
            var referenceTypeSpecInstruction = string.IsNullOrEmpty(_tspProjectPath) ? "" : $"""
                when identify SDK breaking change, search in the typespec code in the provided TypeSpec project `{_tspProjectPath}` to check if it match the **spec pattern** in the SDK Breaking Change Pattern Document.
                """;

            return $"""
            **Current Context:**
            - Language: {_language}

            **Task:**
            {referenceTypeSpecInstruction}
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
                breakingReferenceInstruction += $"""

                    **How to identify the canonical TypeSpec element name from the TypeSpec project:**
                    The TypeSpec project is located at `{_tspProjectPath}`. Follow these steps to resolve the correct TypeSpec element name for each breaking change:

                    1. **Start with `client.tsp`** (if it exists): This file contains client-layer customizations such as `@clientName`, `@@clientName`, `@access`, or model/operation renames applied via augment decorators. If the SDK-level name in the breaking change matches a customized name here, the *origin* TypeSpec name is the one being decorated — use that origin name.

                    2. **Check `back-compatible.tsp`** (or similarly named compatibility files, e.g. `BackCompatible.tsp`): These files suppress or alias breaking changes introduced during Swagger-to-TypeSpec conversion. An element listed here indicates it was added back for compatibility; its origin TypeSpec name may differ from the SDK-visible name.

                    3. **Read the core `.tsp` files** (e.g. `main.tsp`, `models.tsp`, `operations.tsp`, or any other `.tsp` files in the directory): These define the authoritative TypeSpec models, enums, operations, and properties. Use the identifier exactly as declared with `model`, `enum`, `op`, `interface`, or `union` keywords.

                    4. **Mapping rules:**
                       - If the SDK breaking change references a name that differs from what is declared in the core `.tsp` files, look for a `@clientName` or `@@clientName` decorator in `client.tsp` that maps the TypeSpec name to the SDK name. Use the TypeSpec name (the decorated target), not the SDK name.
                       - If no mapping is found in `client.tsp`, use the name as declared in the core `.tsp` files verbatim.
                       - For properties and enum values, qualify the name with its parent: e.g. `Model Foo property bar`, `Enum Status value Active`.

                    5. **When the element cannot be found** in any `.tsp` file, fall back to the SDK-level name from the breaking change and note it as unresolved.
                    """;
            }
            return $"""
            **CRITICAL: Required Output Format**
        
            You MUST output one block per classified SDK breaking change item (one merged root-cause item per block).

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
            **General Requirements**
            - The `[<item-id>]` header refers to the SDK breaking change type.
            {breakingReferenceInstruction}
            - Output exactly one block per classified SDK breaking change.
            - Every original breaking item from sdk changes ### Breaking Changes must be processed.
            - No original breaking item may be skipped.
            - No original breaking item may appear in more than one block.
            **SDK breaking change Rule**
            When describing a SDK breaking change:
            - Report the change from the SDK consumer's perspective.
            - Use the SDK type, method, property, enum, parameter, response type, or namespace names that developers interact with.
            - If available, include the originating typespec entity as additional context, but do not make it the primary subject of the breaking change.
            - Clearly state what SDK element changed and how it changed (renamed, removed, moved, type changed, requiredness changed, visibility changed, etc.).
            - Avoid reporting internal service-model or code-generation changes unless they directly affect the SDK surface.
            - When a service model maps to an SDK type with a different name, describe the SDK type change and optionally mention the corresponding service model for context.e.g Struct ResourceInfo for model WebPubSubResource renamed to Resource.
            - Prefer concise, user-impact-focused wording that helps SDK consumers understand what code may need to be updated.
            **Merging Rules**
            - Merge only when multiple input breaking items clearly share:
                - the same root cause (e.g. a model rename, enum rename, operation signature change), and
                - the same classified breaking intent (e.g. all are model property renames, or all are operation parameter removals).
            - Do **not** merge unrelated breakings merely because they affect similar models or APIs.
            - Do **not** split a single root-cause breaking into multiple blocks.
            - The breaking field must describe exactly one root change.
            **Split Rules**
            - If a candidate breaking description contains multiple root changes, split it into separate blocks.
            - The `breaking` line must describe exactly one classified SDK breaking change (one root change only).
            - If a candidate `breaking` sentence contains two root changes joined by words like `and`, commas, or multiple rename clauses, split into separate blocks.
            - Example split rule: `Model ResourceInfo renamed to Resource, and ResourceInfoList renamed to ResourceList` MUST be two blocks: one for `ResourceInfo -> Resource`, one for `ResourceInfoList -> ResourceList`.
            - category must be exactly one of: emitter change, conversion-by design, conversion-need resolve, spec change, unknown
            **originBreaks Rules**
            - originBreaks is required.
            - It must contain all original breaking items that contribute to the classified breaking.
            - Requirements:
                - Use a bullet list (- item).
                - Preserve the exact original text from sdk changes ### Breaking Changes.
                - Do not paraphrase.
                - Do not modify wording.
                - Do not remove details.
                - Do not duplicate entries.
            **Mandatory Merge Coverage Requirement**
            - If a merged item is caused by N original breaking items:
                - originBreaks must contain exactly N bullets
                - Every contributing input breaking must appear as a separate bullet.
            **Consistency Validation (MANDATORY)**
            Before producing the final answer:
            - Classify every original breaking item.
            - Ensure each original breaking appears in exactly one block.
            - Verify no original breaking is omitted.
            - Verify no original breaking is duplicated.
            - Verify every merged block includes all contributing original breakings.
            - Verify the total number of bullets across all originBreaks equals the total number of original breaking items from the input.
            **Forbidden Pattern**
            A merged block absorbs multiple original breakings but lists only one entry in originBreaks.
            **Required Pattern**
            - Every merged root-cause block must list all contributing original breakings.
            **Output Restrictions**
            - Do not include explanations outside the required blocks.
            - Do not include summaries.
            - Do not include next actions.
            - Output only the classified breaking change blocks.
            """;
        }
    }
}
