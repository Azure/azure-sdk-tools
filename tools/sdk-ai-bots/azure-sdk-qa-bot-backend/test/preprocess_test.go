package test

import (
	"testing"

	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/preprocess"
)

func TestPreprocessHTMLContent(t *testing.T) {
	s := preprocess.NewPreprocessService()

	tests := []struct {
		name     string
		input    string
		expected string
	}{
		{
			name:     "process encoded HTML with anchor",
			input:    `\u003cdiv\u003eVisit \u003ca href=\u0022https://example.com\u0022\u003eExample\u003c/a\u003e\u003c/div\u003e`,
			expected: `Visit <a href="https://example.com">Example</a>`,
		},
		{
			name:     "process HTML entities with tags",
			input:    `&lt;div&gt;Check &lt;a href=&quot;https://docs.com&quot;&gt;docs&lt;/a&gt;&lt;/div&gt;`,
			expected: `Check <a href="https://docs.com">docs</a>`,
		},
		{
			name:     "skip plain text without HTML",
			input:    `This is plain text without HTML`,
			expected: `This is plain text without HTML`,
		},
		{
			name:     "process complex HTML structure",
			input:    `&lt;html&gt;&lt;body&gt;&lt;p&gt;Read &lt;a href=&quot;https://docs.com&quot;&gt;documentation&lt;/a&gt; for &lt;b&gt;details&lt;/b&gt;.&lt;/p&gt;&lt;/body&gt;&lt;/html&gt;`,
			expected: `Read <a href="https://docs.com">documentation</a> for details.`,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			result := s.PreprocessHTMLContent(tt.input)
			if result != tt.expected {
				t.Errorf("PreprocessHTMLContent() = %q, want %q", result, tt.expected)
			}
		})
	}
}
