package storage

import (
	"context"
	"fmt"
	"log"
	"strings"

	"github.com/Azure/azure-sdk-for-go/sdk/azidentity"
	"github.com/Azure/azure-sdk-for-go/sdk/storage/azblob"
	"github.com/Azure/azure-sdk-for-go/sdk/storage/azblob/blob"
	"github.com/Azure/azure-sdk-for-go/sdk/storage/azblob/blockblob"
)

const (
	baseUrl       = "https://typespechelper4storage.blob.core.windows.net"
	blobContainer = "knowledge"
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
	blobClient, err := azblob.NewClient(baseUrl, credential, nil)
	if err != nil {
		return nil, fmt.Errorf("failed to create blob client: %v", err)
	}
	return &StorageService{credential: credential, blobClient: blobClient}, nil
}

func (s *StorageService) PutBlob(path string, content []byte) error {
	// Upload the blob
	_, err := s.blobClient.UploadBuffer(context.Background(), blobContainer, path, content, &azblob.UploadBufferOptions{})
	if err != nil {
		return fmt.Errorf("failed to upload blob: %v", err)
	}

	return nil
}

func (s *StorageService) GetBlobs() []string {
	result := []string{}
	pager := s.blobClient.NewListBlobsFlatPager(blobContainer, &azblob.ListBlobsFlatOptions{})
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

func (s *StorageService) DeleteBlob(path string) error {
	blobUrl := fmt.Sprintf("%s/%s/%s", baseUrl, blobContainer, path)
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
