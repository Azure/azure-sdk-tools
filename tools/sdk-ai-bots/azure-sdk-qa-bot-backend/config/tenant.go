package config

import "github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"

type TenantConfig struct {
	Sources        []model.Source
	PromptTemplate string
}

var typespecSources = []model.Source{
	model.Source_TypeSpecAzure,
	model.Source_TypeSpecQA,
	model.Source_TypeSpec,
	model.Source_AzureRestAPISpec,
	model.Source_TypeSpecMigration,
}

var tenantConfigMap = map[model.TenantID]TenantConfig{
	model.TenantID_PythonChannelQaBot: {
		Sources:        append([]model.Source{model.Source_AzureSDKForPython, model.Source_AzureSDKForPythonWiki}, typespecSources...),
		PromptTemplate: "language_channel.md",
	},
	model.TenantID_AzureSDKQaBot: {
		PromptTemplate: "typespec.md",
		Sources:        append(typespecSources, model.Source_AzureResourceManagerRPC, model.Source_AzureAPIGuidelines),
	},
	model.TenantID_AzureSDKOnboarding: {
		PromptTemplate: "azure_sdk_onboarding.md",
		Sources:        append([]model.Source{model.Source_AzureSDKDocsEng, model.Source_AzureSDKGuidelines}, typespecSources...),
	},
}

func GetTenantConfig(tenantID model.TenantID) (TenantConfig, bool) {
	config, ok := tenantConfigMap[tenantID]
	if !ok {
		return TenantConfig{}, false
	}
	return config, true
}
