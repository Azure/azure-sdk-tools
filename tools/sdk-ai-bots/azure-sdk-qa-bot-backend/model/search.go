package model

import (
	"strings"
)

// ExpansionType defines how a chunk should be expanded
type ExpansionType int

const (
	ExpansionNone         ExpansionType = iota // No expansion needed - use chunk as-is
	ExpansionHierarchical                      // Expand based on header hierarchy
	ExpansionQA                                // Expand based on Q&A pairs
	ExpansionMapping                           // Expand based on TypeSpec to Swagger mapping
)

// ChunkWithExpansion wraps a chunk with its expansion strategy
type ChunkWithExpansion struct {
	Chunk     Index
	Expansion ExpansionType
}

// ChunkHierarchy represents the header level of a chunk
type ChunkHierarchy int

const (
	HierarchyHeader3 ChunkHierarchy = iota // Full chunk with header3 (most specific)
	HierarchyHeader2                       // Chunk with header1 and header2
	HierarchyHeader1                       // Chunk with only header1
	HierarchyUnknown                       // Unknown or no headers
)

func (s ChunkHierarchy) String() string {
	switch s {
	case HierarchyHeader3:
		return "HierarchyHeader3"
	case HierarchyHeader2:
		return "HierarchyHeader2"
	case HierarchyHeader1:
		return "HierarchyHeader1"
	default:
		return "HierarchyUnknown"
	}
}

type AgenticSearchRequest struct {
	Messages                 []KnowledgeAgentMessage   `json:"messages"`
	KnowledgeSourceParams    []KnowledgeSourceParams   `json:"knowledgeSourceParams,omitempty"`
	IncludeActivity          bool                      `json:"includeActivity,omitempty"`
	MaxRuntimeInSeconds      *int                      `json:"maxRuntimeInSeconds,omitempty"`
	MaxOutputSize            *int                      `json:"maxOutputSize,omitempty"`
	RetrievalReasoningEffort *RetrievalReasoningEffort `json:"retrievalReasoningEffort,omitempty"`
	OutputMode               OutputMode                `json:"outputMode,omitempty"`
}

// KnowledgeSourceKind is an enum for knowledge source types
type KnowledgeSourceKind string

const (
	KnowledgeSourceKindSearchIndex       KnowledgeSourceKind = "searchIndex"       // A knowledge source that retrieves data from a Search Index
	KnowledgeSourceKindAzureBlob         KnowledgeSourceKind = "azureBlob"         // A knowledge source that retrieves and ingests data from Azure Blob Storage to a Search Index
	KnowledgeSourceKindWeb               KnowledgeSourceKind = "web"               // A knowledge source that retrieves data from the web
	KnowledgeSourceKindRemoteSharePoint  KnowledgeSourceKind = "remoteSharePoint"  // A knowledge source that retrieves data from a remote SharePoint endpoint
	KnowledgeSourceKindIndexedSharePoint KnowledgeSourceKind = "indexedSharePoint" // A knowledge source that retrieves and ingests data from SharePoint to a Search Index
	KnowledgeSourceKindIndexedOneLake    KnowledgeSourceKind = "indexedOneLake"    // A knowledge source that retrieves and ingests data from OneLake to a Search Index
)

// KnowledgeSourceParams contains parameters for knowledge source retrieval
type KnowledgeSourceParams struct {
	KnowledgeSourceName        string              `json:"knowledgeSourceName"`
	Kind                       KnowledgeSourceKind `json:"kind"`
	FilterAddOn                string              `json:"filterAddOn,omitempty"`
	IncludeReferenceSourceData *bool               `json:"includeReferenceSourceData,omitempty"`
	IncludeReferences          *bool               `json:"includeReferences,omitempty"`
	RerankerThreshold          *float64            `json:"rerankerThreshold,omitempty"`
	AlwaysQuerySource          *bool               `json:"alwaysQuerySource,omitempty"`
}

// OutputMode is an enum for output mode values
type OutputMode string

const (
	OutputModeAnswerSynthesis OutputMode = "answerSynthesis" // Synthesize an answer for the response payload
	OutputModeExtractiveData  OutputMode = "extractiveData"  // Return data from the knowledge sources directly without generative alteration
)

// RetrievalReasoningEffortKind is an enum for reasoning effort
type RetrievalReasoningEffortKind string

const (
	RetrievalReasoningEffortMinimal RetrievalReasoningEffortKind = "minimal" // Does not perform any source selections, query planning, or iterative search
	RetrievalReasoningEffortLow     RetrievalReasoningEffortKind = "low"     // Use low reasoning during retrieval
	RetrievalReasoningEffortMedium  RetrievalReasoningEffortKind = "medium"  // Use a moderate amount of reasoning during retrieval
)

// RetrievalReasoningEffort specifies the level of LLM processing for retrieval
type RetrievalReasoningEffort struct {
	Kind RetrievalReasoningEffortKind `json:"kind"`
}

// ActivityRecordType is an enum for activity record types
type ActivityRecordType string

const (
	ActivityRecordTypeAgenticReasoning     ActivityRecordType = "agenticReasoning"
	ActivityRecordTypeSearchIndex          ActivityRecordType = "searchIndex"
	ActivityRecordTypeWeb                  ActivityRecordType = "web"
	ActivityRecordTypeAzureBlob            ActivityRecordType = "azureBlob"
	ActivityRecordTypeIndexedSharePoint    ActivityRecordType = "indexedSharePoint"
	ActivityRecordTypeIndexedOneLake       ActivityRecordType = "indexedOneLake"
	ActivityRecordTypeRemoteSharePoint     ActivityRecordType = "remoteSharePoint"
	ActivityRecordTypeModelQueryPlanning   ActivityRecordType = "modelQueryPlanning"
	ActivityRecordTypeModelAnswerSynthesis ActivityRecordType = "modelAnswerSynthesis"
)

