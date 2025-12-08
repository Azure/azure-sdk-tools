package agent

import (
	"context"
	"encoding/json"
	"fmt"
	"log"
	"os"
	"sort"
	"strings"
	"sync"
	"time"

	"github.com/Azure/azure-sdk-for-go/sdk/ai/azopenai"
	"github.com/Azure/azure-sdk-for-go/sdk/azcore/to"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/prompt"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/search"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/utils"
	"github.com/google/uuid"
)

type CodeReviewService struct {
	searchClient *search.SearchClient
	promptParser prompt.DefaultPromptParser
}

// CodeReviewResponse represents the JSON response from the LLM
type CodeReviewResponse struct {
	Comments []model.ReviewComment `json:"comments"`
}

const (
	maxGuidelineSnippets      = 8
	maxGuidelineContentLength = 4000
	maxResultsPerQuery        = 5
)

func NewCodeReviewService() (*CodeReviewService, error) {
	return &CodeReviewService{
		searchClient: search.NewSearchClient(),
		promptParser: prompt.DefaultPromptParser{},
	}, nil
}

func (s *CodeReviewService) Review(ctx context.Context, req *model.CodeReviewReq) (*model.CodeReviewResp, error) {
	if err := s.validateRequest(req); err != nil {
		return nil, err
	}

	requestID := uuid.New().String()
	log.SetPrefix(fmt.Sprintf("[RequestID: %s] ", requestID))

	jsonReq, err := json.Marshal(req)
	if err != nil {
		log.Printf("Failed to marshal request: %v\n", err)
	} else {
		log.Printf("Request: %s", utils.SanitizeForLog(string(jsonReq)))
	}

	guidelines, err := s.retrieveGuidelines(req.Language, req.FilePath, req.Code)
	if err != nil {
		log.Printf("[RequestID: %s] Failed to retrieve guidelines: %v", requestID, err)
		return nil, model.NewLLMServiceFailureError(fmt.Errorf("failed to retrieve guidelines: %w", err))
	}

	promptStr, err := s.buildCodeReviewPrompt(req, guidelines)
	if err != nil {
		log.Printf("[RequestID: %s] Failed to build prompt: %v", requestID, err)
		return nil, model.NewLLMServiceFailureError(fmt.Errorf("failed to build prompt: %w", err))
	}

	// debug: write prompt into a file
	_ = os.WriteFile("debug_prompt.prompty", []byte(promptStr), 0644)

	comments, err := getLLMReviewComments(ctx, promptStr, requestID)
	if err != nil {
		log.Printf("[RequestID: %s] Failed to get LLM review: %v", requestID, err)
		return nil, err
	}

	resp := &model.CodeReviewResp{
		ID:       requestID,
		Language: req.Language,
		Comments: comments,
	}

	if len(comments) > 0 {
		resp.Summary = fmt.Sprintf("Found %d potential issues or suggestions for improvement.", len(comments))
	} else {
		resp.Summary = "No guideline violations detected. Code looks good!"
	}

	log.Printf("[RequestID: %s] Code review completed with %d comments", requestID, len(comments))
	return resp, nil
}

func (s *CodeReviewService) validateRequest(req *model.CodeReviewReq) error {
	if req == nil {
		return model.NewInvalidRequestError("Request body is required", "")
	}
	if strings.TrimSpace(req.Language) == "" {
		return model.NewInvalidRequestError("Language is required", "")
	}
	if strings.TrimSpace(req.Code) == "" {
		return model.NewInvalidRequestError("Code is required", "")
	}
	return nil
}

func (s *CodeReviewService) retrieveGuidelines(language string, filePath string, code string) (string, error) {
	sources, sourceFilter := config.GetLanguageSources(language)

	// Build search query from code context
	query := s.buildSearchQuery(language, filePath, code)
	log.Printf("=== Code Review Search Query: %s ===", query)

	// Use agentic search to automatically generate sub-queries and find relevant guidelines
	chunks, err := s.agenticSearch(context.Background(), query, sources, sourceFilter)
	if err != nil {
		log.Printf("Agentic search failed: %v", err)
		return "", fmt.Errorf("failed to retrieve guidelines: %w", err)
	}

	if len(chunks) == 0 {
		return "No specific guidelines found. Apply general SDK best practices.", nil
	}

	// Process and format the chunks
	guidelines := s.processGuidelineChunks(chunks)

	return joinWithSeparator(guidelines, "\n---\n"), nil
}

