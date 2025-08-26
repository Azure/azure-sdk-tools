package test

import (
	"context"
	"log"
	"testing"

	"github.com/Azure/azure-sdk-for-go/sdk/azidentity"
	"github.com/Azure/azure-sdk-for-go/sdk/keyvault/azsecrets"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
)

func TestListSecrets(t *testing.T) {
	config.InitEnvironment()
	//Create a credential using the NewDefaultAzureCredential type.
	cred, err := azidentity.NewDefaultAzureCredential(nil)
	if err != nil {
		t.Fatalf("failed to obtain a credential: %v", err)
	}

	//Establish a connection to the Key Vault client
	client, err := azsecrets.NewClient(config.KEYVAULT_ENDPOINT, cred, nil)
	if err != nil {
		t.Fatalf("failed to connect to client: %v", err)
	}
	//List the keys in the Key Vault
	pager := client.NewListSecretsPager(nil)
	for pager.More() {
		resp, err := pager.NextPage(context.Background())
		if err != nil {
			t.Fatalf("failed to list keys: %v", err)
		}
		for _, secret := range resp.Value {
			log.Printf("Key: %s\n", secret.ID.Name())
		}
	}
}
