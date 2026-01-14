package agent

import (
	"context"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"net/http"
	neturl "net/url"
	"strings"
	"sync"
	"time"

	"github.com/Azure/azure-sdk-for-go/sdk/ai/azopenai"
	"github.com/Azure/azure-sdk-for-go/sdk/azcore/policy"
	"github.com/Azure/azure-sdk-for-go/sdk/azcore/to"
	"github.com/Azure/azure-sdk-for-go/sdk/azidentity"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/preprocess"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/prompt"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/search"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/utils"
	"github.com/google/uuid"
)

type CompletionService struct {
	model        string
	searchClient *search.SearchClient
}

func NewCompletionService() (*CompletionService, error) {
	return &CompletionService{
		model:        config.AppConfig.AOAI_CHAT_COMPLETIONS_MODEL,
		searchClient: search.NewSearchClient(),
	}, nil
}

func (s *CompletionService) CheckArgs(req *model.CompletionReq) error {
	if req == nil {
		return model.NewInvalidRequestError("request is nil", nil)
	}
	if req.Message.Content == "" {
		return model.NewEmptyContentError()
	}
	if req.TopK == nil {
		topK := config.AppConfig.AI_SEARCH_TOPK
		req.TopK = &topK
	}
	tenantConfig, hasConfig := config.GetTenantConfig(req.TenantID)
	if hasConfig {
		if req.Sources == nil {
			req.Sources = tenantConfig.Sources
		}
	} else {
		return model.NewInvalidTenantIDError(string(req.TenantID))
	}
	return nil
}

func (s *CompletionService) ChatCompletion(ctx context.Context, req *model.CompletionReq) (*model.CompletionResp, error) {
	startTime := time.Now()
	requestID := uuid.New().String()
	var routed bool
	var routedTenantID model.TenantID

	log.SetPrefix(fmt.Sprintf("[RequestID: %s] ", requestID))

	jsonReq, err := json.Marshal(req)
	if err != nil {
		log.Printf("Failed to marshal request: %v\n", err)
	} else {
		log.Printf("Request: %s", utils.SanitizeForLog(string(jsonReq)))
	}

	if err = s.CheckArgs(req); err != nil {
		log.Printf("Request validation failed: %v", err)
		return nil, err
	}

	tenantConfig, _ := config.GetTenantConfig(req.TenantID)

	// 1. Build messages from the openai request
	llmMessages, reasoningModelMessages := s.buildMessages(req)

	// 2. Handle tenant routing if enabled
	if tenantConfig.EnableRouting {
		routedTenantID, routed = s.RouteTenant(req.TenantID, reasoningModelMessages)
		if routed {
			tenantConfig, _ = config.GetTenantConfig(routedTenantID)
			req.Sources = tenantConfig.Sources
			req.TenantID = routedTenantID
		}
	}

	// 3. Build query for search
	query, intention := s.buildQueryForSearch(req, reasoningModelMessages)

	var knowledges []model.Knowledge
	var prompt string
	promptTemplate := tenantConfig.PromptTemplate

	if intention != nil && !intention.NeedsRagProcessing {
		// Skip RAG workflow for non-technical messages
		log.Printf("Skipping RAG workflow - non-technical message detected")
		knowledges = []model.Knowledge{}
		promptTemplate = "common/non_technical_question.md"
	} else {
		// Run agentic search and vector search in parallel, then merge results
		knowledges, err = s.runParallelSearchAndMergeResults(ctx, req, query)
		if err != nil {
			log.Printf("Parallel search failed: %v", err)
			return nil, model.NewSearchFailureError(err)
		}
	}

	// 4. Build prompt
	prompt, err = s.buildPrompt(intention, knowledges, promptTemplate)
	if err != nil {
		log.Printf("Prompt building failed: %v", err)
		return nil, err
	}

	// 5. Get answer from LLM
	llmMessages = append([]azopenai.ChatRequestMessageClassification{
		&azopenai.ChatRequestSystemMessage{Content: azopenai.NewChatRequestSystemMessageContent(prompt)},
	}, llmMessages...)
	result, err := s.getLLMResult(llmMessages, tenantConfig.PromptTemplate)
	if err != nil {
		log.Printf("LLM request failed: %v", err)
		return nil, model.NewLLMServiceFailureError(err)
	}

	// 6. Process the result
	result.ID = requestID
	if req.WithFullContext != nil && *req.WithFullContext {
		fullContext, _ := json.Marshal(knowledges)
		result.FullContext = to.Ptr(string(fullContext))
	}
	result.Intention = intention
	result.References = utils.FilterInvalidReferenceLinks(result.References, knowledges)
	if routed {
		result.RouteTenant = to.Ptr(routedTenantID)
	}
	log.Printf("Total ChatCompletion time: %v", time.Since(startTime))
	return result, nil
}

