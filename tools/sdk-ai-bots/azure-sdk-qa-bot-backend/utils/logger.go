package utils

import (
	"io"
	"log"
	"os"
	"strings"
)

// SingleLineWriter wraps an io.Writer and escapes newlines
// This prevents log entries from being split across multiple lines in Azure App Service
// while preserving the original newline information.
// It also limits log entry length to prevent log flooding attacks.
type SingleLineWriter struct {
	writer   io.Writer
	maxBytes int
}

// NewSingleLineWriter creates a new SingleLineWriter with a default max length of 10000 bytes
func NewSingleLineWriter(w io.Writer) *SingleLineWriter {
	return &SingleLineWriter{
		writer:   w,
		maxBytes: 10000,
	}
}

// NewSingleLineWriterWithMaxBytes creates a new SingleLineWriter with a custom max length
func NewSingleLineWriterWithMaxBytes(w io.Writer, maxBytes int) *SingleLineWriter {
	return &SingleLineWriter{
		writer:   w,
		maxBytes: maxBytes,
	}
}

// Write implements io.Writer interface
// It escapes newlines as \n, \r\n, \r and limits content length before writing to the underlying writer
// This prevents log injection attacks (CWE-117) and log flooding
func (w *SingleLineWriter) Write(p []byte) (n int, err error) {
	// Convert to string, escape newlines, then convert back to bytes
	content := string(p)

	// Limit content length to prevent log flooding
	if len(content) > w.maxBytes {
		content = content[:w.maxBytes] + "... [truncated]"
	}

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
	writtenBytes, err := w.writer.Write([]byte(content))
	if err != nil {
		return writtenBytes, err
	}

	// Return the original byte count to satisfy io.Writer contract
	return len(p), nil
}

// ConfigureAzureCompatibleLogging configures the Go log package to work well with Azure App Service
// Call this in your main function to set up logging that won't be split across multiple log entries
// Newlines will be escaped as \n, \r\n, \r to preserve the original information
// Log entries will be truncated to prevent log flooding attacks
func ConfigureAzureCompatibleLogging() {
	// Create a single-line writer that wraps stdout
	singleLineWriter := NewSingleLineWriter(os.Stdout)

	// Set the log output to use our custom writer
	log.SetOutput(singleLineWriter)
}
