package agent

import (
	"context"
	"fmt"
	"log"
	"strings"
	"sync"
	"time"

	"github.com/Azure/azure-sdk-for-go/sdk/ai/azopenai"
	"github.com/Azure/azure-sdk-for-go/sdk/azcore/to"
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

func (s *CompletionService) ChatCompletion(req *model.CompletionReq) (*model.CompletionResp, error) {
	startTime := time.Now()
	requestID := uuid.New().String()
	log.SetPrefix(fmt.Sprintf("[RequestID: %s] ", requestID))
	if err := s.CheckArgs(req); err != nil {
		log.Printf("ERROR: %s", err)
		return nil, err
	}
	result := &model.CompletionResp{}

	// avoid token limit error, we need to limit the number of messages in the history
	if len(req.Message.Content) > config.AOAI_CHAT_MAX_TOKENS {
		log.Printf("Message content is too long, truncating to %d characters", config.AOAI_CHAT_MAX_TOKENS)
		req.Message.Content = req.Message.Content[:config.AOAI_CHAT_MAX_TOKENS]
	}

	// This is a conversation in progress.
	// NOTE: all messages, regardless of role, count against token usage for this API.
	messages := []azopenai.ChatRequestMessageClassification{}
	for _, message := range req.History {
		if message.Role == model.Role_Assistant {
			messages = append(messages, &azopenai.ChatRequestAssistantMessage{Content: azopenai.NewChatRequestAssistantMessageContent(message.Content)})
		} else if message.Role == model.Role_User {
			messages = append(messages, &azopenai.ChatRequestUserMessage{
				Content: azopenai.NewChatRequestUserMessageContent(message.Content),
				Name:    message.Name,
			})
		}
	}

	if req.WithPreprocess != nil && *req.WithPreprocess {
		preProcessedMessage := preprocess.NewPreprocessService().ExtractAdditionalInfo(req.Message.Content)
		if preProcessedMessage == "" {
			log.Println("User message is empty after preprocessing, skipping completion.")
			return result, nil
		}
		log.Println("user message with additional info:", preProcessedMessage)
		// avoid token limit error, we need to limit the number of messages in the history
		if len(preProcessedMessage) > config.AOAI_CHAT_MAX_TOKENS {
			log.Printf("Message content is too long, truncating to %d characters", config.AOAI_CHAT_MAX_TOKENS)
			preProcessedMessage = preProcessedMessage[:config.AOAI_CHAT_MAX_TOKENS]
		}
		messages = append(messages, &azopenai.ChatRequestUserMessage{Content: azopenai.NewChatRequestUserMessageContent(preProcessedMessage)})
	} else {
		messages = append(messages, &azopenai.ChatRequestUserMessage{
			Content: azopenai.NewChatRequestUserMessageContent(req.Message.Content),
			Name:    req.Message.Name,
		})
	}

	query := req.Message.Content
	if req.Message.RawContent != nil && len(*req.Message.RawContent) > 0 {
		query = *req.Message.RawContent
	}
	query = preprocess.NewPreprocessService().PreprocessInput(query)
	log.Printf("User query: %s", query)

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
	}
	log.Printf("category: %v, intension: %v", intentResult.Category, intentResult.Question)
	log.Printf("Intent recognition took: %v", time.Since(intentStart))

	searchStart := time.Now()
	searchClient := search.NewSearchClient()
	results, err := searchClient.SearchTopKRelatedDocuments(query, *req.TopK, req.Sources)
	if err != nil {
		return nil, fmt.Errorf("failed to search for related documents: %w", err)
	}
	log.Printf("Search operation took: %v", time.Since(searchStart))

	// filter unrelevant results
	needCompleteResults := make([]model.Index, 0)
	normalResult := make([]model.Index, 0)
	for _, result := range results {
		if result.RerankScore < model.RerankScoreLowRelevanceThreshold {
			log.Printf("Skipping result with low score: %s, score: %f", result.Title, result.RerankScore)
			continue
		}
		if result.RerankScore >= model.RerankScoreRelevanceThreshold {
			needCompleteResults = append(needCompleteResults, result)
			log.Printf("Adding result with high score: %s, score: %f", result.Title, result.RerankScore)
			continue
		}
		log.Printf("Result: %s/%s, score: %f", result.ContextID, result.Title, result.RerankScore)
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

	chunks := make([]string, 0)
	for _, result := range mergedChunks {
		chunk := processDocument(result)
		chunks = append(chunks, chunk)
	}
	for _, result := range normalResult {
		if files[result.Title] {
			continue
		}
		chunk := processChunk(result)
		chunks = append(chunks, chunk)
	}
	log.Printf("Chunk processing took: %v", time.Since(chunkProcessStart))
	promptParser := prompt.DefaultPromptParser{}
	promptStr, err := promptParser.ParsePrompt(map[string]string{"context": strings.Join(chunks, "-------------------------\n")}, *req.PromptTemplate)
	if err != nil {
		log.Printf("ERROR: %s", err)
		return nil, err
	}
	messages = append(messages, &azopenai.ChatRequestSystemMessage{Content: azopenai.NewChatRequestSystemMessageContent(promptStr)})

	completionStart := time.Now()
	var temperature float32 = 0.0001
	resp, err := config.OpenAIClient.GetChatCompletions(context.TODO(), azopenai.ChatCompletionsOptions{
		// This is a conversation in progress.
		// NOTE: all messages count against token usage for this API.
		Messages:       messages,
		DeploymentName: &s.model,
		Temperature:    &temperature,
	}, nil)

	if err != nil {
		// TODO: Update the following line with your application specific error handling logic
		log.Printf("ERROR: %s", err)
		return nil, err
	}
	log.Printf("OpenAI completion took: %v", time.Since(completionStart))

	for _, choice := range resp.Choices {
		answer, err := promptParser.ParseResponse(*choice.Message.Content, *req.PromptTemplate)
		if err != nil {
			log.Printf("ERROR: %s, content:%s", err, *choice.Message.Content)
			return nil, err
		}
		result = answer
	}
	result.ID = requestID
	if req.WithFullContext != nil && *req.WithFullContext {
		fullContext := strings.Join(chunks, "-------------------------\n")
		result.FullContext = &fullContext
	}
	result.Intension = intentResult

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
	var temperature float32 = 0.0001
	resp, err := config.OpenAIClient.GetChatCompletions(context.TODO(), azopenai.ChatCompletionsOptions{
		// This is a conversation in progress.
		// NOTE: all messages count against token usage for this API.
		Messages:       messages,
		DeploymentName: to.Ptr(string(config.AOAI_CHAT_REASONING_MODEL)),
		Temperature:    &temperature,
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
