package test

import (
	"context"
	"testing"
	"time"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/agent"
	"github.com/stretchr/testify/require"
)

func TestCompletionAPI(t *testing.T) {
	// Create a context with timeout
	ctx, cancel := context.WithTimeout(context.Background(), 120*time.Second)
	defer cancel()

	config.InitConfiguration()
	config.InitSecrets()
	config.InitOpenAIClient()
	service, err := agent.NewCompletionService()
	require.NoError(t, err)
	req := model.CompletionReq{
		TenantID: model.TenantID_AzureSDKQaBot,
		Message: model.Message{
			Role:    model.Role_User,
			Content: "Hello, how can I define different versions for my API?",
		},
		History: []model.Message{
			{
				Role:    model.Role_User,
				Content: "Hello, how can I onboard to TypeSpec?",
			},
		},
	}
	resp, err := service.ChatCompletion(ctx, &req)
	require.NoError(t, err)
	require.NotNil(t, resp)
	require.Greater(t, len(resp.Answer), 0, "Expected non-empty answer")
}
