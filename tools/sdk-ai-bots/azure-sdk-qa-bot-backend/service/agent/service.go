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
		model: config.AOAI_CHAT_COMPLETIONS_MODEL,
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

	if err := s.CheckArgs(req); err != nil {
		log.Printf("Request validation failed: %v", err)
		return nil, err
	}

	// 1. Build messages from the openai request
	llmMessages, reasoningModelMessages := s.buildMessages(req)

	// 2. Build query for search
	query, intension := s.buildQueryForSearch(req, reasoningModelMessages)

	// 3. Agentic search
	agenticSearchChunks, err := s.agenticSearch(ctx, req.Message.Content, req)
	if err != nil {
		log.Printf("Agentic search failed: %v", err)
		return nil, model.NewSearchFailureError(err)
	}

	// 4. Search for related documents
	chunks, err := s.searchRelatedKnowledge(req, query, agenticSearchChunks)
	if err != nil {
		log.Printf("Knowledge search failed: %v", err)
		return nil, model.NewSearchFailureError(err)
	}

	// 5. Build prompt
	prompt, err := s.buildPrompt(chunks, *req.PromptTemplate)
	if err != nil {
		log.Printf("Prompt building failed: %v", err)
		return nil, err
	}

	// 6. Get answer from LLM
	llmMessages = append(llmMessages, &azopenai.ChatRequestSystemMessage{Content: azopenai.NewChatRequestSystemMessageContent(prompt)})
	result, err := s.getLLMResult(llmMessages, *req.PromptTemplate)
	if err != nil {
		log.Printf("LLM request failed: %v", err)
		return nil, model.NewLLMServiceFailureError(err)
	}

	// 7. Process the result
	result.ID = requestID
	if req.WithFullContext != nil && *req.WithFullContext {
		fullContext := strings.Join(chunks, "-------------------------\n")
		result.FullContext = &fullContext
	}
	result.Intension = intension
	log.Printf("Total ChatCompletion time: %v", time.Since(startTime))
	return result, nil
}

