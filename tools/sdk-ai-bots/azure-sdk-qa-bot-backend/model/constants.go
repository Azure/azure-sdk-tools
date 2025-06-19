package model

var KeywordReplaceMap = map[string]string{
	"tsp":          "typespec",
	"oa3":          "openapi3",
	"tcgc":         "typespec-client-generator-core",
	"dpg":          "data plane",
	"mpg":          "management plane",
	"arm":          "Azure Resource Manager",
	"common types": "Common Types to Azure Resource Manager (ARM)",
	"common-types": "Common Types to Azure Resource Manager (ARM)",
}

const RerankScoreLowRelevanceThreshold = 2
const RerankScoreRelevanceThreshold = 3
const RerankScoreHighRelevanceThreshold = 4
