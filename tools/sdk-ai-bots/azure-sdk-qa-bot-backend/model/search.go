package model

import (
	"strings"
)

type AgenticSearchRequest struct {
	Messages          []KnowledgeAgentMessage     `json:"messages"`
	TargetIndexParams []KnowledgeAgentIndexParams `json:"targetIndexParams,omitempty"`
}

type KnowledgeAgentIndexParams struct {
	FilterAddOn                string  `json:"filterAddOn"`
	IncludeReferenceSourceData *bool   `json:"includeReferenceSourceData,omitempty"`
	IndexName                  string  `json:"indexName"`
	MaxDocsForReranker         *int    `json:"maxDocsForReranker,omitempty"`
	RerankerThreshold          float64 `json:"rerankerThreshold"`
}

type AgenticSearchResponse struct {
	Activity   []KnowledgeAgentActivityRecord `json:"activity"`
	References []KnowledgeAgentReference      `json:"references"`
	Response   []KnowledgeAgentMessage        `json:"response"`
}

type KnowledgeAgentActivityRecord struct {
	ElapsedMs    int64                             `json:"elapsedMs"`
	ID           int64                             `json:"id"`
	InputTokens  int64                             `json:"inputTokens"`
	OutputTokens int64                             `json:"outputTokens"`
	Type         string                            `json:"type"`
	Count        int                               `json:"count,omitempty"`
	Query        KnowledgeAgentActivityRecordQuery `json:"query,omitempty"`
	QueryTime    string                            `json:"queryTime,omitempty"`
	TargetIndex  string                            `json:"targetIndex,omitempty"`
}

type KnowledgeAgentActivityRecordQuery struct {
	Filter string `json:"filter,omitempty"`
	Search string `json:"search,omitempty"`
}

type KnowledgeAgentReference struct {
	ActivitySource int         `json:"activitySource"`
	DocKey         string      `json:"docKey"`
	ID             string      `json:"id"`
	SourceData     interface{} `json:"sourceData"`
	Type           string      `json:"type"`
}

type KnowledgeAgentMessage struct {
	Content []KnowledgeAgentMessageContent `json:"content"`
	Role    Role                           `json:"role"`
}

type KnowledgeAgentMessageContent struct {
	Type  string                             `json:"type"`
	Text  string                             `json:"text,omitempty"`
	Image *KnowledgeAgentMessageImageContent `json:"image,omitempty"`
}

type KnowledgeAgentMessageImageContent struct {
	URL string `json:"url"`
}

type QueryIndexRequest struct {
	Count                 bool          `json:"count,omitempty"`
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
		return "https://github.com/cloud-and-ai-microsoft/resource-provider-contract/blob/master/" + path
	case Source_AzureSDKDocsEng:
		path = strings.TrimPrefix(path, "docs/")
		path = TrimFileFormat(path)
		if path == "index" {
			return "https://eng.ms/docs/products/azure-developer-experience"
		}
		return "https://eng.ms/docs/products/azure-developer-experience/" + path
	case Source_AzureSDKGuidelines:
		return "https://azure.github.io/azure-sdk/" + path
	case Source_TypeSpecAzureHttpSpecs:
		// change the suffix to .tsp
		path = TrimFileFormat(path)
		return "https://github.com/Azure/typespec-azure/blob/main/packages/azure-http-specs/specs/" + path + ".tsp"
	case Source_TypeSpecHttpSpecs:
		// change the suffix to .tsp
		path = TrimFileFormat(path)
		return "https://github.com/microsoft/typespec/tree/main/packages/http-specs/specs/" + path + ".tsp"
	case Source_TypeSpecMigration:
		// link to the migration loop
		return "https://microsoft.sharepoint.com/:fl:/g/contentstorage/CSP_bedaa9e1-03aa-4737-868d-2b78d646ba64/EfGbxeojp1VIjL-YDJc88REBx6bOG5VEnp16QSImbm3MXw?e=fGdnEH&nav=cz0lMkZjb250ZW50c3RvcmFnZSUyRkNTUF9iZWRhYTllMS0wM2FhLTQ3MzctODY4ZC0yYjc4ZDY0NmJhNjQmZD1iJTIxNGFuYXZxb0ROMGVHalN0NDFrYTZaRkZsMVVqejNiaEtpY3ZkUE04QWo2a1pyRS1mMFd1aFRZVXVuTlE1bW0yTCZmPTAxRFE3TDNKSFJUUEM2VUk1SEtWRUlaUDRZQlNMVFo0SVImYz0lMkYmYT1Mb29wQXBwJnA9JTQwZmx1aWR4JTJGbG9vcC1wYWdlLWNvbnRhaW5lciZ4PSU3QiUyMnclMjIlM0ElMjJUMFJUVUh4dGFXTnliM052Wm5RdWMyaGhjbVZ3YjJsdWRDNWpiMjE4WWlFMFlXNWhkbkZ2UkU0d1pVZHFVM1EwTVd0aE5scEdSbXd4VldwNk0ySm9TMmxqZG1SUVRUaEJhalpyV25KRkxXWXdWM1ZvVkZsVmRXNU9VVFZ0YlRKTWZEQXhSRkUzVEROS1JVSlhOMWhGTXpkTVVGazFRa3BTVjBrelZVSkVRVmszUjBvJTNEJTIyJTJDJTIyaSUyMiUzQSUyMjA2NDFmMzk1LTY4MjctNGFjYi04OTM5LTliODBkYTEyYzk3MiUyMiU3RA%3D%3D"
	case Source_AzureSDKForGo:
		return "https://github.com/Azure/azure-sdk-for-go/blob/main/documentation/" + path
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

type AgentSearchReference struct {
	RefID   string `json:"ref_id"`
	Title   string `json:"title"`
	Terms   string `json:"terms"`
	Content string `json:"content"`
}
