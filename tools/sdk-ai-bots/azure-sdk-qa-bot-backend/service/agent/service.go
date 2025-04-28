package agent

import (
	"context"
	"fmt"
	"log"
	"os"
	"strings"
	"sync"
	"time"

	"github.com/Azure/azure-sdk-for-go/sdk/ai/azopenai"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/prompt"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/search"
)

type CompletionService struct {
	model string
}

func NewCompletionService() (*CompletionService, error) {
	return &CompletionService{
		model: os.Getenv("AOAI_CHAT_COMPLETIONS_MODEL"),
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
		topK := 10
		req.TopK = &topK
	}
	if req.PromptTemplate == nil {
		defaultTemplate := "default.md"
		req.PromptTemplate = &defaultTemplate
	}
	return nil
}

func (s *CompletionService) ChatCompletion(req *model.CompletionReq) (*model.CompletionResp, error) {
	startTime := time.Now()
	if err := s.CheckArgs(req); err != nil {
		log.Printf("ERROR: %s", err)
		return nil, err
	}
	result := &model.CompletionResp{}

	// This is a conversation in progress.
	// NOTE: all messages, regardless of role, count against token usage for this API.
	messages := []azopenai.ChatRequestMessageClassification{}
	for _, message := range req.History {
		if message.Role == model.Role_Assistant {
			messages = append(messages, &azopenai.ChatRequestAssistantMessage{Content: azopenai.NewChatRequestAssistantMessageContent(message.Content)})
		} else if message.Role == model.Role_User {
			messages = append(messages, &azopenai.ChatRequestUserMessage{Content: azopenai.NewChatRequestUserMessageContent(message.Content)})
		}
	}
	userMessage := req.Message.Content
	// lower case
	userMessage = strings.ToLower(userMessage)
	// replace keyword
	for k, v := range model.KeywordReplaceMap {
		userMessage = strings.ReplaceAll(userMessage, fmt.Sprintf(" %s ", k), fmt.Sprintf(" %s ", v))
	}

	// The user asks a question
	messages = append(messages, &azopenai.ChatRequestUserMessage{Content: azopenai.NewChatRequestUserMessageContent(req.Message.Content)})

	intentStart := time.Now()
	intentResult, err := s.RecongnizeIntension(messages)
	if err != nil {
		log.Printf("ERROR: %s", err)
	} else if intentResult != nil {
		log.Printf("category: %v, question: %v", intentResult.Category, intentResult.Question)
		if intentResult.Category == model.QuestionCategory_Unbranded {
			req.Sources = []model.Source{model.Source_TypeSpec}
		}
		if len(intentResult.Question) > 0 {
			userMessage = intentResult.Question
		}
	}
	log.Printf("Intent recognition took: %v", time.Since(intentStart))

	// Filter empty messages
	if userMessage == "" {
		return result, nil
	}
	log.Println("msg:", userMessage)

	searchStart := time.Now()
	searchClient := search.NewSearchClient()
	results, err := searchClient.SearchTopKRelatedDocuments(userMessage, *req.TopK, req.Sources)
	if err != nil {
		return nil, fmt.Errorf("failed to search for related documents: %w", err)
	}
	log.Printf("Search operation took: %v", time.Since(searchStart))

	chunkProcessStart := time.Now()
	files := make(map[string]bool)
	mergedChunks := make([]model.Index, 0)
	for _, result := range results {
		if files[result.Title] {
			continue
		}
		files[result.Title] = true
		mergedChunks = append(mergedChunks, model.Index{
			Title:     result.Title,
			ContextID: result.ContextID,
		})
		if len(files) == 3 {
			break
		}
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
	printChunks := make([]string, 0)
	chunkLength := 0
	for _, result := range mergedChunks {
		chunk := fmt.Sprintf("- document_dir: %s\n", result.ContextID)
		chunk += fmt.Sprintf("- document_filename: %s\n", result.Title)
		chunk += fmt.Sprintf("- document_title: %s\n", result.Header1)
		chunk += fmt.Sprintf("- document_link: %s\n", model.GetIndexLink(result))
		printChunks = append(printChunks, chunk)
		chunk += fmt.Sprintf("- document_content: %s\n", result.Chunk)

		chunkLength += len(chunk)
		if chunkLength > 100000 {
			break
		}
		chunks = append(chunks, chunk)
	}
	log.Printf("Chunk processing took: %v", time.Since(chunkProcessStart))

	promptParser := prompt.DefaultPromptParser{}
	promptStr, err := promptParser.ParsePrompt(map[string]string{"context": strings.Join(chunks, "-------------------------\n")}, *req.PromptTemplate)
	if err != nil {
		log.Printf("ERROR: %s", err)
		return nil, err
	}
	log.Println(fmt.Printf("message: %s, related documents:\n%v", userMessage, printChunks))
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
		if choice.Message != nil && choice.Message.Content != nil {
			fmt.Fprintf(os.Stderr, "Content[%d]: %s\n", *choice.Index, *choice.Message.Content)
		}

		if choice.FinishReason != nil {
			// this choice's conversation is complete.
			fmt.Fprintf(os.Stderr, "Finish reason[%d]: %s\n", *choice.Index, *choice.FinishReason)
		}
		answer, err := promptParser.ParseResponse(*choice.Message.Content, *req.PromptTemplate)
		if err != nil {
			log.Printf("ERROR: %s", err)
			return nil, err
		}
		result = answer
	}
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
		DeploymentName: &s.model,
		Temperature:    &temperature,
	}, nil)

	if err != nil {
		// TODO: Update the following line with your application specific error handling logic
		log.Printf("ERROR: %s", err)
		return nil, err
	}

	for _, choice := range resp.Choices {
		if choice.Message != nil && choice.Message.Content != nil {
			fmt.Fprintf(os.Stderr, "Content[%d]: %s\n", *choice.Index, *choice.Message.Content)
		}

		if choice.FinishReason != nil {
			// this choice's conversation is complete.
			fmt.Fprintf(os.Stderr, "Finish reason[%d]: %s\n", *choice.Index, *choice.FinishReason)
		}
		result, error := promptParser.ParseResponse(*choice.Message.Content, "intension.md")
		if error != nil {
			log.Printf("ERROR: %s", error)
			return nil, error
		}
		return result, nil
	}
	return nil, nil
}
