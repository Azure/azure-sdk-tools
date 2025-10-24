package agent

import (
	"context"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"net/http"
	"strings"
	"sync"
	"time"

	"github.com/Azure/azure-sdk-for-go/sdk/ai/azopenai"
	"github.com/Azure/azure-sdk-for-go/sdk/azcore/policy"
	"github.com/Azure/azure-sdk-for-go/sdk/azcore/to"
	"github.com/Azure/azure-sdk-for-go/sdk/azidentity"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/preprocess"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/prompt"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/search"
	"github.com/google/uuid"
)

type CompletionService struct {
	model string
}

func NewCompletionService() (*CompletionService, error) {
	return &CompletionService{
		model: config.AppConfig.AOAI_CHAT_COMPLETIONS_MODEL,
	}, nil
}

func (s *CompletionService) CheckArgs(req *model.CompletionReq) error {
	if req == nil {
		return model.NewInvalidRequestError("request is nil", nil)
	}
	if req.Message.Content == "" {
		return model.NewEmptyContentError()
	}
	if req.TopK == nil {
		topK := 20
		req.TopK = &topK
	}
	tenantConfig, hasConfig := config.GetTenantConfig(req.TenantID)
	if hasConfig {
		if req.Sources == nil {
			req.Sources = tenantConfig.Sources
		}
		if req.PromptTemplate == nil {
			req.PromptTemplate = &tenantConfig.PromptTemplate
		}
		if req.IntentionPromptTemplate == nil {
			req.IntentionPromptTemplate = &tenantConfig.IntentionPromptTemplate
		}
	} else {
		return model.NewInvalidTenantIDError(string(req.TenantID))
	}
	return nil
}

func (s *CompletionService) ChatCompletion(ctx context.Context, req *model.CompletionReq) (*model.CompletionResp, error) {
	startTime := time.Now()
	requestID := uuid.New().String()
	log.SetPrefix(fmt.Sprintf("[RequestID: %s] ", requestID))

	jsonReq, err := json.Marshal(req)
	if err != nil {
		log.Printf("Failed to marshal request: %v\n", err)
	} else {
		log.Printf("Request: %s", jsonReq)
	}

	if err = s.CheckArgs(req); err != nil {
		log.Printf("Request validation failed: %v", err)
		return nil, err
	}

	// 1. Build messages from the openai request
	llmMessages, reasoningModelMessages := s.buildMessages(req)

	// 2. Build query for search and recognize intention
	query, intention := s.buildQueryForSearch(req, reasoningModelMessages)

	// 3. Check if we need RAG processing
	var chunks []string
	var prompt string

	if intention != nil && !intention.NeedsRagProcessing {
		// Skip RAG workflow for greetings/announcements
		log.Printf("Skipping RAG workflow - non-technical message detected")
		chunks = []string{}
		prompt = "You are a helpful AI assistant. Respond naturally to the user's message."
	} else {
		// Run agentic search and knowledge search in parallel, then merge results
		var err error
		chunks, err = s.runParallelSearchAndMergeResults(ctx, req, query)
		if err != nil {
			log.Printf("Parallel search failed: %v", err)
			return nil, model.NewSearchFailureError(err)
		}

		// Build prompt with retrieved context
		prompt, err = s.buildPrompt(intention, chunks, *req.PromptTemplate)
		if err != nil {
			log.Printf("Prompt building failed: %v", err)
			return nil, err
		}
	}

	// 4. Get answer from LLM
	llmMessages = append(llmMessages, &azopenai.ChatRequestSystemMessage{Content: azopenai.NewChatRequestSystemMessageContent(prompt)})
	result, err := s.getLLMResult(llmMessages, *req.PromptTemplate)
	if err != nil {
		log.Printf("LLM request failed: %v", err)
		return nil, model.NewLLMServiceFailureError(err)
	}

	// 5. Process the result
	result.ID = requestID
	if req.WithFullContext != nil && *req.WithFullContext {
		fullContext := strings.Join(chunks, "-------------------------\n")
		result.FullContext = &fullContext
	}
	result.Intention = intention
	log.Printf("Total ChatCompletion time: %v", time.Since(startTime))
	return result, nil
}

