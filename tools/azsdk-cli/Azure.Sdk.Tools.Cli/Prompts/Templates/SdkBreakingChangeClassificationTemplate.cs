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
            You are a classifier for SDK breaking changes.
            You analyze multiple SDK changes, identify and classify SDK breaking changes, assign a category to each, and provide a resolution when one exists.
            Your task is to {Description} by determining the breaking changes and their categories. You MUST review each SDK breaking change entry in `sdk changes ### Breaking Changes` exactly once.
            After reviewing all entries, merge related entries into one classified SDK breaking change only when they clearly share the same root cause, then provide the matched resolution when one exists.
            If an SDK breaking change entry in `sdk changes ### Breaking Changes` does not match any provided SDK breaking change pattern, keep the original breaking change text unchanged.

            ## SAFETY GUIDELINES
            - Follow the Azure SDK breaking change policy and guidelines as defined in the provided SDK breaking change pattern document.
            - Refuse requests involving sensitive data - ask for clarification if uncertain
            - Provide accurate, actionable classifications based on TypeSpec capabilities
            """;
        }

        private string BuildTaskInstructions()
        {
            var referenceTypeSpecInstruction = string.IsNullOrEmpty(_tspProjectPath) ? "" : $"""
                When identifying an SDK breaking change, you **must** search the TypeSpec code in the provided TypeSpec project `{_tspProjectPath}` for the matching **spec pattern** in the SDK Breaking Change Pattern Document.
                """;

            return $"""
            **Current Context:**
            - Language: {_language}

            **Task:**
            Analyze the following SDK changes and classify each SDK breaking change based on the provided SDK breaking change pattern document.
            {referenceTypeSpecInstruction}
            Compare each SDK change against the patterns and conditions in the document to determine whether it is a breaking change, assign the correct category, and retrieve the resolution when one is available in the matched pattern.
            First pass requirement: iterate through every entry in `sdk changes ### Breaking Changes` and reference to `sdk changes in ### Features Added` if any to determine its root-cause candidate.
            Merge entries that share the same root cause into one classified breaking change, for example: "model 'User' renamed to 'Customer'". Include every related breaking caused by that root change (direct and cascading) in the same merged breaking change.
            This includes all related changes across models, properties, enums, operations, parameters, return types, overloads, convenience methods, and serialization shape changes triggered by the same root cause.
            Do not omit related breakings, and do not split a single root cause into multiple classified breaking changes.
            Keep each classified SDK breaking change atomic: one classified breaking change must represent exactly one root cause.

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
            if (!string.IsNullOrEmpty(_tspProjectPath))
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
            return $$"""
            **CRITICAL: Required Output Format**

            Return exactly one valid JSON object following this exact format:
            {
                "hasBreakingChange": true, //if any breaking changes are detected, otherwise false
                "breakingChanges": //classified SDK breaking changes
                [
                    {
                        "breakingChange": "<one-line sdk breaking change>",
                        "category": "<emitter change | conversion-by design | conversion-need resolve | spec change | unknown>",
                        "resolution": "<one-line mitigation resolution for this sdk breaking change, optional>",
                        "originBreaks": [
                            "<exact original breaking change #1 from sdk changes ### Breaking Changes>",
                            "<exact original breaking change #2 from sdk changes ### Breaking Changes>"
                        ]
                    }
                ]
            }
            Output must be raw JSON only.
            Do not wrap the JSON in markdown code fences.
            Do not include any text before or after the JSON object.
            The first character must be `{` and the last character must be `}`.
            The JSON must be parseable by a standard JSON parser (RFC 8259): no comments, no trailing commas.

            **Rules:**
            **General Requirements**
            {{breakingReferenceInstruction}}
            - Every original breaking entry from sdk changes ### Breaking Changes must be processed.
            - No original breaking entry may be skipped.
            - No original breaking entry may appear in more than one classified breaking changes.
            **SDK breaking change Rule**
            When describing a SDK breaking change:
            - Report the change from the SDK consumer's perspective.
            - Use the SDK type, method, property, enum, parameter, response type, or namespace names that developers interact with.
            - Clearly state what SDK element changed and how it changed (renamed, removed, moved, type changed, requiredness changed, visibility changed, etc.).
            - Avoid reporting internal service-model or code-generation changes unless they directly affect the SDK surface.
            - When a service model maps to an SDK type with a different name, describe the SDK type change and optionally mention the corresponding service model for context.e.g Struct ResourceInfo for model WebPubSubResource renamed to Resource.
            - Prefer concise, user-impact-focused wording that helps SDK consumers understand what code may need to be updated.
            - If one entry shows that `struct A` was removed and another shows that `struct B` was added, examine related entries together. For example, if another entry shows an operation parameter type changing from `A` to `B`, treat the combined evidence as a likely model rename from `A` to `B` rather than as unrelated changes and merged those as one classified breaking change.
            **Merging Rules**
            When a root SDK element (e.g., a model, enum, or operation) is renamed or restructured, all downstream breaking changes caused by that same root change MUST be merged into one classified breaking change. Examples:
            - If model 'User' is renamed to 'Customer', every breaking change involving properties, parameters, or return types changing from 'User' to 'Customer' is part of the same root change and must be reported as one merged breaking change.
            - If an enum is renamed, all usages of that enum across models and operations are part of the same root change and must be merged.
            - If an operation signature changes (e.g., parameter added or removed), all related cascading breakings (overloads and convenience methods) are part of the same root change and must be merged.
            - Do NOT output separate breaking changes for occurrences that share the same root cause.
            - Merge only when multiple input breaking entries clearly share:
                - the same root cause (e.g. a model rename, enum rename, operation signature change), and
                - the same classified breaking intent (e.g. all are model property renames, or all are operation parameter removals).
            - Do **not** split a single root-cause breaking into multiple ones.
            - The breakingChange field must describe exactly one root change.
            **Split Rules**
            - If a candidate breaking description contains multiple root changes, split it into separate breaking changes.
            - One `breakingChange` must describe exactly one classified SDK breaking change (one root change only).
            - If a candidate `breakingChange` sentence contains two root changes joined by words like `and`, commas, or multiple rename clauses, split into separate breaking changes.
            - Example split rule: `Model ResourceInfo renamed to Resource, and ResourceInfoList renamed to ResourceList` MUST be two breaking changes: one for `ResourceInfo -> Resource`, one for `ResourceInfoList -> ResourceList`.

            **Category Definitions**
            When categorizing a breaking change, `category` must be exactly one of: `emitter change`, `conversion-by design`, `conversion-need resolve`, `spec change`, `unknown`.
            - **Emitter Change**: The breaking change is caused by SDK code-generation or emitter logic changes, rather than a service specification change.
            - **Conversion-by Design**: The breaking change results from migrating OpenAPI/Swagger definitions to TypeSpec and is expected by design; no resolution is required.
            - **Conversion-Need Resolve**: The breaking change results from migrating OpenAPI/Swagger definitions to TypeSpec and requires a resolution.
            - **Spec Change**: The breaking change is caused by a TypeSpec service specification change, such as an API contract update, data-model update, or other service-level change that affects the SDK.
            - **Unknown**: The cause of the breaking change cannot be determined from the available input and evidence.
            **originBreaks Rules**
            - originBreaks is required.
            - It must contain all original breaking entries that contribute to the classified breaking.
            - Each original breaking entry must appear in exactly one classified breaking change.
            - Origin breaking entries must come from the input `### Breaking Changes` section of sdk changes.
            - Requirements:
                - Use a JSON array of strings.
                - Preserve the exact original text from sdk changes `### Breaking Changes` section.
                - Do not paraphrase.
                - Do not modify wording.
                - Do not remove details.
                - Do not duplicate entries.
            **Mandatory Merge Coverage Requirement**
            - If a merged breaking change is caused by N original breaking entries:
                - originBreaks must contain exactly N entries.
                - Every contributing input breaking entry must appear as a separate array entry.
            **Consistency Validation (MANDATORY)**
            Before producing the final answer:
            - Classify every original breaking entry.
            - Ensure each original breaking entry appears in exactly one classified breaking change.
            - Verify no original breaking entry is omitted.
            - Verify no original breaking entry is duplicated.
            - Verify every merged breaking change includes all contributing original breaking entries.
            - Verify the total number of entries across all originBreaks equals the total number of original breaking entries from the input `### Breaking Changes` section.
            **Forbidden Pattern**
            A merged breaking change absorbs multiple original breaking entries but lists only part of them in originBreaks.
            **Required Pattern**
            - Every merged root-cause breaking change must list all contributing original breaking entries.
            **Output Restrictions**
            - Do not include summaries.
            - Do not include next actions.
            - Output only the classified breaking changes.
            """;
        }
    }
}
