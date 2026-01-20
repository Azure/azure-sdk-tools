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

	var contents []string
	// var tocEntries []string
	totalLength := 0

	// Get the first chunk for header information
	firstChunk := chunks[0]
	chunk.Header1 = firstChunk.Header1

	// Add document title at the beginning
	contents = append(contents, fmt.Sprintf("# %s", chunk.Title))

	// Set headers based on hierarchy and collect TOC entries in single pass
	var currentHeader1, currentHeader2, currentHeader3 string
	for _, c := range chunks {
		// Add headers to content and TOC when they change
		if c.Header1 != currentHeader1 {
			currentHeader1 = c.Header1
			currentHeader2 = "" // Reset lower level headers
			currentHeader3 = ""
			if currentHeader1 != "" {
				contents = append(contents, fmt.Sprintf("# %s", currentHeader1))
			}
		}
		if c.Header2 != currentHeader2 {
			currentHeader2 = c.Header2
			currentHeader3 = "" // Reset lower level header
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

		totalLength += len(c.Chunk)
		contents = append(contents, c.Chunk)
	}

	chunk.Chunk = strings.Join(contents, "\n\n") // Add extra newline between sections

	return chunk
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
