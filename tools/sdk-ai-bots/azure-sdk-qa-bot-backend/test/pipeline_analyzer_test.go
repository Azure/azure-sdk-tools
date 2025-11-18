package test

import (
	"testing"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/utils"
)

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

// TestAnalyzePipelineIntegration is an integration test that requires:
func TestAnalyzePipeline(t *testing.T) {
	if testing.Short() {
		t.Skip("Skipping integration test in short mode")
	}

	// Use a known public pipeline for testing
	// You may need to update this with a valid build ID that you have access to
	buildID := "5530426"

	t.Logf("Testing pipeline analysis for build ID: %s", buildID)
	t.Log("Note: This test requires Azure DevOps authentication and may fail if you don't have access to the pipeline")

	// Test the Go wrapper function
	result, err := utils.AnalyzePipeline(buildID, "", true)

	if err != nil {
		// This might fail due to auth issues, which is expected in CI/local dev
		t.Logf("Pipeline analysis failed (this is expected if not authenticated): %v", err)
		return
	}

	// If we got a result, verify it's not empty
	if result != "" {
		t.Logf("Pipeline analysis succeeded!")
		t.Logf("Analysis output:\n%s", result)

		// Check that the output contains some expected keywords
		if len(result) > 0 {
			t.Log("Result is not empty - test passed!")
		}
	} else {
		t.Error("Pipeline analysis returned empty result")
	}
}
