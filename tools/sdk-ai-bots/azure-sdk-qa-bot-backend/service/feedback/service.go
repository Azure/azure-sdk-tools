package feedback

import (
	"bytes"
	"encoding/json"
	"fmt"
	"log"
	"time"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/storage"
	"github.com/xuri/excelize/v2"
)

type FeedbackService struct{}

func NewFeedbackService() *FeedbackService {
	return &FeedbackService{}
}

func (s *FeedbackService) SaveFeedback(feedback model.FeedbackReq) error {
	timestamp := time.Now()
	// Get month and week number
	_, month, _ := timestamp.Date()
	_, week := timestamp.ISOWeek()

	// Format: feedback_MM_WW.xlsx (2-digit month, 2-digit week)
	filename := fmt.Sprintf("feedback_month_%02d_week_%02d.xlsx", int(month), week)

	// Read file from storage
	storageService, err := storage.NewStorageService()
	if err != nil {
		return fmt.Errorf("failed to create storage service: %w", err)
	}

	var f *excelize.File
	var existingData bool

	// Try to download existing Excel file from storage
	content, err := storageService.DownloadBlob(config.STORAGE_FEEDBACK_CONTAINER, filename)
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

	defer f.Close()

	sheetName := "Feedback"

	// If this is a new file, set up the headers
	if !existingData {
		// Rename default sheet to "Feedback"
		f.SetSheetName("Sheet1", sheetName)

		// Set headers
		headers := []string{"Timestamp", "TenantID", "Messages", "Reaction", "Comment", "Reasons", "Link"}
		for i, header := range headers {
			cell := fmt.Sprintf("%c1", 'A'+i)
			f.SetCellValue(sheetName, cell, header)
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
	}

	for i, value := range rowData {
		cell := fmt.Sprintf("%c%d", 'A'+i, nextRow)
		f.SetCellValue(sheetName, cell, value)
	}

	// Write to buffer instead of saving to file
	var buf bytes.Buffer
	if err := f.Write(&buf); err != nil {
		return fmt.Errorf("failed to write Excel to buffer: %w", err)
	}

	err = storageService.PutBlob(config.STORAGE_FEEDBACK_CONTAINER, filename, buf.Bytes())
	return err
}
