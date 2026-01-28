package search

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"net/http"
	"sort"
	"strings"

	"github.com/Azure/azure-sdk-for-go/sdk/azcore/to"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/utils"
)

type SearchClient struct {
	BaseUrl string
	ApiKey  string
	Index   string
	Agent   string
}

func NewSearchClient() *SearchClient {
	return &SearchClient{
		BaseUrl: config.AppConfig.AI_SEARCH_BASE_URL,
		ApiKey:  config.AI_SEARCH_APIKEY,
		Index:   config.AppConfig.AI_SEARCH_INDEX,
		Agent:   config.AppConfig.AI_SEARCH_KNOWLEDGE_BASE,
	}
}

func (s *SearchClient) QueryIndex(ctx context.Context, req *model.QueryIndexRequest) (*model.QueryIndexResponse, error) {
	body, err := json.Marshal(req)
	if err != nil {
		return nil, fmt.Errorf("failed to marshal request: %w", err)
	}

	httpReq, err := http.NewRequestWithContext(ctx, http.MethodPost, fmt.Sprintf("%s/indexes/%s/%s", s.BaseUrl, s.Index, "docs/search?api-version=2025-05-01-preview"), bytes.NewReader(body))
	if err != nil {
		return nil, fmt.Errorf("failed to create request: %w", err)
	}
	httpReq.Header.Set("Content-Type", "application/json")
	httpReq.Header.Set("Accept", "application/json")
	httpReq.Header.Set("api-key", s.ApiKey)
	resp, err := (&http.Client{}).Do(httpReq)
	if err != nil {
		return nil, fmt.Errorf("failed to send request: %w", err)
	}

	if resp.StatusCode != http.StatusOK {
		b, _ := io.ReadAll(resp.Body)
		fmt.Println(string(b))
		return nil, fmt.Errorf("unexpected status code: %d", resp.StatusCode)
	}
	b, _ := io.ReadAll(resp.Body)
	httpResp := &model.QueryIndexResponse{}
	if err := json.Unmarshal(b, httpResp); err != nil {
		return nil, fmt.Errorf("failed to unmarshal response: %w", err)
	}

	return httpResp, nil
}

// SearchOptions contains common parameters for search operations
type SearchOptions struct {
	Sources       []model.Source
	SourceFilter  map[model.Source]string
	QuestionScope *model.QuestionScope
	ServicePlane  *model.ServicePlane
}

func (s *SearchClient) BatchGetChunks(ctx context.Context, chunkIDs []string) ([]model.Index, error) {
	var filters []string
	for _, id := range chunkIDs {
		filters = append(filters, fmt.Sprintf("chunk_id eq '%s'", id))
	}
	req := &model.QueryIndexRequest{
		Filter: strings.Join(filters, " or "),
	}
	body, err := json.Marshal(req)
	if err != nil {
		return nil, fmt.Errorf("failed to marshal request: %w", err)
	}

	httpReq, err := http.NewRequestWithContext(ctx, http.MethodPost, fmt.Sprintf("%s/indexes/%s/%s", s.BaseUrl, s.Index, "docs/search?api-version=2025-05-01-preview"), bytes.NewReader(body))
	if err != nil {
		return nil, fmt.Errorf("failed to create request: %w", err)
	}
	httpReq.Header.Set("Content-Type", "application/json")
	httpReq.Header.Set("Accept", "application/json")
	httpReq.Header.Set("api-key", s.ApiKey)
	resp, err := (&http.Client{}).Do(httpReq)
	if err != nil {
		return nil, fmt.Errorf("failed to send request: %w", err)
	}

	if resp.StatusCode != http.StatusOK {
		b, _ := io.ReadAll(resp.Body)
		fmt.Println(string(b))
		return nil, fmt.Errorf("unexpected status code: %d", resp.StatusCode)
	}
	b, _ := io.ReadAll(resp.Body)
	httpResp := &model.QueryIndexResponse{}
	if err := json.Unmarshal(b, httpResp); err != nil {
		return nil, fmt.Errorf("failed to unmarshal response: %w", err)
	}
	return httpResp.Value, nil
}

