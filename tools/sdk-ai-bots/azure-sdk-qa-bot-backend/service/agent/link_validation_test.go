package agent

import (
	"testing"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/stretchr/testify/assert"
)

func TestExtractLinksFromChunks(t *testing.T) {
	tests := []struct {
		name     string
		chunks   []model.Index
		expected []string
	}{
		{
			name: "Extract from document link",
			chunks: []model.Index{
				{
					ContextID: "typespec_docs",
					Title:     "intro",
					Chunk:     "Some content",
				},
			},
			expected: []string{"https://typespec.io/docs/intro"},
		},
		{
			name: "Extract from chunk content",
			chunks: []model.Index{
				{
					ContextID: "static_typespec_qa",
					Title:     "test",
					Chunk:     "Check this link: https://example.com/test for more info",
				},
			},
			expected: []string{"https://example.com/test"},
		},
		{
			name: "Extract multiple links from content",
			chunks: []model.Index{
				{
					ContextID: "static_typespec_qa",
					Title:     "test",
					Chunk:     "Link1: https://example.com/link1 and Link2: https://example.com/link2",
				},
			},
			expected: []string{"https://example.com/link1", "https://example.com/link2"},
		},
		{
			name: "Extract playground link from content",
			chunks: []model.Index{
				{
					ContextID: "static_typespec_qa",
					Title:     "test",
					Chunk:     "<a href=\"https://azure.github.io/typespec-azure/playground/?options=%7B%22test%22%3A%22value%22%7D&c=test123\">link</a>",
				},
			},
			expected: []string{"https://azure.github.io/typespec-azure/playground/?options=%7B%22test%22%3A%22value%22%7D&c=test123"},
		},
		{
			name: "Deduplicate links",
			chunks: []model.Index{
				{
					ContextID: "typespec_docs",
					Title:     "intro",
					Chunk:     "https://typespec.io/docs/intro is a great resource",
				},
			},
			// Should only return one instance even though link appears in both GetIndexLink and content
			expected: []string{"https://typespec.io/docs/intro"},
		},
		{
			name:     "No links",
			chunks:   []model.Index{},
			expected: []string{},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			result := extractLinksFromChunks(tt.chunks)

			// Since map iteration order is random, check length and membership
			assert.Equal(t, len(tt.expected), len(result), "Number of links should match")

			// Convert to map for easier comparison
			resultMap := make(map[string]bool)
			for _, link := range result {
				resultMap[link] = true
			}

			for _, expectedLink := range tt.expected {
				assert.True(t, resultMap[expectedLink], "Expected link %s should be in result", expectedLink)
			}
		})
	}
}

func TestValidateAndCompleteReferences(t *testing.T) {
	tests := []struct {
		name       string
		references []model.Reference
		chunkLinks []string
		expected   []model.Reference
	}{
		{
			name: "Exact match - keep link",
			references: []model.Reference{
				{
					Title:   "Test",
					Source:  "test",
					Link:    "https://example.com/complete",
					Content: "content",
				},
			},
			chunkLinks: []string{"https://example.com/complete"},
			expected: []model.Reference{
				{
					Title:   "Test",
					Source:  "test",
					Link:    "https://example.com/complete",
					Content: "content",
				},
			},
		},
		{
			name: "Prefix match - complete link",
			references: []model.Reference{
				{
					Title:   "Test",
					Source:  "test",
					Link:    "https://example.com/incomplete",
					Content: "content",
				},
			},
			chunkLinks: []string{"https://example.com/incomplete/full/path"},
			expected: []model.Reference{
				{
					Title:   "Test",
					Source:  "test",
					Link:    "https://example.com/incomplete/full/path",
					Content: "content",
				},
			},
		},
		{
			name: "No match - set to empty",
			references: []model.Reference{
				{
					Title:   "Test",
					Source:  "test",
					Link:    "https://example.com/notfound",
					Content: "content",
				},
			},
			chunkLinks: []string{"https://other.com/path"},
			expected: []model.Reference{
				{
					Title:   "Test",
					Source:  "test",
					Link:    "",
					Content: "content",
				},
			},
		},
		{
			name: "Playground link completion",
			references: []model.Reference{
				{
					Title:   "Test",
					Source:  "test",
					Link:    "https://azure.github.io/typespec-azure/playground/?options=%7B%22test%22%3A%22value%22%7D&c=partial",
					Content: "content",
				},
			},
			chunkLinks: []string{"https://azure.github.io/typespec-azure/playground/?options=%7B%22test%22%3A%22value%22%7D&c=partialfullcontent&e=extra"},
			expected: []model.Reference{
				{
					Title:   "Test",
					Source:  "test",
					Link:    "https://azure.github.io/typespec-azure/playground/?options=%7B%22test%22%3A%22value%22%7D&c=partialfullcontent&e=extra",
					Content: "content",
				},
			},
		},
		{
			name: "Multiple references - mixed results",
			references: []model.Reference{
				{
					Title:   "Test1",
					Source:  "test",
					Link:    "https://example.com/exact",
					Content: "content1",
				},
				{
					Title:   "Test2",
					Source:  "test",
					Link:    "https://example.com/prefix",
					Content: "content2",
				},
				{
					Title:   "Test3",
					Source:  "test",
					Link:    "https://notfound.com/link",
					Content: "content3",
				},
			},
			chunkLinks: []string{
				"https://example.com/exact",
				"https://example.com/prefix/full/path",
			},
			expected: []model.Reference{
				{
					Title:   "Test1",
					Source:  "test",
					Link:    "https://example.com/exact",
					Content: "content1",
				},
				{
					Title:   "Test2",
					Source:  "test",
					Link:    "https://example.com/prefix/full/path",
					Content: "content2",
				},
				{
					Title:   "Test3",
					Source:  "test",
					Link:    "",
					Content: "content3",
				},
			},
		},
		{
			name:       "Empty references",
			references: []model.Reference{},
			chunkLinks: []string{"https://example.com/link"},
			expected:   []model.Reference{},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			result := validateAndCompleteReferences(tt.references, tt.chunkLinks)
			assert.Equal(t, tt.expected, result)
		})
	}
}
