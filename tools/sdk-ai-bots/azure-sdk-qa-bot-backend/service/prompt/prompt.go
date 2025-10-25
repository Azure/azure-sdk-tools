package prompt

import (
	"encoding/json"
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

func (p *DefaultPromptParser) ParsePrompt(params map[string]string, template string) (string, error) {
	moduleRoot, err := findModuleRoot()
	if err != nil {
		panic(err)
	}
	templatePath := filepath.Join(moduleRoot, "service", "prompt", "prompt_template", template)
	content, err := os.ReadFile(templatePath)
	if err != nil {
		panic(err)
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

type IntentionPromptParser struct {
}

func (p *IntentionPromptParser) ParsePrompt(params map[string]string, template string) (string, error) {
	moduleRoot, err := findModuleRoot()
	if err != nil {
		panic(err)
	}
	templatePath := filepath.Join(moduleRoot, "service", "prompt", "prompt_template", template)
	content, err := os.ReadFile(templatePath)
	if err != nil {
		panic(err)
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

func (p *IntentionPromptParser) ParseResponse(response, template string) (*model.IntentionResult, error) {
	// Implement your response parsing logic here
	// For example, you can unmarshal the response into a struct
	var resp model.IntentionResult
	err := json.Unmarshal([]byte(response), &resp)
	if err != nil {
		return nil, err
	}
	return &resp, nil
}