func (s *CompletionService) RecognizeIntention(promptTemplate string, messages []azopenai.ChatRequestMessageClassification) (*model.IntentionResult, error) {
	promptParser := prompt.IntentionPromptParser{
		DefaultPromptParser: &prompt.DefaultPromptParser{},
	}
	promptStr, err := promptParser.ParsePrompt(nil, promptTemplate)
	if err != nil {
		log.Printf("Failed to parse intention prompt: %v", err)
		return nil, err
	}

	messages = append([]azopenai.ChatRequestMessageClassification{
		&azopenai.ChatRequestSystemMessage{Content: azopenai.NewChatRequestSystemMessageContent(promptStr)},
	}, messages...)

	resp, err := config.OpenAIClient.GetChatCompletions(context.TODO(), azopenai.ChatCompletionsOptions{
		Messages:       messages,
		DeploymentName: to.Ptr(string(config.AppConfig.AOAI_CHAT_REASONING_MODEL)),
		Temperature:    to.Ptr(float32(config.AppConfig.AOAI_CHAT_REASONING_MODEL_TEMPERATURE)),
		ResponseFormat: &azopenai.ChatCompletionsJSONResponseFormat{},
	}, nil)

	if err != nil {
		log.Printf("LLM intention recognition failed: %v", err)
		return nil, model.NewLLMServiceFailureError(err)
	}

	if len(resp.Choices) > 0 {
		result, err := promptParser.ParseResponse(*resp.Choices[0].Message.Content, promptTemplate)
		if err != nil {
			respStr, _ := resp.MarshalJSON()
			log.Printf("Failed to parse intention response: %v, response:%s", err, respStr)
			return nil, err
		}
		return result, nil
	}

	return nil, model.NewLLMServiceFailureError(fmt.Errorf("no valid response received from LLM"))
}

func processChunk(result model.Index) model.Knowledge {
	chunk := ""
	title := ""
	if len(result.Header1) > 0 {
		chunk += "# " + result.Header1 + "\n"
		title = result.Header1
	}
	if len(result.Header2) > 0 {
		chunk += "## " + result.Header2 + "\n"
		if title == "" {
			title = result.Header2
		}
	}
	if len(result.Header3) > 0 {
		chunk += "### " + result.Header3 + "\n"
		if title == "" {
			title = result.Header3
		}
	}
	chunk += result.Chunk
	return model.Knowledge{
		Source:   result.ContextID,
		FileName: result.Title,
		Link:     model.GetIndexLink(result),
		Content:  chunk,
		Title:    title,
	}
}

func processName(name *string) *string {
	if name == nil {
		return nil
	}
	// Remove spaces from the name
	processedName := strings.ReplaceAll(*name, " ", "")
	if len(processedName) == 0 {
		return nil
	}
	return to.Ptr(processedName)
}

