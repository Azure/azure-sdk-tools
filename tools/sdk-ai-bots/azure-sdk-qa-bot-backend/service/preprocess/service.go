package preprocess

import (
	"bytes"
	"encoding/json"
	"fmt"
	"html"
	"io"
	"log"
	"net/http"
	"regexp"
	"strings"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
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
	// First decode HTML entities and Unicode escapes
	decoded := s.DecodeHTMLContent(input)

	log.Printf("HTML preprocessed: %s", decoded)
	return decoded
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

func (s *PreprocessService) ExtractAdditionalInfo(input string) string {
	// Extract image links from the input
	imageLinks := extractImageLinks(input)

	// Create the request payload
	req := PreprocessRequest{
		Text:   input,
		Images: imageLinks,
	}

	// Serialize the request to JSON
	reqBody, err := json.Marshal(req)
	if err != nil {
		log.Printf("Failed to marshal preprocess request: %v", err)
		return input // Return original input in case of error
	}

	// Create a new HTTP request
	preprocessURL := "http://localhost:3000/api/prompts/preprocess"
	httpReq, err := http.NewRequest("POST", preprocessURL, bytes.NewReader(reqBody))
	if err != nil {
		log.Printf("Failed to create preprocess request: %v", err)
		return input
	}

	// Set headers
	httpReq.Header.Set("Content-Type", "application/json")
	httpReq.Header.Set("x-api-key", config.PREPROCESS_ENV_LOCAL_KEY)

	// Send the request
	client := &http.Client{}
	resp, err := client.Do(httpReq)
	if err != nil {
		log.Printf("Failed to send preprocess request: %v", err)
		return input
	}
	defer func() {
		if err := resp.Body.Close(); err != nil {
			log.Printf("Failed to close response body: %v", err)
		}
	}()

	// Check the response status
	if resp.StatusCode != http.StatusOK {
		log.Printf("Preprocess API returned non-OK status: %d", resp.StatusCode)
		return input
	}

	// Read and parse the response
	respBody, err := io.ReadAll(resp.Body)
	if err != nil {
		log.Printf("Failed to read preprocess response body: %v", err)
		return input
	}

	var preprocessResp PreprocessResponse
	err = json.Unmarshal(respBody, &preprocessResp)
	if err != nil {
		log.Printf("Failed to unmarshal preprocess response: %v", err)
		return input
	}

	// If there are warnings, log them
	if len(preprocessResp.Warnings) > 0 {
		log.Printf("Preprocess warnings: %v", preprocessResp.Warnings)
	}

	// Return the preprocessed text
	return preprocessResp.Text
}

// Common image file extensions
var imageExtensions = []string{
	"png", "jpg", "jpeg", "gif", "bmp", "webp", "svg", "tiff", "ico", "heif", "heic",
}

// extractImageLinks parses the input text and extracts URLs that appear to be image links
func extractImageLinks(input string) []string {
	// Regular expression to catch common URL patterns
	// This matches URLs starting with http/https and having common image extensions
	urlPattern := regexp.MustCompile(`https?://[^\s"'<>)]+\.(png|jpg|jpeg|gif|bmp|webp|svg|tiff|ico|heif|heic)`)

	// Find all matches in the input text
	matches := urlPattern.FindAllString(input, -1)

	// Remove duplicates
	uniqueLinks := make(map[string]bool)
	var result []string

	for _, match := range matches {
		// Clean up the URL if needed (remove trailing punctuation, etc.)
		cleanUrl := strings.TrimRight(match, ".,;:!?")

		if !uniqueLinks[cleanUrl] {
			uniqueLinks[cleanUrl] = true
			result = append(result, cleanUrl)
		}
	}

	// Look for image URLs with query parameters that might not have file extensions
	// like those from CDNs or image hosting services
	potentialImageServices := []string{
		"imgur.com", "i.imgur.com",
		"cloudinary.com",
		"res.cloudinary.com",
		"images.unsplash.com",
		"img.youtube.com",
		"raw.githubusercontent.com",
	}

	// More complex pattern for image URLs that might not have file extensions
	complexUrlPattern := regexp.MustCompile(`https?://(?:www\.)?([^\s"'<>)]+)`)
	allMatches := complexUrlPattern.FindAllStringSubmatch(input, -1)

	for _, match := range allMatches {
		if len(match) > 1 {
			url := match[0]
			domain := match[1]

			// Check if the domain is in our list of image hosting services
			for _, service := range potentialImageServices {
				if strings.Contains(domain, service) && !uniqueLinks[url] {
					uniqueLinks[url] = true
					result = append(result, url)
					break
				}
			}
		}
	}

	return result
}
