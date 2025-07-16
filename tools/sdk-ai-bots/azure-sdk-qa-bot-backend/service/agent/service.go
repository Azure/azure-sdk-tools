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
		return fmt.Errorf("request is nil")
	}
	if req.Message.Content == "" {
		return fmt.Errorf("message content is empty")
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
		log.Printf("ERROR: %s", err)
		return nil, err
	}

	// 1. Build messages from the openai request
	llmMessages, _ := s.buildMessages(req)

	// 2. Build query for search
	query := s.buildQueryForSearch(req, llmMessages)

	// 3. Agentic search
	agenticSearchChunks, err := s.agenticSearch(ctx, req.Message.Content, req)
	if err != nil {
		log.Printf("ERROR: %s", err)
		return nil, err
	}

	// 4. Search for related documents
	chunks, err := s.searchRelatedKnowledge(req, query, agenticSearchChunks)
	if err != nil {
		log.Printf("ERROR: %s", err)
		return nil, err
	}

	// 5. Build prompt
	prompt, err := s.buildPrompt(chunks, *req.PromptTemplate)
	if err != nil {
		log.Printf("ERROR: %s", err)
		return nil, err
	}

	// 6. Get answer from LLM
	llmMessages = append(llmMessages, &azopenai.ChatRequestSystemMessage{Content: azopenai.NewChatRequestSystemMessageContent(prompt)})
	result, err := s.getLLMResult(llmMessages, *req.PromptTemplate)
	if err != nil {
		log.Printf("ERROR: %s", err)
		return nil, err
	}

	// 7. Process the result
	result.ID = requestID
	if req.WithFullContext != nil && *req.WithFullContext {
		fullContext := strings.Join(chunks, "-------------------------\n")
		result.FullContext = &fullContext
	}

	log.Printf("Total ChatCompletion time: %v", time.Since(startTime))
	return result, nil
}

