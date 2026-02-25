package utils

import (
	"bytes"
	"fmt"
	"log"
	"os/exec"
	"regexp"
	"time"
)

// IsPipelineLink checks if a URL is an Azure DevOps pipeline link
func IsPipelineLink(url string) bool {
	pipelineRegex := regexp.MustCompile(`^https?://dev\.azure\.com/.+buildId=\d+`)
	return pipelineRegex.MatchString(url)
}

// ExtractBuildID extracts the build ID from a pipeline URL
func ExtractBuildID(url string) string {
	buildIDRegex := regexp.MustCompile(`buildId=(\d+)`)
	matches := buildIDRegex.FindStringSubmatch(url)
	if len(matches) < 2 {
		return ""
	}
	return matches[1]
}

// AnalyzePipeline calls the azsdk CLI tool to analyze a pipeline
// pipelineURL: the Azure DevOps pipeline URL or build ID
// query: optional query string for the analysis
// useAgent: whether to use AI agent for analysis (default: true)
// Returns the plain text output from the CLI tool
func AnalyzePipeline(pipelineURL string, query string, useAgent bool) (string, error) {
	startTime := time.Now()
	defer func() {
		elapsed := time.Since(startTime)
		log.Printf("AnalyzePipeline completed in %v", elapsed)
	}()

	// Extract build ID from URL if it's a full URL
	buildID := ExtractBuildID(pipelineURL)
	if buildID == "" {
		return "", fmt.Errorf("invalid pipeline URL: %s", pipelineURL)
	}
	log.Printf("Extracted build ID %s from URL: %s", buildID, pipelineURL)

	// Build the command arguments
	args := []string{"azp", "analyze", buildID}

	if query != "" {
		args = append(args, "--query", query)
	}

	if useAgent {
		args = append(args, "--agent")
	}

	log.Printf("Calling azsdk CLI: azsdk %v", args)

	// Execute the CLI command
	cmd := exec.Command("azsdk", args...)

	var stdout, stderr bytes.Buffer
	cmd.Stdout = &stdout
	cmd.Stderr = &stderr

	err := cmd.Run()
	if err != nil {
		log.Printf("Error running azsdk CLI: %v, stderr: %s", err, stderr.String())
		return "", fmt.Errorf("failed to analyze pipeline: %v, stderr: %s", err, stderr.String())
	}

	// Return the plain text output
	return stdout.String(), nil
}