func (s *SearchClient) SearchTopKRelatedDocuments(query string, k int, opts SearchOptions) ([]model.Index, error) {
	// Base request template
	baseReq := model.QueryIndexRequest{
		Search: query,
		Count:  false,
		Select: "title, context_id, chunk, header_1, header_2, header_3, chunk_id, ordinal_position",
		VectorQueries: []model.VectorQuery{
			{
				Text:   query,
				Fields: "text_vector",
				Kind:   "text",
			},
		},
		QueryType:     "semantic",
		Captions:      "extractive",
		Answers:       "extractive|count-3",
		QueryLanguage: "en-us",
	}

	// If no sources specified, search all at once
	if len(opts.Sources) == 0 {
		baseReq.Top = k
		baseReq.Filter = s.buildFilter(nil, opts.SourceFilter, opts.QuestionScope, opts.ServicePlane)
		resp, err := s.QueryIndex(context.Background(), &baseReq)
		if err != nil {
			return nil, fmt.Errorf("QueryIndex() got an error: %v", err)
		}
		return resp.Value, nil
	}

	// Query each source and apply weighted scoring
	allResults := []model.Index{}

	// Store results by source for weighted scoring
	sourceResults := make(map[model.Source][]model.Index)

	// Query each source separately
	for _, source := range opts.Sources {
		req := baseReq
		req.Top = k
		if val, ok := config.SourceTopK[source]; ok {
			req.Top = val
		}
		req.Filter = s.buildFilter([]model.Source{source}, opts.SourceFilter, opts.QuestionScope, opts.ServicePlane)

		resp, err := s.QueryIndex(context.Background(), &req)
		if err != nil {
			log.Printf("Warning: search error for source %s: %v", utils.SanitizeForLog(string(source)), err)
			continue
		}

		// Filter results by relevance threshold
		filteredResults := []model.Index{}
		for _, doc := range resp.Value {
			if doc.RerankScore < model.RerankScoreLowRelevanceThreshold {
				continue
			}

			filteredResults = append(filteredResults, doc)
		}

		sourceResults[source] = filteredResults
		allResults = append(allResults, filteredResults...)
	}

	// Sort all results by rerank score in descending order
	sortResultsByScore(allResults)

	// Take top K results
	if len(allResults) > k {
		allResults = allResults[:k]
	}

	log.Printf("Returning %d weighted search results from %d sources", len(allResults), len(opts.Sources))

	return allResults, nil
}

// Helper function to sort results by rerank score in descending order
func sortResultsByScore(results []model.Index) {
	sort.Slice(results, func(i, j int) bool {
		return results[i].RerankScore > results[j].RerankScore
	})
}

func (s *SearchClient) GetCompleteContext(chunk model.Index) ([]model.Index, error) {
	req := &model.QueryIndexRequest{
		Count:   false,
		OrderBy: "ordinal_position",
		Select:  "chunk_id, chunk, title, header_1, header_2, header_3, ordinal_position, context_id",
		Filter:  fmt.Sprintf("title eq '%s' and context_id eq '%s'", chunk.Title, chunk.ContextID),
	}
	resp, err := s.QueryIndex(context.Background(), req)
	if err != nil {
		return nil, fmt.Errorf("QueryIndex() got an error: %v", err)
	}
	return resp.Value, nil
}

func (s *SearchClient) CompleteChunkByHierarchy(chunk model.Index, hierarchy model.ChunkHierarchy) ([]model.Index, error) {
	// Escape single quotes by replacing them with double single quotes (OData filter syntax)
	escapedHeader1 := strings.ReplaceAll(chunk.Header1, "'", "''")
	escapedHeader2 := strings.ReplaceAll(chunk.Header2, "'", "''")
	escapedHeader3 := strings.ReplaceAll(chunk.Header3, "'", "''")
	var filters []string
	filters = append(filters, fmt.Sprintf("title eq '%s'", chunk.Title))
	filters = append(filters, fmt.Sprintf("context_id eq '%s'", chunk.ContextID))
	switch hierarchy {
	case model.HierarchyHeader1:
		filters = append(filters, fmt.Sprintf("header_1 eq '%s'", escapedHeader1))
	case model.HierarchyHeader2:
		filters = append(filters, fmt.Sprintf("header_1 eq '%s'", escapedHeader1))
		filters = append(filters, fmt.Sprintf("header_2 eq '%s'", escapedHeader2))
	case model.HierarchyHeader3:
		filters = append(filters, fmt.Sprintf("header_1 eq '%s'", escapedHeader1))
		filters = append(filters, fmt.Sprintf("header_2 eq '%s'", escapedHeader2))
		filters = append(filters, fmt.Sprintf("header_3 eq '%s'", escapedHeader3))
	}
	filterStr := strings.Join(filters, " and ")
	req := &model.QueryIndexRequest{
		Count:   false,
		OrderBy: "ordinal_position",
		Select:  "chunk_id, chunk, title, header_1, header_2, header_3, ordinal_position, context_id",
		Filter:  filterStr,
	}
	resp, err := s.QueryIndex(context.Background(), req)
	if err != nil {
		return nil, fmt.Errorf("QueryIndex() got an error: %v", err)
	}
	return resp.Value, nil
}

