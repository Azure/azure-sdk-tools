package model

var CommonKeywordReplaceMap = map[string]string{
	"tsp":          "typespec",
	"oa3":          "openapi3",
	"tcgc":         "typespec-client-generator-core",
	"dpg":          "data plane",
	"mpg":          "management plane",
	"arm":          "Azure Resource Manager",
	"common types": "Common Types to Azure Resource Manager (ARM)",
	"common-types": "Common Types to Azure Resource Manager (ARM)",
	"swagger":      "Open API",
}

const RerankScoreLowRelevanceThreshold = 2
const RerankScoreMediumRelevanceThreshold = 2
const RerankScoreRelevanceThreshold = 2.7
const RerankScoreHighRelevanceThreshold = 4