func getImageDataURI(url string) (string, error) {
	// Parse and validate the URL to prevent SSRF attacks
	parsedURL, err := neturl.Parse(url)
	if err != nil {
		log.Printf("Invalid URL format: %s, error: %v", utils.SanitizeForLog(url), err)
		return "", fmt.Errorf("invalid URL format: %w", err)
	}

	// Validate scheme is HTTPS
	if parsedURL.Scheme != "https" {
		log.Printf("URL scheme is not HTTPS: %s", utils.SanitizeForLog(url))
		return "", fmt.Errorf("only HTTPS URLs are allowed")
	}

	// Validate hostname is exactly smba.trafficmanager.net
	if parsedURL.Hostname() != "smba.trafficmanager.net" {
		log.Printf("URL hostname is not allowed: %s", utils.SanitizeForLog(parsedURL.Hostname()))
		return "", fmt.Errorf("only smba.trafficmanager.net hostname is allowed")
	}

	cred, err := azidentity.NewManagedIdentityCredential(&azidentity.ManagedIdentityCredentialOptions{
		ID: azidentity.ClientID(config.GetBotClientID()),
	})
	if err != nil {
		log.Printf("Failed to create managed identity credential: %v", err)
		return "", err
	}
	token, err := cred.GetToken(context.Background(), policy.TokenRequestOptions{
		TenantID: config.GetBotTenantID(),
		Scopes:   []string{"https://api.botframework.com/.default"},
	})
	if err != nil {
		log.Printf("Failed to get token: %v", err)
		return "", err
	}
	req, err := http.NewRequest("GET", parsedURL.String(), nil)
	if err != nil {
		log.Printf("Failed to create request: %v", err)
		return "", err
	}
	req.Header.Set("Authorization", "Bearer "+token.Token)
	client := &http.Client{}
	resp, err := client.Do(req)
	if err != nil {
		log.Printf("Failed to download attachment: %v", err)
		return "", err
	}
	defer func() {
		if err = resp.Body.Close(); err != nil {
			log.Printf("Failed to close response body: %v", err)
		}
	}()
	if resp.StatusCode != http.StatusOK {
		return "", fmt.Errorf("failed to download attachment, status code: %d", resp.StatusCode)
	}
	body, err := io.ReadAll(resp.Body)
	if err != nil {
		log.Printf("Failed to read response body: %v", err)
		return "", err
	}
	base64Encode := base64.StdEncoding.EncodeToString(body)
	contentType := resp.Header.Get("Content-Type")
	switch contentType {
	case "image/png":
		return fmt.Sprintf("data:image/png;base64,%s", base64Encode), nil
	case "image/jpeg":
		return fmt.Sprintf("data:image/jpeg;base64,%s", base64Encode), nil
	case "image/gif":
		return fmt.Sprintf("data:image/gif;base64,%s", base64Encode), nil
	default:
		return fmt.Sprintf("data:image/png;base64,%s", base64Encode), nil
	}
}

func (s *CompletionService) buildMessages(req *model.CompletionReq) ([]azopenai.ChatRequestMessageClassification, []azopenai.ChatRequestMessageClassification) {
	// avoid token limit error, we need to limit the number of messages in the history
	if len(req.Message.Content) > config.AppConfig.AOAI_CHAT_MAX_TOKENS {
		log.Printf("Message content is too long, truncating to %d characters", config.AppConfig.AOAI_CHAT_MAX_TOKENS)
		req.Message.Content = req.Message.Content[:config.AppConfig.AOAI_CHAT_MAX_TOKENS]
	}

	// Preprocess HTML content if it contains HTML entities or tags
	preprocessService := preprocess.NewPreprocessService()
	req.Message.Content = preprocessService.PreprocessHTMLContent(req.Message.Content)

	// This is a conversation in progress.
	// NOTE: all llmMessages, regardless of role, count against token usage for this API.
	llmMessages := []azopenai.ChatRequestMessageClassification{}
	reasoningModelMessages := []azopenai.ChatRequestMessageClassification{}

	// process history messages
	for _, message := range req.History {
		// Preprocess HTML content in history messages
		content := preprocessService.PreprocessHTMLContent(message.Content)

		if message.Role == model.Role_Assistant {
			msg := &azopenai.ChatRequestAssistantMessage{Content: azopenai.NewChatRequestAssistantMessageContent(content)}
			llmMessages = append(llmMessages, msg)
			reasoningModelMessages = append(reasoningModelMessages, msg)
		}
		if message.Role == model.Role_User {
			msg := &azopenai.ChatRequestUserMessage{Content: azopenai.NewChatRequestUserMessageContent(content), Name: processName(message.Name)}
			llmMessages = append(llmMessages, msg)
			reasoningModelMessages = append(reasoningModelMessages, msg)
		}
	}

	// process additional info(image, link)
	if len(req.AdditionalInfos) > 0 {
		for _, info := range req.AdditionalInfos {
			if info.Type == model.AdditionalInfoType_Link {
				content := info.Content
				info.Link = preprocessService.PreprocessHTMLContent(info.Link)

				// Check if this is a pipeline link and analyze it
				if utils.IsPipelineLink(info.Link) {
					log.Printf("Detected Azure DevOps pipeline link: %s", info.Link)
					analysisText, err := utils.AnalyzePipeline(info.Link, "", true) // Use agent analysis
					if err != nil {
						log.Printf("Failed to analyze pipeline: %v", err)
						// Fall back to regular link processing
					} else {
						// Use the pipeline analysis as content
						content = analysisText
						log.Printf("Pipeline analysis completed successfully, result: %s", utils.SanitizeForLog(content))
					}
				}

				if len(content) > config.AppConfig.AOAI_CHAT_MAX_TOKENS {
					log.Printf("Link content is too long, truncating to %d characters", config.AppConfig.AOAI_CHAT_MAX_TOKENS)
					content = content[:config.AppConfig.AOAI_CHAT_MAX_TOKENS]
				}
				var msg *azopenai.ChatRequestUserMessage
				if strings.Contains(content, "graph.microsoft.com") {
					msg = &azopenai.ChatRequestUserMessage{
						Content: azopenai.NewChatRequestUserMessageContent(fmt.Sprintf("Image URL: %s\nImage Content: %s", info.Link, content)),
					}
				} else {
					msg = &azopenai.ChatRequestUserMessage{
						Content: azopenai.NewChatRequestUserMessageContent(fmt.Sprintf("Link URL: %s\nLink Content: %s", info.Link, content)),
					}
				}
				llmMessages = append(llmMessages, msg)
				reasoningModelMessages = append(reasoningModelMessages, msg)
			} else if info.Type == model.AdditionalInfoType_Image {
				link, err := getImageDataURI(info.Link)
				if err != nil {
					log.Printf("Failed to get image data URI: %v", err)
					continue
				}
				log.Println("Image link:", utils.SanitizeForLog(link))
				llmMessages = append(llmMessages, &azopenai.ChatRequestUserMessage{
					Content: azopenai.NewChatRequestUserMessageContent(
						[]azopenai.ChatCompletionRequestMessageContentPartClassification{
							&azopenai.ChatCompletionRequestMessageContentPartImage{
								ImageURL: to.Ptr(azopenai.ChatCompletionRequestMessageContentPartImageURL{
									URL: to.Ptr(link),
								}),
							},
						},
					),
				})
			}
		}
	}

	// process current user message
	currentMessage := req.Message.Content
	msg := &azopenai.ChatRequestUserMessage{
		Content: azopenai.NewChatRequestUserMessageContent(currentMessage),
		Name:    processName(req.Message.Name),
	}
	llmMessages = append(llmMessages, msg)
	reasoningModelMessages = append(reasoningModelMessages, msg)
	return llmMessages, reasoningModelMessages
}

