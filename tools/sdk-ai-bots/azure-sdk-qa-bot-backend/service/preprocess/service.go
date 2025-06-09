package preprocess

import (
	"bytes"
	"encoding/json"
	"fmt"
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

func (s *PreprocessService) PreprocessInput(input string) string {
	// lower case
	input = strings.ToLower(input)
	// replace keyword
	for k, v := range model.KeywordReplaceMap {
		input = strings.ReplaceAll(input, fmt.Sprintf(" %s ", k), fmt.Sprintf(" %s ", v))
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
	defer resp.Body.Close()

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