func (s *CompletionService) RecognizeIntention(promptTemplate string, messages []azopenai.ChatRequestMessageClassification) (*model.IntentionResult, error) {
	promptParser := prompt.IntentionPromptParser{}
	promptStr, err := promptParser.ParsePrompt(nil, promptTemplate)
	if err != nil {
		log.Printf("Failed to parse intention prompt: %v", err)
		return nil, err
	}

	messages = append(messages, &azopenai.ChatRequestSystemMessage{Content: azopenai.NewChatRequestSystemMessageContent(promptStr)})

	resp, err := config.OpenAIClient.GetChatCompletions(context.TODO(), azopenai.ChatCompletionsOptions{
		Messages:       messages,
		DeploymentName: to.Ptr(string(config.AppConfig.AOAI_CHAT_REASONING_MODEL)),
	}, nil)

	if err != nil {
		log.Printf("LLM intention recognition failed: %v", err)
		return nil, model.NewLLMServiceFailureError(err)
	}

	if len(resp.Choices) > 0 {
		result, err := promptParser.ParseResponse(*resp.Choices[0].Message.Content, "intention.md")
		if err != nil {
			log.Printf("Failed to parse intention response: %v, content: %s", err, *resp.Choices[0].Message.Content)
			return nil, err
		}
		return result, nil
	}

	return nil, model.NewLLMServiceFailureError(fmt.Errorf("no valid response received from LLM"))
}

func processDocument(result model.Index) string {
	chunk := fmt.Sprintf("- document_category: %s\n", result.ContextID)
	chunk += fmt.Sprintf("- document_filename: %s\n", result.Title)
	chunk += fmt.Sprintf("- document_title: %s\n", result.Header1)
	chunk += fmt.Sprintf("- document_link: %s\n", model.GetIndexLink(result))
	chunk += fmt.Sprintf("- document_content: %s\n", result.Chunk)
	return chunk
}

func processChunk(result model.Index) string {
	chunk := fmt.Sprintf("- document_category: %s\n", result.ContextID)
	chunk += fmt.Sprintf("- document_filename: %s\n", result.Title)
	chunk += fmt.Sprintf("- document_link: %s\n", model.GetIndexLink(result))
	chunk += fmt.Sprintf("- chunk_header_1: %s\n", result.Header1)
	chunk += fmt.Sprintf("- chunk_header_2: %s\n", result.Header2)
	chunk += fmt.Sprintf("- chunk_header_3: %s\n", result.Header3)
	chunk += fmt.Sprintf("- chunk_content: %s\n", result.Chunk)
	return chunk
}

func processName(name *string) *string {
	if name == nil {
		return nil
	}
	// Remove spaces from the name
	processedName := strings.ReplaceAll(*name, " ", "")
	if len(processedName) == 0 {
		return nil
	}
	return to.Ptr(processedName)
}

func getImageDataURI(url string) (string, error) {
	if !strings.HasPrefix(url, "https://smba.trafficmanager.net") {
		log.Printf("URL does not start with expected prefix: %s", url)
		return url, nil
	}
	cred, err := azidentity.NewManagedIdentityCredential(&azidentity.ManagedIdentityCredentialOptions{
		ID: azidentity.ClientID(config.GetBotClientID()),
	})
	if err != nil {
		log.Printf("Failed to create managed identity credential: %v", err)
		return "", err
	}
	token, err := cred.GetToken(context.Background(), policy.TokenRequestOptions{
		TenantID: config.GetBotTenantID(),
		Scopes:   []string{"https://api.botframework.com/.default"},
	})
	if err != nil {
		log.Printf("Failed to get token: %v", err)
		return "", err
	}
	req, err := http.NewRequest("GET", url, nil)
	if err != nil {
		log.Printf("Failed to create request: %v", err)
		return "", err
	}
	req.Header.Set("Authorization", "Bearer "+token.Token)
	client := &http.Client{}
	resp, err := client.Do(req)
	if err != nil {
		log.Printf("Failed to download attachment: %v", err)
		return "", err
	}
	defer func() {
		if err = resp.Body.Close(); err != nil {
			log.Printf("Failed to close response body: %v", err)
		}
	}()
	if resp.StatusCode != http.StatusOK {
		return "", fmt.Errorf("failed to download attachment, status code: %d", resp.StatusCode)
	}
	body, err := io.ReadAll(resp.Body)
	if err != nil {
		log.Printf("Failed to read response body: %v", err)
		return "", err
	}
	base64Encode := base64.StdEncoding.EncodeToString(body)
	contentType := resp.Header.Get("Content-Type")
	switch contentType {
	case "image/png":
		return fmt.Sprintf("data:image/png;base64,%s", base64Encode), nil
	case "image/jpeg":
		return fmt.Sprintf("data:image/jpeg;base64,%s", base64Encode), nil
	case "image/gif":
		return fmt.Sprintf("data:image/gif;base64,%s", base64Encode), nil
	default:
		return fmt.Sprintf("data:image/png;base64,%s", base64Encode), nil
	}
}