func (s *CompletionService) buildQueryForSearch(req *model.CompletionReq, messages []azopenai.ChatRequestMessageClassification) (string, *model.IntentionResult) {
	query := req.Message.Content
	if req.Message.RawContent != nil && len(*req.Message.RawContent) > 0 {
		query = *req.Message.RawContent
	}
	intentStart := time.Now()
	tenantConfig, _ := config.GetTenantConfig(req.TenantID)
	intentResult, err := s.RecognizeIntention(tenantConfig.IntentionPromptTemplate, messages)
	if err != nil {
		log.Printf("ERROR: %s", err)
	} else if intentResult != nil {
		if len(req.Sources) == 0 {
			if intentResult.Scope == model.QuestionScope_Unbranded {
				req.Sources = []model.Source{model.Source_TypeSpec, model.Source_TypeSpecHttpSpecs}
			} else {
				req.Sources = []model.Source{model.Source_TypeSpec, model.Source_TypeSpecAzure, model.Source_AzureRestAPISpec}
			}
			req.Sources = append(req.Sources, model.Source_TypeSpecQA)
		}
		if len(intentResult.Question) > 0 {
			query = fmt.Sprintf("category:%s question:%s", intentResult.Category, intentResult.Question)
		}
		log.Printf("Intent Result: %+v", intentResult)
	}
	query = preprocess.NewPreprocessService().PreprocessInput(req.TenantID, query)
	log.Printf("Intent recognition took: %v", time.Since(intentStart))
	log.Printf("Searching query: %s", utils.SanitizeForLog(query))
	return query, intentResult
}

