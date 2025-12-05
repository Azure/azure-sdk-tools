package preprocess

import (
	"fmt"
	"html"
	"log"
	"net/url"
	"regexp"
	"strings"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/utils"
)

type PreprocessService struct{}

func NewPreprocessService() *PreprocessService {
	return &PreprocessService{}
}

// DecodeHTMLContent decodes HTML-encoded content including Unicode escape sequences
func (s *PreprocessService) DecodeHTMLContent(input string) string {
	// First decode HTML entities like &nbsp;, &lt;, etc.
	decoded := html.UnescapeString(input)

	// Then decode Unicode escape sequences like \u003c, \u0026, etc.
	decoded = strings.ReplaceAll(decoded, "\\u003c", "<")
	decoded = strings.ReplaceAll(decoded, "\\u003e", ">")
	decoded = strings.ReplaceAll(decoded, "\\u0026", "&")
	decoded = strings.ReplaceAll(decoded, "\\u0027", "'")
	decoded = strings.ReplaceAll(decoded, "\\u0022", "\"")
	decoded = strings.ReplaceAll(decoded, "\\u002f", "/")
	decoded = strings.ReplaceAll(decoded, "\\u003d", "=")
	decoded = strings.ReplaceAll(decoded, "\\u0020", " ")
	decoded = strings.ReplaceAll(decoded, "\\u00a0", " ") // Non-breaking space to regular space
	decoded = strings.ReplaceAll(decoded, "\\u000a", "\n")
	decoded = strings.ReplaceAll(decoded, "\\u000d", "\r")
	decoded = strings.ReplaceAll(decoded, "\\u0009", "\t")

	// Handle other common Unicode escapes using a more general approach
	re := regexp.MustCompile(`\\u([0-9a-fA-F]{4})`)
	decoded = re.ReplaceAllStringFunc(decoded, func(match string) string {
		var r rune
		if _, err := fmt.Sscanf(match, "\\u%04x", &r); err == nil {
			return string(r)
		}
		return match // Return original if parsing fails
	})

	// Decode URL encoding (e.g., %20 -> space, %E2%80%A6 -> …)
	if decodedURL, err := url.QueryUnescape(decoded); err == nil {
		decoded = decodedURL
	}

	return decoded
}

// CleanHTMLTags removes HTML tags from the content while preserving the text
func (s *PreprocessService) CleanHTMLTags(input string) string {
	// Remove HTML tags but keep the text content
	re := regexp.MustCompile(`<[^>]*>`)
	cleaned := re.ReplaceAllString(input, "")

	// Clean up extra whitespace and newlines
	cleaned = regexp.MustCompile(`\s+`).ReplaceAllString(cleaned, " ")
	cleaned = strings.TrimSpace(cleaned)

	return cleaned
}

// PreprocessHTMLContent handles HTML-encoded content by decoding and cleaning it
func (s *PreprocessService) PreprocessHTMLContent(input string) string {
	// Check if content contains HTML entities or tags
	if !strings.Contains(input, "\\u003c") && !strings.Contains(input, "&lt;") &&
		!strings.Contains(input, "<") && !strings.Contains(input, "&amp;") &&
		!strings.Contains(input, "\\u0026") {
		// No HTML content detected, return original
		return input
	}

	log.Printf("Detected HTML content, preprocessing...")

	// First decode HTML entities and Unicode escapes
	decoded := s.DecodeHTMLContent(input)

	// Then remove HTML tags while preserving the text content
	cleaned := s.CleanHTMLTags(decoded)

	log.Printf("HTML preprocessed: %s", utils.SanitizeForLog(cleaned))
	return cleaned
}

type PreprocessRequest struct {
	Text   string   `json:"text"`
	Images []string `json:"images,omitempty"`
}

type PreprocessWarning struct {
	ID      string `json:"id"`
	Warning string `json:"warning"`
}

type PreprocessResponse struct {
	Text     string              `json:"text"`
	Warnings []PreprocessWarning `json:"warnings,omitempty"`
}

func (s *PreprocessService) PreprocessInput(tenantID model.TenantID, input string) string {
	// lower case
	input = strings.ToLower(input)
	// replace keyword
	for k, v := range model.CommonKeywordReplaceMap {
		input = strings.ReplaceAll(input, fmt.Sprintf(" %s ", k), fmt.Sprintf(" %s ", v))
	}
	if tenantConfig, ok := config.GetTenantConfig(tenantID); ok && tenantConfig.KeywordReplaceMap != nil {
		for k, v := range tenantConfig.KeywordReplaceMap {
			input = strings.ReplaceAll(input, fmt.Sprintf(" %s ", k), fmt.Sprintf(" %s ", v))
		}
	}
	return input
}
