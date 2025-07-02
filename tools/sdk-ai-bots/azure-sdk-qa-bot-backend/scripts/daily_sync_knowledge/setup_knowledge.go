package main

import (
	"bufio"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"regexp"
	"sort"
	"strings"
	"time"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/storage"
)

// Defines the structure for source directories
type Source struct {
	path              string
	folder            string
	name              string
	fileNameLowerCase bool
}

// Defines the structure for processed markdown content
type ProcessedMarkdown struct {
	Title    string
	Filename string
	Content  string
}

func main() {
	// Process both repositories
	sources := []Source{
		{
			path:              "docs/typespec/website/src/content/docs/docs",
			folder:            "typespec_docs",
			name:              "TypeSpec",
			fileNameLowerCase: true,
		},
		{
			path:              "docs/typespec-azure/website/src/content/docs/docs",
			folder:            "typespec_azure_docs",
			name:              "TypeSpec Azure",
			fileNameLowerCase: true,
		},
		{
			path:   "docs/azure-rest-api-specs.wiki",
			folder: "azure_rest_api_specs_wiki",
		},
		{
			path:   "docs/azure-sdk-for-python.wiki",
			folder: "azure_sdk_for_python_wiki",
		},
		{
			path:   "docs/azure-sdk-for-python/doc",
			folder: "azure_sdk_for_python_docs",
		},
		{
			path:   "docs/api-guidelines",
			folder: "azure_api_guidelines",
		},
		{
			path:   "docs/resource-provider-contract",
			folder: "azure_resource_manager_rpc",
		},
		{
			path:   "docs/azure-sdk-docs-eng.ms",
			folder: "azure-sdk-docs-eng",
		},
		{
			path:   "docs/azure-sdk",
			folder: "azure-sdk-guidelines",
		},
	}

	// Process each source directory
	for _, source := range sources {
		sourceDir := source.path
		targetDir := "temp_docs/" + source.folder
		// Create target directory
		if err := os.MkdirAll(targetDir, 0755); err != nil {
			fmt.Printf("Error creating target directory: %v\n", err)
			return
		}
		err := createReleaseNotesIndex(source, targetDir)
		if err != nil {
			fmt.Printf("Error creating release notes index: %v\n", err)
			return
		}
		// Walk through all markdown files in source directory
		err = filepath.Walk(sourceDir, func(path string, info os.FileInfo, err error) error {
			if err != nil {
				return err
			}
			if info.IsDir() {
				return nil
			}

			if !strings.HasSuffix(path, ".md") && !strings.HasSuffix(path, ".mdx") {
				return nil
			}

			relPath, err := filepath.Rel(source.path, path)
			if err != nil {
				return fmt.Errorf("error getting relative path: %w", err)
			}
			if strings.HasPrefix(relPath, "reference") {
				return nil
			}
			if strings.HasPrefix(info.Name(), "release-") {
				return nil
			}

			if err := processMarkdownFile(path, source, targetDir); err != nil {
				fmt.Printf("Error processing file %s: %v\n", path, err)
			}
			return nil
		})

		if err != nil {
			fmt.Printf("Error walking through directory: %v\n", err)
			return
		}
	}
	// Upload all files to blob storage
	storageService, err := storage.NewStorageService()
	if err != nil {
		fmt.Printf("Error creating storage service: %v\n", err)
		return
	}
	currentFiles := []string{}
	for _, source := range sources {
		targetDir := "temp_docs/" + source.folder
		err := filepath.Walk(targetDir, func(path string, info os.FileInfo, err error) error {
			if err != nil {
				return err
			}

			if info.IsDir() {
				return nil
			}
			fileName := info.Name()
			if source.fileNameLowerCase {
				fileName = strings.ToLower(strings.ReplaceAll(info.Name(), " ", "-"))
			}
			blobPath := filepath.Join(source.folder, fileName)
			currentFiles = append(currentFiles, blobPath)
			// read the content of the target file
			content, err := os.ReadFile(path)
			if err != nil {
				return fmt.Errorf("error reading target file: %w", err)
			}
			if err := storageService.PutBlob(config.STORAGE_KNOWLEDGE_CONTAINER, blobPath, content); err != nil {
				fmt.Printf("Error uploading file %s: %v\n", path, err)
			}
			fmt.Printf("Uploaded %s to blob storage\n", blobPath)
			return nil
		})
		if err != nil {
			fmt.Printf("Error walking through directory: %v\n", err)
			return
		}
	}
	// Delete expired blobs
	err = deleteExpiredBlobs(currentFiles)
	if err != nil {
		fmt.Printf("Error deleting expired blobs: %v\n", err)
		return
	}
	// Remove the temp_docs directory
	if err := os.RemoveAll("temp_docs"); err != nil {
		fmt.Printf("Error removing temp_docs directory: %v\n", err)
		return
	}
	// Remove the docs directory
	if err := os.RemoveAll("docs"); err != nil {
		fmt.Printf("Error removing temp_docs directory: %v\n", err)
		return
	}
	fmt.Println("Processing completed successfully.")
}