func (s *SearchClient) CompleteChunk(chunk model.Index) model.Index {
	var chunks []model.Index
	var err error
	switch chunk.ContextID {
	case model.Source_TypeSpecQA, model.Source_TypeSpecMigration:
		chunks, err = s.CompleteChunkByHierarchy(chunk, model.HierarchyHeader1)
	case model.Source_StaticTypeSpecToSwaggerMapping:
		chunks, err = s.CompleteChunkByHierarchy(chunk, model.HierarchyHeader2)
	default:
		chunks, err = s.GetCompleteContext(chunk)
	}
	if err != nil {
		return chunk
	}
	if len(chunks) == 0 {
		return chunk
	}

	// Use MergeChunksWithHeaders to avoid code duplication
	return s.MergeChunksWithHeaders(chunk, chunks)
}

// DetectChunkHierarchy determines what level of header hierarchy a chunk represents
func (s *SearchClient) DetectChunkHierarchy(chunk model.Index) model.ChunkHierarchy {
	hasHeader1 := len(chunk.Header1) > 0
	hasHeader2 := len(chunk.Header2) > 0
	hasHeader3 := len(chunk.Header3) > 0

	if hasHeader3 {
		return model.HierarchyHeader3
	}
	if hasHeader2 && hasHeader1 {
		return model.HierarchyHeader2
	}
	if hasHeader1 {
		return model.HierarchyHeader1
	}
	return model.HierarchyUnknown
}

// MergeChunksWithHeaders merges multiple chunks with proper header formatting
// Reuses the same merging logic as CompleteChunk to maintain consistency
func (s *SearchClient) MergeChunksWithHeaders(parentChunk model.Index, subChunks []model.Index) model.Index {
	if len(subChunks) == 0 {
		return parentChunk
	}

	var contents []string
	contents = append(contents, fmt.Sprintf("# %s", parentChunk.Title))

	var currentHeader1, currentHeader2, currentHeader3 string
	for _, c := range subChunks {
		if c.Header1 != currentHeader1 {
			currentHeader1 = c.Header1
			currentHeader2 = ""
			currentHeader3 = ""
			if currentHeader1 != "" {
				contents = append(contents, fmt.Sprintf("# %s", currentHeader1))
			}
		}
		if c.Header2 != currentHeader2 {
			currentHeader2 = c.Header2
			currentHeader3 = ""
			if currentHeader2 != "" {
				contents = append(contents, fmt.Sprintf("## %s", currentHeader2))
			}
		}
		if c.Header3 != currentHeader3 {
			currentHeader3 = c.Header3
			if currentHeader3 != "" {
				contents = append(contents, fmt.Sprintf("### %s", currentHeader3))
			}
		}
		contents = append(contents, c.Chunk)
	}

	mergedChunk := parentChunk
	mergedChunk.Chunk = strings.Join(contents, "\n\n")
	if len(subChunks) > 0 {
		mergedChunk.Header1 = subChunks[0].Header1
	}

	return mergedChunk
}

// AgenticSearchOptions contains parameters specific to agentic search
type AgenticSearchOptions struct {
	SearchOptions
	Prompt string
}

