package test

import (
	"context"
	"testing"

	"github.com/Azure/azure-sdk-for-go/sdk/ai/azopenai"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/stretchr/testify/require"
)

func TestCompletion(t *testing.T) {
	config.InitConfiguration()
	config.InitSecrets()
	config.InitOpenAIClient()
	// Define the request
	messages := []azopenai.ChatRequestMessageClassification{
		&azopenai.ChatRequestUserMessage{Content: azopenai.NewChatRequestUserMessageContent("What is the capital of France?")},
	}
	model := config.AppConfig.AOAI_CHAT_COMPLETIONS_MODEL
	resp, err := config.OpenAIClient.GetChatCompletions(context.TODO(), azopenai.ChatCompletionsOptions{
		// This is a conversation in progress.
		// NOTE: all messages count against token usage for this API.
		Messages:       messages,
		DeploymentName: &model,
	}, nil)

	require.NoError(t, err)
	require.NotEmpty(t, resp.Choices)
}
