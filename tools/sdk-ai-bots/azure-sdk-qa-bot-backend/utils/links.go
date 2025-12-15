package utils

import (
	"log"

	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
)

// FilterInvalidReferenceLinks checks if links in the LLM response actually appeared in the prompt chunks
// to prevent hallucination. It removes references with invalid links and logs warnings.
func FilterInvalidReferenceLinks(references []model.Reference, knowledges []model.Knowledge) []model.Reference {
	if len(references) == 0 {
		return nil
	}

	// Extract all links from chunks
	validLinks := make(map[string]bool)
	for _, knowledge := range knowledges {
		validLinks[knowledge.Link] = true
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
