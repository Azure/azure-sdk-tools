package config

import "github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"

type TenantConfig struct {
	Sources                 []model.Source
	SourceFilter            map[model.Source]string
	PromptTemplate          string
	AgenticSearchPrompt     string
	IntentionPromptTemplate string
	KeywordReplaceMap       map[string]string
	EnableRouting           bool
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
	model.Source_StaticTypeSpecToSwaggerMapping,
}

var SourceTopK = map[model.Source]int{
	model.Source_TypeSpecMigration:              3,
	model.Source_TypeSpecQA:                     3,
	model.Source_StaticTypeSpecToSwaggerMapping: 3,
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
	model.TenantID_DotnetChannelQaBot: {
		Sources: append([]model.Source{model.Source_AzureSDKForNetDocs, model.Source_AzureSDKGuidelines, model.Source_AzureSDKDocsEng}, typespecSources...),
		SourceFilter: map[model.Source]string{
			model.Source_AzureSDKGuidelines: "search.ismatch('dotnet_*', 'title')",
		},
		PromptTemplate:          "language_channel/qa.md",
		IntentionPromptTemplate: "language_channel/intention.md",
		AgenticSearchPrompt:     "language_channel/agentic_search.md",
	},
	model.TenantID_GolangChannelQaBot: {
		Sources: append([]model.Source{model.Source_AzureSDKForGo, model.Source_AzureSDKGuidelines, model.Source_AzureSDKDocsEng}, typespecSources...),
		SourceFilter: map[model.Source]string{
			model.Source_AzureSDKGuidelines: "search.ismatch('golang_*', 'title')",
		},
		PromptTemplate:          "language_channel/qa.md",
		IntentionPromptTemplate: "language_channel/intention.md",
		AgenticSearchPrompt:     "language_channel/agentic_search.md",
	},
	model.TenantID_JavaChannelQaBot: {
		Sources: append([]model.Source{model.Source_AzureSDKForJava, model.Source_AzureSDKForJavaWiki, model.Source_AzureSDKGuidelines, model.Source_AutorestJava, model.Source_AzureSDKDocsEng}, typespecSources...),
		SourceFilter: map[model.Source]string{
			model.Source_AzureSDKGuidelines: "search.ismatch('java_*', 'title')",
		},
		PromptTemplate:          "language_channel/qa.md",
		IntentionPromptTemplate: "language_channel/intention.md",
		AgenticSearchPrompt:     "language_channel/agentic_search.md",
	},
	model.TenantID_JavaScriptChannelQaBot: {
		Sources: append([]model.Source{model.Source_AzureSDKForJavaScript, model.Source_AzureSDKForJavaScriptWiki, model.Source_AzureSDKGuidelines, model.Source_AzureSDKDocsEng}, typespecSources...),
		SourceFilter: map[model.Source]string{
			model.Source_AzureSDKGuidelines: "search.ismatch('typescript_*', 'title')",
		},
		PromptTemplate:          "language_channel/qa.md",
		IntentionPromptTemplate: "language_channel/intention.md",
		AgenticSearchPrompt:     "language_channel/agentic_search.md",
	},
	model.TenantID_AzureSDKQaBot: {
		PromptTemplate: "typespec/qa.md",
		Sources:        append(typespecSources, model.Source_AzureSDKDocsEng),
		SourceFilter: map[model.Source]string{
			model.Source_AzureSDKDocsEng: "search.ismatch('design*', 'title')",
		},
		IntentionPromptTemplate: "typespec/intention.md",
		AgenticSearchPrompt:     "typespec/agentic_search.md",
	},
	model.TenantID_AzureSDKOnboarding: {
		PromptTemplate:          "azure_sdk_onboarding/qa.md",
		Sources:                 []model.Source{model.Source_AzureSDKDocsEng},
		AgenticSearchPrompt:     "azure_sdk_onboarding/agentic_search.md",
		IntentionPromptTemplate: "azure_sdk_onboarding/intention.md",
	},
	model.TenantID_GeneralQaBot: {
		PromptTemplate:          "general/qa.md",
		IntentionPromptTemplate: "general/intention.md",
		AgenticSearchPrompt:     "general/agentic_search.md",
		EnableRouting:           true,
	},
	model.TenantID_APISpecReviewBot: {
		PromptTemplate: "api_spec_review/qa.md",
		Sources:        []model.Source{model.Source_StaticAzureDocs, model.Source_AzureRestAPISpec, model.Source_AzureSDKDocsEng},
		SourceFilter: map[model.Source]string{
			model.Source_AzureSDKDocsEng: "search.ismatch('design*', 'title')",
		},
		IntentionPromptTemplate: "api_spec_review/intention.md",
		AgenticSearchPrompt:     "api_spec_review/agentic_search.md",
		EnableRouting:           true,
	},
}

func GetTenantConfig(tenantID model.TenantID) (TenantConfig, bool) {
	config, ok := tenantConfigMap[tenantID]
	if !ok {
		return TenantConfig{}, false
	}
	return config, true
}
