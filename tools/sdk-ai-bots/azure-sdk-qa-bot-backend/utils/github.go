package utils

import (
	"compress/gzip"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"net/http"
	"regexp"
	"strings"
	"time"
)

// GitHub API base URL
const githubAPIBaseURL = "https://api.github.com"

// Max log size to return (200KB) to avoid overwhelming the LLM context
const maxGitHubLogSize = 200 * 1024

// gitHubAnnotation represents a single annotation from a check run
type gitHubAnnotation struct {
	Path            string `json:"path"`
	StartLine       int    `json:"start_line"`
	EndLine         int    `json:"end_line"`
	AnnotationLevel string `json:"annotation_level"`
	Message         string `json:"message"`
	Title           string `json:"title"`
}

// gitHubCheckRunResponse represents the GitHub API response for a check run
type gitHubCheckRunResponse struct {
	ID         int64  `json:"id"`
	Name       string `json:"name"`
	Status     string `json:"status"`
	Conclusion string `json:"conclusion"`
	Output     struct {
		Title       string             `json:"title"`
		Summary     string             `json:"summary"`
		Text        string             `json:"text"`
		Annotations []gitHubAnnotation `json:"annotations"`
	} `json:"output"`
	HTMLURL    string `json:"html_url"`
	ExternalID string `json:"external_id"`
	App        struct {
		Name string `json:"name"`
	} `json:"app"`
}

// gitHubWorkflowRunResponse represents GitHub API response for a workflow run
type gitHubWorkflowRunResponse struct {
	ID         int64  `json:"id"`
	Name       string `json:"name"`
	Status     string `json:"status"`
	Conclusion string `json:"conclusion"`
	HTMLURL    string `json:"html_url"`
}

