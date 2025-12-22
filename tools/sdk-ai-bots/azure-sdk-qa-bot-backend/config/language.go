package config

import (
	"strings"

	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
)

type LanguageConfig struct {
	Sources      []model.Source
	SourceFilter map[model.Source]string
}

var baseLanguageSources = []model.Source{
	model.Source_AzureSDKGuidelines,
}

var languageConfigMap = map[string]LanguageConfig{
	"python": {
		Sources: appendBaseLanguageSources(model.Source_StaticAzureSDKForPythonReviewMeeting),
		SourceFilter: map[model.Source]string{
			model.Source_AzureSDKGuidelines: "search.ismatch('python_*', 'title') or search.ismatch('general_*', 'title')",
		},
	},
	"go": {
		Sources: appendBaseLanguageSources(model.Source_AzureSDKForGo),
		SourceFilter: map[model.Source]string{
			model.Source_AzureSDKGuidelines: "search.ismatch('golang_*', 'title') or search.ismatch('general_*', 'title')",
		},
	},
	"java": {
		Sources: appendBaseLanguageSources(),
		SourceFilter: map[model.Source]string{
			model.Source_AzureSDKGuidelines: "search.ismatch('java_*', 'title') or search.ismatch('general_*', 'title')",
		},
	},
	"javascript": {
		Sources: appendBaseLanguageSources(),
		SourceFilter: map[model.Source]string{
			model.Source_AzureSDKGuidelines: "search.ismatch('typescript_*', 'title') or search.ismatch('general_*', 'title')",
		},
	},
	"dotnet": {
		Sources: appendBaseLanguageSources(),
		SourceFilter: map[model.Source]string{
			model.Source_AzureSDKGuidelines: "search.ismatch('dotnet_*', 'title') or search.ismatch('general_*', 'title')",
		},
	},
}

func appendBaseLanguageSources(additional ...model.Source) []model.Source {
	sources := make([]model.Source, 0, len(baseLanguageSources)+len(additional))
	sources = append(sources, baseLanguageSources...)
	sources = append(sources, additional...)
	return sources
}

func cloneSourceFilter(filter map[model.Source]string) map[model.Source]string {
	if len(filter) == 0 {
		return map[model.Source]string{}
	}
	cloned := make(map[model.Source]string, len(filter))
	for k, v := range filter {
		cloned[k] = v
	}
	return cloned
}

func GetLanguageSources(language string) ([]model.Source, map[model.Source]string) {
	key := normalizeLanguageKey(language)
	if cfg, ok := languageConfigMap[key]; ok {
		return append([]model.Source{}, cfg.Sources...), cloneSourceFilter(cfg.SourceFilter)
	}
	return append([]model.Source{}, baseLanguageSources...), map[model.Source]string{}
}

func normalizeLanguageKey(language string) string {
	lang := strings.TrimSpace(strings.ToLower(language))
	switch lang {
	case "", "general":
		return ""
	case "py":
		return "python"
	case "golang":
		return "go"
	case "js", "typescript", "ts":
		return "javascript"
	case "c#", "csharp", ".net":
		return "dotnet"
	default:
		return lang
	}
}
