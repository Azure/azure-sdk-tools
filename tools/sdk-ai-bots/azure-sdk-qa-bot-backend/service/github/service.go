package github

import (
	"context"
	"encoding/json"
	"fmt"
	"log"
	"strings"
	"time"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/google/go-github/v66/github"
	"golang.org/x/oauth2"
)

type GitHubService struct{}

func NewGitHubService() *GitHubService {
	return &GitHubService{}
}

// CreateIssueFromFeedbackRequest creates a GitHub sub-issue from feedback request
func (s *GitHubService) CreateSubIssueFromFeedbackRequest(feedback model.FeedbackReq) error {
	// Check if reaction is bad
	if feedback.Reaction != model.Reaction_Bad {
		log.Printf("Feedback is not negative, skipping GitHub issue creation")
		return nil
	}

	// Issue body template
	const issueBodyTemplate = `## Root Cause [TODO]
This is the most important part of the issue. It's a place holder for the root cause of this issue. And it's also a reminder for assignee to remember fill in this section before close the issue.

## Reasons

- %s

## Comment

%s

## Channel Name
%s

## Link
%s

%s---
%s`

	// Build issue title
	userName := feedback.UserName
	if userName == "" {
		userName = "Anonymous User"
	}
	// Add date to title for easier tracking
	currentDate := time.Now().Format("2006-01-02")
	title := fmt.Sprintf("[Teams Chatbot]: Negative Feedback from %s - %s", userName, currentDate)

	// Prepare template variables
	reasons := "[\"Other\"]"
	if len(feedback.Reasons) > 0 {
		if reasonsJSON, err := json.Marshal(feedback.Reasons); err == nil {
			reasons = string(reasonsJSON)
		}
	}

	comment := feedback.Comment
	if comment == "" {
		comment = "N/A"
	}

	channelID := feedback.ChannelID
	if channelID == "" {
		channelID = "Unknown Channel"
	}

	link := feedback.Link
	if link == "" {
		link = "No link provided"
	}

	// Build conversation history
	conversationHistory := ""
	if len(feedback.Messages) > 0 {
		var historyBuilder strings.Builder
		historyBuilder.WriteString("<details>\n<summary>Conversation History (click to expand)</summary>\n\n")
		for i, msg := range feedback.Messages {
			historyBuilder.WriteString(fmt.Sprintf("**%s:**\n```\n%s\n```\n\n", msg.Role, msg.Content))
			if i >= 4 { // Limit to first 5 messages
				remaining := len(feedback.Messages) - i - 1
				if remaining > 0 {
					historyBuilder.WriteString(fmt.Sprintf("*... and %d more messages*\n\n", remaining))
				}
				break
			}
		}
		historyBuilder.WriteString("</details>\n\n")
		conversationHistory = historyBuilder.String()
	}

	// Build metadata footer
	metadataFooter := ""
	if feedback.UserName != "" {
		metadataFooter += fmt.Sprintf("**User:** %s\n", feedback.UserName)
	}
	if feedback.TenantID != "" {
		metadataFooter += fmt.Sprintf("**Tenant ID:** %s\n", feedback.TenantID)
	}
	metadataFooter += fmt.Sprintf("**Reported at:** %s", time.Now().Format(time.RFC3339))

	// Format the issue body using the template
	issueBody := fmt.Sprintf(
		issueBodyTemplate,
		reasons,
		comment,
		channelID,
		link,
		conversationHistory,
		metadataFooter,
	)

	// Add default labels for negative feedback
	labels := []string{"feedback", "negative-feedback"}

	// Create the GitHub issue
	issueNumber, err := s.CreateIssueFromFeedback(title, issueBody, labels)
	if err != nil {
		return fmt.Errorf("failed to create GitHub issue from feedback: %w", err)
	}

	// If issue was created successfully (issueNumber > 0), add it to parent issue's task list
	if issueNumber > 0 {
		if err := s.AddSubIssueToParent(issueNumber); err != nil {
			log.Printf("Warning: Failed to add sub-issue to parent issue task list: %v", err)
			// Don't return error as the sub-issue was created successfully
		}
	}

	return nil
}

// CreateIssueFromFeedback creates a GitHub issue from negative feedback
func (s *GitHubService) CreateIssueFromFeedback(title, body string, labels []string) (int, error) {
	// Check if GitHub is configured
	if config.AppConfig.GITHUB_TOKEN == "" ||
		config.AppConfig.GITHUB_OWNER == "" ||
		config.AppConfig.GITHUB_REPO == "" {
		log.Printf("GitHub not configured, skipping issue creation")
		return 0, nil
	}

	// Create GitHub client with authentication
	ctx := context.Background()
	ts := oauth2.StaticTokenSource(
		&oauth2.Token{AccessToken: config.AppConfig.GITHUB_TOKEN},
	)
	tc := oauth2.NewClient(ctx, ts)
	client := github.NewClient(tc)

	// Prepare the issue request
	githubIssue := &github.IssueRequest{
		Title:  &title,
		Body:   &body,
		Labels: &labels,
	}

	// Create the issue
	issue, _, err := client.Issues.Create(
		ctx,
		config.AppConfig.GITHUB_OWNER,
		config.AppConfig.GITHUB_REPO,
		githubIssue,
	)
	if err != nil {
		return 0, fmt.Errorf("failed to create GitHub issue: %w", err)
	}

	issueNumber := issue.GetNumber()
	log.Printf("GitHub issue created: #%d - %s", issueNumber, issue.GetHTMLURL())

	return issueNumber, nil
}

