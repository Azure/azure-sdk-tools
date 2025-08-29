package config

import (
	"context"
	"encoding/base64"
	"log"
	"strings"

	"github.com/Azure/azure-sdk-for-go/sdk/azidentity"
	"github.com/Azure/azure-sdk-for-go/sdk/keyvault/azsecrets"
)

var AI_SEARCH_APIKEY string
var AOAI_CHAT_COMPLETIONS_API_KEY string
var API_KEY string
var PREPROCESS_ENV_LOCAL_KEY string

func InitSecrets() {
	//Create a credential using the NewDefaultAzureCredential type.
	cred, err := azidentity.NewDefaultAzureCredential(nil)
	if err != nil {
		log.Fatalf("failed to obtain a credential: %v", err)
	}

	//Establish a connection to the Key Vault client
	client, err := azsecrets.NewClient(AppConfig.KEYVAULT_ENDPOINT, cred, nil)
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
	resp, err = client.GetSecret(context.Background(), "API-KEY", "", nil)
	if err != nil {
		log.Fatalf("failed to get the secret: %v", err)
	}
	if resp.Value == nil {
		log.Fatalf("failed to get the secret value: %v", err)
	}
	API_KEY = *resp.Value
	resp, err = client.GetSecret(context.Background(), "PREPROCESS-ENV-LOCAL-BASE64", "", nil)
	if err != nil {
		log.Fatalf("failed to get the secret: %v", err)
	}
	if resp.Value == nil {
		log.Fatalf("failed to get the secret value: %v", err)
	}
	decoded, err := base64.StdEncoding.DecodeString(*resp.Value)
	if err != nil {
		log.Fatalf("failed to decode PREPROCESS-ENV-LOCAL-BASE64: %v", err)
	}
	PREPROCESS_ENV_LOCAL_KEY = string(decoded)
	for _, line := range strings.Split(string(PREPROCESS_ENV_LOCAL_KEY), "\n") {
		if len(line) >= 8 && line[:8] == "API_KEY=" {
			PREPROCESS_ENV_LOCAL_KEY = line[8:]
			break
		}
	}
}
