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

func (s *SearchClient) SearchTopKRelatedDocuments(query string, k int, sources []model.Source, sourceFilter map[model.Source]string) ([]model.Index, error) {
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
	if len(sources) == 0 {
		baseReq.Top = k
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
	for _, source := range sources {
		req := baseReq
		req.Top = k
		if val, ok := config.SourceTopK[source]; ok {
			req.Top = val
		}
		req.Filter = fmt.Sprintf("context_id eq '%s'", source)
		if sourceFilterStr, ok := sourceFilter[source]; ok && sourceFilterStr != "" {
			req.Filter = fmt.Sprintf("(%s and %s)", req.Filter, sourceFilterStr)
		}

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

	// Sort the top K results by source priority exactly
	sortResultsBySource(allResults, sources)

	log.Printf("Returning %d weighted search results from %d sources", len(allResults), len(sources))

	return allResults, nil
}

// Helper function to sort results by rerank score in descending order
func sortResultsByScore(results []model.Index) {
	sort.Slice(results, func(i, j int) bool {
		return results[i].RerankScore > results[j].RerankScore
	})
}

// Helper function to sort results by source priority
func sortResultsBySource(results []model.Index, sources []model.Source) {
	sort.Slice(results, func(i, j int) bool {
		// Find priority index for sources
		sourceI := model.Source(results[i].ContextID)
		sourceJ := model.Source(results[j].ContextID)

		priorityI := -1
		priorityJ := -1
		for idx, source := range sources {
			if source == sourceI {
				priorityI = idx
			}
			if source == sourceJ {
				priorityJ = idx
			}
		}
		if priorityI == priorityJ {
			return results[i].RerankScore > results[j].RerankScore // If same source, sort by score
		}
		// If both sources are in the priority list, sort by priority
		if priorityI >= 0 && priorityJ >= 0 {
			return priorityI < priorityJ // Lower index = higher priority
		}

		// If one source is in priority list and the other isn't, prioritize the one in the list
		if priorityI >= 0 && priorityJ < 0 {
			return true // i is in priority list, j is not
		}
		if priorityI < 0 && priorityJ >= 0 {
			return false // j is in priority list, i is not
		}

		// If neither source is in priority list, keep original order based on score
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

func (s *SearchClient) GetHeader1CompleteContext(chunk model.Index) ([]model.Index, error) {
	// Escape single quotes by replacing them with double single quotes (OData filter syntax)
	escapedHeader1 := strings.ReplaceAll(chunk.Header1, "'", "''")

	req := &model.QueryIndexRequest{
		Count:   false,
		OrderBy: "ordinal_position",
		Select:  "chunk_id, chunk, title, header_1, header_2, header_3, ordinal_position, context_id",
		Filter:  fmt.Sprintf("title eq '%s' and context_id eq '%s' and header_1 eq '%s'", chunk.Title, chunk.ContextID, escapedHeader1),
	}
	resp, err := s.QueryIndex(context.Background(), req)
	if err != nil {
		return nil, fmt.Errorf("QueryIndex() got an error: %v", err)
	}
	return resp.Value, nil
}

func (s *SearchClient) GetHeader2CompleteContext(chunk model.Index) ([]model.Index, error) {
	// Escape single quotes by replacing them with double single quotes (OData filter syntax)
	escapedHeader1 := strings.ReplaceAll(chunk.Header1, "'", "''")
	escapedHeader2 := strings.ReplaceAll(chunk.Header2, "'", "''")

	req := &model.QueryIndexRequest{
		Count:   false,
		OrderBy: "ordinal_position",
		Select:  "chunk_id, chunk, title, header_1, header_2, header_3, ordinal_position, context_id",
		Filter:  fmt.Sprintf("title eq '%s' and context_id eq '%s' and header_1 eq '%s' and header_2 eq '%s'", chunk.Title, chunk.ContextID, escapedHeader1, escapedHeader2),
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
		chunks, err = s.GetHeader1CompleteContext(chunk)
	case model.Source_StaticTypeSpecToSwaggerMapping:
		chunks, err = s.GetHeader2CompleteContext(chunk)
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

// FetchHierarchicalSubChunks fetches all sub-chunks under a given header hierarchy
func (s *SearchClient) FetchHierarchicalSubChunks(chunk model.Index, hierarchy model.ChunkHierarchy) []model.Index {
	var subChunks []model.Index
	var err error

	switch hierarchy {
	case model.HierarchyHeader1:
		// Fetch all chunks under this header1
		subChunks, err = s.GetHeader1CompleteContext(chunk)
		if err != nil {
			log.Printf("Failed to fetch header1 sub-chunks for %s/%s#%s: %v", chunk.ContextID, chunk.Title, chunk.Header1, err)
			return []model.Index{chunk} // Fall back to original chunk
		}
		log.Printf("Expanded header1 '%s/%s/%s' → %d sub-chunks", chunk.ContextID, chunk.Title, chunk.Header1, len(subChunks))

	case model.HierarchyHeader2:
		// Fetch all chunks under this header1+header2
		subChunks, err = s.GetHeader2CompleteContext(chunk)
		if err != nil {
			log.Printf("Failed to fetch header2 sub-chunks for %s/%s#%s#%s: %v", chunk.ContextID, chunk.Title, chunk.Header1, chunk.Header2, err)
			return []model.Index{chunk} // Fall back to original chunk
		}
		log.Printf("Expanded header2 '%s/%s/%s/%s' → %d sub-chunks", chunk.ContextID, chunk.Title, chunk.Header1, chunk.Header2, len(subChunks))

	default:
		// No expansion needed for header3 or unknown Hierarchy
		return []model.Index{chunk}
	}

	if len(subChunks) == 0 {
		return []model.Index{chunk}
	}

	return subChunks
}

func (s *SearchClient) AgenticSearch(ctx context.Context, query string, sources []model.Source, sourceFilter map[model.Source]string, agenticSearchPrompt string) (*model.AgenticSearchResponse, error) {
	var messages []model.KnowledgeAgentMessage

	// Use custom prompt if provided, otherwise fall back to default
	promptText := agenticSearchPrompt
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

	filters := make([]string, 0, len(sources))
	for _, source := range sources {
		filter := fmt.Sprintf("context_id eq '%s'", source)
		if sourceFilterStr, ok := sourceFilter[source]; ok && sourceFilterStr != "" {
			filter = fmt.Sprintf("(%s and %s)", filter, sourceFilterStr)
		}
		filters = append(filters, filter)
	}

	knowledgeSourceParams := []model.KnowledgeSourceParams{
		{
			KnowledgeSourceName: config.AppConfig.AI_SEARCH_KNOWLEDGE_SOURCE,
			Kind:                model.KnowledgeSourceKindSearchIndex,
			FilterAddOn:         strings.Join(filters, " or "),
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

// deduplicateExpansions removes duplicate chunk expansions considering hierarchy:
// - If a header1 is being expanded, remove any subsequent header2/header3 under it
// - If a header2 is being expanded, remove any subsequent header3 under it
// This respects the order/priority of expansions in the array
func (s *SearchClient) DeduplicateExpansions(expansions []model.ChunkWithExpansion) []model.ChunkWithExpansion {
	result := make([]model.ChunkWithExpansion, 0)
	processedChunks := make(map[string]bool)

	// Track what hierarchies have been expanded (built incrementally during iteration)
	expandedHeader1 := make(map[string]bool) // contextID|title|header1
	expandedHeader2 := make(map[string]bool) // contextID|title|header1|header2

	// Process expansions in order, tracking what's been expanded
	for _, cwe := range expansions {
		c := cwe.Chunk

		// Check if already processed (exact duplicate)
		chunkKey := fmt.Sprintf("%s|%s|%s", c.ContextID, c.Title, c.Chunk)
		if processedChunks[chunkKey] {
			continue
		}

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

		// Not redundant, keep it
		processedChunks[chunkKey] = true
		result = append(result, cwe)

		// Track this expansion for future iterations
		if cwe.Expansion == model.ExpansionHierarchical {
			switch hierarchy {
			case model.HierarchyHeader1:
				h1Key := fmt.Sprintf("%s|%s|%s", c.ContextID, c.Title, c.Header1)
				expandedHeader1[h1Key] = true
			case model.HierarchyHeader2:
				h2Key := fmt.Sprintf("%s|%s|%s|%s", c.ContextID, c.Title, c.Header1, c.Header2)
				expandedHeader2[h2Key] = true
			}
		}
	}

	return result
}