func (s *SearchClient) AgenticSearch(ctx context.Context, query string, opts AgenticSearchOptions) (*model.AgenticSearchResponse, error) {
	var messages []model.KnowledgeAgentMessage

	// Use custom prompt if provided, otherwise fall back to default
	promptText := opts.Prompt
	if promptText == "" {
		promptText = "You are a TypeSpec expert assistant. You are deeply knowledgeable about TypeSpec syntax, decorators, patterns, and best practices. you must extract every single questions of user's query, and answer every question about TypeSpec"
	}

	messages = append(messages, model.KnowledgeAgentMessage{
		Role: "assistant",
		Content: []model.KnowledgeAgentMessageContent{
			{
				Type: "text",
				Text: promptText,
			},
		},
	})
	messages = append(messages, model.KnowledgeAgentMessage{
		Role: "user",
		Content: []model.KnowledgeAgentMessageContent{
			{
				Type: "text",
				Text: query,
			},
		},
	})

	knowledgeSourceParams := []model.KnowledgeSourceParams{
		{
			KnowledgeSourceName: config.AppConfig.AI_SEARCH_KNOWLEDGE_SOURCE,
			Kind:                model.KnowledgeSourceKindSearchIndex,
			FilterAddOn:         s.buildFilter(opts.Sources, opts.SourceFilter, opts.QuestionScope, opts.ServicePlane),
			RerankerThreshold:   to.Ptr(float64(model.RerankScoreMediumRelevanceThreshold)),
			IncludeReferences:   to.Ptr(true),
		},
	}

	req := &model.AgenticSearchRequest{
		Messages:                 messages,
		KnowledgeSourceParams:    knowledgeSourceParams,
		IncludeActivity:          true,
		OutputMode:               model.OutputModeExtractiveData,
		RetrievalReasoningEffort: &model.RetrievalReasoningEffort{Kind: model.RetrievalReasoningEffortMedium},
	}

	body, err := json.Marshal(req)
	if err != nil {
		return nil, fmt.Errorf("failed to marshal request: %w", err)
	}
	knowledgeBaseAPI := config.AppConfig.AI_SEARCH_KNOWLEDGE_BASE_API
	knowledgeBaseAPI = strings.ReplaceAll(knowledgeBaseAPI, "{AI_SEARCH_BASE_URL}", s.BaseUrl)
	knowledgeBaseAPI = strings.ReplaceAll(knowledgeBaseAPI, "{AI_SEARCH_AGENT}", s.Agent)
	httpReq, err := http.NewRequestWithContext(ctx, http.MethodPost, knowledgeBaseAPI, bytes.NewReader(body))
	if err != nil {
		return nil, fmt.Errorf("failed to create request: %w", err)
	}
	httpReq.Header.Set("Content-Type", "application/json")
	httpReq.Header.Set("Accept", "application/json")
	httpReq.Header.Set("api-key", s.ApiKey)
	resp, err := (&http.Client{}).Do(httpReq)
	if err != nil {
		return nil, fmt.Errorf("failed to send request: %w", err)
	}

	if resp.StatusCode != http.StatusOK && resp.StatusCode != http.StatusPartialContent {
		b, _ := io.ReadAll(resp.Body)
		log.Println(string(b))
		return nil, fmt.Errorf("unexpected status code: %d", resp.StatusCode)
	}
	b, _ := io.ReadAll(resp.Body)
	httpResp := &model.AgenticSearchResponse{}
	if err := json.Unmarshal(b, httpResp); err != nil {
		return nil, fmt.Errorf("failed to unmarshal response: %w", err)
	}
	return httpResp, nil
}

// buildFilter creates a combined OData filter string from sources, source-specific filters, and metadata filters.
func (s *SearchClient) buildFilter(sources []model.Source, sourceFilter map[model.Source]string, scope *model.QuestionScope, plane *model.ServicePlane) string {
	// Build metadata filter from scope and plane
	var metadataFilters []string
	if scope != nil && *scope == model.QuestionScope_Unbranded {
		metadataFilters = append(metadataFilters, fmt.Sprintf("scope eq '%s'", *scope))
	}
	if plane != nil && *plane != model.ServicePlane_Unknown {
		// For a specific plane, include documents with that plane or no plane specified.
		metadataFilters = append(metadataFilters, fmt.Sprintf("(plane eq '%s' or plane eq null)", *plane))
	}
	metadataFilter := strings.Join(metadataFilters, " and ")

	// Build source-level filters
	sourceFilters := make([]string, 0, len(sources))
	for _, source := range sources {
		filter := fmt.Sprintf("context_id eq '%s'", source)
		if sourceFilterStr, ok := sourceFilter[source]; ok && sourceFilterStr != "" {
			filter = fmt.Sprintf("(%s and %s)", filter, sourceFilterStr)
		}
		sourceFilters = append(sourceFilters, filter)
	}
	sourceLevelFilter := strings.Join(sourceFilters, " or ")

	// Combine source and metadata filters
	if sourceLevelFilter != "" && metadataFilter != "" {
		return fmt.Sprintf("(%s) and (%s)", sourceLevelFilter, metadataFilter)
	}
	if sourceLevelFilter != "" {
		return sourceLevelFilter
	}
	return metadataFilter
}

