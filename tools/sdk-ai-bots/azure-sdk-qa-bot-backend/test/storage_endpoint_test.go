package test

import (
	"testing"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/storage"
)

func TestGetBlobs(t *testing.T) {
	config.InitEnvironment()
	// Initialize the storage client
	storageClient, err := storage.NewStorageService()
	if err != nil {
		t.Fatalf("Failed to create storage client: %v", err)
	}

	// Define the container name and prefix
	containerName := "test"

	// Call the GetBlobs method
	blobs := storageClient.GetBlobs(containerName)
	if err != nil {
		t.Fatalf("Failed to get blobs: %v", err)
	}

	// Check if the blobs are not empty
	if len(blobs) == 0 {
		t.Fatal("No blobs found")
	}

	// Print the blob names
	for _, blob := range blobs {
		t.Logf("Blob name: %s", blob)
	}
}

func TestPutBlob(t *testing.T) {
	config.InitEnvironment()
	// Initialize the storage client
	storageClient, err := storage.NewStorageService()
	if err != nil {
		t.Fatalf("Failed to create storage client: %v", err)
	}

	// Define the container name, blob name, and content
	containerName := "test"
	blobName := "test_blob.txt"
	content := []byte("This is a test blob.")

	// Call the PutBlob method
	err = storageClient.PutBlob(containerName, blobName, content)
	if err != nil {
		t.Fatalf("Failed to put blob: %v", err)
	}

	t.Logf("Blob %s uploaded successfully to container %s", blobName, containerName)
}

func TestDeleteBlob(t *testing.T) {
	config.InitEnvironment()
	// Initialize the storage client
	storageClient, err := storage.NewStorageService()
	if err != nil {
		t.Fatalf("Failed to create storage client: %v", err)
	}

	// Define the container name and blob name
	containerName := "test"
	blobName := "test_blob.txt"

	// Call the DeleteBlob method
	err = storageClient.DeleteBlob(containerName, blobName)
	if err != nil {
		t.Fatalf("Failed to delete blob: %v", err)
	}

	t.Logf("Blob %s deleted successfully from container %s", blobName, containerName)
}