func processMarkdownFile(filePath string, source Source, targetDir string) error {
	// Read source file
	file, err := os.Open(filePath)
	if err != nil {
		return fmt.Errorf("error opening file: %w", err)
	}
	defer file.Close()

	// Generate new filename: replace path separators with underscores
	relPath, err := filepath.Rel(source.path, filePath)
	if err != nil {
		return fmt.Errorf("error getting relative path: %w", err)
	}
	// Replace path separators with underscores to create unique filename
	newFileName := strings.ReplaceAll(relPath, string(os.PathSeparator), "#")

	// Process file content
	processed, err := convertMarkdown(file)
	if err != nil {
		return fmt.Errorf("error processing markdown: %w", err)
	}
	if processed.Filename == "" {
		// If no filename was found in frontmatter, use the newFileName
		processed.Filename = newFileName
		if source.folder == "azure-sdk-guidelines" {
			return nil // Skip processing empty filename case for azure-sdk-guidelines
		}
	}

	// Create target file and write processed content
	targetPath := filepath.Join(targetDir, processed.Filename)
	outFile, err := os.Create(targetPath)
	if err != nil {
		return fmt.Errorf("error creating target file: %w", err)
	}
	defer outFile.Close()

	if _, err := outFile.WriteString(processed.Content); err != nil {
		return fmt.Errorf("error writing processed content: %w", err)
	}

	return nil
}

func convertMarkdown(r io.Reader) (*ProcessedMarkdown, error) {
	scanner := bufio.NewScanner(r)
	var contentBuilder strings.Builder

	var title string
	var fileName string
	inFrontmatter := false
	foundTitle := false
	firstContentLine := true

	for scanner.Scan() {
		line := scanner.Text()

		// Process frontmatter
		if line == "---" {
			if !inFrontmatter {
				inFrontmatter = true
				continue
			} else {
				inFrontmatter = false
				continue
			}
		}

		if inFrontmatter {
			if strings.HasPrefix(line, "title:") {
				title = strings.TrimSpace(strings.TrimPrefix(line, "title:"))
				// Remove possible quotes
				title = strings.Trim(title, "\"'")
				foundTitle = true
			}
			if strings.HasPrefix(line, "permalink:") {
				fileName = strings.TrimSpace(strings.TrimPrefix(line, "permalink:"))
				// Remove possible quotes
				fileName = strings.Trim(fileName, "\"'")
			}
			continue
		}

		// Add title at the beginning of file content
		if !inFrontmatter && firstContentLine {
			if foundTitle {
				contentBuilder.WriteString(fmt.Sprintf("# %s\n\n", title))
			}
			firstContentLine = false
		}

		// Write non-empty lines
		if !inFrontmatter {
			contentBuilder.WriteString(line + "\n")
		}
	}

	if err := scanner.Err(); err != nil {
		return nil, fmt.Errorf("error scanning file: %w", err)
	}

	return &ProcessedMarkdown{
		Title:    title,
		Filename: fileName,
		Content:  contentBuilder.String(),
	}, nil
}