// gitHubJobResponse represents GitHub API response for a workflow job
type gitHubJobResponse struct {
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

// gitHubJobsListResponse represents the GitHub API response for listing jobs
type gitHubJobsListResponse struct {
	TotalCount int                 `json:"total_count"`
	Jobs       []gitHubJobResponse `json:"jobs"`
}

// gitHubCheckRunsListResponse represents the GitHub API response for listing check runs
type gitHubCheckRunsListResponse struct {
	TotalCount int                      `json:"total_count"`
	CheckRuns  []gitHubCheckRunResponse `json:"check_runs"`
}

// gitHubPRResponse represents the GitHub API response for a pull request (minimal fields)
type gitHubPRResponse struct {
	Number int    `json:"number"`
	Title  string `json:"title"`
	State  string `json:"state"`
	Head   struct {
		SHA string `json:"sha"`
	} `json:"head"`
	HTMLURL string `json:"html_url"`
}

// gitHubCheckLink holds parsed info from a GitHub check/actions URL
type gitHubCheckLink struct {
	Owner      string
	Repo       string
	RunID      string // workflow run ID (for /actions/runs/{id})
	JobID      string // job ID (for /actions/runs/{id}/job/{job_id})
	CheckRunID string // check run ID (for /runs/{id})
}

// gitHubPRLink holds parsed info from a GitHub PR URL
type gitHubPRLink struct {
	Owner    string
	Repo     string
	PRNumber string
}

// IsGitHubCheckLink checks if a URL is a GitHub check run or GitHub Actions link
func IsGitHubCheckLink(url string) bool {
	return parseGitHubCheckLink(url) != nil
}

// IsGitHubPRLink checks if a URL is a GitHub pull request link
func IsGitHubPRLink(url string) bool {
	return parseGitHubPRLink(url) != nil
}

// ciRelatedKeywords are terms in the intention category or question that signal
// the user is asking about CI checks, build/validation failures, or pipeline issues.
var ciRelatedKeywords = []string{
	"check", "ci", "pipeline", "build", "fail", "error",
	"lint", "validation", "action", "workflow",
	"blocking", "merge", "spec-validation",
}

// IsCIRelatedIntention returns true if the recognized intention indicates the user
// is asking about CI checks, build failures, or pipeline issues.
// It inspects both the category and the rewritten question from intention recognition.
func IsCIRelatedIntention(category string, question string) bool {
	combined := strings.ToLower(category + " " + question)
	for _, keyword := range ciRelatedKeywords {
		if strings.Contains(combined, keyword) {
			return true
		}
	}
	return false
}

// parseGitHubPRLink extracts owner, repo, and PR number from a GitHub PR URL.
// Supported URL formats:
//   - https://github.com/{owner}/{repo}/pull/{pr_number}
func parseGitHubPRLink(url string) *gitHubPRLink {
	prRegex := regexp.MustCompile(`^https?://github\.com/([^/]+)/([^/]+)/pull/(\d+)`)
	if matches := prRegex.FindStringSubmatch(url); len(matches) >= 4 {
		return &gitHubPRLink{
			Owner:    matches[1],
			Repo:     matches[2],
			PRNumber: matches[3],
		}
	}
	return nil
}

// parseGitHubCheckLink extracts owner, repo, and IDs from a GitHub check/actions URL.
// Supported URL formats:
//   - https://github.com/{owner}/{repo}/actions/runs/{run_id}
//   - https://github.com/{owner}/{repo}/actions/runs/{run_id}/job/{job_id}
//   - https://github.com/{owner}/{repo}/actions/runs/{run_id}/jobs/{job_id}
//   - https://github.com/{owner}/{repo}/runs/{check_run_id}
func parseGitHubCheckLink(url string) *gitHubCheckLink {
	// Match GitHub Actions workflow run with optional job ID
	// e.g., https://github.com/Azure/azure-rest-api-specs/actions/runs/18752237048/job/53494737696
	actionsRunRegex := regexp.MustCompile(`^https?://github\.com/([^/]+)/([^/]+)/actions/runs/(\d+)(?:/jobs?/(\d+))?`)
	if matches := actionsRunRegex.FindStringSubmatch(url); len(matches) >= 4 {
		link := &gitHubCheckLink{
			Owner: matches[1],
			Repo:  matches[2],
			RunID: matches[3],
		}
		if len(matches) >= 5 && matches[4] != "" {
			link.JobID = matches[4]
		}
		return link
	}

	// Match GitHub check run URL
	// e.g., https://github.com/Azure/azure-rest-api-specs/runs/53495233105
	checkRunRegex := regexp.MustCompile(`^https?://github\.com/([^/]+)/([^/]+)/runs/(\d+)`)
	if matches := checkRunRegex.FindStringSubmatch(url); len(matches) >= 4 {
		return &gitHubCheckLink{
			Owner:      matches[1],
			Repo:       matches[2],
			CheckRunID: matches[3],
		}
	}

	return nil
}

// FetchGitHubCheckLogs fetches logs/details for a GitHub check run or Actions workflow run.
// Uses the public GitHub API (no authentication required for public repos).
// Returns a formatted text summary of the check/action results and logs.
func FetchGitHubCheckLogs(url string) (string, error) {
	startTime := time.Now()
	defer func() {
		log.Printf("FetchGitHubCheckLogs completed in %v", time.Since(startTime))
	}()

	link := parseGitHubCheckLink(url)
	if link == nil {
		return "", fmt.Errorf("not a valid GitHub check/actions URL: %s", url)
	}

	if link.CheckRunID != "" {
		return fetchCheckRunDetails(link)
	}

	if link.JobID != "" {
		return fetchJobLogs(link)
	}

	if link.RunID != "" {
		return fetchWorkflowRunDetails(link)
	}

	return "", fmt.Errorf("unable to determine GitHub check type from URL: %s", url)
}

// FetchGitHubPRChecks fetches all check runs for a GitHub PR and returns details
// of failed/problematic checks including their output, annotations, and logs.
// For passing checks, only a summary line is returned.
func FetchGitHubPRChecks(url string) (string, error) {
	startTime := time.Now()
	defer func() {
		log.Printf("FetchGitHubPRChecks completed in %v", time.Since(startTime))
	}()

	prLink := parseGitHubPRLink(url)
	if prLink == nil {
		return "", fmt.Errorf("not a valid GitHub PR URL: %s", url)
	}

	return fetchPRCheckRuns(prLink)
}

// fetchPRCheckRuns fetches the PR details and all check runs, then returns
// a formatted summary with detailed info for failed checks.
func fetchPRCheckRuns(prLink *gitHubPRLink) (string, error) {
	// 1. Get PR details to obtain the head SHA
	prURL := fmt.Sprintf("%s/repos/%s/%s/pulls/%s", githubAPIBaseURL, prLink.Owner, prLink.Repo, prLink.PRNumber)
	log.Printf("Fetching GitHub PR details: %s", prURL)

	body, err := githubAPIGet(prURL)
	if err != nil {
		return "", fmt.Errorf("failed to fetch PR details: %w", err)
	}

	var pr gitHubPRResponse
	if err = json.Unmarshal(body, &pr); err != nil {
		return "", fmt.Errorf("failed to parse PR response: %w", err)
	}

	var sb strings.Builder
	sb.WriteString(fmt.Sprintf("## GitHub PR #%d: %s\n", pr.Number, pr.Title))
	sb.WriteString(fmt.Sprintf("- **State**: %s\n", pr.State))
	sb.WriteString(fmt.Sprintf("- **URL**: %s\n", pr.HTMLURL))

	// 2. Get all check runs for the head SHA
	checksURL := fmt.Sprintf("%s/repos/%s/%s/commits/%s/check-runs?per_page=100", githubAPIBaseURL, prLink.Owner, prLink.Repo, pr.Head.SHA)
	log.Printf("Fetching check runs for SHA %s", pr.Head.SHA)

	body, err = githubAPIGet(checksURL)
	if err != nil {
		log.Printf("Failed to fetch check runs: %v", err)
		sb.WriteString("*Failed to fetch check runs*\n")
		return sb.String(), nil
	}

	var checksList gitHubCheckRunsListResponse
	if err := json.Unmarshal(body, &checksList); err != nil {
		log.Printf("Failed to parse check runs response: %v", err)
		sb.WriteString("*Failed to parse check runs*\n")
		return sb.String(), nil
	}

	// 3. Categorize checks
	var failed, succeeded, pending []gitHubCheckRunResponse
	for _, check := range checksList.CheckRuns {
		switch {
		case check.Conclusion == "failure" || check.Conclusion == "action_required" || check.Conclusion == "timed_out" || check.Conclusion == "cancelled":
			failed = append(failed, check)
		case check.Status == "completed" && (check.Conclusion == "success" || check.Conclusion == "neutral" || check.Conclusion == "skipped"):
			succeeded = append(succeeded, check)
		default:
			pending = append(pending, check)
		}
	}

	sb.WriteString(fmt.Sprintf("### Check Runs Summary: %d total, %d failed, %d passed, %d pending\n\n",
		checksList.TotalCount, len(failed), len(succeeded), len(pending)))

	// 4. Show detailed info for failed checks
	if len(failed) > 0 {
		sb.WriteString("### Failed Checks\n")
		for _, check := range failed {
			sb.WriteString(fmt.Sprintf("\n#### %s\n", check.Name))
			sb.WriteString(fmt.Sprintf("- **Conclusion**: %s\n", check.Conclusion))
			sb.WriteString(fmt.Sprintf("- **App**: %s\n", check.App.Name))
			sb.WriteString(fmt.Sprintf("- **URL**: %s\n", check.HTMLURL))

			if check.Output.Title != "" {
				sb.WriteString(fmt.Sprintf("- **Output Title**: %s\n", check.Output.Title))
			}
			if check.Output.Summary != "" {
				summary := check.Output.Summary
				if len(summary) > maxGitHubLogSize/4 {
					summary = summary[:maxGitHubLogSize/4] + "\n... [truncated]"
				}
				sb.WriteString(fmt.Sprintf("\n**Summary**:\n%s\n", summary))
			}
			if check.Output.Text != "" {
				text := check.Output.Text
				if len(text) > maxGitHubLogSize/4 {
					text = text[:maxGitHubLogSize/4] + "\n... [truncated]"
				}
				sb.WriteString(fmt.Sprintf("\n**Details**:\n%s\n", text))
			}

			// Fetch annotations separately (the list check-runs endpoint does not include them)
			annotations := fetchCheckRunAnnotations(prLink.Owner, prLink.Repo, check.ID)
			if len(annotations) > 0 {
				sb.WriteString("\n**Annotations**:\n")
				for _, a := range annotations {
					sb.WriteString(fmt.Sprintf("- **[%s]** %s (line %d", a.AnnotationLevel, a.Path, a.StartLine))
					if a.EndLine != a.StartLine {
						sb.WriteString(fmt.Sprintf("-%d", a.EndLine))
					}
					sb.WriteString(fmt.Sprintf("): %s", a.Message))
					if a.Title != "" {
						sb.WriteString(fmt.Sprintf(" — %s", a.Title))
					}
					sb.WriteString("\n")
				}
			}
		}
	}

	return sb.String(), nil
}

// fetchCheckRunAnnotations fetches annotations for a check run via the dedicated endpoint.
// The list check-runs endpoint does NOT include annotations; they must be fetched separately.
func fetchCheckRunAnnotations(owner, repo string, checkRunID int64) []gitHubAnnotation {
	apiURL := fmt.Sprintf("%s/repos/%s/%s/check-runs/%d/annotations?per_page=50", githubAPIBaseURL, owner, repo, checkRunID)
	log.Printf("Fetching annotations for check run %d: %s", checkRunID, apiURL)

	body, err := githubAPIGet(apiURL)
	if err != nil {
		log.Printf("Failed to fetch annotations for check run %d: %v", checkRunID, err)
		return nil
	}

	var annotations []gitHubAnnotation
	if err := json.Unmarshal(body, &annotations); err != nil {
		log.Printf("Failed to parse annotations for check run %d: %v", checkRunID, err)
		return nil
	}

	return annotations
}

// fetchCheckRunDetails gets details and annotations for a GitHub check run
func fetchCheckRunDetails(link *gitHubCheckLink) (string, error) {
	apiURL := fmt.Sprintf("%s/repos/%s/%s/check-runs/%s", githubAPIBaseURL, link.Owner, link.Repo, link.CheckRunID)
	log.Printf("Fetching GitHub check run details: %s", apiURL)

	body, err := githubAPIGet(apiURL)
	if err != nil {
		return "", fmt.Errorf("failed to fetch check run details: %w", err)
	}

	var checkRun gitHubCheckRunResponse
	if err := json.Unmarshal(body, &checkRun); err != nil {
		return "", fmt.Errorf("failed to parse check run response: %w", err)
	}

	var sb strings.Builder
	sb.WriteString(fmt.Sprintf("## GitHub Check Run: %s\n", checkRun.Name))
	sb.WriteString(fmt.Sprintf("- **Status**: %s\n", checkRun.Status))
	sb.WriteString(fmt.Sprintf("- **App**: %s\n", checkRun.App.Name))
	sb.WriteString(fmt.Sprintf("- **URL**: %s\n\n", checkRun.HTMLURL))

	if checkRun.Output.Title != "" {
		sb.WriteString(fmt.Sprintf("### Output: %s\n", checkRun.Output.Title))
	}
	if checkRun.Output.Summary != "" {
		sb.WriteString(fmt.Sprintf("**Summary**:\n%s\n\n", checkRun.Output.Summary))
	}
	if checkRun.Output.Text != "" {
		text := checkRun.Output.Text
		if len(text) > maxGitHubLogSize {
			text = text[:maxGitHubLogSize] + "\n... [truncated]"
		}
		sb.WriteString(fmt.Sprintf("**Details**:\n%s\n\n", text))
	}

	// Fetch annotations via dedicated endpoint (the check-run response may not include them)
	annotations := fetchCheckRunAnnotations(link.Owner, link.Repo, checkRun.ID)
	if len(annotations) > 0 {
		sb.WriteString("### Annotations\n")
		for _, a := range annotations {
			sb.WriteString(fmt.Sprintf("- **[%s]** %s (line %d", a.AnnotationLevel, a.Path, a.StartLine))
			if a.EndLine != a.StartLine {
				sb.WriteString(fmt.Sprintf("-%d", a.EndLine))
			}
			sb.WriteString(fmt.Sprintf("): %s", a.Message))
			if a.Title != "" {
				sb.WriteString(fmt.Sprintf(" — %s", a.Title))
			}
			sb.WriteString("\n")
		}
	}

	return sb.String(), nil
}

// fetchWorkflowRunDetails gets the workflow run info and its failed jobs' logs
func fetchWorkflowRunDetails(link *gitHubCheckLink) (string, error) {
	// First, get workflow run details
	runURL := fmt.Sprintf("%s/repos/%s/%s/actions/runs/%s", githubAPIBaseURL, link.Owner, link.Repo, link.RunID)
	log.Printf("Fetching GitHub workflow run details: %s", runURL)

	body, err := githubAPIGet(runURL)
	if err != nil {
		return "", fmt.Errorf("failed to fetch workflow run details: %w", err)
	}

	var run gitHubWorkflowRunResponse
	if err = json.Unmarshal(body, &run); err != nil {
		return "", fmt.Errorf("failed to parse workflow run response: %w", err)
	}

	var sb strings.Builder
	sb.WriteString(fmt.Sprintf("## GitHub Actions Workflow Run: %s\n", run.Name))
	sb.WriteString(fmt.Sprintf("- **Status**: %s\n", run.Status))
	sb.WriteString(fmt.Sprintf("- **Conclusion**: %s\n", run.Conclusion))
	sb.WriteString(fmt.Sprintf("- **URL**: %s\n\n", run.HTMLURL))

	// Get jobs for this run
	jobsURL := fmt.Sprintf("%s/repos/%s/%s/actions/runs/%s/jobs", githubAPIBaseURL, link.Owner, link.Repo, link.RunID)
	body, err = githubAPIGet(jobsURL)
	if err != nil {
		log.Printf("Failed to fetch jobs for workflow run: %v", err)
		return sb.String(), nil // Return what we have
	}

	var jobsList gitHubJobsListResponse
	if err = json.Unmarshal(body, &jobsList); err != nil {
		log.Printf("Failed to parse jobs response: %v", err)
		return sb.String(), nil
	}

	sb.WriteString(fmt.Sprintf("### Jobs (%d total)\n", jobsList.TotalCount))

	for _, job := range jobsList.Jobs {
		sb.WriteString(fmt.Sprintf("\n#### Job: %s\n", job.Name))
		sb.WriteString(fmt.Sprintf("- **Status**: %s, **Conclusion**: %s\n", job.Status, job.Conclusion))

		// Show steps
		if len(job.Steps) > 0 {
			sb.WriteString("**Steps**:\n")
			for _, step := range job.Steps {
				icon := "✓"
				if step.Conclusion == "failure" {
					icon = "✗"
				} else if step.Conclusion == "skipped" {
					icon = "○"
				} else if step.Status == "in_progress" {
					icon = "●"
				}
				sb.WriteString(fmt.Sprintf("  %s %d. %s (%s)\n", icon, step.Number, step.Name, step.Conclusion))
			}
		}

		// Fetch logs for failed jobs
		if job.Conclusion == "failure" {
			log.Printf("Fetching logs for failed job %d: %s", job.ID, job.Name)
			jobLink := &gitHubCheckLink{
				Owner: link.Owner,
				Repo:  link.Repo,
				RunID: link.RunID,
				JobID: fmt.Sprintf("%d", job.ID),
			}
			logs, err := downloadJobLogs(jobLink)
			if err != nil {
				log.Printf("Failed to fetch logs for job %d: %v", job.ID, err)
				sb.WriteString(fmt.Sprintf("\n*Failed to fetch logs: %v*\n", err))
			} else {
				sb.WriteString(fmt.Sprintf("\n**Logs**:\n```\n%s\n```\n", logs))
			}
		}
	}

	return sb.String(), nil
}

// fetchJobLogs gets the details and logs for a specific job
func fetchJobLogs(link *gitHubCheckLink) (string, error) {
	// Get job details first
	jobURL := fmt.Sprintf("%s/repos/%s/%s/actions/jobs/%s", githubAPIBaseURL, link.Owner, link.Repo, link.JobID)
	log.Printf("Fetching GitHub job details: %s", jobURL)

	body, err := githubAPIGet(jobURL)
	if err != nil {
		return "", fmt.Errorf("failed to fetch job details: %w", err)
	}

	var job gitHubJobResponse
	if err = json.Unmarshal(body, &job); err != nil {
		return "", fmt.Errorf("failed to parse job response: %w", err)
	}

	var sb strings.Builder
	sb.WriteString(fmt.Sprintf("## GitHub Actions Job: %s\n", job.Name))
	sb.WriteString(fmt.Sprintf("- **Status**: %s\n", job.Status))
	sb.WriteString(fmt.Sprintf("- **Conclusion**: %s\n", job.Conclusion))

	// Show steps
	if len(job.Steps) > 0 {
		sb.WriteString("\n**Steps**:\n")
		for _, step := range job.Steps {
			icon := "✓"
			if step.Conclusion == "failure" {
				icon = "✗"
			} else if step.Conclusion == "skipped" {
				icon = "○"
			} else if step.Status == "in_progress" {
				icon = "●"
			}
			sb.WriteString(fmt.Sprintf("  %s %d. %s (%s)\n", icon, step.Number, step.Name, step.Conclusion))
		}
	}

	// Fetch the logs
	logs, err := downloadJobLogs(link)
	if err != nil {
		log.Printf("Failed to download job logs: %v", err)
		sb.WriteString(fmt.Sprintf("\n*Failed to fetch logs: %v*\n", err))
	} else {
		sb.WriteString(fmt.Sprintf("\n**Logs**:\n```\n%s\n```\n", logs))
	}

	return sb.String(), nil
}

// downloadJobLogs downloads the plain text logs for a specific job
func downloadJobLogs(link *gitHubCheckLink) (string, error) {
	logsURL := fmt.Sprintf("%s/repos/%s/%s/actions/jobs/%s/logs", githubAPIBaseURL, link.Owner, link.Repo, link.JobID)
	log.Printf("Downloading job logs: %s", logsURL)

	req, err := http.NewRequest("GET", logsURL, nil)
	if err != nil {
		return "", fmt.Errorf("failed to create request: %w", err)
	}

	req.Header.Set("Accept", "application/vnd.github.v3+json")
	req.Header.Set("User-Agent", "azure-sdk-qa-bot")

	client := &http.Client{
		Timeout: 30 * time.Second,
		// Don't follow redirects automatically - we handle them
		CheckRedirect: func(req *http.Request, via []*http.Request) error {
			return http.ErrUseLastResponse
		},
	}

	resp, err := client.Do(req)
	if err != nil {
		return "", fmt.Errorf("failed to request logs: %w", err)
	}
	defer func() { _ = resp.Body.Close() }()

	// GitHub returns a 302 redirect to the actual log URL
	if resp.StatusCode == http.StatusFound {
		redirectURL := resp.Header.Get("Location")
		if redirectURL == "" {
			return "", fmt.Errorf("received redirect but no Location header")
		}
		log.Printf("Following redirect to log URL")
		var redirectReq *http.Request
		redirectReq, err = http.NewRequest("GET", redirectURL, nil)
		if err != nil {
			return "", fmt.Errorf("failed to create redirect request: %w", err)
		}

		// Close the original redirect response before reassigning resp
		_ = resp.Body.Close()

		// Use a new client that follows redirects for the blob storage URL
		redirectClient := &http.Client{Timeout: 30 * time.Second}
		resp, err = redirectClient.Do(redirectReq)
		if err != nil {
			return "", fmt.Errorf("failed to follow redirect: %w", err)
		}
		defer func() { _ = resp.Body.Close() }()
	}

	if resp.StatusCode != http.StatusOK {
		return "", fmt.Errorf("unexpected status code %d when fetching logs", resp.StatusCode)
	}

	// The response may be gzip-encoded
	var reader io.Reader = resp.Body
	if resp.Header.Get("Content-Encoding") == "gzip" {
		var gzReader *gzip.Reader
		gzReader, err = gzip.NewReader(resp.Body)
		if err != nil {
			return "", fmt.Errorf("failed to create gzip reader: %w", err)
		}
		defer func() { _ = gzReader.Close() }()
		reader = gzReader
	}

	// Read with size limit
	limitedReader := io.LimitReader(reader, int64(maxGitHubLogSize)+1)
	logBytes, err := io.ReadAll(limitedReader)
	if err != nil {
		return "", fmt.Errorf("failed to read logs: %w", err)
	}

	logText := string(logBytes)
	if len(logBytes) > maxGitHubLogSize {
		logText = logText[:maxGitHubLogSize] + "\n... [truncated]"
	}

	return logText, nil
}

// githubAPIGet makes a GET request to the public GitHub API (no authentication).
func githubAPIGet(url string) ([]byte, error) {
	req, err := http.NewRequest("GET", url, nil)
	if err != nil {
		return nil, fmt.Errorf("failed to create request: %w", err)
	}

	req.Header.Set("Accept", "application/vnd.github.v3+json")
	req.Header.Set("User-Agent", "azure-sdk-qa-bot")

	client := &http.Client{Timeout: 30 * time.Second}
	resp, err := client.Do(req)
	if err != nil {
		return nil, fmt.Errorf("request failed: %w", err)
	}
	defer func() { _ = resp.Body.Close() }()

	if resp.StatusCode != http.StatusOK {
		body, _ := io.ReadAll(resp.Body)
		return nil, fmt.Errorf("GitHub API returned status %d: %s", resp.StatusCode, string(body))
	}

	return io.ReadAll(resp.Body)
}
