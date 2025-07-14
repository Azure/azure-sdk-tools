package main

import (
	"context"
	"fmt"
	"io/fs"
	"os"
	"path/filepath"
	"regexp"

	"strings"

	"github.com/sourcegraph/conc/pool"

	"github.com/Azure/azure-sdk-for-go/sdk/azidentity"
	"github.com/openai/openai-go"
	"github.com/openai/openai-go/azure"
)

var MAX_CONCURRENT_COUNT = 10
var IGNORED_SPECS = []string{"special-words"}

type ConvertResult struct {
	count int
	error error
}

func main() {
	p := pool.NewWithResults[ConvertResult]()
	p.Go(func() ConvertResult {
		return convertSpectorCasesToMarkdown("docs/typespec/packages/http-specs/specs", "docs/typespec/packages/http-specs/specs/generated")
	})
	p.Go(func() ConvertResult {
		return convertSpectorCasesToMarkdown("docs/typespec-azure/packages/azure-http-specs/specs", "docs/typespec-azure/packages/azure-http-specs/specs/generated")
	})

	results := p.Wait()
	for _, result := range results {
		if result.error != nil {
			fmt.Printf("Error processing specs: %v\n", result.error)
			return
		}
	}
}

func convertSpectorCasesToMarkdown(root string, targetRoot string) ConvertResult {
	fmt.Printf("Contents of folder: %s\n", root)
	specs, paths, err := getSpecs(root)
	if err != nil {
		fmt.Printf("Error getting specs: %v\n", err)
		return ConvertResult{
			count: 0,
			error: err,
		}
	}

	p := pool.NewWithResults[error]().WithMaxGoroutines(MAX_CONCURRENT_COUNT)

	for i, spc := range specs {
		path := paths[i]
		spec := spc
		p.Go(func() error {
			dir := filepath.Dir(path)
			relativeDir, err := filepath.Rel(root, dir)
			if err != nil {
				fmt.Printf("Error getting relative path: %v\n", err)
				return err
			}
			fmt.Printf("Processing spec path: %s\n", relativeDir)
			scenarios := getScenarios("@scenario\n", spec)
			doc, error := createMarkdownDoc(scenarios, spec)
			if error != nil {
				fmt.Printf("Error creating markdown document: %v\n", error)
				return error
			}
			// Create target directory if it doesn't exist
			targetDir := filepath.Join(targetRoot, relativeDir)
			targetPath := getTargetPath(path, targetDir)
			error = save(doc, targetDir, targetPath)
			if error != nil {
				fmt.Printf("Error saving markdown document: %v\n", error)
				return error
			}
			return nil
		})
	}

	errs := p.Wait()
	for _, err := range errs {
		if err != nil {
			fmt.Printf("Error processing spec: %v\n", err)
			return ConvertResult{
				count: 0,
				error: err,
			}
		}
	}

	return ConvertResult{
		count: len(specs),
		error: nil,
	}
}

func getTargetPath(sourcePath string, targetDir string) string {
	originalFilename := filepath.Base(sourcePath)
	markdownFilename := strings.TrimSuffix(originalFilename, ".tsp") + ".md"
	targetPath := filepath.Join(targetDir, markdownFilename)
	return targetPath
}

func save(doc string, targetDir string, targetPath string) error {
	if err := os.MkdirAll(targetDir, 0755); err != nil {
		fmt.Printf("Error creating directory %s: %v\n", targetDir, err)
		return err
	}

	// Write the markdown content to file
	if err := os.WriteFile(targetPath, []byte(doc), 0644); err != nil {
		fmt.Printf("Error writing file %s: %v\n", targetPath, err)
		return err
	}
	fmt.Printf("Saved markdown to: %s\n", targetPath)
	return nil
}

func createMarkdownDoc(scenarios []string, spec string) (string, error) {
	title, err := getChatCompletions(
		"Get a title from the @scenarioService or @doc that is closest to @scenarioService.\n" +
			"do not get title from other @doc. @doc for @scenarioService maybe not existed\n" +
			"the title will be used as markdown heading, so should be one line.\n" +
			"the reply should only contains the title, no extra characters\n" +
			"the below is the typespec content\n\n" +
			spec)
	if err != nil {
		fmt.Printf("ERROR: %s\n", err)
		return "", err
	}

	doc := "# Usages for " + title + "\n\n"

	for _, scenario := range scenarios {
		scenarioMarkdownSection, error := createScenarioSection(scenario)
		if error != nil {
			return "", error
		}
		doc += scenarioMarkdownSection + "\n"
	}

	removed, error := removeSpectorContent(spec)
	if error != nil {
		return "", error
	}
	doc += "## Full Sample: \n" +
		"``` typespec\n" +
		removed + "\n" +
		"```\n"
	return doc, nil
}