func deleteExpiredBlobs(currentFiles []string) error {
	currentFileMap := make(map[string]bool)
	for _, file := range currentFiles {
		currentFileMap[file] = true
	}
	storageService, err := storage.NewStorageService()
	if err != nil {
		fmt.Printf("Error creating storage service: %v\n", err)
		return err
	}

	// List all blobs in the container
	blobs := storageService.GetBlobs(config.STORAGE_KNOWLEDGE_CONTAINER)
	// Iterate through blobs and delete those not in the current files list
	for _, blob := range blobs {
		if strings.HasPrefix(blob, "static_") {
			// Skip static files
			fmt.Printf("Skipping static blob %s\n", blob)
			continue
		}
		if _, exists := currentFileMap[blob]; !exists {
			if err := storageService.DeleteBlob(config.STORAGE_KNOWLEDGE_CONTAINER, blob); err != nil {
				fmt.Printf("Error deleting blob %s: %v\n", blob, err)
				return err
			} else {
				fmt.Printf("Deleted blob %s\n", blob)
			}
		}
	}
	return nil
}

// createReleaseNotesIndex creates an index file with content from the 10 most recent release notes
func createReleaseNotesIndex(source Source, targetDir string) error {
	// Path to release notes directory
	releaseNotesDir := filepath.Join(source.path, "release-notes")

	// Check if release notes directory exists
	if _, err := os.Stat(releaseNotesDir); os.IsNotExist(err) {
		fmt.Printf("Release notes directory not found for %s, skipping index creation\n", source.folder)
		return nil
	}

	releaseFiles := []string{}
	err := filepath.Walk(releaseNotesDir, func(path string, info os.FileInfo, err error) error {
		if err != nil {
			return err
		}

		if info.IsDir() {
			return nil
		}

		// Match files with pattern release-YYYY-MM-DD.md or release-YYYY-MM-DD.mdx
		matched, err := regexp.MatchString(`release-\d{4}-\d{2}-\d{2}\.(md|mdx)$`, info.Name())
		if err != nil {
			return err
		}

		if matched {
			releaseFiles = append(releaseFiles, path)
		}

		return nil
	})

	if err != nil {
		return fmt.Errorf("error walking release notes directory: %w", err)
	}

	// Sort files by date (newest first)
	sort.Slice(releaseFiles, func(i, j int) bool {
		// Extract dates from filenames
		dateI := extractDateFromFilename(releaseFiles[i])
		dateJ := extractDateFromFilename(releaseFiles[j])

		// Compare dates (reverse order for newest first)
		return dateI.After(dateJ)
	})

	// Take only the 10 most recent files (or fewer if less than 10 exist)
	maxFiles := 10
	if len(releaseFiles) < maxFiles {
		maxFiles = len(releaseFiles)
	}
	recentReleaseFiles := releaseFiles[:maxFiles]

	// Create index content
	indexTitle := fmt.Sprintf("# %s - Recent Version Release Notes\n", source.folder)
	description := fmt.Sprintf("There contains latest release version and changes of %s\n\n", source.name)
	content := indexTitle + description

	// Add content from each release note file
	for _, filePath := range recentReleaseFiles {
		fileContent, err := os.ReadFile(filePath)
		if err != nil {
			fmt.Printf("Error reading release note file %s: %v\n", filePath, err)
			continue
		}

		// Get relative file path for building the release link
		relFilePath, err := filepath.Rel(source.path, filePath)
		if err != nil {
			fmt.Printf("Error getting relative path for %s: %v\n", filePath, err)
			relFilePath = filepath.Base(filePath)
		}

		// Prepare release link based on the source repository
		var releaseLink string
		if source.folder == "typespec_docs" {
			releaseLink = fmt.Sprintf("https://typespec.io/docs/%s", relFilePath)
		} else if source.folder == "typespec_azure_docs" {
			releaseLink = fmt.Sprintf("https://azure.github.io/typespec-azure/docs/%s", relFilePath)
		}

		// Remove file extension from link
		releaseLink = strings.TrimSuffix(releaseLink, filepath.Ext(releaseLink))

		// Extract title, release date, and version from frontmatter
		title, releaseDate, version := extractReleaseInfo(string(fileContent))

		// Create section header with extracted information and link
		releaseHeader := fmt.Sprintf("## [version-%s-%s](%s)\n", title, releaseDate, releaseLink)
		if version != "" {
			releaseHeader = fmt.Sprintf("## [version-%s-%s (v%s)](%s)\n", title, releaseDate, version, releaseLink)
		}

		// Extract and organize sections
		sections := extractSections(string(fileContent))

		// Add the release header and sections to content
		content += releaseHeader + sections + "\n"
	}

	// Create the index file in the target directory
	indexPath := filepath.Join(targetDir, "version-release-notes-index.md")
	err = os.WriteFile(indexPath, []byte(content), 0644)
	if err != nil {
		return fmt.Errorf("error writing release notes index: %w", err)
	}

	fmt.Printf("Created release notes index for %s\n", source.folder)
	return nil
}

