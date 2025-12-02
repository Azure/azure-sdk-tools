package model

// CodeReviewReq represents a request for SDK code review
type CodeReviewReq struct {
	Language string `json:"language" jsonschema:"required,description=The programming language of the code (e.g., python, java, go, javascript)"`
	FilePath string `json:"file_path,omitempty" jsonschema:"omitempty,description=The relative file path of the code being reviewed (e.g., sdk/storage/azblob/client.go). Helps identify file type for appropriate guidelines."`
	Code     string `json:"code" jsonschema:"required,description=The code content to review, can be a single function or entire API surface"`
	Context  string `json:"context,omitempty" jsonschema:"omitempty,description=Additional context or guidelines for the review"`
}

// ReviewComment represents a single review comment with location and suggestion
type ReviewComment struct {
	LineNumber       int     `json:"line_number,omitempty" jsonschema:"omitempty,description=The line number where the issue occurs"`
	BadCode          string  `json:"bad_code" jsonschema:"required,description=The problematic code snippet"`
	Suggestion       *string `json:"suggestion" jsonschema:"omitempty,description=Suggested fix for the issue, null if no specific fix available"`
	Comment          string  `json:"comment" jsonschema:"required,description=Human-readable description of the issue"`
	GuidelineID      string  `json:"guideline_id,omitempty" jsonschema:"omitempty,description=The guideline ID that was violated"`
	GuidelineContent string  `json:"guideline_content,omitempty" jsonschema:"omitempty,description=Excerpt from the referenced guideline that was violated"`
	GuidelineLink    string  `json:"guideline_link,omitempty" jsonschema:"omitempty,description=URL link to the referenced guideline documentation"`
}

// CodeReviewResp represents the response from a code review
type CodeReviewResp struct {
	ID       string          `json:"id" jsonschema:"required,description=The unique ID of the review"`
	Language string          `json:"language" jsonschema:"required,description=The programming language that was reviewed"`
	Comments []ReviewComment `json:"comments" jsonschema:"required,description=List of review comments and suggestions"`
	Summary  string          `json:"summary,omitempty" jsonschema:"omitempty,description=Overall summary of the review"`
}
