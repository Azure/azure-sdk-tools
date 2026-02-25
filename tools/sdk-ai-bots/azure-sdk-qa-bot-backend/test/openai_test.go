package test

import (
	"context"
	"testing"

	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/openai/openai-go/v3"
	"github.com/stretchr/testify/require"
)

func TestCompletion(t *testing.T) {
	config.InitConfiguration()
	config.InitSecrets()
	config.InitOpenAIClient()
	// Define the request
	messages := []openai.ChatCompletionMessageParamUnion{
		openai.UserMessage("What is the capital of France?"),
	}
	model := config.AppConfig.AOAI_CHAT_COMPLETIONS_MODEL
	resp, err := config.OpenAIClient.Chat.Completions.New(context.TODO(), openai.ChatCompletionNewParams{
		// This is a conversation in progress.
		// NOTE: all messages count against token usage for this API.
		Messages: messages,
		Model:    openai.ChatModel(model),
	})

	require.NoError(t, err)
	require.NotEmpty(t, resp.Choices)
}
