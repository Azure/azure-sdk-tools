package model

// =====================================================================
// GitHub API response types
// =====================================================================

// GitHubAnnotation represents a single annotation on a check run.
type GitHubAnnotation struct {
	Path            string `json:"path"`
	StartLine       int    `json:"start_line"`
	EndLine         int    `json:"end_line"`
	AnnotationLevel string `json:"annotation_level"`
	Message         string `json:"message"`
	Title           string `json:"title"`
}

// GitHubCheckRunResponse represents a GitHub check run API response.
type GitHubCheckRunResponse struct {
	ID         int64  `json:"id"`
	Name       string `json:"name"`
	Status     string `json:"status"`
	Conclusion string `json:"conclusion"`
	Output     struct {
		Title       string             `json:"title"`
		Summary     string             `json:"summary"`
		Text        string             `json:"text"`
		Annotations []GitHubAnnotation `json:"annotations"`
	} `json:"output"`
	HTMLURL    string `json:"html_url"`
	ExternalID string `json:"external_id"`
	App        struct {
		Name string `json:"name"`
	} `json:"app"`
}

// GitHubActionsRunResponse represents a GitHub Actions workflow run.
type GitHubActionsRunResponse struct {
	ID         int64  `json:"id"`
	Name       string `json:"name"`
	Status     string `json:"status"`
	Conclusion string `json:"conclusion"`
	HTMLURL    string `json:"html_url"`
}

// GitHubJobResponse represents a single job within a GitHub Actions workflow run.
type GitHubJobResponse struct {
	ID         int64  `json:"id"`
	RunID      int64  `json:"run_id"`
	Name       string `json:"name"`
	Status     string `json:"status"`
	Conclusion string `json:"conclusion"`
	Steps      []struct {
		Name       string `json:"name"`
		Status     string `json:"status"`
		Conclusion string `json:"conclusion"`
		Number     int    `json:"number"`
	} `json:"steps"`
}

// GitHubJobsListResponse wraps a list of jobs returned by the GitHub API.
type GitHubJobsListResponse struct {
	TotalCount int                 `json:"total_count"`
	Jobs       []GitHubJobResponse `json:"jobs"`
}

// GitHubCheckRunsListResponse wraps a list of check runs returned by the GitHub API.
type GitHubCheckRunsListResponse struct {
	TotalCount int                      `json:"total_count"`
	CheckRuns  []GitHubCheckRunResponse `json:"check_runs"`
}

// GitHubPRResponse represents a GitHub pull request API response.
type GitHubPRResponse struct {
	Number int    `json:"number"`
	Title  string `json:"title"`
	State  string `json:"state"`
	Head   struct {
		SHA string `json:"sha"`
	} `json:"head"`
	HTMLURL string `json:"html_url"`
}

// =====================================================================
// Link types used by the GitHub client for URL parsing
// =====================================================================

// GitHubCheckLink holds parsed data from a GitHub check run or Actions URL.
type GitHubCheckLink struct {
	Owner        string
	Repo         string
	ActionsRunID string
	JobID        string
	CheckRunID   string
}

// GitHubPRLink holds parsed data from a GitHub pull request URL.
type GitHubPRLink struct {
	Owner    string
	Repo     string
	PRNumber string
}
