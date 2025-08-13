package prompt

import (
	"encoding/json"
	"os"
	"path/filepath"
	"strings"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
)

type DefaultPromptParser struct {
}

func replacePlaceholder(prompt, placeholder, value string) string {
	// Replace the placeholder with the actual value
	return strings.ReplaceAll(prompt, placeholder, value)
}

func (p *DefaultPromptParser) ParsePrompt(params map[string]string, template string) (string, error) {
	templatePath := filepath.Join("service", "prompt", "prompt_template", template)
	absPath, err := filepath.Abs(templatePath)
	if err != nil {
		panic(err)
	}
	content, err := os.ReadFile(absPath)
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

type IntensionPromptParser struct {
}

func (p *IntensionPromptParser) ParsePrompt(params map[string]string, template string) (string, error) {
	templatePath := filepath.Join("service", "prompt", "prompt_template", template)
	absPath, err := filepath.Abs(templatePath)
	if err != nil {
		panic(err)
	}
	content, err := os.ReadFile(absPath)
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
