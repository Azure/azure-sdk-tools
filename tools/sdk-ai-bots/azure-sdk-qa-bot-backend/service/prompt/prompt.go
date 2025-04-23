package prompt

import (
	"encoding/json"
	"os"
	"path/filepath"
	"strings"

	"github.com/copilot-extensions/rag-extension/model"
)

// read prompt_template/answer_question.md, replace {{context}} with question and context
func BuildPrompt(context string, templateFile string) (string, error) {
	switch templateFile {
	case "default.md":
		parser := &DefaultPromptParser{}
		prompt, err := parser.ParsePrompt(context, templateFile)
		if err != nil {
			return "", err
		}
		return prompt, nil
	default:
		return "", nil
	}
}

func ParseAnswer(response string, templateFile string) (*model.CompletionResp, error) {
	switch templateFile {
	case "default.md":
		parser := &DefaultPromptParser{}
		result, err := parser.ParseResponse(response, templateFile)
		if err != nil {
			return nil, err
		}
		return result, nil
	default:
		return nil, nil
	}
}

type PromptParser interface {
	ParsePrompt(context, template string) (string, error)
	ParseResponse(response string, template string) (*model.CompletionResp, error)
}

type DefaultPromptParser struct {
}

func replacePlaceholder(prompt, placeholder, value string) string {
	// Replace the placeholder with the actual value
	return strings.ReplaceAll(prompt, placeholder, value)
}

func (p *DefaultPromptParser) ParsePrompt(context, template string) (string, error) {
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
	prompt = replacePlaceholder(prompt, "{{context}}", context)
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
		Answer:     resp.Answer,
		HasResult:  resp.HasResult,
		References: resp.References,
	}, nil
}