func (s *CompletionService) buildPrompt(intention *model.IntentionResult, knowledges []model.Knowledge, promptTemplate string) (string, error) {
	// make sure the tokens of chunks limited in 100000 tokens
	tokenCnt := 0
	for i, knowledge := range knowledges {
		if len(knowledge.Content) > config.AppConfig.AOAI_CHAT_MAX_TOKENS {
			log.Printf("Chunk %d is too long, truncating to %d characters", i+1, config.AppConfig.AOAI_CHAT_MAX_TOKENS)
			knowledges[i].Content = knowledge.Content[:config.AppConfig.AOAI_CHAT_MAX_TOKENS]
		}
		tokenCnt += len(knowledges[i].Content)
		if tokenCnt > config.AppConfig.AOAI_CHAT_CONTEXT_MAX_TOKENS {
			log.Printf("%v chunks has exceed max token limit, truncating to %d tokens", i+1, config.AppConfig.AOAI_CHAT_CONTEXT_MAX_TOKENS)
			knowledges = knowledges[:i+1] // truncate the chunks to the current index
			break
		}
	}
	knowledgeStr, _ := json.Marshal(knowledges)
	intentionStr, _ := json.Marshal(intention)
	promptParser := prompt.CompletionPromptParser{
		DefaultPromptParser: &prompt.DefaultPromptParser{},
	}
	promptStr, err := promptParser.ParsePrompt(map[string]string{
		"context":   string(knowledgeStr),
		"intention": string(intentionStr),
	}, promptTemplate)
	if err != nil {
		log.Printf("ERROR: %s", err)
		return "", err
	}
	return promptStr, nil
}

func (s *CompletionService) getLLMResult(messages []azopenai.ChatRequestMessageClassification, promptTemplate string) (*model.CompletionResp, error) {
	completionStart := time.Now()
	resp, err := config.OpenAIClient.GetChatCompletions(context.TODO(), azopenai.ChatCompletionsOptions{
		// This is a conversation in progress.
		// NOTE: all messages count against token usage for this API.
		Messages:       messages,
		DeploymentName: &s.model,
		ResponseFormat: &azopenai.ChatCompletionsJSONResponseFormat{},
		Temperature:    to.Ptr(float32(config.AppConfig.AOAI_CHAT_COMPLETIONS_TEMPERATURE)),
	}, nil)
	if err != nil {
		// Check if this is a rate limit error (429)
		if strings.Contains(err.Error(), "429") || strings.Contains(err.Error(), "Too Many Requests") {
			return nil, model.NewLLMRateLimitFailureError(err)
		}
		log.Printf("ERROR: %s", err)
		return nil, err
	}
	log.Printf("OpenAI completion took: %v", time.Since(completionStart))
	promptParser := prompt.CompletionPromptParser{
		DefaultPromptParser: &prompt.DefaultPromptParser{},
	}
	if len(resp.Choices) == 0 {
		return nil, fmt.Errorf("no choices found in response")
	}
	answer, err := promptParser.ParseResponse(*resp.Choices[0].Message.Content, promptTemplate)
	if err != nil {
		respStr, _ := resp.MarshalJSON()
		log.Printf("ERROR: %s, response:%s", err, respStr)
		return nil, err
	}
	return answer, nil

}

func (s *CompletionService) agenticSearch(ctx context.Context, query string, req *model.CompletionReq) ([]model.Index, error) {
	agenticSearchStart := time.Now()

	// Get the tenant-specific agentic search prompt
	tenantConfig, hasConfig := config.GetTenantConfig(req.TenantID)
	agenticSearchPromptTemplate := ""
	if hasConfig {
		agenticSearchPromptTemplate = tenantConfig.AgenticSearchPrompt
	}
	promptParser := prompt.AgenticSearchPromptParser{
		DefaultPromptParser: &prompt.DefaultPromptParser{},
	}
	agenticSearchPrompt, err := promptParser.ParsePrompt(nil, agenticSearchPromptTemplate)
	if err != nil {
		log.Printf("ERROR: %s", err)
		return nil, err
	}
	sourceFilter := map[model.Source]string{}
	if hasConfig && tenantConfig.SourceFilter != nil {
		sourceFilter = tenantConfig.SourceFilter
	}

	resp, err := s.searchClient.AgenticSearch(ctx, query, req.Sources, sourceFilter, agenticSearchPrompt)
	if err != nil {
		log.Printf("ERROR: %s", err)
		return nil, err
	}
	for _, activity := range resp.Activity {
		if activity.Type == model.ActivityRecordTypeSearchIndex {
			log.Printf("Agentic search sub query: %s", activity.SearchIndexArguments.Search)
		}
	}
	if resp.Response == nil {
		return nil, nil
	}
	var docKeys []string
	for _, reference := range resp.References {
		docKeys = append(docKeys, reference.DocKey)
	}
	chunks, err := s.searchClient.BatchGetChunks(ctx, docKeys)
	if err != nil {
		log.Printf("ERROR: %s", err)
		return nil, err
	}
	log.Printf("Agentic search took: %v", time.Since(agenticSearchStart))
	return chunks, nil
}

