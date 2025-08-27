package storage

import (
	"context"
	"fmt"
	"io"
	"log"
	"strings"

	"github.com/Azure/azure-sdk-for-go/sdk/azidentity"
	"github.com/Azure/azure-sdk-for-go/sdk/storage/azblob"
	"github.com/Azure/azure-sdk-for-go/sdk/storage/azblob/blob"
	"github.com/Azure/azure-sdk-for-go/sdk/storage/azblob/blockblob"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
)

type StorageService struct {
	blobClient *azblob.Client
	credential *azidentity.DefaultAzureCredential
}

func NewStorageService() (*StorageService, error) {
	// Create a DefaultAzureCredential
	credential, err := azidentity.NewDefaultAzureCredential(nil)
	if err != nil {
		return nil, fmt.Errorf("failed to create credential: %v", err)
	}

	// Create a blob client
	blobClient, err := azblob.NewClient(config.STORAGE_BASE_URL, credential, nil)
	if err != nil {
		return nil, fmt.Errorf("failed to create blob client: %v", err)
	}
	return &StorageService{credential: credential, blobClient: blobClient}, nil
}

func (s *StorageService) DownloadBlob(container, path string) ([]byte, error) {
	// Download the blob
	resp, err := s.blobClient.DownloadStream(context.Background(), container, path, nil)
	if err != nil {
		return nil, fmt.Errorf("failed to download blob: %v", err)
	}
	// Read the blob content
	body := resp.Body
	defer func() {
		if err = body.Close(); err != nil {
			log.Printf("Failed to close response body: %v", err)
		}
	}()
	content, err := io.ReadAll(body)
	if err != nil {
		return nil, fmt.Errorf("failed to read blob content: %v", err)
	}
	return content, nil
}

func (s *StorageService) PutBlob(container, path string, content []byte) error {
	// Upload the blob
	_, err := s.blobClient.UploadBuffer(context.Background(), container, path, content, &azblob.UploadBufferOptions{})
	if err != nil {
		return fmt.Errorf("failed to upload blob: %v", err)
	}

	return nil
}

func (s *StorageService) GetBlobs(container string) []string {
	result := []string{}
	pager := s.blobClient.NewListBlobsFlatPager(container, &azblob.ListBlobsFlatOptions{})
	for pager.More() {
		resp, err := pager.NextPage(context.TODO())
		if err != nil {
			log.Fatal(err)
		}
		for _, blob := range resp.Segment.BlobItems {
			result = append(result, *blob.Name)
		}
	}
	return result
}

func (s *StorageService) DeleteBlob(container, path string) error {
	blobUrl := fmt.Sprintf("%s/%s/%s", config.STORAGE_BASE_URL, container, path)
	blobUrl = strings.ReplaceAll(blobUrl, "#", "%23")
	// Create a blockBlob client
	blockBlobClient, err := blockblob.NewClient(blobUrl, s.credential, &blockblob.ClientOptions{})
	if err != nil {
		return fmt.Errorf("failed to create container client: %v", err)
	}
	isDeleted := "true"
	_, err = blockBlobClient.SetMetadata(context.Background(), map[string]*string{
		"IsDeleted": &isDeleted,
	}, &blob.SetMetadataOptions{})
	if err != nil {
		return fmt.Errorf("failed to set metadata: %v", err)
	}
	return nil
}
