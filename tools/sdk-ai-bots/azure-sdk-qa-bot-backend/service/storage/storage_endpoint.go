package storage

import (
	"context"
	"fmt"

	"github.com/Azure/azure-sdk-for-go/sdk/azidentity"
	"github.com/Azure/azure-sdk-for-go/sdk/storage/azblob"
)

const (
	baseUrl       = "https://typespechelper4storage.blob.core.windows.net"
	blobContainer = "knowledge"
)

type StorageService struct {
	blobClient *azblob.Client
}

func NewStorageService() (*StorageService, error) {
	// Create a DefaultAzureCredential
	credential, err := azidentity.NewDefaultAzureCredential(nil)
	if err != nil {
		return nil, fmt.Errorf("failed to create credential: %v", err)
	}

	// Create a client
	blobClient, err := azblob.NewClient(baseUrl, credential, nil)
	if err != nil {
		return nil, fmt.Errorf("failed to create blob client: %v", err)
	}

	return &StorageService{blobClient: blobClient}, nil
}

func (s *StorageService) PutBlob(path string, content []byte) error {
	// Upload the blob
	_, err := s.blobClient.UploadBuffer(context.Background(), blobContainer, path, content, &azblob.UploadBufferOptions{})
	if err != nil {
		return fmt.Errorf("failed to upload blob: %v", err)
	}

	return nil
}