// runParallelSearchAndMergeResults runs agentic search and knowledge search in parallel where possible,
// then merges and processes their results
func (s *CompletionService) runParallelSearchAndMergeResults(ctx context.Context, req *model.CompletionReq, query string) ([]model.Knowledge, error) {
	parallelSearchStart := time.Now()

	// Use channels to collect results from parallel operations
	type agenticResult struct {
		chunks []model.Index
		err    error
	}

	type knowledgeResult struct {
		rawResults []model.Index
		err        error
	}

	agenticCh := make(chan agenticResult, 1)
	knowledgeCh := make(chan knowledgeResult, 1)

	// Start agentic search in a goroutine
	go func() {
		defer close(agenticCh)
		chunks, err := s.agenticSearch(ctx, req.Message.Content, req)
		agenticCh <- agenticResult{chunks: chunks, err: err}
	}()

	// Start knowledge search in parallel (without agentic chunks for now)
	go func() {
		defer close(knowledgeCh)
		rawResults, err := s.searchKnowledgeBase(req, query)
		knowledgeCh <- knowledgeResult{rawResults: rawResults, err: err}
	}()

	// Wait for both searches to complete
	agenticRes := <-agenticCh
	knowledgeRes := <-knowledgeCh

	if agenticRes.err != nil && knowledgeRes.err != nil {
		return nil, fmt.Errorf("both agentic and knowledge searches failed: agentic error: %v, knowledge error: %v", agenticRes.err, knowledgeRes.err)
	}

	var agenticChunks []model.Index
	if agenticRes.err != nil {
		log.Printf("Agentic search failed: %v", agenticRes.err)
		agenticChunks = []model.Index{}
	} else {
		agenticChunks = agenticRes.chunks
	}

	if knowledgeRes.err != nil {
		return nil, knowledgeRes.err
	}

	// Merge and process the results
	knowledges := s.mergeAndProcessSearchResults(agenticChunks, knowledgeRes.rawResults)

	log.Printf("Parallel search and merge took: %v", time.Since(parallelSearchStart))
	return knowledges, nil
}

// searchKnowledgeBase performs the core knowledge search without dependency on agentic results
func (s *CompletionService) searchKnowledgeBase(req *model.CompletionReq, query string) ([]model.Index, error) {
	searchStart := time.Now()
	sourceFilter := map[model.Source]string{}
	if tenantConfig, hasConfig := config.GetTenantConfig(req.TenantID); hasConfig && tenantConfig.SourceFilter != nil {
		sourceFilter = tenantConfig.SourceFilter
	}
	results, err := s.searchClient.SearchTopKRelatedDocuments(query, *req.TopK, req.Sources, sourceFilter)
	if err != nil {
		return nil, fmt.Errorf("failed to search for related documents: %w", err)
	}
	log.Printf("Vector Search took: %v", time.Since(searchStart))
	return results, nil
}

