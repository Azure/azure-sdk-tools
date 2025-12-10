package test

import (
	"testing"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/utils"
	"github.com/stretchr/testify/assert"
)

func TestFilterInvalidReferenceLinks_ValidLinks(t *testing.T) {
	chunks := []string{
		"Check out this guide: https://example.com/path?param=value&other=123#section",
		"More info at https://docs.example.com/api",
		"See the docs at https://example.com/guide.",
	}

	references := []model.Reference{
		{
			Title:  "Complex URL",
			Source: "docs",
			Link:   "https://example.com/path?param=value&other=123#section",
		},
		{
			Title:  "Normal URL",
			Source: "docs",
			Link:   "https://docs.example.com/api",
		},
		{
			Title:  "URL with punctuation",
			Source: "docs",
			Link:   "https://example.com/guide",
		},
		{
			Title:   "Reference without link",
			Source:  "docs",
			Link:    "",
			Content: "Some content",
		},
		{
			Title:  "Fake 1",
			Source: "docs",
			Link:   "https://fake1.com/ref",
		},
		{
			Title:  "Fake 2",
			Source: "docs",
			Link:   "https://fake2.com/ref",
		},
	}

	result := utils.FilterInvalidReferenceLinks(references, chunks)
	assert.Equal(t, 4, len(result))
	assert.Equal(t, "https://example.com/path?param=value&other=123#section", result[0].Link)
	assert.Equal(t, "https://docs.example.com/api", result[1].Link)
	assert.Equal(t, "https://example.com/guide", result[2].Link)
	assert.Equal(t, "", result[3].Link)
}