func (s *CompletionService) RecongnizeIntension(messages []azopenai.ChatRequestMessageClassification) (*model.IntensionResult, error) {
	promptParser := prompt.IntensionPromptParser{}
	promptStr, err := promptParser.ParsePrompt(nil, "intension.md")
	if err != nil {
		log.Printf("Failed to parse intension prompt: %v", err)
		return nil, err
	}

	messages = append(messages, &azopenai.ChatRequestSystemMessage{Content: azopenai.NewChatRequestSystemMessageContent(promptStr)})

	resp, err := config.OpenAIClient.GetChatCompletions(context.TODO(), azopenai.ChatCompletionsOptions{
		Messages:       messages,
		DeploymentName: to.Ptr(string(config.AOAI_CHAT_REASONING_MODEL)),
	}, nil)

	if err != nil {
		log.Printf("LLM intension recognition failed: %v", err)
		return nil, model.NewLLMServiceFailureError(err)
	}

	for _, choice := range resp.Choices {
		result, err := promptParser.ParseResponse(*choice.Message.Content, "intension.md")
		if err != nil {
			log.Printf("Failed to parse intension response: %v, content: %s", err, *choice.Message.Content)
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
	defer resp.Body.Close()
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
	if contentType == "image/png" {
		return fmt.Sprintf("data:image/png;base64,%s", base64Encode), nil
	} else if contentType == "image/jpeg" {
		return fmt.Sprintf("data:image/jpeg;base64,%s", base64Encode), nil
	} else if contentType == "image/gif" {
		return fmt.Sprintf("data:image/gif;base64,%s", base64Encode), nil
	} else {
		return fmt.Sprintf("data:image/png;base64,%s", base64Encode), nil
	}
}

func (s *CompletionService) buildMessages(req *model.CompletionReq) ([]azopenai.ChatRequestMessageClassification, []azopenai.ChatRequestMessageClassification) {
	// avoid token limit error, we need to limit the number of messages in the history
	if len(req.Message.Content) > config.AOAI_CHAT_MAX_TOKENS {
		log.Printf("Message content is too long, truncating to %d characters", config.AOAI_CHAT_MAX_TOKENS)
		req.Message.Content = req.Message.Content[:config.AOAI_CHAT_MAX_TOKENS]
	}

	// This is a conversation in progress.
	// NOTE: all llmMessages, regardless of role, count against token usage for this API.
	llmMessages := []azopenai.ChatRequestMessageClassification{}
	reasoningModelMessages := []azopenai.ChatRequestMessageClassification{}

	// process additional info(image, link)
	if len(req.AdditionalInfos) > 0 {
		for _, info := range req.AdditionalInfos {
			if info.Type == model.AdditionalInfoType_Link {
				if len(info.Content) > config.AOAI_CHAT_MAX_TOKENS {
					log.Printf("Link content is too long, truncating to %d characters", config.AOAI_CHAT_MAX_TOKENS)
					info.Content = info.Content[:config.AOAI_CHAT_MAX_TOKENS]
				}
				msg := &azopenai.ChatRequestUserMessage{
					Content: azopenai.NewChatRequestUserMessageContent(fmt.Sprintf("Link URL: %s\nLink Content: %s", info.Link, info.Content)),
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

	// process history messages
	for _, message := range req.History {
		if message.Role == model.Role_Assistant {
			msg := &azopenai.ChatRequestAssistantMessage{Content: azopenai.NewChatRequestAssistantMessageContent(message.Content)}
			llmMessages = append(llmMessages, msg)
			reasoningModelMessages = append(reasoningModelMessages, msg)
		}
		if message.Role == model.Role_User {
			msg := &azopenai.ChatRequestUserMessage{Content: azopenai.NewChatRequestUserMessageContent(message.Content), Name: processName(message.Name)}
			llmMessages = append(llmMessages, msg)
			reasoningModelMessages = append(reasoningModelMessages, msg)
		}
	}

	// process current user message
	currentMessage := req.Message.Content
	if req.WithPreprocess != nil && *req.WithPreprocess {
		preProcessedMessage := preprocess.NewPreprocessService().ExtractAdditionalInfo(req.Message.Content)
		log.Println("user message with additional info:", preProcessedMessage)
		// avoid token limit error, we need to limit the number of messages in the history
		if len(preProcessedMessage) > config.AOAI_CHAT_MAX_TOKENS {
			log.Printf("Message content is too long, truncating to %d characters", config.AOAI_CHAT_MAX_TOKENS)
			preProcessedMessage = preProcessedMessage[:config.AOAI_CHAT_MAX_TOKENS]
		}
		currentMessage = preProcessedMessage
	}
	msg := &azopenai.ChatRequestUserMessage{
		Content: azopenai.NewChatRequestUserMessageContent(currentMessage),
		Name:    processName(req.Message.Name),
	}
	llmMessages = append(llmMessages, msg)
	reasoningModelMessages = append(reasoningModelMessages, msg)
	return llmMessages, reasoningModelMessages
}

func (s *CompletionService) buildQueryForSearch(req *model.CompletionReq, messages []azopenai.ChatRequestMessageClassification) (string, *model.IntensionResult) {
	query := req.Message.Content
	if req.Message.RawContent != nil && len(*req.Message.RawContent) > 0 {
		query = *req.Message.RawContent
	}
	query = preprocess.NewPreprocessService().PreprocessInput(query)
	intentStart := time.Now()
	intentResult, err := s.RecongnizeIntension(messages)
	if err != nil {
		log.Printf("ERROR: %s", err)
	} else if intentResult != nil {
		if len(req.Sources) == 0 {
			if intentResult.Category == model.QuestionCategory_Unbranded {
				req.Sources = []model.Source{model.Source_TypeSpec}
			} else {
				req.Sources = []model.Source{model.Source_TypeSpec, model.Source_TypeSpecAzure, model.Source_AzureRestAPISpec}
			}
			req.Sources = append(req.Sources, model.Source_TypeSpecQA)
		}
		if len(intentResult.Question) > 0 {
			query = intentResult.Question
		}
		log.Printf("Intent Result: category: %v, intension: %v", intentResult.Category, intentResult.Question)
	}
	log.Printf("Intent recognition took: %v", time.Since(intentStart))
	log.Printf("Searching query: %s", query)
	return query, intentResult
}

func (s *CompletionService) searchRelatedKnowledge(req *model.CompletionReq, query string, existingChunks []model.Index) ([]string, error) {
	searchStart := time.Now()
	searchClient := search.NewSearchClient()
	results, err := searchClient.SearchTopKRelatedDocuments(query, *req.TopK, req.Sources)
	if err != nil {
		return nil, fmt.Errorf("failed to search for related documents: %w", err)
	}
	log.Printf("Vector Search took: %v", time.Since(searchStart))

	// filter unrelevant results
	needCompleteFiles := make([]model.Index, 0)
	needCompleteChunks := make([]model.Index, 0)
	normalResult := make([]model.Index, 0)
	for i, result := range results {
		if result.RerankScore < model.RerankScoreLowRelevanceThreshold {
			log.Printf("Skipping result with low score: %s/%s, score: %f", result.ContextID, result.Title, result.RerankScore)
			continue
		}
		if result.ContextID == string(model.Source_TypeSpecQA) || result.ContextID == string(model.Source_TypeSpecMigration) {
			needCompleteChunks = append(needCompleteChunks, result)
			log.Printf("Vector searched chunk(Q&A): %+v", result)
			continue
		}
		if result.RerankScore >= model.RerankScoreRelevanceThreshold {
			needCompleteFiles = append(needCompleteFiles, result)
			log.Printf("Vector searched chunk(high score): %+v", result)
			continue
		}
		// every source first document should be completed
		if i == 0 || (results[i].ContextID != results[i-1].ContextID) {
			needCompleteFiles = append(needCompleteFiles, result)
			log.Printf("Vector searched chunk(first document of source): %+v", result)
			continue
		}
		log.Printf("Vector searched chunk: %+v", result)
		normalResult = append(normalResult, result)
	}
	supplyNum := 5
	if len(needCompleteFiles) < supplyNum {
		log.Printf("No results found with high relevance score, supply with normal results")
		if len(normalResult) < supplyNum {
			supplyNum = len(normalResult)
		}
		needCompleteFiles = normalResult[:supplyNum]
		normalResult = normalResult[supplyNum:]
	}

	chunkProcessStart := time.Now()
	files := make(map[string]bool)
	chunks := make(map[string]bool)
	mergedChunks := make([]model.Index, 0)
	for _, result := range needCompleteFiles {
		if files[result.Title] {
			continue
		}
		files[result.Title] = true
		mergedChunks = append(mergedChunks, model.Index{
			Title:     result.Title,
			ContextID: result.ContextID,
			Header1:   result.Header1,
		})
		log.Printf("Complete chunk: %s/%s", result.ContextID, result.Title)
	}
	mergedChunks = append(mergedChunks, needCompleteChunks...)
	var wg sync.WaitGroup
	wg.Add(len(mergedChunks))
	for i := range mergedChunks {
		i := i
		go func() {
			defer wg.Done()
			mergedChunks[i] = searchClient.CompleteChunk(mergedChunks[i])
		}()
	}
	wg.Wait()

	result := make([]string, 0)
	for _, mergedChunk := range mergedChunks {
		chunk := processDocument(mergedChunk)
		result = append(result, chunk)
	}
	for _, existingChunk := range existingChunks {
		if files[existingChunk.Title] {
			continue
		}
		chunk := processChunk(existingChunk)
		result = append(result, chunk)
		chunks[existingChunk.Chunk] = true
	}
	for _, normalChunk := range normalResult {
		if files[normalChunk.Title] {
			continue
		}
		if chunks[normalChunk.Chunk] {
			continue
		}
		chunk := processChunk(normalChunk)
		result = append(result, chunk)
	}
	log.Printf("Chunk processing took: %v", time.Since(chunkProcessStart))
	return result, nil
}

func (s *CompletionService) buildPrompt(chunks []string, promptTemplate string) (string, error) {
	// make sure the tokens of chunks limited in 100000 tokens
	tokenCnt := 0
	for i, chunk := range chunks {
		tokenCnt += len(chunk)
		if tokenCnt > config.AOAI_CHAT_CONTEXT_MAX_TOKENS {
			log.Printf("Chunks exceed max token limit, truncating to %d tokens", config.AOAI_CHAT_CONTEXT_MAX_TOKENS)
			chunks = chunks[:i+1] // truncate the chunks to the current index
			break
		}
	}
	promptParser := prompt.DefaultPromptParser{}
	promptStr, err := promptParser.ParsePrompt(map[string]string{"context": strings.Join(chunks, "-------------------------\n")}, promptTemplate)
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
			"category": map[string]interface{}{
				"type":        "string",
				"description": "the category of user's question(eg: typespec synax, typespec migration, ci-failure and so on)",
			},
			"reasoning_progress": map[string]interface{}{
				"type":        "string",
				"description": "output your reasoning progress of generating the answer",
			},
		},
		"required":             []string{"has_result", "answer", "references", "category", "reasoning_progress"},
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
		TopP: to.Ptr(float32(config.AOAI_CHAT_COMPLETIONS_TOP_P)),
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
	for _, choice := range resp.Choices {
		answer, err := promptParser.ParseResponse(*choice.Message.Content, promptTemplate)
		if err != nil {
			log.Printf("ERROR: %s, content:%s", err, *choice.Message.Content)
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
	for _, chunk := range chunks {
		log.Printf("Agentic searched chunk: %+v", chunk)
	}
	log.Printf("Agentic search took: %v", time.Since(agenticSearchStart))
	return chunks, nil
}