// Default API version to use for Azure OpenAI
const DefaultAPIVersion = "2024-08-01-preview"

// CreateOpenAIClientWithToken creates an OpenAI client with Azure AD token authentication
func CreateOpenAIClientWithToken(endpoint string, apiVersion string) (*openai.Client, error) {
	if apiVersion == "" {
		apiVersion = DefaultAPIVersion
	}

	tokenCredential, err := azidentity.NewDefaultAzureCredential(nil)
	if err != nil {
		return nil, fmt.Errorf("failed to create default Azure credential: %w", err)
	}

	client := openai.NewClient(
		azure.WithEndpoint(endpoint, apiVersion),
		azure.WithTokenCredential(tokenCredential),
	)

	return &client, nil
}

func getChatCompletions(question string) (string, error) {
	model := os.Getenv("AOAI_CHAT_COMPLETIONS_MODEL")
	endpoint := os.Getenv("AOAI_CHAT_COMPLETIONS_ENDPOINT")

	client, err := CreateOpenAIClientWithToken(endpoint, "")
	if err != nil {
		fmt.Fprintf(os.Stderr, "ERROR: %s\n", err)
		return "", err
	}

	resp, err := client.Chat.Completions.New(context.TODO(), openai.ChatCompletionNewParams{
		Model: openai.ChatModel(model),
		Messages: []openai.ChatCompletionMessageParamUnion{
			// You set the tone and rules of the conversation with a prompt as the system role.
			{
				OfSystem: &openai.ChatCompletionSystemMessageParam{
					Content: openai.ChatCompletionSystemMessageParamContentUnion{
						OfString: openai.String("You are a helpful assistant. And you are a great TypeSpec expert."),
					},
				},
			},
			// The user asks a question
			{
				OfUser: &openai.ChatCompletionUserMessageParam{
					Content: openai.ChatCompletionUserMessageParamContentUnion{
						OfString: openai.String(question),
					},
				},
			},
		},
	})

	if err != nil {
		fmt.Printf("ERROR: %s\n", err)
		return "", nil
	}

	gotReply := false

	for _, choice := range resp.Choices {
		gotReply = true

		if choice.Message.Content != "" {
			return choice.Message.Content, nil
		}
	}

	if gotReply {
		fmt.Fprintf(os.Stderr, "Got chat completions reply\n")
	}
	return "", err
}

func getHeading(scenario string) (string, error) {
	heading, err := getChatCompletions(
		"Get a title from the @scenarioDoc or @doc.\n" +
			"the title will be used as markdown heading, so should be one line.\n" +
			"If the first line is good, just copy the first line in @scenarioDoc or @doc.\n" +
			"do not make the 'expected' test result in the title.\n" +
			"the reply should only contains the title, no extra characters\n" +
			"the below is the typespec content\n\n" +
			scenario)
	if err != nil {
		fmt.Printf("ERROR: %s\n", err)
		return "no-title", err
	}
	return heading, err
}

func getDescription(scenario string) (string, error) {
	description, err := getChatCompletions(
		"Get (do not modify words) a description from the @scenarioDoc or @doc.\n" +
			"the description will be used in a markdown description, so should be one or more paragraphs.\n" +
			"If the first line is good, just copy the first line in @scenarioDoc or @doc.\n" +
			"do not make the 'expected' test result in the @scenarioDoc or @doc.\n" +
			"must contains the clarify or details other than the 'expected' test result\n" +
			"the reply should only contains the description, no extra characters\n" +
			"the below is the typespec content\n\n" +
			scenario)
	if err != nil {
		fmt.Printf("ERROR: %s\n", err)
		return "no-description", err
	}
	return description, err
}