// buildSearchQuery creates a comprehensive search query from code context
func (s *CodeReviewService) buildSearchQuery(language, filePath, code string) string {
	// Extract key information from the code to build search query
	query := fmt.Sprintf("### Language:%s\n### File:%s \n### Code:%s", language, filePath, code)
	return query
}

func (s *CodeReviewService) buildCodeReviewPrompt(req *model.CodeReviewReq, guidelines string) (string, error) {
	promptTemplate := "code_review/sdk_code_review.md"

	params := map[string]string{
		"language":  req.Language,
		"file_path": req.FilePath,
		"context":   guidelines,
		"content":   req.Code,
	}

	promptStr, err := s.promptParser.ParsePrompt(params, promptTemplate)
	if err != nil {
		return "", fmt.Errorf("failed to parse prompt template: %w", err)
	}

	return promptStr, nil
}

func getLLMReviewComments(ctx context.Context, promptStr string, requestID string) ([]model.ReviewComment, error) {
	messages := []azopenai.ChatRequestMessageClassification{
		&azopenai.ChatRequestSystemMessage{
			Content: azopenai.NewChatRequestSystemMessageContent(promptStr),
		},
	}

	resp, err := config.OpenAIClient.GetChatCompletions(ctx, azopenai.ChatCompletionsOptions{
		Messages:       messages,
		DeploymentName: to.Ptr("gpt-5.1"),
		ResponseFormat: &azopenai.ChatCompletionsJSONResponseFormat{},
		// TopP:           to.Ptr(float32(config.AppConfig.AOAI_CHAT_COMPLETIONS_TOP_P)),
	}, nil)

	if err != nil {
		return nil, model.NewLLMServiceFailureError(fmt.Errorf("failed to call LLM: %w", err))
	}

	if len(resp.Choices) == 0 {
		return nil, model.NewLLMServiceFailureError(fmt.Errorf("no response from LLM"))
	}

	content := *resp.Choices[0].Message.Content
	log.Printf("[RequestID: %s] LLM response: %s", requestID, content)

	var reviewResp CodeReviewResponse
	if err := json.Unmarshal([]byte(content), &reviewResp); err != nil {
		return nil, model.NewLLMServiceFailureError(fmt.Errorf("failed to parse LLM response: %w", err))
	}

	return reviewResp.Comments, nil
}

func buildGuidelineKey(idx model.Index) string {
	if idx.ChunkID != "" {
		return idx.ChunkID
	}
	return fmt.Sprintf("%s|%s|%s|%s", idx.ContextID, idx.Title, idx.Header1, idx.Header2)
}

func formatGuidelineSection(original model.Index) string {
	guidelineParts := make([]string, 0, 4)
	if title := strings.TrimSpace(original.Title); title != "" {
		guidelineParts = append(guidelineParts, title)
	}
	for _, header := range []string{original.Header1, original.Header2, original.Header3} {
		if trimmed := strings.TrimSpace(header); trimmed != "" {
			guidelineParts = append(guidelineParts, trimmed)
		}
	}
	if len(guidelineParts) == 0 {
		guidelineParts = append(guidelineParts, firstNonEmpty(original.ContextID, original.ChunkID))
	}
	guidelineID := strings.Join(guidelineParts, "#")
	guidelineLink := model.GetIndexLink(original)

	var builder strings.Builder
	builder.WriteString(fmt.Sprintf("> **guideline_id:** %s<br>", guidelineID))
	builder.WriteString(fmt.Sprintf("> **guideline_link:** %s<br>", guidelineLink))
	builder.WriteString(fmt.Sprintf("> **source:** %s<br>", original.ContextID))
	builder.WriteString(fmt.Sprintf("> **score:** %.0f<br>\n", original.RerankScore*100))
	content := strings.TrimSpace(original.Chunk)
	truncated := truncateRunes(content, maxGuidelineContentLength)
	if len([]rune(content)) > maxGuidelineContentLength {
		truncated = strings.TrimSpace(truncated) + "..."
	}
	if truncated != "" {
		builder.WriteString(truncated)
		builder.WriteString("\n")
	}

	return builder.String()
}

func joinWithSeparator(items []string, separator string) string {
	result := ""
	for i, item := range items {
		result += item
		if i < len(items)-1 {
			result += separator
		}
	}
	return result
}

func truncateRunes(text string, limit int) string {
	if limit <= 0 {
		return ""
	}
	runes := []rune(text)
	if len(runes) <= limit {
		return text
	}
	return string(runes[:limit])
}

func firstNonEmpty(values ...string) string {
	for _, value := range values {
		if strings.TrimSpace(value) != "" {
			return value
		}
	}
	return ""
}