// mergeAndProcessSearchResults intelligently merges agentic and knowledge search results,
//
// Detailed logic:
// 1. Vector Search Results Processing:
//   - Filters out chunks with rerank score below low relevance threshold
//   - Applies special expansion rules for static chunks (TypeSpec QA, TypeSpec Migration, Mapping)
//   - Header-level chunks (H1/H2): applies hierarchical expansion to include sub chunks
//   - Other chunks(H3): kept as-is without expansion
//
// 2. Agentic Search Results Processing:
//   - Applies same special expansion rules for static chunks
//   - Header-level chunks(H1/H2): applies hierarchical expansion
//   - Other chunks(H3): kept without expansion
//
// 3. Deduplication:
//   - Removes duplicate expansions to avoid redundant content
//
// 4. Parallel Chunk Expansion:
//   - Uses goroutines to process all chunks concurrently for performance
//   - Expansion strategies:
//   - ExpansionQA: expands complete Q&A section under Header1
//   - ExpansionMapping: expands complete code mapping section under Header2
//   - ExpansionHierarchical: fetches all sub-chunks under detected hierarchy level (H1/H2)
//   - ExpansionNone: keeps original chunk unchanged
//
// 5. Build Result:
//   - Converts all expanded chunks into Knowledge objects
//
// Returns: slice of Knowledge objects with merged and expanded search results
func (s *CompletionService) mergeAndProcessSearchResults(agenticChunks []model.Index, knowledgeResults []model.Index) []model.Knowledge {
	mergeStart := time.Now()

	allChunks := make([]model.ChunkWithExpansion, 0)

	//  Add knowledge search results with scoring based on relevance
	for _, result := range knowledgeResults {
		// Skip low relevance results
		if result.RerankScore < model.RerankScoreLowRelevanceThreshold {
			log.Printf("Skipping result with low score: %s/%s, score: %f", result.ContextID, result.Title, result.RerankScore)
			continue
		}

		log.Printf("Vector searched chunk: %+v, rerankScore: %f", result, result.RerankScore)

		// Static chunks specific expansion rules
		if result.ContextID == model.Source_TypeSpecQA || result.ContextID == model.Source_TypeSpecMigration {
			allChunks = append(allChunks, model.ChunkWithExpansion{
				Chunk:     result,
				Expansion: model.ExpansionQA,
			})
			continue
		}
		if result.ContextID == model.Source_StaticTypeSpecToSwaggerMapping {
			allChunks = append(allChunks, model.ChunkWithExpansion{
				Chunk:     result,
				Expansion: model.ExpansionMapping,
			})
			continue
		}

		// Other chunks: check if needs hierarchical expansion
		hierarchy := s.searchClient.DetectChunkHierarchy(result)
		if hierarchy == model.HierarchyHeader1 || hierarchy == model.HierarchyHeader2 {
			allChunks = append(allChunks, model.ChunkWithExpansion{
				Chunk:     result,
				Expansion: model.ExpansionHierarchical,
			})
			log.Printf("Vector chunk needs hierarchical expansion: %s/%s#%s#%s", result.ContextID, result.Title, result.Header1, result.Header2)
			continue
		}

		// No expansion needed
		allChunks = append(allChunks, model.ChunkWithExpansion{
			Chunk:     result,
			Expansion: model.ExpansionNone,
		})
	}

	// Then, add agentic search results after vector search results
	topK := config.AppConfig.AI_SEARCH_TOPK / 2
	if len(agenticChunks) > topK {
		agenticChunks = agenticChunks[:topK] // Limit to TopK results
	}
	for _, chunk := range agenticChunks {
		// Static chunks specific expansion rules
		if chunk.ContextID == model.Source_TypeSpecQA || chunk.ContextID == model.Source_TypeSpecMigration {
			allChunks = append(allChunks, model.ChunkWithExpansion{
				Chunk:     chunk,
				Expansion: model.ExpansionQA,
			})
			continue
		}
		if chunk.ContextID == model.Source_StaticTypeSpecToSwaggerMapping {
			allChunks = append(allChunks, model.ChunkWithExpansion{
				Chunk:     chunk,
				Expansion: model.ExpansionMapping,
			})
			continue
		}

		// Check if needs hierarchical expansion
		hierarchy := s.searchClient.DetectChunkHierarchy(chunk)
		if hierarchy == model.HierarchyHeader1 || hierarchy == model.HierarchyHeader2 {
			allChunks = append(allChunks, model.ChunkWithExpansion{
				Chunk:     chunk,
				Expansion: model.ExpansionHierarchical,
			})
			log.Printf("Agentic chunk needs hierarchical expansion: %s/%s#%s#%s", chunk.ContextID, chunk.Title, chunk.Header1, chunk.Header2)
			continue
		}
		log.Printf("Agentic searched chunk: %+v", chunk)
		allChunks = append(allChunks, model.ChunkWithExpansion{
			Chunk:     chunk,
			Expansion: model.ExpansionNone,
		})
	}

	allChunks = s.searchClient.DeduplicateExpansions(allChunks)

	// Process all chunks in parallel
	var wg sync.WaitGroup
	finalChunks := make([]model.Index, len(allChunks))
	wg.Add(len(allChunks))
	for i := range allChunks {
		i := i
		go func() {
			defer wg.Done()
			cwe := allChunks[i]
			chunk := cwe.Chunk

			switch cwe.Expansion {
			case model.ExpansionQA:
				// Expand complete QA chunk
				subChunks, _ := s.searchClient.GetHeader1CompleteContext(chunk)
				finalChunks[i] = s.searchClient.MergeChunksWithHeaders(chunk, subChunks)
				log.Printf("✓ Expanded complete QA chunk: %s/%s/%s", chunk.ContextID, chunk.Title, chunk.Header1)
			case model.ExpansionMapping:
				// Expand complete Mapping chunk
				subChunks, _ := s.searchClient.GetHeader2CompleteContext(chunk)
				finalChunks[i] = s.searchClient.MergeChunksWithHeaders(chunk, subChunks)
				log.Printf("✓ Expanded complete code mapping chunk: %s/%s/%s/%s", chunk.ContextID, chunk.Title, chunk.Header1, chunk.Header2)
			case model.ExpansionHierarchical:
				// Process by hierarchy level
				Hierarchy := s.searchClient.DetectChunkHierarchy(chunk)
				// Expand all chunks under header1
				subChunks := s.searchClient.FetchHierarchicalSubChunks(chunk, Hierarchy)
				finalChunks[i] = s.searchClient.MergeChunksWithHeaders(chunk, subChunks)

			default:
				// Unknown expansion type - keep original
				finalChunks[i] = chunk
				log.Printf("✓ Kept original chunk: %s/%s/%s/%s/%s", chunk.ContextID, chunk.Title, chunk.Header1, chunk.Header2, chunk.Header3)
			}
		}()
	}
	wg.Wait()

	// Build final result
	results := make([]model.Knowledge, 0)
	for _, chunk := range finalChunks {
		results = append(results, processChunk(chunk))
	}

	log.Printf("Search merge summary: %d agentic + %d knowledge → %d total chunks",
		len(agenticChunks), len(knowledgeResults), len(finalChunks))
	log.Printf("Merge and processing took: %v", time.Since(mergeStart))
	return results
}

