package prompt

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"runtime"
	"strings"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
)

type DefaultPromptParser struct {
}

// findModuleRoot finds the module root by looking for go.mod file
func findModuleRoot() (string, error) {
	// Start from the current file's directory
	_, filename, _, _ := runtime.Caller(0)
	dir := filepath.Dir(filename)

	for {
		goModPath := filepath.Join(dir, "go.mod")
		if _, err := os.Stat(goModPath); err == nil {
			return dir, nil
		}

		parent := filepath.Dir(dir)
		if parent == dir {
			// Reached root directory
			break
		}
		dir = parent
	}

	// Fallback to using current working directory if go.mod not found
	return os.Getwd()
}

func replacePlaceholder(prompt, placeholder, value string) string {
	// Replace the placeholder with the actual value
	return strings.ReplaceAll(prompt, placeholder, value)
}

// validateTemplateName validates that the template name is safe to use
// It prevents path traversal attacks by ensuring:
// 1. No path separators (/ or \)
// 2. No parent directory references (..)
// 3. Only allows filename-safe characters
func validateTemplateName(template string) error {
	if template == "" {
		return fmt.Errorf("template name cannot be empty")
	}

	// Check for path separators
	if strings.Contains(template, "/") || strings.Contains(template, "\\") {
		return fmt.Errorf("template name cannot contain path separators")
	}

	// Check for parent directory references
	if strings.Contains(template, "..") {
		return fmt.Errorf("template name cannot contain parent directory references")
	}

	// Additional validation: only allow safe characters for filenames
	// Allow alphanumeric, underscore, hyphen, and dot
	for _, char := range template {
		if !((char >= 'a' && char <= 'z') ||
			(char >= 'A' && char <= 'Z') ||
			(char >= '0' && char <= '9') ||
			char == '_' || char == '-' || char == '.') {
			return fmt.Errorf("template name contains invalid characters")
		}
	}

	return nil
}

func (p *DefaultPromptParser) ParsePrompt(params map[string]string, template string) (string, error) {
	// Validate template name to prevent path injection
	if err := validateTemplateName(template); err != nil {
		return "", fmt.Errorf("invalid template name: %w", err)
	}

	moduleRoot, err := findModuleRoot()
	if err != nil {
		return "", fmt.Errorf("failed to find module root: %w", err)
	}

	templatePath := filepath.Join(moduleRoot, "service", "prompt", "prompt_template", template)

	// Verify the resolved path is still within the template directory (defense in depth)
	templateDir := filepath.Join(moduleRoot, "service", "prompt", "prompt_template")
	absTemplatePath, err := filepath.Abs(templatePath)
	if err != nil {
		return "", fmt.Errorf("failed to resolve template path: %w", err)
	}
	absTemplateDir, err := filepath.Abs(templateDir)
	if err != nil {
		return "", fmt.Errorf("failed to resolve template directory: %w", err)
	}
	// Ensure the resolved path starts with the template directory followed by a separator
	if !strings.HasPrefix(absTemplatePath, absTemplateDir+string(filepath.Separator)) {
		return "", fmt.Errorf("template path is outside of allowed directory")
	}

	content, err := os.ReadFile(templatePath)
	if err != nil {
		return "", fmt.Errorf("failed to read template file: %w", err)
	}
	prompt := string(content)
	if params == nil {
		return prompt, nil
	}
	for k, v := range params {
		prompt = replacePlaceholder(prompt, "{{"+k+"}}", v)
	}
	return prompt, nil
}

func (p *DefaultPromptParser) ParseResponse(response, template string) (*model.CompletionResp, error) {
	// Implement your response parsing logic here
	// For example, you can unmarshal the response into a struct
	var resp model.DefaultPromptResponse
	err := json.Unmarshal([]byte(response), &resp)
	if err != nil {
		return nil, err
	}
	return &model.CompletionResp{
		Answer:            resp.Answer,
		HasResult:         resp.HasResult,
		References:        resp.References,
		ReasoningProgress: &resp.ReasoningProgress,
	}, nil
}

type IntensionPromptParser struct {
}

func (p *IntensionPromptParser) ParsePrompt(params map[string]string, template string) (string, error) {
	// Validate template name to prevent path injection
	if err := validateTemplateName(template); err != nil {
		return "", fmt.Errorf("invalid template name: %w", err)
	}

	moduleRoot, err := findModuleRoot()
	if err != nil {
		return "", fmt.Errorf("failed to find module root: %w", err)
	}

	templatePath := filepath.Join(moduleRoot, "service", "prompt", "prompt_template", template)

	// Verify the resolved path is still within the template directory (defense in depth)
	templateDir := filepath.Join(moduleRoot, "service", "prompt", "prompt_template")
	absTemplatePath, err := filepath.Abs(templatePath)
	if err != nil {
		return "", fmt.Errorf("failed to resolve template path: %w", err)
	}
	absTemplateDir, err := filepath.Abs(templateDir)
	if err != nil {
		return "", fmt.Errorf("failed to resolve template directory: %w", err)
	}
	// Ensure the resolved path starts with the template directory followed by a separator
	if !strings.HasPrefix(absTemplatePath, absTemplateDir+string(filepath.Separator)) {
		return "", fmt.Errorf("template path is outside of allowed directory")
	}

	content, err := os.ReadFile(templatePath)
	if err != nil {
		return "", fmt.Errorf("failed to read template file: %w", err)
	}
	prompt := string(content)
	if params == nil {
		return prompt, nil
	}
	for k, v := range params {
		prompt = replacePlaceholder(prompt, "{{"+k+"}}", v)
	}
	return prompt, nil
}

func (p *IntensionPromptParser) ParseResponse(response, template string) (*model.IntensionResult, error) {
	// Implement your response parsing logic here
	// For example, you can unmarshal the response into a struct
	var resp model.IntensionResult
	err := json.Unmarshal([]byte(response), &resp)
	if err != nil {
		return nil, err
	}
	return &resp, nil
}
