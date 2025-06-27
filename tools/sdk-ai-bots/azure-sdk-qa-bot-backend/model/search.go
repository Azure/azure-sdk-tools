package model

import (
	"strings"
)

type QueryIndexRequest struct {
	Count                 bool          `json:"count,omitempty";`
	Search                string        `json:"search,omitempty"`
	Select                string        `json:"select,omitempty"`
	Top                   int           `json:"top,omitempty"`
	VectorQueries         []VectorQuery `json:"vectorQueries,omitempty"`
	OrderBy               string        `json:"orderby,omitempty"`
	Filter                string        `json:"filter,omitempty"`
	QueryType             string        `json:"queryType,omitempty"`
	SemanticConfiguration string        `json:"semanticConfiguration,omitempty"`
	Captions              string        `json:"captions,omitempty"`
	Answers               string        `json:"answers,omitempty"`
	QueryLanguage         string        `json:"queryLanguage,omitempty"`
}

type VectorQuery struct {
	Text       string `json:"text"`
	K          *int   `json:"k,omitempty"`
	Fields     string `json:"fields"`
	Kind       string `json:"kind"`
	Exhaustive bool   `json:"exhaustive"`
}

type QueryIndexResponse struct {
	Context string  `json:"@odata.context"`
	Count   int     `json:"@odata.count"`
	Value   []Index `json:"value"`
}

type Index struct {
	Score           float64 `json:"@search.score"`
	RerankScore     float64 `json:"@search.rerankerScore"`
	ChunkID         string  `json:"chunk_id"`
	ParentID        string  `json:"parent_id"`
	Chunk           string  `json:"chunk"`
	Title           string  `json:"title"`
	Header1         string  `json:"header_1"`
	Header2         string  `json:"header_2"`
	Header3         string  `json:"header_3"`
	OrdinalPosition int     `json:"ordinal_position"`
	ContextID       string  `json:"context_id"`
}

func GetIndexLink(chunk Index) string {
	if strings.HasPrefix(chunk.Title, "version-release-notes-index") {
		return "Please reference link from document content"
	}
	path := strings.Join(strings.Split(chunk.Title, "#"), "/")
	switch Source(chunk.ContextID) {
	case Source_TypeSpec:
		path = TrimFileFormat(path)
		return "https://typespec.io/docs/" + path
	case Source_TypeSpecAzure:
		path = TrimFileFormat(path)
		return "https://azure.github.io/typespec-azure/docs/" + path
	case Source_AzureRestAPISpec:
		path = TrimFileFormat(path)
		return "https://github.com/Azure/azure-rest-api-specs/wiki/" + path
	case Source_AzureSDKForPython:
		return "https://github.com/Azure/azure-sdk-for-python/blob/main/doc/" + path
	case Source_AzureSDKForPythonWiki:
		path = TrimFileFormat(path)
		return "https://github.com/Azure/azure-sdk-for-python/wiki/" + path
	case Source_AzureAPIGuidelines:
		return "https://github.com/microsoft/api-guidelines/blob/vNext/" + path
	case Source_AzureResourceManagerRPC:
		return "https://github.com/Azure/azure-resource-manager-rpc/blob/master/" + path
	default:
		return ""
	}
}

func TrimFileFormat(path string) string {
	path = strings.TrimSuffix(path, ".md")
	path = strings.TrimSuffix(path, ".mdx")
	path = strings.TrimPrefix(path, "docs/")
	return path
}
