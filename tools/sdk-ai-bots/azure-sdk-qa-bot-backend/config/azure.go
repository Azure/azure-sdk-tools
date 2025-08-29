package config

import (
	"log"

	"github.com/Azure/azure-sdk-for-go/sdk/ai/azopenai"
	"github.com/Azure/azure-sdk-for-go/sdk/azcore"
)

var OpenAIClient *azopenai.Client

func InitOpenAIClient() {
	endpoint := AppConfig.AOAI_CHAT_COMPLETIONS_ENDPOINT
	keyCredential := azcore.NewKeyCredential(AOAI_CHAT_COMPLETIONS_API_KEY)
	client, err := azopenai.NewClientWithKeyCredential(endpoint, keyCredential, nil)
	if err != nil {
		log.Printf("ERROR: %s", err)
		return
	}
	OpenAIClient = client
}
