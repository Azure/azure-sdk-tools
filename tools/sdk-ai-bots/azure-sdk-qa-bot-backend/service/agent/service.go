package agent

import (
	"context"
	"fmt"
	"log"
	"os"
	"strings"

	"github.com/Azure/azure-sdk-for-go/sdk/ai/azopenai"
	"github.com/Azure/azure-sdk-for-go/sdk/azcore"
	"github.com/copilot-extensions/rag-extension/model"
	"github.com/copilot-extensions/rag-extension/service/prompt"
	"github.com/copilot-extensions/rag-extension/service/search"
	"github.com/joho/godotenv"
)

type CompletionService struct {
}

func NewCompletionService() (*CompletionService, error) {
	return &CompletionService{}, nil
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
	if req.ModelConfig == nil {
		modelConfig := model.ModelConfig{}
		err := godotenv.Load()
		if err != nil {
			log.Fatal(err)
		}
		modelConfig.APIKey = os.Getenv("AOAI_CHAT_COMPLETIONS_API_KEY")
		modelConfig.Model = os.Getenv("AOAI_CHAT_COMPLETIONS_MODEL")
		modelConfig.Endpoint = os.Getenv("AOAI_CHAT_COMPLETIONS_ENDPOINT")
		req.ModelConfig = &modelConfig
	}
	return nil
}

func (s *CompletionService) ChatCompletion(req *model.CompletionReq) (*model.CompletionResp, error) {
	if err := s.CheckArgs(req); err != nil {
		log.Printf("ERROR: %s", err)
		return nil, err
	}
	result := &model.CompletionResp{}
	keyCredential := azcore.NewKeyCredential(req.ModelConfig.APIKey)
	// In Azure OpenAI you must deploy a model before you can use it in your client. For more information
	// see here: https://learn.microsoft.com/azure/cognitive-services/openai/how-to/create-resource
	client, err := azopenai.NewClientWithKeyCredential(req.ModelConfig.Endpoint, keyCredential, nil)

	if err != nil {
		log.Printf("ERROR: %s", err)
		return nil, err
	}

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

	// The user asks a question
	messages = append(messages, &azopenai.ChatRequestUserMessage{Content: azopenai.NewChatRequestUserMessageContent(req.Message.Content)})

	// Filter empty messages
	if userMessage == "" {
		return result, nil
	}
	log.Println("msg:", userMessage)
	searchClient := search.NewSearchClient()
	results, err := searchClient.SearchTopKRelatedDocuments(userMessage, *req.TopK, req.Sources)
	if err != nil {
		return nil, fmt.Errorf("failed to search for related documents: %w", err)
	}
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
		if len(files) == 5 {
			break
		}
	}
	for i, _ := range mergedChunks {
		mergedChunks[i] = searchClient.CompleteChunk(mergedChunks[i])
	}
	chunks := make([]string, 0)
	chunkLength := 0
	for _, result := range mergedChunks {
		chunk := fmt.Sprintf("- document_dir: %s\n", result.ContextID)
		chunk += fmt.Sprintf("- document_title: %s\n", result.Title)
		chunk += fmt.Sprintf("- document_link: %s\n", model.GetIndexLink(result))
		chunk += fmt.Sprintf("- document_content: %s\n", result.Chunk)

		chunkLength += len(chunk)
		if chunkLength > 100000 {
			break
		}
		chunks = append(chunks, chunk)
	}
	promptStr, error := prompt.BuildPrompt(strings.Join(chunks, "-------------------------\n"), *req.PromptTemplate)
	if error != nil {
		log.Printf("ERROR: %s", error)
		return nil, error
	}
	log.Println(fmt.Printf("message: %s, prompt:\n%s", userMessage, promptStr))
	messages = append(messages, &azopenai.ChatRequestSystemMessage{Content: azopenai.NewChatRequestSystemMessageContent(promptStr)})

	gotReply := false
	var temperature float32 = 0.0
	resp, err := client.GetChatCompletions(context.TODO(), azopenai.ChatCompletionsOptions{
		// This is a conversation in progress.
		// NOTE: all messages count against token usage for this API.
		Messages:       messages,
		DeploymentName: &req.ModelConfig.Model,
		Temperature:    &temperature,
	}, nil)

	if err != nil {
		// TODO: Update the following line with your application specific error handling logic
		log.Printf("ERROR: %s", err)
		return nil, err
	}

	for _, choice := range resp.Choices {
		gotReply = true

		if choice.Message != nil && choice.Message.Content != nil {
			fmt.Fprintf(os.Stderr, "Content[%d]: %s\n", *choice.Index, *choice.Message.Content)
		}

		if choice.FinishReason != nil {
			// this choice's conversation is complete.
			fmt.Fprintf(os.Stderr, "Finish reason[%d]: %s\n", *choice.Index, *choice.FinishReason)
		}
		answer, error := prompt.ParseAnswer(*choice.Message.Content, *req.PromptTemplate)
		if error != nil {
			log.Printf("ERROR: %s", error)
			return nil, error
		}
		result = answer
	}

	if gotReply {
		result.HasResult = true
		log.Printf("Got chat completions reply\n")
	}

	log.Printf("done")
	return result, nil
}
