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
	intentionResult, err := service.RecognizeIntention(model.TenantID_AzureSDKQaBot, "typespec/intention.md", llmMessages)
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
	intentionResult, err := service.RecognizeIntention(model.TenantID_AzureSDKQaBot, "typespec/intention.md", llmMessages)
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
	intentionResult, err := service.RecognizeIntention(model.TenantID_AzureSDKQaBot, "typespec/intention.md", llmMessages)

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
	intentionResult, err := service.RecognizeIntention(model.TenantID_AzureSDKQaBot, "typespec/intention.md", llmMessages)

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
			Content: `Hi everyone,
 
We released Core and Identity out-of-band this month with some important updates:
Core release
Identity release
We are clear to proceed with the January 2026 SDK releases.
There will be automatic patch releases this month.

If you have updates and plan to release manually, please release before January 26th.
If you plan an out-of-band release, please inform @Jair Myree of your intention.
Note: Updates to documentation require a manual release.
 
Important Dates
Patch Release: January 27th
BOM Release: January 28th
 
Release Guidance
For packages doing both stable & beta releases, use separate branches for each.
For packages releasing stable from main, ensure API reviews are approved by the Azure SDK for Java API review board. Missing approvals will cause pipeline failures.
For packages releasing stable from a release branch, API reviews will auto‑generate and the initial pipeline run will fail. Get those auto‑generated reviews approved, then re-run the pipeline.
For more documentation on how to perform an SDK release, see our wiki.
 
Packages to be Released Next
Releases can now proceed in this order:
 
Group 2:
App Configuration, Attestation, Communication (Common & Identity), Confidential Ledger, Container Registry, Cosmos, Device Update, Digital Twins, Farmbeats, Form Recognizer, Key Vault, Load Testing, Maps, Metrics Advisor, Mixed Reality Authentication, Monitor, Personalizer, Purview, Quantum Jobs, Schema Registry, Search, Service Bus, Storage, Tables, Text Analytics, Web Pub Sub, etc.
 
Group 3 (after Communication Common, Communication Identity, Mixed Reality Authentication, and Storage):
Communication (Chat, Email, Network Traversal, Phone Numbers, Rooms, SMS, etc), Event Grid, Event Hubs, Mixed Reality Remote Rendering.
 
Group 4 (after Group 3):
BOM, Spring Releases.`,
		},
	}

	llmMessages := convertToLLMMessages(messages)
	intentionResult, err := service.RecognizeIntention(model.TenantID_AzureSDKQaBot, "typespec/intention.md", llmMessages)

	require.NoError(t, err)
	require.NotNil(t, intentionResult)
	require.False(t, intentionResult.NeedsRagProcessing, "Announcement message should NOT require RAG processing")
	require.Equal(t, "unknown", intentionResult.Category)
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
	intentionResult, err := service.RecognizeIntention(model.TenantID_AzureSDKQaBot, "typespec/intention.md", llmMessages)

	require.NoError(t, err)
	require.NotNil(t, intentionResult)
	require.False(t, intentionResult.NeedsRagProcessing, "Suggestion message should NOT require RAG processing")
}

func TestIntentionRecognition_ReviewRequest(t *testing.T) {
	config.InitConfiguration()
	config.InitSecrets()
	config.InitOpenAIClient()

	service, err := agent.NewCompletionService()
	require.NoError(t, err)

	// Test case: PR review request (should need RAG processing)
	messages := []model.Message{
		{
			Role:    model.Role_User,
			Content: "Hi team, as discussed in the meeting, here is the TypeSpec PR for the new API version: [VideoTranslation] Add new API version 2026-03-01 to support auto create first iteration. Could you please review, and involve the key person to review as well?",
		},
	}

	llmMessages := convertToLLMMessages(messages)
	intentionResult, err := service.RecognizeIntention(model.TenantID_APISpecReviewBot, "api_spec_review/intention.md", llmMessages)

	require.NoError(t, err)
	require.NotNil(t, intentionResult)
	require.True(t, intentionResult.NeedsRagProcessing, "Review request should require RAG processing")
	require.NotEmpty(t, intentionResult.Question)
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
