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

func TestIsPipelineLink(t *testing.T) {
	tests := []struct {
		name     string
		url      string
		expected bool
	}{
		{
			name:     "Valid pipeline link with results",
			url:      "https://dev.azure.com/azure-sdk/internal/_build/results?buildId=5530426",
			expected: true,
		},
		{
			name:     "Valid pipeline link without results",
			url:      "https://dev.azure.com/azure-sdk/public/_build?buildId=12345",
			expected: true,
		},
		{
			name:     "Invalid - GitHub link",
			url:      "https://github.com/Azure/azure-sdk-tools",
			expected: false,
		},
		{
			name:     "Invalid - regular website",
			url:      "https://www.example.com",
			expected: false,
		},
		{
			name:     "Invalid - missing buildId",
			url:      "https://dev.azure.com/azure-sdk/internal/_build/results",
			expected: false,
		},
		{
			name:     "Valid - URL with space in project name and query params",
			url:      "https://dev.azure.com/azure-sdk/public/public Team/_build/results?buildId=5530426&view=logs",
			expected: true,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			result := utils.IsPipelineLink(tt.url)
			if result != tt.expected {
				t.Errorf("IsPipelineLink(%q) = %v, expected %v", tt.url, result, tt.expected)
			}
		})
	}
}

func TestExtractBuildID(t *testing.T) {
	tests := []struct {
		name        string
		url         string
		expectedID  string
		expectError bool
	}{
		{
			name:        "Valid pipeline link with results",
			url:         "https://dev.azure.com/azure-sdk/internal/_build/results?buildId=5530426",
			expectedID:  "5530426",
			expectError: false,
		},
		{
			name:        "Valid pipeline link without results",
			url:         "https://dev.azure.com/azure-sdk/public/_build?buildId=12345",
			expectedID:  "12345",
			expectError: false,
		},
		{
			name:        "Invalid - no buildId parameter",
			url:         "https://dev.azure.com/azure-sdk/internal/_build/results",
			expectedID:  "0",
			expectError: true,
		},
		{
			name:        "Invalid - not a pipeline link",
			url:         "https://github.com/Azure/azure-sdk-tools",
			expectedID:  "0",
			expectError: true,
		},
		{
			name:        "Valid - URL with space in project name and query params",
			url:         "https://dev.azure.com/azure-sdk/public/public Team/_build/results?buildId=5530426&view=logs",
			expectedID:  "5530426",
			expectError: false,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			buildID := utils.ExtractBuildID(tt.url)
			if tt.expectError {
				if buildID != "" {
					t.Errorf("ExtractBuildID(%q) expected error but got none", tt.url)
				}
			} else {
				if buildID != tt.expectedID {
					t.Errorf("ExtractBuildID(%q) = %s, expected %s", tt.url, buildID, tt.expectedID)
				}
			}
		})
	}
}
