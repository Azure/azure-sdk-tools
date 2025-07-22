package feedback

import (
	"encoding/json"
	"fmt"
	"log"
	"os"
	"time"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/storage"
)

type FeedbackService struct{}

func NewFeedbackService() *FeedbackService {
	return &FeedbackService{}
}

func (s *FeedbackService) SaveFeedback(feedback model.FeedbackReq) error {
	timestamp := time.Now()
	filename := fmt.Sprintf("feedback_%s.csv", timestamp.Format("2006-01-02"))
	header := "Timestamp,TenantID,Messages,Reaction,Comment,Reasons,Link\n"
	// Read file from storage
	storageService, err := storage.NewStorageService()
	if err != nil {
		return fmt.Errorf("failed to create storage service: %w", err)
	}
	// Create or open CSV file
	var f *os.File

	if _, err := os.Stat(filename); os.IsNotExist(err) {
		// Create new file with headers
		f, err = os.Create(filename)
		if err != nil {
			return err
		}
	} else {
		// Open existing file in write mode and truncate it to overwrite content
		f, err = os.OpenFile(filename, os.O_WRONLY|os.O_TRUNC, 0644)
		if err != nil {
			return err
		}
	}
	// Sync the feedback file from storage
	content, err := storageService.DownloadBlob(config.STORAGE_FEEDBACK_CONTAINER, filename)
	if err != nil {
		log.Printf("Failed to download feedback file: %v", err)
	}
	if len(content) > 0 {
		_, err = f.Write(content)
	} else {
		// Write header if file is new
		_, err = f.WriteString(header)
	}
	if err != nil {
		f.Close()
		log.Printf("Failed to write feedback record: %v", err)
	}
	reasonStr, _ := json.Marshal(feedback.Reasons)
	// Convert messages to JSON string for CSV
	messageStr, _ := json.Marshal(feedback.Messages)
	// Format and write the new record
	record := fmt.Sprintf("%s,%s,%s,%s,%s,%s,%s\n",
		timestamp.Format(time.RFC3339),
		feedback.TenantID,
		messageStr,
		feedback.Reaction,
		feedback.Comment,
		reasonStr,
		feedback.Link,
	)
	_, err = f.WriteString(record)
	if err != nil {
		f.Close()
		log.Printf("Failed to write feedback record: %v", err)
	}
	f.Close()

	go updateFeedbackFile(filename)
	return err
}

func updateFeedbackFile(filename string) {
	// read the file
	content, err := os.ReadFile(filename)
	if err != nil {
		fmt.Printf("failed to read feedback file: %v", err)
		return
	}

	// Upload the file
	storageService, err := storage.NewStorageService()
	if err != nil {
		fmt.Printf("failed to create storage service: %v", err)
		return
	}
	if err := storageService.PutBlob(config.STORAGE_FEEDBACK_CONTAINER, filename, content); err != nil {
		fmt.Printf("failed to upload feedback file: %v", err)
		return
	}
}