func (s *CompletionService) RecongnizeIntension(messages []azopenai.ChatRequestMessageClassification) (*model.IntensionResult, error) {
	promptParser := prompt.IntensionPromptParser{}
	promptStr, error := promptParser.ParsePrompt(nil, "intension.md")
	if error != nil {
		log.Printf("ERROR: %s", error)
		return nil, error
	}
	messages = append(messages, &azopenai.ChatRequestSystemMessage{Content: azopenai.NewChatRequestSystemMessageContent(promptStr)})
	// var temperature float32 = 0.0001
	resp, err := config.OpenAIClient.GetChatCompletions(context.TODO(), azopenai.ChatCompletionsOptions{
		// This is a conversation in progress.
		// NOTE: all messages count against token usage for this API.
		Messages:       messages,
		DeploymentName: to.Ptr(string(config.AOAI_CHAT_REASONING_MODEL)),
		// Temperature:    &temperature,
	}, nil)

	if err != nil {
		// TODO: Update the following line with your application specific error handling logic
		log.Printf("ERROR: %s", err)
		return nil, err
	}

	for _, choice := range resp.Choices {
		result, error := promptParser.ParseResponse(*choice.Message.Content, "intension.md")
		if error != nil {
			log.Printf("ERROR: %s, content:%s", err, *choice.Message.Content)
			return nil, error
		}
		return result, nil
	}
	return nil, nil
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

func getImageDataURI(url string) string {
	if !strings.HasPrefix(url, "https://smba.trafficmanager.net") {
		log.Printf("URL does not start with expected prefix: %s", url)
		return url
	}
	cred, err := azidentity.NewManagedIdentityCredential(&azidentity.ManagedIdentityCredentialOptions{
		ID: azidentity.ClientID(config.GetBotClientID()),
	})
	if err != nil {
		log.Printf("Failed to create managed identity credential: %v", err)
		return url
	}
	token, err := cred.GetToken(context.Background(), policy.TokenRequestOptions{
		TenantID: config.GetBotTenantID(),
		Scopes:   []string{"https://api.botframework.com/.default"},
	})
	if err != nil {
		log.Printf("Failed to get token: %v", err)
		return url
	}
	req, err := http.NewRequest("GET", url, nil)
	if err != nil {
		log.Printf("Failed to create request: %v", err)
		return url
	}
	req.Header.Set("Authorization", "Bearer "+token.Token)
	client := &http.Client{}
	resp, err := client.Do(req)
	if err != nil {
		log.Printf("Failed to download attachment: %v", err)
		return url
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		log.Printf("Failed to download attachment, status code: %d", resp.StatusCode)
		return url
	}
	body, err := io.ReadAll(resp.Body)
	if err != nil {
		log.Printf("Failed to read response body: %v", err)
		return url
	}
	base64Encode := base64.StdEncoding.EncodeToString(body)
	contentType := resp.Header.Get("Content-Type")
	if contentType == "image/png" {
		return fmt.Sprintf("data:image/png;base64,%s", base64Encode)
	} else if contentType == "image/jpeg" {
		return fmt.Sprintf("data:image/jpeg;base64,%s", base64Encode)
	} else if contentType == "image/gif" {
		return fmt.Sprintf("data:image/gif;base64,%s", base64Encode)
	} else {
		return fmt.Sprintf("data:image/png;base64,%s", base64Encode)
	}
}

func (s *CompletionService) buildMessages(req *model.CompletionReq) ([]azopenai.ChatRequestMessageClassification, []model.KnowledgeAgentMessage) {
	// avoid token limit error, we need to limit the number of messages in the history
	if len(req.Message.Content) > config.AOAI_CHAT_MAX_TOKENS {
		log.Printf("Message content is too long, truncating to %d characters", config.AOAI_CHAT_MAX_TOKENS)
		req.Message.Content = req.Message.Content[:config.AOAI_CHAT_MAX_TOKENS]
	}

	// This is a conversation in progress.
	// NOTE: all llmMessages, regardless of role, count against token usage for this API.
	llmMessages := []azopenai.ChatRequestMessageClassification{}
	aiSearchMessages := []model.KnowledgeAgentMessage{}

	// process additional info(image, link)
	if len(req.AdditionalInfos) > 0 {
		for _, info := range req.AdditionalInfos {
			if info.Type == model.AdditionalInfoType_Link {
				if len(info.Content) > config.AOAI_CHAT_MAX_TOKENS {
					log.Printf("Link content is too long, truncating to %d characters", config.AOAI_CHAT_MAX_TOKENS)
					info.Content = info.Content[:config.AOAI_CHAT_MAX_TOKENS]
				}
				llmMessages = append(llmMessages, &azopenai.ChatRequestUserMessage{
					Content: azopenai.NewChatRequestUserMessageContent(fmt.Sprintf("Link URL: %s\nLink Content: %s", info.Link, info.Content)),
				})
				aiSearchMessages = append(aiSearchMessages, model.KnowledgeAgentMessage{
					Content: []model.KnowledgeAgentMessageContent{
						{
							Type: "text",
							Text: fmt.Sprintf("Link URL: %s\nLink Content: %s", info.Link, info.Content),
						},
					},
					Role: model.Role_User,
				})
			} else if info.Type == model.AdditionalInfoType_Image {
				link := getImageDataURI(info.Link)
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
				aiSearchMessages = append(aiSearchMessages, model.KnowledgeAgentMessage{
					Content: []model.KnowledgeAgentMessageContent{
						{
							Type: "image",
							Text: link,
						},
					},
					Role: model.Role_User,
				})
			}
		}
	}

	// process history messages
	for _, message := range req.History {
		var role model.Role
		if message.Role == model.Role_Assistant {
			llmMessages = append(llmMessages, &azopenai.ChatRequestAssistantMessage{Content: azopenai.NewChatRequestAssistantMessageContent(message.Content)})
			role = model.Role_Assistant

		}
		if message.Role == model.Role_User {
			role = model.Role_User
			llmMessages = append(llmMessages, &azopenai.ChatRequestUserMessage{
				Content: azopenai.NewChatRequestUserMessageContent(message.Content),
				Name:    processName(message.Name),
			})
		}
		aiSearchMessages = append(aiSearchMessages, model.KnowledgeAgentMessage{
			Content: []model.KnowledgeAgentMessageContent{
				{
					Type: "text",
					Text: message.Content,
				},
			},
			Role: role,
		})
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
	llmMessages = append(llmMessages, &azopenai.ChatRequestUserMessage{
		Content: azopenai.NewChatRequestUserMessageContent(currentMessage),
		Name:    processName(req.Message.Name),
	})
	aiSearchMessages = append(aiSearchMessages, model.KnowledgeAgentMessage{
		Content: []model.KnowledgeAgentMessageContent{
			{
				Type: "text",
				Text: currentMessage,
			},
		},
		Role: model.Role_User,
	})
	return llmMessages, aiSearchMessages
}

func (s *CompletionService) buildQueryForSearch(req *model.CompletionReq, messages []azopenai.ChatRequestMessageClassification) string {
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
	return query
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
	needCompleteResults := make([]model.Index, 0)
	normalResult := make([]model.Index, 0)
	for _, result := range results {
		if result.RerankScore < model.RerankScoreLowRelevanceThreshold {
			log.Printf("Skipping result with low score: %s/%s, score: %f", result.ContextID, result.Title, result.RerankScore)
			continue
		}
		if result.RerankScore >= model.RerankScoreRelevanceThreshold {
			needCompleteResults = append(needCompleteResults, result)
			log.Printf("Vector searched chunk(high score): %+v", result)
			continue
		}
		if result.ContextID == string(model.Source_TypeSpecQA) || result.ContextID == string(model.Source_TypeSpecMigration) {
			needCompleteResults = append(needCompleteResults, result)
			log.Printf("Vector searched chunk(Q&A): %+v", result)
			continue
		}
		log.Printf("Vector searched chunk: %+v", result)
		normalResult = append(normalResult, result)
	}
	if len(needCompleteResults) == 0 && len(normalResult) > 0 {
		log.Printf("No results found with high relevance score, supply with normal results")
		supplyNum := 5
		if len(normalResult) < supplyNum {
			supplyNum = len(normalResult)
		}
		needCompleteResults = normalResult[:supplyNum]
		normalResult = normalResult[supplyNum:]
	}

	chunkProcessStart := time.Now()
	files := make(map[string]bool)
	chunks := make(map[string]bool)
	mergedChunks := make([]model.Index, 0)
	for _, result := range needCompleteResults {
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
	// var temperature float32 = 0.0001
	resp, err := config.OpenAIClient.GetChatCompletions(context.TODO(), azopenai.ChatCompletionsOptions{
		// This is a conversation in progress.
		// NOTE: all messages count against token usage for this API.
		Messages:       messages,
		DeploymentName: &s.model,
		// Temperature:    &temperature,
	}, nil)

	if err != nil {
		// TODO: Update the following line with your application specific error handling logic
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
	resp, err := searchClient.AgenticSearch(ctx, query, req.Sources)
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
