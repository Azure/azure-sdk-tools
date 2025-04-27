package config

import (
	"log"
	"os"

	"github.com/Azure/azure-sdk-for-go/sdk/ai/azopenai"
	"github.com/Azure/azure-sdk-for-go/sdk/azcore"
	"github.com/joho/godotenv"
)

var OpenAIClient *azopenai.Client

func InitOpenAIClient() {
	err := godotenv.Load()
	if err != nil {
		panic(err)
	}
	apiKey := os.Getenv("AOAI_CHAT_COMPLETIONS_API_KEY")
	endpoint := os.Getenv("AOAI_CHAT_COMPLETIONS_ENDPOINT")
	keyCredential := azcore.NewKeyCredential(apiKey)
	client, err := azopenai.NewClientWithKeyCredential(endpoint, keyCredential, nil)
	if err != nil {
		log.Printf("ERROR: %s", err)
		return
	}
	OpenAIClient = client
}
