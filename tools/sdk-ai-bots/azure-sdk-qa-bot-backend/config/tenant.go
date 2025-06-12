package config

import "github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"

type TenantConfig struct {
	Sources        []model.Source
	PromptTemplate string
}

var typespecSources = []model.Source{
	model.Source_TypeSpecAzure,
	model.Source_AzureRestAPISpec,
	model.Source_TypeSpecQA,
	model.Source_TypeSpec,
}

var tenantConfigMap = map[model.TenantID]TenantConfig{
	model.TenantID_PythonChannelQaBot: {
		Sources:        append(typespecSources, model.Source_AzureSDKForPython, model.Source_AzureSDKForPythonWiki),
		PromptTemplate: "language_channel.md",
	},
	model.TenantID_AzureSDKQaBot: {
		PromptTemplate: "typespec.md",
		Sources:        typespecSources,
	},
}

func GetTenantConfig(tenantID model.TenantID) (TenantConfig, bool) {
	config, ok := tenantConfigMap[tenantID]
	if !ok {
		return TenantConfig{}, false
	}
	return config, true
}