type AgenticSearchResponse struct {
	Activity   []KnowledgeAgentActivityRecord `json:"activity"`
	References []KnowledgeAgentReference      `json:"references"`
	Response   []KnowledgeAgentMessage        `json:"response"`
}

type KnowledgeAgentActivityRecord struct {
	// Common fields
	ID        int64              `json:"id"`
	Type      ActivityRecordType `json:"type"`
	ElapsedMs int64              `json:"elapsedMs,omitempty"`
	Error     *ActivityError     `json:"error,omitempty"` // New in 2025-11-01-preview

	// For LLM activities (modelQueryPlanning, modelAnswerSynthesis)
	InputTokens  int64 `json:"inputTokens,omitempty"`
	OutputTokens int64 `json:"outputTokens,omitempty"`

	// For agenticReasoning activities
	RetrievalReasoningEffort *RetrievalReasoningEffort `json:"retrievalReasoningEffort,omitempty"`
	ReasoningTokens          int64                     `json:"reasoningTokens,omitempty"`

	// For search activities (searchIndex, web, azureBlob, etc.)
	KnowledgeSourceName  string                 `json:"knowledgeSourceName,omitempty"`
	QueryTime            string                 `json:"queryTime,omitempty"`
	Count                int                    `json:"count,omitempty"`
	SearchIndexArguments *SearchIndexArguments  `json:"searchIndexArguments,omitempty"`
	WebArguments         map[string]interface{} `json:"webArguments,omitempty"`
	AzureBlobArguments   map[string]interface{} `json:"azureBlobArguments,omitempty"`
	SharePointArguments  map[string]interface{} `json:"sharePointArguments,omitempty"`
	OneLakeArguments     map[string]interface{} `json:"oneLakeArguments,omitempty"`
}

// SearchIndexArguments represents arguments for searchIndex activity
type SearchIndexArguments struct {
	Search                    string           `json:"search,omitempty"`
	Filter                    string           `json:"filter,omitempty"`
	SourceDataFields          []FieldReference `json:"sourceDataFields,omitempty"`
	SearchFields              []FieldReference `json:"searchFields,omitempty"`
	SemanticConfigurationName string           `json:"semanticConfigurationName,omitempty"`
}

// FieldReference represents a field reference in search arguments
type FieldReference struct {
	Name string `json:"name"`
}

// ActivityError represents an error in an activity record
type ActivityError struct {
	Message string `json:"message,omitempty"`
	Code    string `json:"code,omitempty"`
}

type KnowledgeAgentReference struct {
	ActivitySource int     `json:"activitySource"`
	DocKey         string  `json:"docKey"`
	ID             string  `json:"id"`
	SourceData     *Index  `json:"sourceData"`
	Type           string  `json:"type"`
	RerankerScore  float64 `json:"rerankerScore"`
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
	ContextID       Source  `json:"context_id"`
	Scope           string  `json:"scope,omitempty"`
	ServiceType     string  `json:"service_type,omitempty"`

	SearchType SearchType `json:"search_type,omitempty"`
}

type SearchType string

const (
	SearchType_Vector  SearchType = "Vector Search"
	SearchType_Agentic SearchType = "Agentic Search"
)

type Knowledge struct {
	Source   Source `json:"document_source"`
	FileName string `json:"document_filename"`
	Title    string `json:"document_title"`
	Link     string `json:"document_link"`
	Content  string `json:"content"`
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
		// link to the migration faq page
		return "https://azure.github.io/typespec-azure/docs/migrate-swagger/faq/breakingchange"
	case Source_AzureSDKForGo:
		return "https://github.com/Azure/azure-sdk-for-go/blob/main/documentation/" + path
	case Source_AzureSDKForJava:
		return "https://github.com/Azure/azure-sdk-for-java/blob/main/" + path
	case Source_AzureSDKForJavaWiki:
		path = TrimFileFormat(path)
		return "https://github.com/Azure/azure-sdk-for-java/wiki/" + path
	case Source_AutorestJava:
		return "https://github.com/Azure/autorest.java/blob/main/" + path
	case Source_StaticAzureDocs:
		if chunk.Title == "Azure Versioning and Breaking Changes Policy V1.3.2" {
			return "http://aka.ms/azbreakingchangespolicy"
		}
		return ""
	case Source_AzureSDKForJavaScript:
		return "https://github.com/Azure/azure-sdk-for-js/blob/main/" + path
	case Source_AzureSDKForJavaScriptWiki:
		path = TrimFileFormat(path)
		return "https://github.com/Azure/azure-sdk-for-js/wiki/" + path
	case Source_AzureSDKForNetDocs:
		return "https://github.com/Azure/azure-sdk-for-net/blob/main/" + path
	case Source_AzureSDKInternalWiki:
		path = TrimFileFormat(path)
		wikiPath := strings.ReplaceAll(path, "#", "/")
		return "https://dev.azure.com/azure-sdk/internal/_wiki/wikis/internal.wiki?wikiVersion=GBwikiMaster&pagePath=/" + wikiPath
	case Source_AzureRestAPISpecDocs:
		// Handle documents from azure-rest-api-specs documentation
		return "https://github.com/Azure/azure-rest-api-specs/blob/main/" + path
	case Source_AzureOpenapiDiffDocs:
		// Handle documents from openapi-diff/docs
		return "https://github.com/Azure/openapi-diff/blob/main/" + path
	case Source_TypeSpecAzureResourceManagerLib:
		path = TrimFileFormat(path)
		return "https://github.com/Azure/typespec-azure/blob/main/packages/typespec-azure-resource-manager/lib/" + path + ".tsp"
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
