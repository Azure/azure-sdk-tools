package config

import (
	"context"
	"log"

	"github.com/Azure/azure-sdk-for-go/sdk/keyvault/azsecrets"
)

var AI_SEARCH_APIKEY string
var AOAI_CHAT_COMPLETIONS_API_KEY string

func InitSecrets() {
	// Use the global credential
	if Credential == nil {
		log.Fatal("Credential not initialized; call InitConfiguration first")
	}

	//Establish a connection to the Key Vault client
	client, err := azsecrets.NewClient(AppConfig.KEYVAULT_ENDPOINT, Credential, nil)
	if err != nil {
		log.Fatalf("failed to connect to client: %v", err)
	}
	resp, err := client.GetSecret(context.Background(), "AI-SEARCH-APIKEY", "", nil)
	if err != nil {
		log.Fatalf("failed to get the secret: %v", err)
	}
	if resp.Value == nil {
		log.Fatalf("failed to get the secret value: %v", err)
	}
	AI_SEARCH_APIKEY = *resp.Value
	resp, err = client.GetSecret(context.Background(), "AOAI-CHAT-COMPLETIONS-API-KEY", "", nil)
	if err != nil {
		log.Fatalf("failed to get the secret: %v", err)
	}
	if resp.Value == nil {
		log.Fatalf("failed to get the secret value: %v", err)
	}
	AOAI_CHAT_COMPLETIONS_API_KEY = *resp.Value
}
