package model

var KeywordReplaceMap = map[string]string{
	"tsp":  "typespec",
	"oa3":  "openapi3",
	"tcgc": "typespec-client-generator-core",
}

const RerankScoreLowRelevanceThreshold = 2
const RerankScoreRelevanceThreshold = 3
const RerankScoreHighRelevanceThreshold = 4