// AddSubIssueToParent adds a sub-issue to the parent issue using GitHub's GraphQL API
func (s *GitHubService) AddSubIssueToParent(subIssueNumber int) error {
	// Check if parent issue is configured
	if config.AppConfig.GITHUB_PARENT_ISSUE == 0 {
		log.Printf("GitHub parent issue not configured, skipping sub-issue linking")
		return nil
	}

	// Check if GitHub is configured
	if config.AppConfig.GITHUB_TOKEN == "" ||
		config.AppConfig.GITHUB_OWNER == "" ||
		config.AppConfig.GITHUB_REPO == "" {
		return fmt.Errorf("GitHub not configured")
	}

	ctx := context.Background()
	ts := oauth2.StaticTokenSource(
		&oauth2.Token{AccessToken: config.AppConfig.GITHUB_TOKEN},
	)
	tc := oauth2.NewClient(ctx, ts)

	// Step 1: Get the node IDs for both parent and sub-issue using GraphQL
	getIssueNodeIDQuery := `
		query($owner: String!, $repo: String!, $parentNumber: Int!, $subNumber: Int!) {
			repository(owner: $owner, name: $repo) {
				parentIssue: issue(number: $parentNumber) {
					id
				}
				subIssue: issue(number: $subNumber) {
					id
				}
			}
		}
	`

	type IssueNode struct {
		ID string `json:"id"`
	}

	type RepositoryData struct {
		ParentIssue IssueNode `json:"parentIssue"`
		SubIssue    IssueNode `json:"subIssue"`
	}

	type QueryResponse struct {
		Repository RepositoryData `json:"repository"`
	}

	type GraphQLResponse struct {
		Data   QueryResponse `json:"data"`
		Errors []struct {
			Message string `json:"message"`
		} `json:"errors"`
	}

	variables := map[string]interface{}{
		"owner":        config.AppConfig.GITHUB_OWNER,
		"repo":         config.AppConfig.GITHUB_REPO,
		"parentNumber": config.AppConfig.GITHUB_PARENT_ISSUE,
		"subNumber":    subIssueNumber,
	}

	queryPayload := map[string]interface{}{
		"query":     getIssueNodeIDQuery,
		"variables": variables,
	}

	queryJSON, err := json.Marshal(queryPayload)
	if err != nil {
		return fmt.Errorf("failed to marshal GraphQL query: %w", err)
	}

	// Execute the GraphQL query to get node IDs
	resp, err := tc.Post("https://api.github.com/graphql", "application/json", strings.NewReader(string(queryJSON)))
	if err != nil {
		return fmt.Errorf("failed to execute GraphQL query: %w", err)
	}
	defer resp.Body.Close()

	var graphQLResp GraphQLResponse
	if err := json.NewDecoder(resp.Body).Decode(&graphQLResp); err != nil {
		return fmt.Errorf("failed to decode GraphQL response: %w", err)
	}

	if len(graphQLResp.Errors) > 0 {
		return fmt.Errorf("GraphQL query error: %s", graphQLResp.Errors[0].Message)
	}

	parentNodeID := graphQLResp.Data.Repository.ParentIssue.ID
	subIssueNodeID := graphQLResp.Data.Repository.SubIssue.ID

	// Step 2: Add sub-issue to parent issue
	// GitHub's "Add existing issue" feature uses the addSubIssue mutation
	// This creates a proper parent-child relationship in GitHub's issue tracking
	addSubIssueMutation := `
		mutation($issueId: ID!, $subIssueId: ID!) {
			addSubIssue(input: {
				issueId: $issueId,
				subIssueId: $subIssueId
			}) {
				issue {
					id
					number
				}
				subIssue {
					id
					number
				}
			}
		}
	`

	mutationVariables := map[string]interface{}{
		"issueId":    parentNodeID,
		"subIssueId": subIssueNodeID,
	}

	mutationPayload := map[string]interface{}{
		"query":     addSubIssueMutation,
		"variables": mutationVariables,
	}

	mutationJSON, err := json.Marshal(mutationPayload)
	if err != nil {
		return fmt.Errorf("failed to marshal GraphQL mutation: %w", err)
	}

	// Execute the mutation
	mutResp, err := tc.Post("https://api.github.com/graphql", "application/json", strings.NewReader(string(mutationJSON)))
	if err != nil {
		return fmt.Errorf("failed to execute GraphQL mutation: %w", err)
	}
	defer mutResp.Body.Close()

	var mutationResp GraphQLResponse
	if err := json.NewDecoder(mutResp.Body).Decode(&mutationResp); err != nil {
		return fmt.Errorf("failed to decode mutation response: %w", err)
	}

	if len(mutationResp.Errors) > 0 {
		return fmt.Errorf("GraphQL mutation error: %s", mutationResp.Errors[0].Message)
	}

	log.Printf("Successfully added sub-issue #%d to parent issue #%d using GraphQL API", subIssueNumber, config.AppConfig.GITHUB_PARENT_ISSUE)
	return nil
}