func (s *CompletionService) buildMessages(req *model.CompletionReq) ([]azopenai.ChatRequestMessageClassification, []azopenai.ChatRequestMessageClassification) {
	// avoid token limit error, we need to limit the number of messages in the history
	if len(req.Message.Content) > config.AppConfig.AOAI_CHAT_MAX_TOKENS {
		log.Printf("Message content is too long, truncating to %d characters", config.AppConfig.AOAI_CHAT_MAX_TOKENS)
		req.Message.Content = req.Message.Content[:config.AppConfig.AOAI_CHAT_MAX_TOKENS]
	}

	// Preprocess HTML content if it contains HTML entities or tags
	preprocessService := preprocess.NewPreprocessService()
	if strings.Contains(req.Message.Content, "\\u003c") || strings.Contains(req.Message.Content, "&lt;") || strings.Contains(req.Message.Content, "<") {
		log.Printf("Detected HTML content, preprocessing...")
		req.Message.Content = preprocessService.PreprocessHTMLContent(req.Message.Content)
	}

	// This is a conversation in progress.
	// NOTE: all llmMessages, regardless of role, count against token usage for this API.
	llmMessages := []azopenai.ChatRequestMessageClassification{}
	reasoningModelMessages := []azopenai.ChatRequestMessageClassification{}

	// process history messages
	for _, message := range req.History {
		// Preprocess HTML content in history messages
		content := message.Content
		if strings.Contains(content, "\\u003c") || strings.Contains(content, "&lt;") || strings.Contains(content, "<") {
			log.Printf("Detected HTML content in history message, preprocessing...")
			content = preprocessService.PreprocessHTMLContent(content)
		}

		if message.Role == model.Role_Assistant {
			msg := &azopenai.ChatRequestAssistantMessage{Content: azopenai.NewChatRequestAssistantMessageContent(content)}
			llmMessages = append(llmMessages, msg)
			reasoningModelMessages = append(reasoningModelMessages, msg)
		}
		if message.Role == model.Role_User {
			msg := &azopenai.ChatRequestUserMessage{Content: azopenai.NewChatRequestUserMessageContent(content), Name: processName(message.Name)}
			llmMessages = append(llmMessages, msg)
			reasoningModelMessages = append(reasoningModelMessages, msg)
		}
	}

	// process additional info(image, link)
	if len(req.AdditionalInfos) > 0 {
		for _, info := range req.AdditionalInfos {
			if info.Type == model.AdditionalInfoType_Link {
				content := info.Content
				if len(content) > config.AppConfig.AOAI_CHAT_MAX_TOKENS {
					log.Printf("Link content is too long, truncating to %d characters", config.AppConfig.AOAI_CHAT_MAX_TOKENS)
					content = content[:config.AppConfig.AOAI_CHAT_MAX_TOKENS]
				}
				var msg *azopenai.ChatRequestUserMessage
				if strings.Contains(content, "graph.microsoft.com") {
					msg = &azopenai.ChatRequestUserMessage{
						Content: azopenai.NewChatRequestUserMessageContent(fmt.Sprintf("Image URL: %s\nImage Content: %s", info.Link, content)),
					}
				} else {
					msg = &azopenai.ChatRequestUserMessage{
						Content: azopenai.NewChatRequestUserMessageContent(fmt.Sprintf("Link URL: %s\nLink Content: %s", info.Link, content)),
					}
				}
				llmMessages = append(llmMessages, msg)
				reasoningModelMessages = append(reasoningModelMessages, msg)
			} else if info.Type == model.AdditionalInfoType_Image {
				link, err := getImageDataURI(info.Link)
				if err != nil {
					log.Printf("Failed to get image data URI: %v", err)
					continue
				}
				log.Println("Image link:", link)
				llmMessages = append(llmMessages, &azopenai.ChatRequestUserMessage{
					Content: azopenai.NewChatRequestUserMessageContent(
						[]azopenai.ChatCompletionRequestMessageContentPartClassification{
							&azopenai.ChatCompletionRequestMessageContentPartImage{
								ImageURL: to.Ptr(azopenai.ChatCompletionRequestMessageContentPartImageURL{
									URL: to.Ptr(link),
								}),
							},
						},
					),
				})
			}
		}
	}

	// process current user message
	currentMessage := req.Message.Content
	msg := &azopenai.ChatRequestUserMessage{
		Content: azopenai.NewChatRequestUserMessageContent(currentMessage),
		Name:    processName(req.Message.Name),
	}
	llmMessages = append(llmMessages, msg)
	reasoningModelMessages = append(reasoningModelMessages, msg)
	return llmMessages, reasoningModelMessages
}

