package test

import (
	"context"
	"testing"

	"github.com/Azure/azure-sdk-for-go/sdk/ai/azopenai"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
)

func TestCompletionAPI(t *testing.T) {
	config.InitEnvironment()
	config.InitSecrets()
	// Define the request
	messages := []azopenai.ChatRequestMessageClassification{
		&azopenai.ChatRequestUserMessage{Content: azopenai.NewChatRequestUserMessageContent("What is the capital of France?")},
	}

	model := config.AOAI_CHAT_COMPLETIONS_MODEL
	resp, err := config.OpenAIClient.GetChatCompletions(context.TODO(), azopenai.ChatCompletionsOptions{
		// This is a conversation in progress.
		// NOTE: all messages count against token usage for this API.
		Messages:       messages,
		DeploymentName: &model,
	}, nil)

	if err != nil {
		t.Fatalf("Failed to get chat completions: %v", err)
	}
	// Print the response
	for _, choice := range resp.Choices {
		t.Logf("Response: %s", *choice.Message.Content)
	}
}
