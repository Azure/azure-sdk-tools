package config

import "github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"

type TenantConfig struct {
	Sources                 []model.Source
	SourceFilter            map[model.Source]string
	PromptTemplate          string
	AgenticSearchPrompt     string
	IntentionPromptTemplate string
	KeywordReplaceMap       map[string]string
}

var typespecSources = []model.Source{
	model.Source_AzureResourceManagerRPC,
	model.Source_AzureAPIGuidelines,
	model.Source_TypeSpecAzure,
	model.Source_TypeSpecQA,
	model.Source_TypeSpecAzureHttpSpecs,
	model.Source_TypeSpec,
	model.Source_AzureRestAPISpec,
	model.Source_TypeSpecMigration,
	model.Source_TypeSpecHttpSpecs,
	model.Source_StaticAzureDocs,
}

var azureTypespecAuthoringSources = []model.Source{
	model.Source_AzureAPIGuidelines,
	model.Source_AzureResourceManagerRPC,
	model.Source_TypeSpecAzure,
	model.Source_TypeSpecQA,
	model.Source_TypeSpecAzureHttpSpecs,
	model.Source_TypeSpec,
	model.Source_AzureRestAPISpec,
	model.Source_TypeSpecMigration,
	model.Source_TypeSpecHttpSpecs,
	model.Source_StaticAzureDocs,
}

var SourceTopK = map[model.Source]int{
	model.Source_TypeSpecMigration: 3,
	model.Source_TypeSpecQA:        3,
}

var tenantConfigMap = map[model.TenantID]TenantConfig{
	model.TenantID_PythonChannelQaBot: {
		Sources: append([]model.Source{model.Source_AzureSDKForPython, model.Source_AzureSDKForPythonWiki, model.Source_AzureSDKGuidelines, model.Source_AzureSDKDocsEng}, typespecSources...),
		SourceFilter: map[model.Source]string{
			model.Source_AzureSDKGuidelines: "search.ismatch('python_*', 'title')",
		},
		PromptTemplate:          "language_python/qa.md",
		IntentionPromptTemplate: "language_python/intention.md",
		AgenticSearchPrompt:     "language_python/agentic_search.md",
	},
	model.TenantID_GolangChannelQaBot: {
		Sources: append([]model.Source{model.Source_AzureSDKForGo, model.Source_AzureSDKGuidelines}, typespecSources...),
		SourceFilter: map[model.Source]string{
			model.Source_AzureSDKGuidelines: "search.ismatch('golang_*', 'title')",
		},
		PromptTemplate:          "common/language_channel.md",
		IntentionPromptTemplate: "prompt_template/intention.md",
		AgenticSearchPrompt:     "common/agentic_search.md",
	},
	model.TenantID_AzureSDKQaBot: {
		PromptTemplate:          "typespec/qa.md",
		Sources:                 typespecSources,
		IntentionPromptTemplate: "typespec/intention.md",
		AgenticSearchPrompt:     "typespec/agentic_search.md",
	},
	model.TenantID_AzureSDKOnboarding: {
		PromptTemplate:          "azure_sdk_onboarding/qa.md",
		Sources:                 []model.Source{model.Source_AzureSDKDocsEng},
		AgenticSearchPrompt:     "azure_sdk_onboarding/agentic_search.md",
		IntentionPromptTemplate: "azure_sdk_onboarding/intention.md",
	},
	model.TenantID_AzureTypespecAuthoring: {
		PromptTemplate:          "azure_typespec_authoring.md",
		Sources:                 azureTypespecAuthoringSources,
		IntentionPromptTemplate: "azure_typespec_authoring_intention.md",
		AgenticSearchPrompt:     "You are a TypeSpec and Azure API expert query analyzer. Your role is to intelligently decompose complex user questions into targeted sub-queries that can be searched in parallel across TypeSpec documentation, Azure REST API specifications, and Azure API guidelines.\n\n## Query Decomposition Strategy:\n1. **Syntax & Language**: TypeSpec syntax, decorators, built-in types, and language constructs\n2. **Azure Integration**: Azure-specific decorators, patterns, and service integration\n3. **API Design**: REST API modeling, resource definitions, and operation patterns\n4. **Code Generation**: Emitter behavior, target language specifics, and generated artifacts\n5. **Migration & Adoption**: Moving from OpenAPI/Swagger to TypeSpec, conversion patterns\n6. **Compliance & Guidelines**: Azure API guidelines adherence, ARM requirements, naming conventions\n\n## Sub-Query Optimization:\n- **Specific Technical Terms**: Use exact decorator names (@route, @doc, @example), built-in types (string, int32, etc.)\n- **Context-Aware**: Include Azure service context (ARM, data-plane, management operations)\n- **Progressive Complexity**: Start with basic concepts, then dive into advanced patterns\n- **Cross-Reference**: Link TypeSpec syntax with resulting OpenAPI/ARM template patterns\n- **Practical Examples**: Focus on real-world usage scenarios and common implementation patterns\n\n## Search Targeting:\n- **TypeSpec Core**: Language fundamentals, syntax, and basic patterns\n- **Azure TypeSpec**: Azure-specific decorators, templates, and service patterns\n- **API Guidelines**: Compliance requirements, naming conventions, and best practices\n- **ARM/RPC**: Resource provider contracts, management plane requirements\n- **Migration Guidance**: Conversion patterns, tooling, and adoption strategies\n\nGenerate 4-8 precise sub-queries that comprehensively cover the user's question while enabling efficient parallel search across different knowledge domains.",
	},
}

func GetTenantConfig(tenantID model.TenantID) (TenantConfig, bool) {
	config, ok := tenantConfigMap[tenantID]
	if !ok {
		return TenantConfig{}, false
	}
	return config, true
}