func (s *CompletionService) buildQueryForSearch(req *model.CompletionReq, messages []azopenai.ChatRequestMessageClassification) (string, *model.IntentionResult) {
	query := req.Message.Content
	if req.Message.RawContent != nil && len(*req.Message.RawContent) > 0 {
		query = *req.Message.RawContent
	}
	intentStart := time.Now()
	intentResult, err := s.RecognizeIntention(*req.IntentionPromptTemplate, messages)
	if err != nil {
		log.Printf("ERROR: %s", err)
	} else if intentResult != nil {
		if len(req.Sources) == 0 {
			if intentResult.Scope == model.QuestionScope_Unbranded {
				req.Sources = []model.Source{model.Source_TypeSpec, model.Source_TypeSpecHttpSpecs}
			} else {
				req.Sources = []model.Source{model.Source_TypeSpec, model.Source_TypeSpecAzure, model.Source_AzureRestAPISpec}
			}
			req.Sources = append(req.Sources, model.Source_TypeSpecQA)
		}
		if len(intentResult.Question) > 0 {
			query = fmt.Sprintf("category:%s question:%s", intentResult.Category, intentResult.Question)
		}
		log.Printf("Intent Result: %+v", intentResult)
	}
	query = preprocess.NewPreprocessService().PreprocessInput(req.TenantID, query)
	log.Printf("Intent recognition took: %v", time.Since(intentStart))
	log.Printf("Searching query: %s", query)
	return query, intentResult
}

func (s *CompletionService) buildPrompt(intention *model.IntentionResult, chunks []string, promptTemplate string) (string, error) {
	// make sure the tokens of chunks limited in 100000 tokens
	tokenCnt := 0
	for i, chunk := range chunks {
		if len(chunk) > config.AppConfig.AOAI_CHAT_MAX_TOKENS {
			log.Printf("Chunk %d is too long, truncating to %d characters", i+1, config.AppConfig.AOAI_CHAT_MAX_TOKENS)
			chunks[i] = chunk[:config.AppConfig.AOAI_CHAT_MAX_TOKENS]
		}
		tokenCnt += len(chunks[i])
		if tokenCnt > config.AppConfig.AOAI_CHAT_CONTEXT_MAX_TOKENS {
			log.Printf("%v chunks has exceed max token limit, truncating to %d tokens", i+1, config.AppConfig.AOAI_CHAT_CONTEXT_MAX_TOKENS)
			chunks = chunks[:i+1] // truncate the chunks to the current index
			break
		}
	}
	intentionStr, _ := json.Marshal(intention)
	promptParser := prompt.DefaultPromptParser{}
	promptStr, err := promptParser.ParsePrompt(map[string]string{
		"context":   strings.Join(chunks, "-------------------------\n"),
		"intention": string(intentionStr),
	}, promptTemplate)
	if err != nil {
		log.Printf("ERROR: %s", err)
		return "", err
	}
	return promptStr, nil
}