// shouldCompleteSection determines if a chunk should be completed to get full section content
func shouldCompleteSection(chunk model.Index) bool {
	// If there's no header3, this is likely a high-level section that should be completed
	return strings.TrimSpace(chunk.Header3) == ""
}

// loadAgenticSearchPrompt loads the agentic search prompt from template file
func (s *CodeReviewService) loadAgenticSearchPrompt() (string, error) {
	promptTemplate := "code_review/agentic_search.md"
	promptStr, err := s.promptParser.ParsePrompt(nil, promptTemplate)
	if err != nil {
		return "", fmt.Errorf("failed to parse agentic search prompt template: %w", err)
	}
	return promptStr, nil
}

// agenticSearch uses AI to automatically generate sub-queries for code review guideline search
func (s *CodeReviewService) agenticSearch(ctx context.Context, query string, sources []model.Source, sourceFilter map[model.Source]string) ([]model.Index, error) {
	agenticSearchStart := time.Now()

	// Load code review specific agentic search prompt from template
	agenticSearchPrompt, err := s.loadAgenticSearchPrompt()
	if err != nil {
		log.Printf("Failed to load agentic search prompt, using default: %v", err)
		agenticSearchPrompt = "You are searching for Azure SDK design guidelines to review code. Generate targeted search queries to find relevant guidelines for API design, naming conventions, parameter handling, and SDK patterns."
	}

	resp, err := s.searchClient.AgenticSearch(ctx, query, sources, sourceFilter, agenticSearchPrompt)
	if err != nil {
		log.Printf("Agentic search error: %v", err)
		return nil, err
	}

	for _, activity := range resp.Activity {
		if activity.Type == model.ActivityRecordTypeSearchIndex {
			log.Printf("Agentic search sub query: %s", activity.SearchIndexArguments.Search)
		}
	}

	if resp.Response == nil {
		return nil, nil
	}

	var docKeys []string
	for _, reference := range resp.References {
		docKeys = append(docKeys, reference.DocKey)
	}

	chunks, err := s.searchClient.BatchGetChunks(ctx, docKeys)
	if err != nil {
		log.Printf("Failed to batch get chunks: %v", err)
		return nil, err
	}

	log.Printf("Agentic search took: %v, found %d chunks", time.Since(agenticSearchStart), len(chunks))

	for i, chunk := range chunks {
		log.Printf("Chunk %d: ContextID=%s, Title=%s, Header1=%s, Header2=%s", i+1, chunk.ContextID, chunk.Title, chunk.Header1, chunk.Header2)
	}
	return chunks, nil
}

// processGuidelineChunks processes and formats guideline chunks from agentic search
func (s *CodeReviewService) processGuidelineChunks(chunks []model.Index) []string {
	processStart := time.Now()

	guidelineMap := make(map[string]model.Index)
	var mapMutex sync.Mutex

	// Deduplicate chunks
	for _, chunk := range chunks {
		key := buildGuidelineKey(chunk)
		if existing, ok := guidelineMap[key]; ok {
			if chunk.RerankScore > existing.RerankScore {
				guidelineMap[key] = chunk
			}
		} else {
			guidelineMap[key] = chunk
		}
	}

	log.Printf("=== Total Unique Guidelines Found: %d ===", len(guidelineMap))

	// Complete sections in parallel at header1 or header2 level
	var completeWg sync.WaitGroup
	for key, result := range guidelineMap {
		if shouldCompleteSection(result) {
			completeWg.Add(1)
			go func(k string, r model.Index) {
				defer completeWg.Done()
				completed := s.searchClient.CompleteChunk(r)

				mapMutex.Lock()
				guidelineMap[k] = completed
				mapMutex.Unlock()
			}(key, result)
		}
	}

	completeWg.Wait()

	// Sort by relevance score
	sorted := make([]model.Index, 0, len(guidelineMap))
	for _, result := range guidelineMap {
		sorted = append(sorted, result)
	}
	sort.Slice(sorted, func(i, j int) bool {
		if sorted[i].RerankScore == sorted[j].RerankScore {
			return sorted[i].Score > sorted[j].Score
		}
		return sorted[i].RerankScore > sorted[j].RerankScore
	})

	// Format guidelines
	var guidelineTexts []string
	for _, result := range sorted {
		guidelineTexts = append(guidelineTexts, formatGuidelineSection(result))
		if len(guidelineTexts) >= maxGuidelineSnippets {
			break
		}
	}

	log.Printf("Processing guidelines took: %v, returning %d guidelines", time.Since(processStart), len(guidelineTexts))

	return guidelineTexts
}
