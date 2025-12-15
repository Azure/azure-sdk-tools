package test

import (
	"testing"

	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/utils"
	"github.com/stretchr/testify/assert"
)

func TestFilterInvalidReferenceLinks_ValidLinks(t *testing.T) {
	knowledges := []model.Knowledge{
		{
			Link: "https://example.com/path?param=value&other=123#section",
		},
		{
			Link: "https://docs.example.com/api",
		},
		{
			Link: "https://example.com/guide",
		},
	}
	references := []model.Reference{
		{
			Title: "Complex URL",
			Link:  "https://example.com/path?param=value&other=123#section",
		},
		{
			Title: "Normal URL",
			Link:  "https://docs.example.com/api",
		},
		{
			Title: "URL with punctuation",
			Link:  "https://example.com/guide",
		},
		{
			Title:   "Reference without link",
			Link:    "",
			Content: "Some content",
		},
		{
			Title: "Fake 1",
			Link:  "https://fake1.com/ref",
		},
		{
			Title:  "Fake 2",
			Source: "docs",
			Link:   "https://fake2.com/ref",
		},
	}

	result := utils.FilterInvalidReferenceLinks(references, knowledges)
	assert.Equal(t, 4, len(result))
	assert.Equal(t, "https://example.com/path?param=value&other=123#section", result[0].Link)
	assert.Equal(t, "https://docs.example.com/api", result[1].Link)
	assert.Equal(t, "https://example.com/guide", result[2].Link)
	assert.Equal(t, "", result[3].Link)
}