func (s *CompletionService) getLLMResult(messages []azopenai.ChatRequestMessageClassification, promptTemplate string) (*model.CompletionResp, error) {
	completionStart := time.Now()
	schema := map[string]interface{}{
		"type": "object",
		"properties": map[string]interface{}{
			"has_result": map[string]interface{}{
				"type":        "boolean",
				"description": "true if you can answer current question",
			},
			"answer": map[string]interface{}{
				"type":        "string",
				"description": "your complete, formatted response",
			},
			"references": map[string]interface{}{
				"type":        "array",
				"description": "put all supporting for your answer references from Knowledge",
				"items": map[string]interface{}{
					"type": "object",
					"properties": map[string]interface{}{
						"title": map[string]interface{}{
							"type":        "string",
							"description": "section or document title",
						},
						"source": map[string]interface{}{
							"type":        "string",
							"description": "document source",
						},
						"link": map[string]interface{}{
							"type":        "string",
							"description": "complete link to the reference",
						},
						"content": map[string]interface{}{
							"type":        "string",
							"description": "relevant extract that supports your answer",
						},
					},
					"required":             []string{"title", "source", "link", "content"},
					"additionalProperties": false,
				},
			},
			"reasoning_progress": map[string]interface{}{
				"type":        "string",
				"description": "output your reasoning progress of generating the answer",
			},
		},
		"required":             []string{"has_result", "answer", "references", "reasoning_progress"},
		"additionalProperties": false,
	}

	schemaBytes, err := json.Marshal(schema)
	if err != nil {
		log.Printf("ERROR marshaling schema: %s", err)
		return nil, err
	}

	resp, err := config.OpenAIClient.GetChatCompletions(context.TODO(), azopenai.ChatCompletionsOptions{
		// This is a conversation in progress.
		// NOTE: all messages count against token usage for this API.
		Messages:       messages,
		DeploymentName: &s.model,
		ResponseFormat: &azopenai.ChatCompletionsJSONSchemaResponseFormat{
			JSONSchema: &azopenai.ChatCompletionsJSONSchemaResponseFormatJSONSchema{
				Name:        to.Ptr("bot-response-format"),
				Description: to.Ptr("Bot Response Format"),
				Schema:      schemaBytes,
				Strict:      to.Ptr(true),
			},
		},
		TopP: to.Ptr(float32(config.AppConfig.AOAI_CHAT_COMPLETIONS_TOP_P)),
	}, nil)

	if err != nil {
		// Check if this is a rate limit error (429)
		if strings.Contains(err.Error(), "429") || strings.Contains(err.Error(), "Too Many Requests") {
			return nil, model.NewLLMRateLimitFailureError(err)
		}
		log.Printf("ERROR: %s", err)
		return nil, err
	}
	log.Printf("OpenAI completion took: %v", time.Since(completionStart))
	promptParser := prompt.DefaultPromptParser{}
	if len(resp.Choices) > 0 {
		answer, err := promptParser.ParseResponse(*resp.Choices[0].Message.Content, promptTemplate)
		if err != nil {
			log.Printf("ERROR: %s, content:%s", err, *resp.Choices[0].Message.Content)
			return nil, err
		}
		return answer, nil
	}
	log.Printf("No choices found in response")
	return nil, fmt.Errorf("no choices found in response")
}

func (s *CompletionService) agenticSearch(ctx context.Context, query string, req *model.CompletionReq) ([]model.Index, error) {
	agenticSearchStart := time.Now()
	searchClient := search.NewSearchClient()

	// Get the tenant-specific agentic search prompt
	tenantConfig, hasConfig := config.GetTenantConfig(req.TenantID)
	agenticSearchPrompt := ""
	if hasConfig {
		agenticSearchPrompt = tenantConfig.AgenticSearchPrompt
	}

	resp, err := searchClient.AgenticSearch(ctx, query, req.Sources, agenticSearchPrompt)
	if err != nil {
		log.Printf("ERROR: %s", err)
		return nil, err
	}
	log.Printf("Agentic search sub queries: %+v", resp.Activity)
	if resp.Response == nil {
		return nil, nil
	}
	var docKeys []string
	for _, reference := range resp.References {
		docKeys = append(docKeys, reference.DocKey)
	}
	chunks, err := searchClient.BatchGetChunks(ctx, docKeys)
	if err != nil {
		log.Printf("ERROR: %s", err)
		return nil, err
	}
	log.Printf("Agentic search took: %v", time.Since(agenticSearchStart))
	return chunks, nil
}

