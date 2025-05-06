package main

import (
	"bufio"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"strings"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/storage"
)

type Source struct {
	path   string
	folder string
}

type ReleaseNote struct {
	Title       string
	Version     string
	ReleaseDate string
	Link        string
}

func main() {
	// Process both repositories
	sources := []Source{
		{
			path:   "docs/typespec/website/src/content/docs/docs",
			folder: "typespec_docs",
		},
		{
			path:   "docs/typespec-azure/website/src/content/docs/docs",
			folder: "typespec_azure_docs",
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

		// Process release notes and generate index
		releaseNotes, err := processReleaseNotes(sourceDir, strings.Contains(source.folder, "azure"))
		if err != nil {
			fmt.Printf("Error processing release notes: %v\n", err)
			return
		}
		if err := generateReleaseNoteIndex(releaseNotes, targetDir, source); err != nil {
			fmt.Printf("Error generating release notes index: %v\n", err)
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
			fileName := strings.ToLower(strings.ReplaceAll(info.Name(), " ", "-"))
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
	targetPath := filepath.Join(targetDir, newFileName)

	// Create target file
	outFile, err := os.Create(targetPath)
	if err != nil {
		return fmt.Errorf("error creating target file: %w", err)
	}
	defer outFile.Close()

	// Process file content
	return convertMarkdown(file, outFile)
}

func convertMarkdown(r io.Reader, w io.Writer) error {
	scanner := bufio.NewScanner(r)
	writer := bufio.NewWriter(w)
	defer writer.Flush()

	var title string
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
			continue
		}

		// Add title at the beginning of file content
		if !inFrontmatter && firstContentLine {
			if foundTitle {
				if _, err := writer.WriteString(fmt.Sprintf("# %s\n\n", title)); err != nil {
					return fmt.Errorf("error writing title: %w", err)
				}
			}
			firstContentLine = false
		}

		// Write non-empty lines
		if !inFrontmatter {
			if _, err := writer.WriteString(line + "\n"); err != nil {
				return fmt.Errorf("error writing line: %w", err)
			}
		}
	}

	if err := scanner.Err(); err != nil {
		return fmt.Errorf("error scanning file: %w", err)
	}

	return nil
}

func processReleaseNotes(sourceDir string, isTypespecAzure bool) ([]ReleaseNote, error) {
	var releaseNotes []ReleaseNote
	releaseNotesDir := filepath.Join(sourceDir, "release-notes")

	err := filepath.Walk(releaseNotesDir, func(path string, info os.FileInfo, err error) error {
		if err != nil {
			return err
		}
		if info.IsDir() || !strings.HasSuffix(info.Name(), ".md") {
			return nil
		}

		file, err := os.Open(path)
		if err != nil {
			return fmt.Errorf("error opening file %s: %w", path, err)
		}
		defer file.Close()

		relPath, err := filepath.Rel(sourceDir, path)
		if err != nil {
			return fmt.Errorf("error getting relative path: %w", err)
		}

		var note ReleaseNote
		scanner := bufio.NewScanner(file)
		inFrontmatter := false

		baseURL := "https://typespec.io/docs/"
		if isTypespecAzure {
			baseURL = "https://azure.github.io/typespec-azure/docs/"
		}
		note.Link = baseURL + strings.TrimSuffix(relPath, filepath.Ext(relPath)) + "/"

		for scanner.Scan() {
			line := scanner.Text()
			if line == "---" {
				if !inFrontmatter {
					inFrontmatter = true
					continue
				} else {
					break
				}
			}
			if inFrontmatter {
				if strings.HasPrefix(line, "title:") {
					note.Title = strings.TrimSpace(strings.TrimPrefix(line, "title:"))
				}
				if strings.HasPrefix(line, "version:") {
					note.Version = strings.Trim(strings.TrimSpace(strings.TrimPrefix(line, "version:")), "\"'")
				}
				if strings.HasPrefix(line, "releaseDate:") {
					note.ReleaseDate = strings.TrimSpace(strings.TrimPrefix(line, "releaseDate:"))
				}
			}
		}

		releaseNotes = append(releaseNotes, note)

		return scanner.Err()
	})

	return releaseNotes, err
}

func generateReleaseNoteIndex(notes []ReleaseNote, targetDir string, source Source) error {
	// reverse the order of notes
	for i, j := 0, len(notes)-1; i < j; i, j = i+1, j-1 {
		notes[i], notes[j] = notes[j], notes[i]
	}
	indexPath := filepath.Join(targetDir, "release-notes-index.md")
	file, err := os.Create(indexPath)
	if err != nil {
		return fmt.Errorf("error creating index file: %w", err)
	}
	defer file.Close()

	writer := bufio.NewWriter(file)
	defer writer.Flush()

	title := "TypeSpec Release Notes Index"
	if strings.Contains(source.folder, "azure") {
		title = "TypeSpec Azure Release Notes Index"
	}

	_, err = writer.WriteString(fmt.Sprintf("# %s\n\n", title))
	if err != nil {
		return fmt.Errorf("error writing title: %w", err)
	}

	for _, note := range notes {
		_, err = writer.WriteString(fmt.Sprintf("- Version: %s (Released: %s)\n  [%s](%s)\n\n",
			note.Version, note.ReleaseDate, note.Title, note.Link))
		if err != nil {
			return fmt.Errorf("error writing note: %w", err)
		}
	}

	return nil
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
