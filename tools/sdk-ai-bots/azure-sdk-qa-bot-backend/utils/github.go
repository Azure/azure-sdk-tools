package utils

import (
	"context"
	"encoding/json"
	"fmt"
	"log"
	"strings"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/google/go-github/v66/github"
	"golang.org/x/oauth2"
)

// CreateIssue creates a GitHub issue with the given title, body, and labels
func CreateIssue(owner, repo, title, body string, labels []string) (int, error) {
	// Check if GitHub is configured
	if config.GITHUB_TOKEN == "" || owner == "" || repo == "" {
		log.Printf("GitHub integration not configured (optional), skipping issue creation")
		return 0, nil
	}

	log.Printf("Creating GitHub issue in %s/%s", owner, repo)

	// Create GitHub client with authentication
	ctx := context.Background()
	ts := oauth2.StaticTokenSource(
		&oauth2.Token{AccessToken: config.GITHUB_TOKEN},
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
	issue, _, err := client.Issues.Create(ctx, owner, repo, githubIssue)
	if err != nil {
		return 0, fmt.Errorf("failed to create GitHub issue: %w", err)
	}

	issueNumber := issue.GetNumber()
	log.Printf("GitHub issue created: #%d - %s", issueNumber, issue.GetHTMLURL())

	return issueNumber, nil
}

// AddSubIssueToParent adds a sub-issue to the parent issue using GitHub's GraphQL API
func AddSubIssueToParent(owner, repo string, parentIssueNumber, subIssueNumber int) error {
	// Check if GitHub is configured
	if config.GITHUB_TOKEN == "" || owner == "" || repo == "" {
		return fmt.Errorf("GitHub not configured")
	}

	ctx := context.Background()
	ts := oauth2.StaticTokenSource(
		&oauth2.Token{AccessToken: config.GITHUB_TOKEN},
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
		"owner":        owner,
		"repo":         repo,
		"parentNumber": parentIssueNumber,
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

	log.Printf("Successfully added sub-issue #%d to parent issue #%d", subIssueNumber, parentIssueNumber)
	return nil
}