// extractDateFromFilename extracts date from a filename in the format release-YYYY-MM-DD.md
func extractDateFromFilename(path string) time.Time {
	filename := filepath.Base(path)
	re := regexp.MustCompile(`release-(\d{4}-\d{2}-\d{2})`)
	matches := re.FindStringSubmatch(filename)

	if len(matches) < 2 {
		// Return zero time if no match found
		return time.Time{}
	}

	// Parse the date
	t, err := time.Parse("2006-01-02", matches[1])
	if err != nil {
		// Return zero time on error
		return time.Time{}
	}

	return t
}

// extractReleaseInfo extracts title, releaseDate and version from release note frontmatter
func extractReleaseInfo(content string) (string, string, string) {
	// Default values
	title := ""
	releaseDate := ""
	version := ""

	// Look for frontmatter between --- markers
	frontmatterRegex := regexp.MustCompile(`(?s)---\s*(.*?)\s*---`)
	matches := frontmatterRegex.FindStringSubmatch(content)
	if len(matches) < 2 {
		return title, releaseDate, version
	}

	frontmatter := matches[1]

	// Split frontmatter into lines and process each line
	scanner := bufio.NewScanner(strings.NewReader(frontmatter))
	for scanner.Scan() {
		line := scanner.Text()
		line = strings.TrimSpace(line)

		// Look for title, releaseDate, and version fields
		if strings.HasPrefix(line, "title:") {
			title = strings.TrimSpace(strings.TrimPrefix(line, "title:"))
			// Remove quotes if present
			title = strings.Trim(title, "\"'")
		} else if strings.HasPrefix(line, "releaseDate:") {
			releaseDate = strings.TrimSpace(strings.TrimPrefix(line, "releaseDate:"))
		} else if strings.HasPrefix(line, "version:") {
			version = strings.TrimSpace(strings.TrimPrefix(line, "version:"))
			// Remove quotes if present
			version = strings.Trim(version, "\"'")
		}
	}

	return title, releaseDate, version
}

// extractSections extracts content and downgrades the heading levels
func extractSections(content string) string {
	// Remove frontmatter
	frontmatterRegex := regexp.MustCompile(`(?s)---.*?---\s*`)
	contentWithoutFrontmatter := frontmatterRegex.ReplaceAllString(content, "")

	// Remove special markups like caution blocks
	cautionRegex := regexp.MustCompile(`(?s):::caution.*?:::\s*`)
	contentWithoutMarkup := cautionRegex.ReplaceAllString(contentWithoutFrontmatter, "")

	// Downgrade all headers (add one more # to each header)
	// Match headers with any number of # characters
	headerRegex := regexp.MustCompile(`(?m)^(#+)\s+(.+)$`)
	downgradedContent := headerRegex.ReplaceAllString(contentWithoutMarkup, "#$1 $2")

	return strings.TrimSpace(downgradedContent)
}
