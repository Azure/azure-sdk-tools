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
	ChannelName             string
	ChannelLink             string
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
		ChannelName:             "Language - Python",
		ChannelLink:             "https://teams.microsoft.com/l/channel/19%3Ab97d98e6d22c41e0970a1150b484d935%40thread.skype/Language%20-%20Python?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47",
	},
	model.TenantID_DotnetChannelQaBot: {
		Sources: append([]model.Source{model.Source_AzureSDKForNetDocs, model.Source_AzureSDKGuidelines, model.Source_AzureSDKDocsEng}, typespecSources...),
		SourceFilter: map[model.Source]string{
			model.Source_AzureSDKGuidelines: "search.ismatch('dotnet_*', 'title')",
		},
		PromptTemplate:          "language_channel/qa.md",
		IntentionPromptTemplate: "language_channel/intention.md",
		AgenticSearchPrompt:     "language_channel/agentic_search.md",
		ChannelName:             "Language - DotNet",
		ChannelLink:             "https://teams.microsoft.com/l/channel/19%3A7b87fb348f224b37b6206fa9d89a105b%40thread.skype/Language%20-%20DotNet?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47",
	},
	model.TenantID_GolangChannelQaBot: {
		Sources: append([]model.Source{model.Source_AzureSDKForGo, model.Source_AzureSDKGuidelines, model.Source_AzureSDKDocsEng}, typespecSources...),
		SourceFilter: map[model.Source]string{
			model.Source_AzureSDKGuidelines: "search.ismatch('golang_*', 'title')",
		},
		PromptTemplate:          "language_channel/qa.md",
		IntentionPromptTemplate: "language_channel/intention.md",
		AgenticSearchPrompt:     "language_channel/agentic_search.md",
		ChannelName:             "Language - Go",
		ChannelLink:             "https://teams.microsoft.com/l/channel/19%3A104f00188bb64ef48d1b4d94ccb7a361%40thread.skype/Language%20-%20Go?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47",
	},
	model.TenantID_JavaChannelQaBot: {
		Sources: append([]model.Source{model.Source_AzureSDKForJava, model.Source_AzureSDKForJavaWiki, model.Source_AzureSDKGuidelines, model.Source_AutorestJava, model.Source_AzureSDKDocsEng}, typespecSources...),
		SourceFilter: map[model.Source]string{
			model.Source_AzureSDKGuidelines: "search.ismatch('java_*', 'title')",
		},
		PromptTemplate:          "language_channel/qa.md",
		IntentionPromptTemplate: "language_channel/intention.md",
		AgenticSearchPrompt:     "language_channel/agentic_search.md",
		ChannelName:             "Language - Java",
		ChannelLink:             "https://teams.microsoft.com/l/channel/19%3A5e673e41085f4a7eaaf20823b85b2b53%40thread.skype/Language%20-%20Java?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47",
	},
	model.TenantID_JavaScriptChannelQaBot: {
		Sources:                 append([]model.Source{model.Source_AzureSDKForJavaScript, model.Source_AzureSDKForJavaScriptWiki, model.Source_AzureSDKGuidelines}, typespecSources...),
		PromptTemplate:          "language_channel/qa.md",
		IntentionPromptTemplate: "language_channel/intention.md",
		AgenticSearchPrompt:     "language_channel/agentic_search.md",
		ChannelName:             "Language – JS ＆ TS 🥷",
		ChannelLink:             "https://teams.microsoft.com/l/channel/19%3A344f6b5b36ba414daa15473942c7477b%40thread.skype/Language%20%E2%80%93%20JS%E2%80%89%EF%BC%86%E2%80%89TS%20%F0%9F%A5%B7?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47",
	},
	model.TenantID_AzureSDKQaBot: {
		PromptTemplate: "typespec/qa.md",
		Sources:        append(typespecSources, model.Source_AzureSDKDocsEng),
		SourceFilter: map[model.Source]string{
			model.Source_AzureSDKDocsEng: "search.ismatch('design*', 'title')",
		},
		IntentionPromptTemplate: "typespec/intention.md",
		AgenticSearchPrompt:     "typespec/agentic_search.md",
		ChannelName:             "TypeSpec Discussion",
		ChannelLink:             "https://teams.microsoft.com/l/channel/19%3A906c1efbbec54dc8949ac736633e6bdf%40thread.skype/TypeSpec%20Discussion?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47",
	},
	model.TenantID_AzureSDKOnboarding: {
		PromptTemplate:          "azure_sdk_onboarding/qa.md",
		Sources:                 []model.Source{model.Source_AzureSDKDocsEng},
		AgenticSearchPrompt:     "azure_sdk_onboarding/agentic_search.md",
		IntentionPromptTemplate: "azure_sdk_onboarding/intention.md",
		ChannelName:             "Azure SDK Onboarding",
		ChannelLink:             "https://teams.microsoft.com/l/channel/19%3Ade3fce22c2994be18cac50502c55f717%40thread.skype/Azure%20SDK%20Onboarding?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47",
	},
	model.TenantID_GeneralQaBot: {
		PromptTemplate:          "general/qa.md",
		IntentionPromptTemplate: "general/intention.md",
		AgenticSearchPrompt:     "general/agentic_search.md",
		EnableRouting:           true,
		ChannelName:             "General",
		ChannelLink:             "https://teams.microsoft.com/l/channel/19%3Af6d52ac6465c40ea80dc86b8be3825aa%40thread.skype/General?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47",
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
		ChannelName:             "API Spec Review",
		ChannelLink:             "https://teams.microsoft.com/l/channel/19%3A0351f5f9404446e4b4fd4eaf2c27448d%40thread.skype/API%20Spec%20Review?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47",
	},
}

func GetTenantConfig(tenantID model.TenantID) (TenantConfig, bool) {
	config, ok := tenantConfigMap[tenantID]
	if !ok {
		return TenantConfig{}, false
	}
	return config, true
}
