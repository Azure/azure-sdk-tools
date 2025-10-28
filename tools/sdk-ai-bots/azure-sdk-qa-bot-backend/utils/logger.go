package utils

import (
	"io"
	"log"
	"os"
	"strings"
)

// SingleLineWriter wraps an io.Writer and escapes newlines
// This prevents log entries from being split across multiple lines in Azure App Service
// while preserving the original newline information
type SingleLineWriter struct {
	writer io.Writer
}

// NewSingleLineWriter creates a new SingleLineWriter
func NewSingleLineWriter(w io.Writer) *SingleLineWriter {
	return &SingleLineWriter{writer: w}
}

// Write implements io.Writer interface
// It escapes newlines as \n, \r\n, \r before writing to the underlying writer
func (w *SingleLineWriter) Write(p []byte) (n int, err error) {
	// Convert to string, escape newlines, then convert back to bytes
	content := string(p)

	// Check if the content ends with a newline (which is typical for log entries)
	endsWithNewline := strings.HasSuffix(content, "\n")

	// Remove the trailing newline temporarily if it exists
	if endsWithNewline {
		content = strings.TrimSuffix(content, "\n")
	}

	// Order matters: replace \r\n first, then \n and \r separately
	content = strings.ReplaceAll(content, "\r\n", "\\r\\n")
	content = strings.ReplaceAll(content, "\n", "\\n")
	content = strings.ReplaceAll(content, "\r", "\\r")

	// Add back the trailing newline if it was there originally
	if endsWithNewline {
		content += "\n"
	}

	// Write the modified content
	return w.writer.Write([]byte(content))
}

// ConfigureAzureCompatibleLogging configures the Go log package to work well with Azure App Service
// Call this in your main function to set up logging that won't be split across multiple log entries
// Newlines will be escaped as \n, \r\n, \r to preserve the original information
func ConfigureAzureCompatibleLogging() {
	// Create a single-line writer that wraps stdout
	singleLineWriter := NewSingleLineWriter(os.Stdout)

	// Set the log output to use our custom writer
	log.SetOutput(singleLineWriter)
}

// SanitizeForLog removes newline and carriage return characters from user input to prevent log injection
// This function should be used to sanitize any user-provided data before logging it
// to prevent CWE-117 (Improper Output Neutralization for Logs) vulnerabilities
func SanitizeForLog(s string) string {
	s = strings.ReplaceAll(s, "\n", "")
	s = strings.ReplaceAll(s, "\r", "")
	return s
}
