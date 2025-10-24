package test

import (
	"context"
	"testing"
	"time"

	"github.com/Azure/azure-sdk-for-go/sdk/ai/azopenai"
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

func TestIntensionRecognition_TechnicalQuestion(t *testing.T) {
	config.InitConfiguration()
	config.InitSecrets()
	config.InitOpenAIClient()

	service, err := agent.NewCompletionService()
	require.NoError(t, err)

	// Test case: Technical TypeSpec question (should need RAG processing)
	messages := []model.Message{
		{
			Role:    model.Role_User,
			Content: "How do I implement pagination in TypeSpec?",
		},
	}

	llmMessages := convertToLLMMessages(messages)
	intensionResult, err := service.RecongnizeIntension("intension.md", llmMessages)

	require.NoError(t, err)
	require.NotNil(t, intensionResult)
	require.True(t, intensionResult.NeedsRagProcessing, "Technical question should require RAG processing")
	require.NotEmpty(t, intensionResult.Question)
	require.NotEqual(t, "unknown", intensionResult.Category)
}

func TestIntensionRecognition_GreetingMessage(t *testing.T) {
	config.InitConfiguration()
	config.InitSecrets()
	config.InitOpenAIClient()

	service, err := agent.NewCompletionService()
	require.NoError(t, err)

	// Test case: Greeting message (should NOT need RAG processing)
	messages := []model.Message{
		{
			Role:    model.Role_User,
			Content: "Hello!",
		},
	}

	llmMessages := convertToLLMMessages(messages)
	intensionResult, err := service.RecongnizeIntension("intension.md", llmMessages)

	require.NoError(t, err)
	require.NotNil(t, intensionResult)
	require.False(t, intensionResult.NeedsRagProcessing, "Greeting should NOT require RAG processing")
	require.Equal(t, "unknown", intensionResult.Category)
}

func TestIntensionRecognition_ThankYouMessage(t *testing.T) {
	config.InitConfiguration()
	config.InitSecrets()
	config.InitOpenAIClient()

	service, err := agent.NewCompletionService()
	require.NoError(t, err)

	// Test case: Thank you message after technical conversation (should NOT need RAG processing)
	messages := []model.Message{
		{
			Role:    model.Role_User,
			Content: "How do I implement pagination in TypeSpec?",
		},
		{
			Role:    model.Role_Assistant,
			Content: "You can implement pagination in TypeSpec using the Azure.Core pagination templates. Here's an example...",
		},
		{
			Role:    model.Role_User,
			Content: "Thank you for your help!",
		},
	}

	llmMessages := convertToLLMMessages(messages)
	intensionResult, err := service.RecongnizeIntension("intension.md", llmMessages)

	require.NoError(t, err)
	require.NotNil(t, intensionResult)
	require.False(t, intensionResult.NeedsRagProcessing, "Thank you message should NOT require RAG processing")
	require.Equal(t, "unknown", intensionResult.Category)
}

func TestIntensionRecognition_AnnouncementMessage(t *testing.T) {
	config.InitConfiguration()
	config.InitSecrets()
	config.InitOpenAIClient()

	service, err := agent.NewCompletionService()
	require.NoError(t, err)

	// Test case: SDK release announcement (should NOT need RAG processing)
	messages := []model.Message{
		{
			Role: model.Role_User,
			Content: `October SDK Release Kickoff
Hi All,
Welcome to the October Release Kickoff!

Release TO DOs:
- Run the Prepare-Release script by EOB Wednesday October 1st
- Review release notes PR by ship date (Tuesday October 7th)

Important Dates:
- Monthly Release Check-In: Thursday October 2nd @11:00AM PST
- October core code complete: 9/30
- October all package ship: 10/07

Important Links:
- Upcoming release dates
- Releasing Libraries
- Release Checklist`,
		},
	}

	llmMessages := convertToLLMMessages(messages)
	intensionResult, err := service.RecongnizeIntension("intension.md", llmMessages)

	require.NoError(t, err)
	require.NotNil(t, intensionResult)
	require.False(t, intensionResult.NeedsRagProcessing, "Announcement message should NOT require RAG processing")
	require.Equal(t, "unknown", intensionResult.Category)
	require.Equal(t, model.QuestionScope_Unknown, intensionResult.Scope)
}

// Helper function to convert model.Message to LLM message format
func convertToLLMMessages(messages []model.Message) []azopenai.ChatRequestMessageClassification {
	llmMessages := make([]azopenai.ChatRequestMessageClassification, 0, len(messages))
	for _, msg := range messages {
		if msg.Role == model.Role_User {
			llmMessages = append(llmMessages, &azopenai.ChatRequestUserMessage{
				Content: azopenai.NewChatRequestUserMessageContent(msg.Content),
			})
		} else if msg.Role == model.Role_Assistant {
			llmMessages = append(llmMessages, &azopenai.ChatRequestAssistantMessage{
				Content: azopenai.NewChatRequestAssistantMessageContent(msg.Content),
			})
		}
	}
	return llmMessages
}
