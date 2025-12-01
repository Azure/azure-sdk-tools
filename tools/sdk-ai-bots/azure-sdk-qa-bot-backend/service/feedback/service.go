package feedback

import (
	"bytes"
	"encoding/json"
	"fmt"
	"log"
	"strings"
	"time"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/storage"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/utils"
	"github.com/xuri/excelize/v2"
)

type FeedbackService struct{}

func NewFeedbackService() *FeedbackService {
	return &FeedbackService{}
}

func (s *FeedbackService) SaveFeedback(feedback model.FeedbackReq) error {
	timestamp := time.Now()
	// Get year and month
	year, month, _ := timestamp.Date()

	// Format: feedback_YYYY_MM.xlsx
	filename := fmt.Sprintf("feedback_%04d_%02d.xlsx", year, int(month))

	// Read file from storage
	storageService, err := storage.NewStorageService()
	if err != nil {
		return fmt.Errorf("failed to create storage service: %w", err)
	}

	var f *excelize.File
	var existingData bool

	// Try to download existing Excel file from storage
	content, err := storageService.DownloadBlob(config.AppConfig.STORAGE_FEEDBACK_CONTAINER, filename)
	if err != nil || len(content) == 0 {
		log.Printf("Failed to download feedback file or file is empty (creating new): %v", err)
		// Create new Excel file
		f = excelize.NewFile()
		existingData = false
	} else {
		// Open the file directly from bytes in memory
		f, err = excelize.OpenReader(bytes.NewReader(content))
		if err != nil {
			return fmt.Errorf("failed to open existing Excel file: %w", err)
		}
		existingData = true
	}

	defer func() {
		if err = f.Close(); err != nil {
			log.Printf("Failed to close Excel file: %v", err)
		}
	}()

	sheetName := "Feedback"

	// If this is a new file, set up the headers
	if !existingData {
		// Rename default sheet to "Feedback"
		if err = f.SetSheetName("Sheet1", sheetName); err != nil {
			return fmt.Errorf("failed to set sheet name: %w", err)
		}

		// Set headers
		headers := []string{"Timestamp", "TenantID", "Messages", "Reaction", "Comment", "Reasons", "Link", "ChannelID", "UserName"}
		for i, header := range headers {
			cell := fmt.Sprintf("%c1", 'A'+i)
			if err = f.SetCellValue(sheetName, cell, header); err != nil {
				return fmt.Errorf("failed to set cell value: %w", err)
			}
		}
	}

	// Find the next empty row
	rows, err := f.GetRows(sheetName)
	if err != nil {
		return fmt.Errorf("failed to get rows: %w", err)
	}
	nextRow := len(rows) + 1

	// Convert data to JSON bytes for Excel
	reasonBytes, _ := json.Marshal(feedback.Reasons)
	messageBytes, _ := json.Marshal(feedback.Messages)

	// Set the new row data
	rowData := []interface{}{
		timestamp.Format(time.RFC3339),
		feedback.TenantID,
		string(messageBytes),
		feedback.Reaction,
		feedback.Comment,
		string(reasonBytes),
		feedback.Link,
		feedback.ChannelID,
		feedback.UserName
	}

	for i, value := range rowData {
		cell := fmt.Sprintf("%c%d", 'A'+i, nextRow)
		if err = f.SetCellValue(sheetName, cell, value); err != nil {
			return fmt.Errorf("failed to set cell value: %w", err)
		}
	}

	// Write to buffer instead of saving to file
	var buf bytes.Buffer
	if err = f.Write(&buf); err != nil {
		return fmt.Errorf("failed to write Excel to buffer: %w", err)
	}

	err = storageService.PutBlob(config.AppConfig.STORAGE_FEEDBACK_CONTAINER, filename, buf.Bytes())
	return err
}

// CreateGitHubIssueForNegativeFeedback creates a GitHub issue for negative feedback
func (s *FeedbackService) CreateGitHubIssueForNegativeFeedback(feedback model.FeedbackReq) error {
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
	subject := feedback.Subject
	if subject == "" {
		subject = "No Subject"
	}
	title := fmt.Sprintf("[Teams Chatbot]: Negative Feedback - %s", subject)

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
	issueNumber, err := utils.CreateIssue(
		config.AppConfig.GITHUB_OWNER,
		config.AppConfig.GITHUB_REPO,
		title,
		issueBody,
		labels,
	)
	if err != nil {
		return fmt.Errorf("failed to create GitHub issue from feedback: %w", err)
	}

	// If issue was created successfully (issueNumber > 0), add it to parent issue's task list
	if issueNumber > 0 && config.AppConfig.GITHUB_PARENT_ISSUE > 0 {
		if err := utils.AddSubIssueToParent(
			config.AppConfig.GITHUB_OWNER,
			config.AppConfig.GITHUB_REPO,
			config.AppConfig.GITHUB_PARENT_ISSUE,
			issueNumber,
		); err != nil {
			log.Printf("Warning: Failed to add sub-issue to parent issue task list: %v", err)
			// Don't return error as the sub-issue was created successfully
		}
	}

	return nil
}
