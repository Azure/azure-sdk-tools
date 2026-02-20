package record

import (
	"bytes"
	"fmt"
	"log"
	"time"

	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/storage"
	"github.com/xuri/excelize/v2"
)

type RecordService struct{}

func NewRecordService() *RecordService {
	return &RecordService{}
}

func (s *RecordService) SaveAnswerRecord(record model.AnswerRecordReq) error {
	timestamp := time.Now()

	// Get year and month from the timestamp
	year, month, _ := timestamp.Date()

	// Format: answer_records_YYYY_MM.xlsx
	filename := fmt.Sprintf("answer_records_%04d_%02d.xlsx", year, int(month))

	// Read file from storage
	storageService, err := storage.NewStorageService()
	if err != nil {
		return fmt.Errorf("failed to create storage service: %w", err)
	}

	var f *excelize.File
	var existingData bool

	// Try to download existing Excel file from storage
	content, err := storageService.DownloadBlob(config.AppConfig.STORAGE_RECORDS_CONTAINER, filename)
	if err != nil || len(content) == 0 {
		log.Printf("Failed to download answer records file or file is empty (creating new): %v", err)
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

	sheetName := "AnswerRecords"

	// If this is a new file, set up the headers
	if !existingData {
		// Rename default sheet to "AnswerRecords"
		if err = f.SetSheetName("Sheet1", sheetName); err != nil {
			return fmt.Errorf("failed to set sheet name: %w", err)
		}

		// Set headers
		headers := []string{"Timestamp", "ChannelName", "ChannelID", "MessageLink"}
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

	// Set the new row data
	rowData := []interface{}{
		timestamp.Format(time.RFC3339),
		record.ChannelName,
		record.ChannelID,
		record.MessageLink,
	}

	for i, value := range rowData {
		cell := fmt.Sprintf("%c%d", 'A'+i, nextRow)
		err = f.SetCellValue(sheetName, cell, value)
		if err != nil {
			return fmt.Errorf("failed to set cell value: %w", err)
		}
	}

	// Write to buffer instead of saving to file
	var buf bytes.Buffer
	if err = f.Write(&buf); err != nil {
		return fmt.Errorf("failed to write Excel to buffer: %w", err)
	}

	err = storageService.PutBlob(config.AppConfig.STORAGE_RECORDS_CONTAINER, filename, buf.Bytes())
	return err
}
