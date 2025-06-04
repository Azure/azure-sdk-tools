package feedback

import (
	"encoding/json"
	"fmt"
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

	// Create or open CSV file
	var f *os.File
	var err error

	if _, err := os.Stat(filename); os.IsNotExist(err) {
		// Create new file with headers
		f, err = os.Create(filename)
		if err != nil {
			return err
		}
		// Write headers
		_, err = f.WriteString("Timestamp,TenantID,Messages,Reaction\n")
		if err != nil {
			f.Close()
			return err
		}
	} else {
		// Open existing file in append mode
		f, err = os.OpenFile(filename, os.O_APPEND|os.O_WRONLY, 0644)
		if err != nil {
			return err
		}
	}
	defer f.Close()
	messageStr, _ := json.Marshal(feedback.Messages)
	// Format and write the new record
	record := fmt.Sprintf("%s,%s,%s,%s,%s\n",
		timestamp.Format(time.RFC3339),
		feedback.TenantID,
		messageStr,
		feedback.Reaction,
		feedback.Comment,
	)

	_, err = f.WriteString(record)

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