// RouteTenant attempts to route the request to a specialized tenant based on the question content.
// Returns the routed tenant config and true if routing occurred, otherwise returns empty config and false.
func (s *CompletionService) RouteTenant(originalTenantID model.TenantID, messages []azopenai.ChatRequestMessageClassification) (model.TenantID, bool) {
	routingStart := time.Now()
	log.Printf("Starting tenant routing for tenant: %s", originalTenantID)

	// Use the common tenant routing prompt
	promptParser := prompt.RoutingTenantPromptParser{
		DefaultPromptParser: &prompt.DefaultPromptParser{},
	}
	promptStr, err := promptParser.ParsePrompt(nil, "common/tenant_routing.md")
	if err != nil {
		log.Printf("Failed to parse tenant routing prompt: %v", err)
		return originalTenantID, false
	}

	// Build messages for routing detection
	routingMessages := append([]azopenai.ChatRequestMessageClassification{
		&azopenai.ChatRequestSystemMessage{Content: azopenai.NewChatRequestSystemMessageContent(promptStr)},
	}, messages...)

	// Call LLM for tenant routing
	resp, err := config.OpenAIClient.GetChatCompletions(context.TODO(), azopenai.ChatCompletionsOptions{
		Messages:       routingMessages,
		DeploymentName: to.Ptr(string(config.AppConfig.AOAI_CHAT_REASONING_MODEL)),
		ResponseFormat: &azopenai.ChatCompletionsJSONResponseFormat{},
	}, nil)

	if err != nil {
		log.Printf("LLM tenant routing failed: %v", err)
		return originalTenantID, false
	}

	if len(resp.Choices) == 0 {
		log.Printf("No routing response received from LLM")
		return originalTenantID, false
	}

	// Parse the routing result
	result, err := promptParser.ParseResponse(*resp.Choices[0].Message.Content, "common/tenant_routing.md")
	if err != nil {
		log.Printf("Failed to parse routing response: %v", err)
		return originalTenantID, false
	}

	routedTenantID := model.TenantID(result.RouteTenant)
	log.Printf("Tenant routing recommendation: %s", routedTenantID)

	// Validate and apply routing
	if routedTenantID == "" || routedTenantID == originalTenantID {
		log.Printf("No routing needed, staying with current tenant: %s", originalTenantID)
		return originalTenantID, false
	}

	_, hasConfig := config.GetTenantConfig(routedTenantID)
	if !hasConfig {
		log.Printf("Routed tenant '%s' not found, staying with current tenant", routedTenantID)
		return originalTenantID, false
	}

	// Apply routing
	log.Printf("Routing: %s → %s (took %v)", originalTenantID, routedTenantID, time.Since(routingStart))
	return routedTenantID, true
}
