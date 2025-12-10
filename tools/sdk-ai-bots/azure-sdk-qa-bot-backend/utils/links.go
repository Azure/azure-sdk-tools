package utils

import (
	"log"
	"regexp"
	"strings"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
)

// FilterInvalidReferenceLinks checks if links in the LLM response actually appeared in the prompt chunks
// to prevent hallucination. It removes references with invalid links and logs warnings.
func FilterInvalidReferenceLinks(references []model.Reference, chunks []string) []model.Reference {
	if len(references) == 0 {
		return nil
	}

	// Extract all links from chunks
	validLinks := make(map[string]bool)
	// Regex to match URLs in the chunks (http/https links)
	urlRegex := regexp.MustCompile(`https?://[^\s)]+`)

	for _, chunk := range chunks {
		matches := urlRegex.FindAllString(chunk, -1)
		for _, match := range matches {
			// Clean up any trailing punctuation that might have been captured
			match = strings.TrimRight(match, ".,;:)")
			validLinks[match] = true
		}
	}

	// Validate each reference link
	validatedReferences := make([]model.Reference, 0)
	invalidCount := 0

	for _, ref := range references {
		if ref.Link == "" {
			// Keep references without links
			validatedReferences = append(validatedReferences, ref)
			continue
		}

		// Check if the link exists in our valid links
		if validLinks[ref.Link] {
			validatedReferences = append(validatedReferences, ref)
			log.Printf("✓ Validated reference link: %s", SanitizeForLog(ref.Link))
		} else {
			invalidCount++
			log.Printf("✗ Removed hallucinated reference link not found in chunks: %s (title: %s)",
				SanitizeForLog(ref.Link), SanitizeForLog(ref.Title))
		}
	}

	if invalidCount > 0 {
		log.Printf("removed %d hallucinated link(s) from LLM response", invalidCount)
	}

	log.Printf("All %d reference links validated successfully", len(validatedReferences))
	return validatedReferences
}