// runParallelSearchAndMergeResults runs agentic search and knowledge search in parallel where possible,
// then merges and processes their results
func (s *CompletionService) runParallelSearchAndMergeResults(ctx context.Context, req *model.CompletionReq, query string) ([]string, error) {
	parallelSearchStart := time.Now()

	// Use channels to collect results from parallel operations
	type agenticResult struct {
		chunks []model.Index
		err    error
	}

	type knowledgeResult struct {
		rawResults []model.Index
		err        error
	}

	agenticCh := make(chan agenticResult, 1)
	knowledgeCh := make(chan knowledgeResult, 1)

	// Start agentic search in a goroutine
	go func() {
		defer close(agenticCh)
		chunks, err := s.agenticSearch(ctx, req.Message.Content, req)
		agenticCh <- agenticResult{chunks: chunks, err: err}
	}()

	// Start knowledge search in parallel (without agentic chunks for now)
	go func() {
		defer close(knowledgeCh)
		rawResults, err := s.searchKnowledgeBase(req, query)
		knowledgeCh <- knowledgeResult{rawResults: rawResults, err: err}
	}()

	// Wait for both searches to complete
	agenticRes := <-agenticCh
	knowledgeRes := <-knowledgeCh

	if agenticRes.err != nil && knowledgeRes.err != nil {
		return nil, fmt.Errorf("both agentic and knowledge searches failed: agentic error: %v, knowledge error: %v", agenticRes.err, knowledgeRes.err)
	}

	var agenticChunks []model.Index
	if agenticRes.err != nil {
		log.Printf("Agentic search failed: %v", agenticRes.err)
		agenticChunks = []model.Index{}
	} else {
		agenticChunks = agenticRes.chunks
	}

	if knowledgeRes.err != nil {
		return nil, knowledgeRes.err
	}

	// Merge and process the results
	mergedChunks := s.mergeAndProcessSearchResults(req, agenticChunks, knowledgeRes.rawResults)

	log.Printf("Parallel search and merge took: %v", time.Since(parallelSearchStart))
	return mergedChunks, nil
}

// searchKnowledgeBase performs the core knowledge search without dependency on agentic results
func (s *CompletionService) searchKnowledgeBase(req *model.CompletionReq, query string) ([]model.Index, error) {
	searchStart := time.Now()
	searchClient := search.NewSearchClient()
	sourceFilter := map[model.Source]string{}
	if tenantConfig, hasConfig := config.GetTenantConfig(req.TenantID); hasConfig && tenantConfig.SourceFilter != nil {
		sourceFilter = tenantConfig.SourceFilter
	}
	results, err := searchClient.SearchTopKRelatedDocuments(query, *req.TopK, req.Sources, sourceFilter)
	if err != nil {
		return nil, fmt.Errorf("failed to search for related documents: %w", err)
	}
	log.Printf("Vector Search took: %v", time.Since(searchStart))
	return results, nil
}