// deduplicateExpansions removes duplicate chunk expansions considering hierarchy:
// - If a header1 is being expanded, remove any subsequent header2/header3 under it
// - If a header2 is being expanded, remove any subsequent header3 under it
// - If a header3 is being expanded, keep it
// This respects the order/priority of expansions in the array
func (s *SearchClient) DeduplicateExpansions(expansions []model.ChunkWithExpansion) []model.ChunkWithExpansion {
	result := make([]model.ChunkWithExpansion, 0)

	// Track what hierarchies have been expanded (built incrementally during iteration)
	expandedHeader1 := make(map[string]bool) // contextID|title|header1
	expandedHeader2 := make(map[string]bool) // contextID|title|header1|header2
	expandedHeader3 := make(map[string]bool) // contextID|title|header1|header2|header3

	// Process expansions in order, tracking what's been expanded
	for _, cwe := range expansions {
		c := cwe.Chunk

		// Detect hierarchy once for efficiency
		hierarchy := s.DetectChunkHierarchy(c)

		// If this is a header2/header3 chunk under an already expanded header1, skip it
		if c.Header1 != "" {
			h1Key := fmt.Sprintf("%s|%s|%s", c.ContextID, c.Title, c.Header1)
			if expandedHeader1[h1Key] {
				switch hierarchy {
				case model.HierarchyHeader2, model.HierarchyHeader3:
					log.Printf("Skipping chunk (header1 already expanded): %s/%s#%s#%s", c.ContextID, c.Title, c.Header1, c.Header2)
					continue
				}
			}
		}

		// If this is a header3 chunk under an already expanded header2, skip it
		if c.Header1 != "" && c.Header2 != "" {
			h2Key := fmt.Sprintf("%s|%s|%s|%s", c.ContextID, c.Title, c.Header1, c.Header2)
			if expandedHeader2[h2Key] {
				switch hierarchy {
				case model.HierarchyHeader3:
					log.Printf("Skipping chunk (header2 already expanded): %s/%s#%s#%s#%s", c.ContextID, c.Title, c.Header1, c.Header2, c.Header3)
					continue
				}
			}
		}

		h3Key := fmt.Sprintf("%s|%s|%s|%s|%s", c.ContextID, c.Title, c.Header1, c.Header2, c.Header3)
		// If this exact chunk has already been processed, skip it
		if expandedHeader3[h3Key] {
			log.Printf("Skipping chunk (already processed): %s/%s#%s#%s#%s", c.ContextID, c.Title, c.Header1, c.Header2, c.Header3)
			continue
		}

		result = append(result, cwe)

		// Track this expansion for future iterations
		switch hierarchy {
		case model.HierarchyHeader1:
			h1Key := fmt.Sprintf("%s|%s|%s", c.ContextID, c.Title, c.Header1)
			expandedHeader1[h1Key] = true
		case model.HierarchyHeader2:
			h2Key := fmt.Sprintf("%s|%s|%s|%s", c.ContextID, c.Title, c.Header1, c.Header2)
			expandedHeader2[h2Key] = true
		case model.HierarchyHeader3:
			expandedHeader3[h3Key] = true
		}
	}

	return result
}

// DetermineChunkExpansion determines the expansion type for a given chunk based on its source and hierarchy
func (s *SearchClient) DetermineChunkExpansion(chunk model.Index) model.ChunkWithExpansion {
	// Static chunks specific expansion rules
	if chunk.ContextID == model.Source_TypeSpecQA || chunk.ContextID == model.Source_TypeSpecMigration {
		return model.ChunkWithExpansion{
			Chunk:     chunk,
			Expansion: model.ExpansionQA,
		}
	}
	if chunk.ContextID == model.Source_StaticTypeSpecToSwaggerMapping {
		return model.ChunkWithExpansion{
			Chunk:     chunk,
			Expansion: model.ExpansionMapping,
		}
	}

	// Check if needs hierarchical expansion
	hierarchy := s.DetectChunkHierarchy(chunk)
	if hierarchy != model.HierarchyUnknown {
		return model.ChunkWithExpansion{
			Chunk:     chunk,
			Expansion: model.ExpansionHierarchical,
		}
	}

	// No expansion needed
	return model.ChunkWithExpansion{
		Chunk:     chunk,
		Expansion: model.ExpansionNone,
	}
}
