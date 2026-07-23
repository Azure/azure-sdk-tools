package utils

import (
	"bytes"
	"context"
	"fmt"
	"log"
	"os/exec"
	"regexp"
	"time"
)

// AnalyzePipelineTimeout is the maximum time allowed for pipeline analysis
const AnalyzePipelineTimeout = 30 * time.Second

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
// If the command times out or fails with agent mode, automatically retries without agent.
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
	log.Printf("Extracted build ID %s from URL: %s", SanitizeForLog(buildID), SanitizeForLog(pipelineURL))

	// Try with agent mode first if requested
	if useAgent {
		ctx, cancel := context.WithTimeout(context.Background(), AnalyzePipelineTimeout)
		result, err := executeAnalyzePipelineCommand(ctx, buildID, query, true)
		cancel()
		if err == nil {
			return result, nil
		}
		log.Printf("Agent mode failed: %v, falling back to non-agent mode", err)
	}

	// Fallback to non-agent mode with a fresh timeout
	ctx, cancel := context.WithTimeout(context.Background(), AnalyzePipelineTimeout)
	defer cancel()
	return executeAnalyzePipelineCommand(ctx, buildID, query, false)
}

// executeAnalyzePipelineCommand executes the azsdk CLI analyze command with the given context
func executeAnalyzePipelineCommand(ctx context.Context, buildID string, query string, useAgent bool) (string, error) {
	// Build the command arguments
	args := []string{"azp", "analyze", buildID}

	if query != "" {
		args = append(args, "--query", query)
	}

	if useAgent {
		args = append(args, "--agent")
	}

	log.Printf("Calling azsdk CLI: azsdk %s", SanitizeForLog(fmt.Sprintf("%v", args)))

	// Execute the CLI command with timeout context
	cmd := exec.CommandContext(ctx, "azsdk", args...)

	var stdout, stderr bytes.Buffer
	cmd.Stdout = &stdout
	cmd.Stderr = &stderr

	err := cmd.Run()
	if err != nil {
		if ctx.Err() == context.DeadlineExceeded {
			log.Printf("azsdk CLI timed out after %v", AnalyzePipelineTimeout)
			return "", fmt.Errorf("failed to analyze pipeline after %v: %w", AnalyzePipelineTimeout, ctx.Err())
		}
		log.Printf("Error running azsdk CLI: %v, stderr: %s", err, stderr.String())
		return "", fmt.Errorf("failed to analyze pipeline: %v, stderr: %s", err, stderr.String())
	}

	output := stdout.String()
	log.Printf("azsdk CLI output: %s", SanitizeForLog(output))
	return output, nil
}
