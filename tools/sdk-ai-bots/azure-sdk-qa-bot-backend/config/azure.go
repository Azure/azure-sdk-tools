package config

import (
	"context"
	"encoding/base64"
	"log"
	"strings"

	"github.com/Azure/azure-sdk-for-go/sdk/ai/azopenai"
	"github.com/Azure/azure-sdk-for-go/sdk/azcore"
	"github.com/Azure/azure-sdk-for-go/sdk/azidentity"
	"github.com/Azure/azure-sdk-for-go/sdk/keyvault/azsecrets"
)

var OpenAIClient *azopenai.Client
var AI_SEARCH_APIKEY string
var AOAI_CHAT_COMPLETIONS_API_KEY string
var API_KEY string
var PREPROCESS_ENV_LOCAL_KEY string

const (
	AOAI_CHAT_REASONING_MODEL      = "o3-mini"
	AOAI_CHAT_COMPLETIONS_MODEL    = "gpt-4.1"
	AOAI_CHAT_COMPLETIONS_TOP_P    = 0.8
	AOAI_CHAT_MAX_TOKENS           = 10000
	AOAI_CHAT_CONTEXT_MAX_TOKENS   = 100000
	AOAI_CHAT_COMPLETIONS_ENDPOINT = "https://UxAutoTestEast.openai.azure.com"
	AI_SEARCH_BASE_URL             = "https://typspehelper4search.search.windows.net"
	AI_SEARCH_INDEX                = "typespec-knowledge"
	AI_SEARCH_AGENT                = "azure-sdk-agent"
	STORAGE_BASE_URL               = "https://typespechelper4storage.blob.core.windows.net"
	STORAGE_KNOWLEDGE_CONTAINER    = "knowledge"
	STORAGE_FEEDBACK_CONTAINER     = "feedback-v2"
	STORAGE_RECORDS_CONTAINER      = "records"
)

func InitOpenAIClient() {
	endpoint := AOAI_CHAT_COMPLETIONS_ENDPOINT
	keyCredential := azcore.NewKeyCredential(AOAI_CHAT_COMPLETIONS_API_KEY)
	client, err := azopenai.NewClientWithKeyCredential(endpoint, keyCredential, nil)
	if err != nil {
		log.Printf("ERROR: %s", err)
		return
	}
	OpenAIClient = client
}

func InitSecrets() {
	keyVaultURL := "https://azuresdkqabotea.vault.azure.net"

	//Create a credential using the NewDefaultAzureCredential type.
	cred, err := azidentity.NewDefaultAzureCredential(nil)
	if err != nil {
		log.Fatalf("failed to obtain a credential: %v", err)
	}

	//Establish a connection to the Key Vault client
	client, err := azsecrets.NewClient(keyVaultURL, cred, nil)
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
