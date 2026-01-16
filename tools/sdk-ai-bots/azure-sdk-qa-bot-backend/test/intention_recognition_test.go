package test

import (
	"testing"

	"github.com/Azure/azure-sdk-for-go/sdk/ai/azopenai"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/agent"
	"github.com/stretchr/testify/require"
)

func TestIntentionRecognition_TechnicalQuestion(t *testing.T) {
	config.InitConfiguration()
	config.InitSecrets()
	config.InitOpenAIClient()

	service, err := agent.NewCompletionService()
	require.NoError(t, err)

	messages := []model.Message{
		{
			Role:    model.Role_User,
			Content: "I'm from the MySQL service team. I’ve already migrated the swagger to TypeSpec, but I have two questions and would like to ask if you have any best practices.\n1. How do you maintain TypeSpec in parallel?\nHere are our common scenarios: we usually have two versions in progress.\nFor example, the 2025-09-01 version is almost finished — we’re waiting for the backend code to be ready before we can release it, though we may still have some small changes.\nAnother version, 2025-12-01, has just started. Feature owners are designing their features and want to start merging code into this version.\nHow can we develop these two versions in parallel? I don’t have permission to create branches.\n2. For the server team, each feature owner needs to write their own feature, which means everyone has to understand how to write TypeSpec.\nHow can we reduce their learning and writing cost?",
		},
	}
	llmMessages := convertToLLMMessages(messages)
	intentionResult, err := service.RecognizeIntention("typespec/intention.md", llmMessages)
	require.NoError(t, err)
	require.NotNil(t, intentionResult)
	require.True(t, intentionResult.NeedsRagProcessing, "Technical question should require RAG processing")
	require.NotEmpty(t, intentionResult.Question)
}

func TestIntentionRecognition_PermissionMessage(t *testing.T) {
	config.LoadEnvFile()
	config.InitConfiguration()
	config.InitSecrets()
	config.InitOpenAIClient()

	service, err := agent.NewCompletionService()
	require.NoError(t, err)

	messages := []model.Message{
		{
			Role:    model.Role_User,
			Content: "Hi team, could someone please help grant me permission to view the workflow for my Azure REST API PR?",
		},
	}
	llmMessages := convertToLLMMessages(messages)
	intentionResult, err := service.RecognizeIntention("typespec/intention.md", llmMessages)
	require.NoError(t, err)
	require.NotNil(t, intentionResult)
	require.True(t, intentionResult.NeedsRagProcessing, "Permission question should require RAG processing")
	require.NotEmpty(t, intentionResult.Question)
}

func TestIntentionRecognition_GreetingMessage(t *testing.T) {
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
	intentionResult, err := service.RecognizeIntention("typespec/intention.md", llmMessages)

	require.NoError(t, err)
	require.NotNil(t, intentionResult)
	require.False(t, intentionResult.NeedsRagProcessing, "Greeting should NOT require RAG processing")
	require.Equal(t, "unknown", intentionResult.Category)
}

func TestIntentionRecognition_ThankYouMessage(t *testing.T) {
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
	intentionResult, err := service.RecognizeIntention("typespec/intention.md", llmMessages)

	require.NoError(t, err)
	require.NotNil(t, intentionResult)
	require.False(t, intentionResult.NeedsRagProcessing, "Thank you message should NOT require RAG processing")
}

func TestIntentionRecognition_AnnouncementMessage(t *testing.T) {
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
	intentionResult, err := service.RecognizeIntention("typespec/intention.md", llmMessages)

	require.NoError(t, err)
	require.NotNil(t, intentionResult)
	require.False(t, intentionResult.NeedsRagProcessing, "Announcement message should NOT require RAG processing")
	require.Equal(t, "unknown", intentionResult.Category)
	require.Equal(t, model.QuestionScope_Unknown, intentionResult.Scope)
}

func TestIntentionRecongition_SuggestionsMessage(t *testing.T) {
	config.InitConfiguration()
	config.InitSecrets()
	config.InitOpenAIClient()

	service, err := agent.NewCompletionService()
	require.NoError(t, err)

	messages := []model.Message{
		{
			Role:    model.Role_User,
			Content: "How to enhance the knowledge of Azure SDK Q&A Bot?",
		},
	}

	llmMessages := convertToLLMMessages(messages)
	intentionResult, err := service.RecognizeIntention("typespec/intention.md", llmMessages)

	require.NoError(t, err)
	require.NotNil(t, intentionResult)
	require.False(t, intentionResult.NeedsRagProcessing, "Suggestion message should NOT require RAG processing")
}

// Helper function to convert model.Message to LLM message format
func convertToLLMMessages(messages []model.Message) []azopenai.ChatRequestMessageClassification {
	llmMessages := make([]azopenai.ChatRequestMessageClassification, 0, len(messages))
	for _, msg := range messages {
		switch msg.Role {
		case model.Role_User:
			llmMessages = append(llmMessages, &azopenai.ChatRequestUserMessage{
				Content: azopenai.NewChatRequestUserMessageContent(msg.Content),
			})
		case model.Role_Assistant:
			llmMessages = append(llmMessages, &azopenai.ChatRequestAssistantMessage{
				Content: azopenai.NewChatRequestAssistantMessageContent(msg.Content),
			})
		}
	}
	return llmMessages
}