func createScenarioSection(scenario string) (string, error) {
	heading, err := getHeading(scenario)
	if err != nil {
		fmt.Printf("ERROR: %s\n", err)
		return "", err
	}

	description, err := getDescription(scenario)
	if err != nil {
		fmt.Printf("ERROR: %s\n", err)
		return "", err
	}

	scenario, err = removeSpectorContent(scenario)
	if description == heading {
		description = ""
	}
	section :=
		"## Scenario: " + heading + "\n" +
			description + "\n" +
			"``` typespec\n" +
			scenario + "\n" +
			"```\n"
	return section, err
}

func getSpecs(root string) ([]string, []string, error) {
	var specs []string
	var paths []string
	err := filepath.WalkDir(root, func(path string, d fs.DirEntry, err error) error {
		if err != nil {
			fmt.Printf("Error accessing path %s: %w\n", path, err)
			return err
		}
		if filepath.Ext(path) != ".tsp" {
			return nil
		}

		for _, ignored := range IGNORED_SPECS {
			if strings.Contains(path, ignored) {
				fmt.Printf("Ignoring spec path: %s\n", path)
				return nil
			}
		}

		fmt.Printf("Found spec path: %s\n", path)
		paths = append(paths, path)
		content, err := os.ReadFile(path)
		if err != nil {
			fmt.Printf("Failed to read file %s: %w\n", path, err)
			return err
		}
		spec := string(content)
		specs = append(specs, spec)
		return nil
	})
	return specs, paths, err
}

func findIndexes(searchStr string, spec string) []int {
	findPreviousNewLine := func(end int) int {
		for ind := end - 1; ind >= 1; ind-- {
			if spec[ind] == '\n' && spec[ind-1] == '\n' {
				return ind + 1 // Return the index after the newline character
			}
		}
		return end
	}
	var indexes []int
	for i := 0; ; {
		pos := strings.Index(spec[i:], searchStr)
		if pos == -1 {
			break
		}
		index := i + pos
		blockStartIndex := findPreviousNewLine(index)
		indexes = append(indexes, blockStartIndex)
		i = index + len(searchStr)
	}
	return indexes
}

func getScenarios(searchStr string, spec string) []string {
	indexes := findIndexes(searchStr, spec)
	var scenarios []string
	for i := range indexes {
		startIndex := indexes[i]
		var endIndex int
		if i < len(indexes)-1 {
			endIndex = indexes[i+1]
		} else {
			endIndex = len(spec)
		}
		scenarioContent := spec[startIndex:endIndex]
		scenarios = append(scenarios, scenarioContent)
	}
	return scenarios
}

func removeSpectorContent(content string) (string, error) {
	reForScenarioDoc := regexp.MustCompile(`@scenarioDoc\("(?s:.*?)"\)\n`)
	reForScenarioDoc2 := regexp.MustCompile(`@scenarioDoc\("""(?s:.*?)"""\)\n`)
	reForScenarioService := regexp.MustCompile(`@scenarioService\("(?s:.*?)"\)\n`)
	reForScenarioService2 := regexp.MustCompile(`@scenarioService\(\n(?s:.*?)\n\)\n`)
	reForScenario := regexp.MustCompile(`@scenario`)
	reForImportSpector := regexp.MustCompile(`import "@typespec/spector";\n`)
	reForUsingSpector := regexp.MustCompile(`using Spector;\n`)

	cleanedContent := content
	cleanedContent = reForScenarioDoc.ReplaceAllString(cleanedContent, "")
	cleanedContent = reForScenarioDoc2.ReplaceAllString(cleanedContent, "")
	cleanedContent = reForScenarioService.ReplaceAllString(cleanedContent, "")
	cleanedContent = reForScenarioService2.ReplaceAllString(cleanedContent, "")
	cleanedContent = reForScenario.ReplaceAllString(cleanedContent, "")
	cleanedContent = reForImportSpector.ReplaceAllString(cleanedContent, "")
	cleanedContent = reForUsingSpector.ReplaceAllString(cleanedContent, "")

	lines := strings.Split(cleanedContent, "\n")
	cleanedLines := []string{}
	for _, line := range lines {
		if !strings.Contains(line, "#suppress ") && !strings.Contains(line, "missing-scenario") {
			cleanedLines = append(cleanedLines, line)
		}
	}
	cleanedContent = strings.Join(cleanedLines, "\n")
	return cleanedContent, nil
}
