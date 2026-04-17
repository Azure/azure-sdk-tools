package config

import (
	"github.com/openai/openai-go/v3"
	"github.com/openai/openai-go/v3/option"
)

var OpenAIClient *openai.Client

func InitOpenAIClient() {
	endpoint := AppConfig.AOAI_CHAT_COMPLETIONS_ENDPOINT
	apiKey := AOAI_CHAT_COMPLETIONS_API_KEY
	client := openai.NewClient(
		option.WithBaseURL(endpoint+"/openai/v1/"),
		option.WithAPIKey(apiKey),
	)
	OpenAIClient = &client
}
