package test

import (
	"testing"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/storage"
	"github.com/stretchr/testify/require"
)

func TestGetBlobs(t *testing.T) {
	config.InitConfiguration()
	// Initialize the storage client
	storageClient, err := storage.NewStorageService()
	require.NoError(t, err)

	// Define the container name and prefix
	containerName := "test"

	// Call the GetBlobs method
	blobs := storageClient.GetBlobs(containerName)

	// Check if the blobs are not empty
	require.NotEmpty(t, blobs)

	// Print the blob names
	for _, blob := range blobs {
		require.NotEmpty(t, blob)
	}
}

func TestPutBlob(t *testing.T) {
	config.InitConfiguration()
	// Initialize the storage client
	storageClient, err := storage.NewStorageService()
	require.NoError(t, err)

	// Define the container name, blob name, and content
	containerName := "test"
	blobName := "test_blob.txt"
	content := []byte("This is a test blob.")

	// Call the PutBlob method
	err = storageClient.PutBlob(containerName, blobName, content)
	require.NoError(t, err)
}

func TestDeleteBlob(t *testing.T) {
	config.InitConfiguration()
	// Initialize the storage client
	storageClient, err := storage.NewStorageService()
	require.NoError(t, err)

	// Define the container name and blob name
	containerName := "test"
	blobName := "test_blob.txt"

	// Call the DeleteBlob method
	err = storageClient.DeleteBlob(containerName, blobName)
	require.NoError(t, err)
}