// mergeAndProcessSearchResults intelligently merges agentic and knowledge search results,
// prioritizes them based on relevance and source, then processes them
func (s *CompletionService) mergeAndProcessSearchResults(req *model.CompletionReq, agenticChunks []model.Index, knowledgeResults []model.Index) []string {
	mergeStart := time.Now()

	allChunks := make([]model.Index, 0)
	processedChunks := make(map[string]bool) // track processed chunk content to avoid duplicates
	processedFiles := make(map[string]bool)  // track processed file titles to avoid duplicates

	// Separate chunks that need completion vs those that can be used as-is
	needCompleteFiles := make([]model.Index, 0)
	needCompleteChunks := make([]model.Index, 0)

	// Add agentic chunks with high priority (they were specifically found by AI reasoning)
	topK := 10
	highReleventTopK := 2
	if len(agenticChunks) > topK {
		agenticChunks = agenticChunks[:topK] // Limit to TopK results
	}
	for _, chunk := range agenticChunks {
		// Skip if we've already seen this chunk content
		chunkKey := fmt.Sprintf("%s|%s", chunk.Title, chunk.Chunk)
		if processedChunks[chunkKey] {
			continue
		}
		if processedFiles[chunk.Title] {
			continue
		}
		processedChunks[chunkKey] = true
		if strings.HasPrefix(chunk.ContextID, "static") {
			needCompleteChunks = append(needCompleteChunks, chunk)
			continue
		}
		log.Printf("Agentic searched chunk: %+v", chunk)
		allChunks = append(allChunks, chunk)
	}
	completeFileMaxCnt := 5

	// Add knowledge search results with scoring based on relevance
	for i, result := range knowledgeResults {
		// Skip low relevance results
		if result.RerankScore < model.RerankScoreLowRelevanceThreshold {
			log.Printf("Skipping result with low score: %s/%s, score: %f", result.ContextID, result.Title, result.RerankScore)
			continue
		}

		// Skip if we've already seen this chunk content
		chunkKey := fmt.Sprintf("%s|%s", result.Title, result.Chunk)
		if processedChunks[chunkKey] {
			continue
		}
		if processedFiles[result.Title] {
			continue
		}
		processedChunks[chunkKey] = true

		log.Printf("Vector searched chunk: %+v, rerankScore: %f", result, result.RerankScore)

		if strings.HasPrefix(result.ContextID, "static") {
			needCompleteChunks = append(needCompleteChunks, result)
			continue
		}
		if len(needCompleteFiles) < completeFileMaxCnt && result.RerankScore >= model.RerankScoreRelevanceThreshold {
			needCompleteFiles = append(needCompleteFiles, result)
			processedFiles[result.Title] = true
			continue
		}
		if len(needCompleteFiles) < completeFileMaxCnt && i < highReleventTopK {
			needCompleteFiles = append(needCompleteFiles, result)
			processedFiles[result.Title] = true
			continue
		}
		if len(needCompleteFiles) < completeFileMaxCnt && i > 0 && knowledgeResults[i-1].ContextID != knowledgeResults[i].ContextID {
			needCompleteFiles = append(needCompleteFiles, result)
			processedFiles[result.Title] = true
			continue
		}
		allChunks = append(allChunks, result)
	}

	searchClient := search.NewSearchClient()

	// Prepare chunks for completion
	files := make([]model.Index, 0)
	for _, result := range needCompleteFiles {
		files = append(files, model.Index{
			Title:     result.Title,
			ContextID: result.ContextID,
			Header1:   result.Header1,
		})
	}
	completedFilesCnt := len(files)
	files = append(files, needCompleteChunks...)

	// Complete chunks in parallel
	var wg sync.WaitGroup
	wg.Add(len(files))
	for i := range files {
		i := i
		go func() {
			defer wg.Done()
			files[i] = searchClient.CompleteChunk(files[i])
		}()
	}
	wg.Wait()

	// Build final result
	result := make([]string, 0)

	// Add completed chunks first (avoid duplicates by chunk content)
	for _, file := range files {
		chunk := processDocument(file)
		result = append(result, chunk)
		log.Printf("✓ Completed document: %s/%s", file.ContextID, file.Title)
	}

	// Add remaining ready chunks (avoid duplicates by chunk content)
	for _, chunk := range allChunks {
		content := processChunk(chunk)
		result = append(result, content)
		log.Printf("- Normal chunks: %s/%s#%s#%s#%s", chunk.ContextID, chunk.Title, chunk.Header1, chunk.Header2, chunk.Header3)
	}

	log.Printf("Search merge summary: %d agentic + %d knowledge → %d completed docs + %d q&a + %d chunks",
		len(agenticChunks), len(knowledgeResults), completedFilesCnt, len(needCompleteChunks), len(allChunks))
	log.Printf("Merge and processing took: %v", time.Since(mergeStart))
	return result
}
