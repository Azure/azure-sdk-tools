package utils

import (
	"archive/zip"
	"bytes"
	"context"
	"crypto/sha256"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"net/http"
	"regexp"
	"strings"
	"sync"
	"time"

	"github.com/Azure/azure-sdk-for-go/sdk/azidentity"
	"github.com/Azure/azure-sdk-for-go/sdk/security/keyvault/azkeys"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
)

// GitHub API base URL
const githubAPIBaseURL = "https://api.github.com"

// Max log size to return (200KB) to avoid overwhelming the LLM context
const maxGitHubLogSize = 200 * 1024

// GitHub check run / job status values
const (
	checkStatusCompleted  = "completed"
	checkStatusInProgress = "in_progress"
)

// GitHub check run conclusion values
const (
	conclusionFailure        = "failure"
	conclusionActionRequired = "action_required"
	conclusionTimedOut       = "timed_out"
	conclusionCancelled      = "cancelled"
	conclusionSkipped        = "skipped"
	conclusionNeutral        = "neutral"
)

// Well-known names / identifiers
const (
	githubActionsAppName     = "GitHub Actions"
	jobSummaryArtifactName   = "job-summary"
	defaultInstallationOwner = "Azure"
	githubAPIVersion         = "2022-11-28"
	checkRunsPerPage         = 100
	annotationsPerPage       = 50
	httpClientTimeout        = 30 * time.Second
	maxRetries               = 3
	retryDelay               = 1 * time.Second
)

// Pre-compiled regexes for URL parsing (compiled once at package init).
var (
	prRegex         = regexp.MustCompile(`^https?://github\.com/([^/]+)/([^/]+)/pull/(\d+)`)
	prCheckRunRegex = regexp.MustCompile(`^https?://github\.com/([^/]+)/([^/]+)/pull/\d+/checks\?check_run_id=(\d+)`)
	actionsRunRegex = regexp.MustCompile(`^https?://github\.com/([^/]+)/([^/]+)/actions/runs/(\d+)(?:/jobs?/(\d+))?`)
	checkRunRegex   = regexp.MustCompile(`^https?://github\.com/([^/]+)/([^/]+)/runs/(\d+)`)
)

// =====================================================================
// Singleton
// =====================================================================

var (
	githubClientOnce     sync.Once
	githubClientInstance *GitHubClient
)

// GetGitHubClient returns the singleton GitHubClient instance.
// The client is lazily initialised on first call and reused thereafter.
func GetGitHubClient() *GitHubClient {
	githubClientOnce.Do(func() {
		githubClientInstance = &GitHubClient{
			httpClient: &http.Client{Timeout: httpClientTimeout},
			userAgent:  "azure-sdk-qa-bot",
		}
	})
	return githubClientInstance
}

// =====================================================================
// GitHubClient
// =====================================================================

// GitHubClient encapsulates GitHub API access including GitHub App
// authentication via Azure Key Vault signing. Obtain an instance via
// GetGitHubClient().
type GitHubClient struct {
	httpClient *http.Client
	userAgent  string

	// Token cache (guarded by tokenMu)
	tokenMu  sync.Mutex
	token    string
	tokenExp time.Time
}

// =====================================================================
// Public API
// =====================================================================

// FetchCheckLogs fetches logs/details for a GitHub check run or Actions
//
//	run. Uses GitHub App authentication when configured.
func (g *GitHubClient) FetchCheckLogs(url string) (string, error) {
	startTime := time.Now()
	defer func() {
		log.Printf("FetchCheckLogs completed in %v", time.Since(startTime))
	}()

	link := parseGitHubCheckLink(url)
	if link == nil {
		return "", fmt.Errorf("not a valid GitHub check/actions URL: %s", url)
	}

	if link.CheckRunID != "" {
		return g.buildCheckRunSummary(link)
	}
	if link.JobID != "" {
		return g.buildJobLogsSummary(link)
	}
	if link.ActionsRunID != "" {
		return g.buildActionsRunSummary(link)
	}
	return "", fmt.Errorf("unable to determine GitHub check type from URL: %s", url)
}

// FetchPRChecks fetches all check runs for a GitHub PR and returns details
// of failed/problematic checks including their output, annotations, and logs.
func (g *GitHubClient) FetchPRChecks(url string) (string, error) {
	startTime := time.Now()
	defer func() {
		log.Printf("FetchPRChecks completed in %v", time.Since(startTime))
	}()

	prLink := parseGitHubPRLink(url)
	if prLink == nil {
		return "", fmt.Errorf("not a valid GitHub PR URL: %s", url)
	}
	return g.buildPRChecksSummary(prLink)
}

// IsGitHubCheckLink checks if a URL is a GitHub check run or GitHub Actions link.
func IsGitHubCheckLink(url string) bool {
	return parseGitHubCheckLink(url) != nil
}

// IsGitHubPRLink checks if a URL is a GitHub pull request link.
func IsGitHubPRLink(url string) bool {
	return parseGitHubPRLink(url) != nil
}

// ciRelatedKeywords are terms that signal CI-related questions.
var ciRelatedKeywords = []string{
	"check", "ci", "pipeline", "build", "fail", "error",
	"lint", "validation", "action", "workflow",
	"blocking", "merge", "spec-validation",
}

// IsCIRelatedIntention returns true if the recognised intention indicates the
// user is asking about CI checks, build failures, or pipeline issues.
func IsCIRelatedIntention(category string, question string) bool {
	combined := strings.ToLower(category + " " + question)
	for _, keyword := range ciRelatedKeywords {
		if strings.Contains(combined, keyword) {
			return true
		}
	}
	return false
}

func parseGitHubPRLink(url string) *model.GitHubPRLink {
	if matches := prRegex.FindStringSubmatch(url); len(matches) >= 4 {
		return &model.GitHubPRLink{
			Owner:    matches[1],
			Repo:     matches[2],
			PRNumber: matches[3],
		}
	}
	return nil
}

func parseGitHubCheckLink(url string) *model.GitHubCheckLink {
	// PR check run link: /pull/N/checks?check_run_id=N
	if matches := prCheckRunRegex.FindStringSubmatch(url); len(matches) >= 4 {
		return &model.GitHubCheckLink{
			Owner:      matches[1],
			Repo:       matches[2],
			CheckRunID: matches[3],
		}
	}

	if matches := actionsRunRegex.FindStringSubmatch(url); len(matches) >= 4 {
		link := &model.GitHubCheckLink{
			Owner:        matches[1],
			Repo:         matches[2],
			ActionsRunID: matches[3],
		}
		if len(matches) >= 5 && matches[4] != "" {
			link.JobID = matches[4]
		}
		return link
	}

	if matches := checkRunRegex.FindStringSubmatch(url); len(matches) >= 4 {
		return &model.GitHubCheckLink{
			Owner:      matches[1],
			Repo:       matches[2],
			CheckRunID: matches[3],
		}
	}
	return nil
}

// =====================================================================
// Internal: API helpers
// =====================================================================

// apiGet makes an authenticated GET request to the GitHub API with simple retry.
func (g *GitHubClient) apiGet(url string) ([]byte, error) {
	req, err := http.NewRequest("GET", url, nil)
	if err != nil {
		return nil, fmt.Errorf("failed to create request: %w", err)
	}

	req.Header.Set("Accept", "application/vnd.github.v3+json")
	req.Header.Set("User-Agent", g.userAgent)

	if token, tokenErr := g.getToken(); tokenErr == nil && token != "" {
		req.Header.Set("Authorization", "token "+token)
	} else if tokenErr != nil {
		log.Printf("GitHub App auth not available, using unauthenticated API: %v", tokenErr)
	}

	var lastErr error
	for attempt := 0; attempt <= maxRetries; attempt++ {
		if attempt > 0 {
			log.Printf("Retrying request to %s (attempt %d/%d)", url, attempt, maxRetries)
			time.Sleep(retryDelay)
		}

		var resp *http.Response
		resp, err = g.httpClient.Do(req)
		if err != nil {
			lastErr = fmt.Errorf("request failed: %w", err)
			continue
		}

		if resp.StatusCode != http.StatusOK {
			body, _ := io.ReadAll(resp.Body)
			_ = resp.Body.Close()
			lastErr = fmt.Errorf("GitHub API returned status %d for %s: %s", resp.StatusCode, url, string(body))
			continue
		}

		data, readErr := io.ReadAll(resp.Body)
		_ = resp.Body.Close()
		if readErr != nil {
			return nil, fmt.Errorf("failed to read response body from %s: %w", url, readErr)
		}
		return data, nil
	}

	return nil, fmt.Errorf("request to %s failed after %d retries: %w", url, maxRetries, lastErr)
}

// apiGetJSON makes an authenticated GET request and unmarshals the JSON response into T.
func apiGetJSON[T any](g *GitHubClient, url string) (T, error) {
	var zero T
	body, err := g.apiGet(url)
	if err != nil {
		return zero, err
	}
	var result T
	if err := json.Unmarshal(body, &result); err != nil {
		return zero, fmt.Errorf("failed to parse response from %s: %w", url, err)
	}
	return result, nil
}

// =====================================================================
// Internal: simple API fetchers (reusable building blocks)
// =====================================================================

// fetchPR fetches the details of a GitHub pull request.
func (g *GitHubClient) fetchPR(owner, repo, prNumber string) (model.GitHubPRResponse, error) {
	url := fmt.Sprintf("%s/repos/%s/%s/pulls/%s", githubAPIBaseURL, owner, repo, prNumber)
	log.Printf("Fetching GitHub PR details: %s", url)
	return apiGetJSON[model.GitHubPRResponse](g, url)
}

// fetchPRReviews fetches the reviews submitted on a pull request.
func (g *GitHubClient) fetchPRReviews(owner, repo, prNumber string) ([]model.GitHubReview, error) {
	url := fmt.Sprintf("%s/repos/%s/%s/pulls/%s/reviews?per_page=%d", githubAPIBaseURL, owner, repo, prNumber, checkRunsPerPage)
	log.Printf("Fetching GitHub PR reviews: %s", url)
	return apiGetJSON[[]model.GitHubReview](g, url)
}

// fetchNextStepsComment returns the body of the "Next Steps to Merge" comment for a
// spec-repo PR, or an empty string when not applicable or not found. The comment is only
// posted by automation in the spec repos, so the call is skipped for other repos.
func (g *GitHubClient) fetchNextStepsComment(owner, repo, prNumber string) string {
	if !IsSpecRepo(owner, repo) {
		return ""
	}
	url := fmt.Sprintf("%s/repos/%s/%s/issues/%s/comments?per_page=%d", githubAPIBaseURL, owner, repo, prNumber, checkRunsPerPage)
	log.Printf("Fetching GitHub PR comments: %s", url)
	comments, err := apiGetJSON[[]model.GitHubIssueComment](g, url)
	if err != nil {
		log.Printf("Failed to fetch PR comments: %v", err)
		return ""
	}
	// Return the most recent comment that contains the marker.
	for i := len(comments) - 1; i >= 0; i-- {
		if strings.Contains(comments[i].Body, nextStepsMarker) {
			return comments[i].Body
		}
	}
	return ""
}

// fetchCommitCheckRuns fetches all check runs for a given commit SHA.
func (g *GitHubClient) fetchCommitCheckRuns(owner, repo, sha string) (model.GitHubCheckRunsListResponse, error) {
	url := fmt.Sprintf("%s/repos/%s/%s/commits/%s/check-runs?per_page=%d", githubAPIBaseURL, owner, repo, sha, checkRunsPerPage)
	log.Printf("Fetching check runs for SHA %s", sha)
	return apiGetJSON[model.GitHubCheckRunsListResponse](g, url)
}

// fetchCheckRunAnnotations fetches annotations for a single check run.
func (g *GitHubClient) fetchCheckRunAnnotations(owner, repo string, checkRunID int64) []model.GitHubAnnotation {
	apiURL := fmt.Sprintf("%s/repos/%s/%s/check-runs/%d/annotations?per_page=%d",
		githubAPIBaseURL, owner, repo, checkRunID, annotationsPerPage)
	log.Printf("Fetching annotations for check run %d: %s", checkRunID, apiURL)

	annotations, err := apiGetJSON[[]model.GitHubAnnotation](g, apiURL)
	if err != nil {
		log.Printf("Failed to fetch annotations for check run %d: %v", checkRunID, err)
		return nil
	}
	return annotations
}

// fetchRunSummaryArtifact fetches the job summary (written via $GITHUB_STEP_SUMMARY)
// by downloading the "job-summary" artifact from the workflow run.
// The check-runs API does not expose this content (output fields are null for Actions jobs).
func (g *GitHubClient) fetchRunSummaryArtifact(owner, repo, actionsRunID string) string {
	artifactsURL := fmt.Sprintf("%s/repos/%s/%s/actions/runs/%s/artifacts", githubAPIBaseURL, owner, repo, actionsRunID)
	log.Printf("Fetching artifacts for run %s: %s", actionsRunID, artifactsURL)

	artifactsList, err := apiGetJSON[model.GitHubArtifactsListResponse](g, artifactsURL)
	if err != nil {
		log.Printf("Failed to fetch artifacts for run %s: %v", actionsRunID, err)
		return ""
	}

	for _, a := range artifactsList.Artifacts {
		if a.Name == jobSummaryArtifactName {
			log.Printf("Found %s artifact (id=%d, size=%d), downloading...", jobSummaryArtifactName, a.ID, a.SizeInBytes)
			return g.downloadArtifactContent(a.ArchiveDownloadURL)
		}
	}

	log.Printf("No job-summary artifact found for run %s", actionsRunID)
	return ""
}

// downloadArtifactContent downloads a GitHub artifact zip and extracts its text content.
func (g *GitHubClient) downloadArtifactContent(archiveURL string) string {
	data, err := g.apiGet(archiveURL)
	if err != nil {
		log.Printf("Failed to download artifact: %v", err)
		return ""
	}

	zipReader, err := zip.NewReader(bytes.NewReader(data), int64(len(data)))
	if err != nil {
		log.Printf("Failed to open artifact zip: %v", err)
		return ""
	}

	var sb strings.Builder
	for _, f := range zipReader.File {
		rc, err := f.Open()
		if err != nil {
			log.Printf("Failed to open file %s in artifact zip: %v", f.Name, err)
			continue
		}
		content, err := io.ReadAll(rc)
		_ = rc.Close()
		if err != nil {
			log.Printf("Failed to read file %s in artifact zip: %v", f.Name, err)
			continue
		}
		sb.WriteString(strings.TrimSpace(string(content)))
		sb.WriteString("\n")
	}

	summary := strings.TrimSpace(sb.String())
	if len(summary) > maxGitHubLogSize {
		summary = summary[:maxGitHubLogSize] + "\n... [truncated]"
	}
	return summary
}

// downloadJobLogs downloads the raw logs for a GitHub Actions job.
func (g *GitHubClient) downloadJobLogs(link *model.GitHubCheckLink) (string, error) {
	logsURL := fmt.Sprintf("%s/repos/%s/%s/actions/jobs/%s/logs", githubAPIBaseURL, link.Owner, link.Repo, link.JobID)
	log.Printf("Downloading job logs: %s", logsURL)

	data, err := g.apiGet(logsURL)
	if err != nil {
		return "", fmt.Errorf("failed to download job logs: %w", err)
	}

	logText := string(data)
	if len(data) > maxGitHubLogSize {
		logText = logText[:maxGitHubLogSize] + "\n... [truncated]"
	}
	return logText, nil
}

// =====================================================================
// Internal: summary builders (fetch data + format markdown)
// =====================================================================

// buildPRChecksSummary fetches all PR check runs and formats a summary of failed checks.
// writePRReviewSummary appends review status, the responsible reviewer set, a conservative
// merge-readiness verdict, and (for spec repos only) the "Next Steps to Merge" comment to
// the PR summary. All fetches are best-effort: failures degrade gracefully rather than
// aborting the overall summary.
func (g *GitHubClient) writePRReviewSummary(sb *strings.Builder, prLink *model.GitHubPRLink, pr model.GitHubPRResponse, cls checkRunClassification) {
	labelNames := make([]string, 0, len(pr.Labels))
	for _, l := range pr.Labels {
		labelNames = append(labelNames, l.Name)
	}
	_, blockingLabels := HasBlockingLabels(prLink.Owner, prLink.Repo, labelNames)

	reviews, err := g.fetchPRReviews(prLink.Owner, prLink.Repo, prLink.PRNumber)
	if err != nil {
		log.Printf("Failed to fetch PR reviews: %v", err)
	}
	decision := LatestReviewDecision(reviews)
	reviewers := BuildReviewerSet(pr.RequestedReviewers, reviews)
	readiness := AssessMergeReadiness(len(cls.failed), cls.pending, decision, blockingLabels)

	sb.WriteString("### Review Status\n")
	if pr.Base.Ref != "" {
		fmt.Fprintf(sb, "- **Target branch**: %s\n", pr.Base.Ref)
	}
	fmt.Fprintf(sb, "- **Review decision**: %s\n", decision)
	if pr.MergeableState != "" {
		fmt.Fprintf(sb, "- **Mergeable state**: %s\n", pr.MergeableState)
	}
	if len(blockingLabels) > 0 {
		fmt.Fprintf(sb, "- **Blocking labels**: %s\n", strings.Join(blockingLabels, ", "))
	}
	sb.WriteString("\n")

	sb.WriteString("### Reviewers\n")
	if len(reviewers) == 0 {
		sb.WriteString("- No reviewers requested yet.\n")
	} else {
		for _, r := range reviewers {
			if r.HTMLURL != "" {
				fmt.Fprintf(sb, "- [@%s](%s)\n", r.Login, r.HTMLURL)
			} else {
				fmt.Fprintf(sb, "- @%s\n", r.Login)
			}
		}
	}
	sb.WriteString("\n")

	if readiness.Ready {
		sb.WriteString("### Merge Readiness: likely ready for final review/approval\n")
		sb.WriteString("No failing checks, pending checks, blocking labels, or requested changes were detected.\n\n")
	} else {
		sb.WriteString("### Merge Readiness: not ready\n")
		sb.WriteString("Blockers:\n")
		for _, reason := range readiness.Reasons {
			fmt.Fprintf(sb, "- %s\n", reason)
		}
		sb.WriteString("\n")
	}

	if comment := g.fetchNextStepsComment(prLink.Owner, prLink.Repo, prLink.PRNumber); comment != "" {
		const maxExcerpt = 4000
		excerpt := comment
		if len(excerpt) > maxExcerpt {
			excerpt = excerpt[:maxExcerpt] + "\n...(truncated)"
		}
		sb.WriteString("### Next Steps to Merge (from PR comment)\n")
		sb.WriteString(excerpt)
		sb.WriteString("\n\n")
	}
}

func (g *GitHubClient) buildPRChecksSummary(prLink *model.GitHubPRLink) (string, error) {
	pr, err := g.fetchPR(prLink.Owner, prLink.Repo, prLink.PRNumber)
	if err != nil {
		return "", fmt.Errorf("failed to fetch PR details: %w", err)
	}

	var sb strings.Builder
	fmt.Fprintf(&sb, "## GitHub PR #%d: %s\n", pr.Number, pr.Title)
	fmt.Fprintf(&sb, "- **State**: %s\n", pr.State)
	fmt.Fprintf(&sb, "- **URL**: %s\n\n", pr.HTMLURL)

	checkRunsList, err := g.fetchCommitCheckRuns(prLink.Owner, prLink.Repo, pr.Head.SHA)
	if err != nil {
		log.Printf("Failed to fetch check runs: %v", err)
		sb.WriteString("*Failed to fetch check runs*\n")
		return sb.String(), nil
	}

	cls := classifyCheckRuns(checkRunsList.CheckRuns)

	fmt.Fprintf(&sb, "### Checks Summary: %d total, %d failed, %d passed, %d skipped, %d pending\n\n",
		checkRunsList.TotalCount, len(cls.failed), cls.passed, cls.skipped, cls.pending)

	g.writePRReviewSummary(&sb, prLink, pr, cls)

	if len(cls.failed) == 0 {
		return sb.String(), nil
	}

	fmt.Fprintf(&sb, "### Failed Checks (%d)\n", len(cls.failed))

	runIDs, externalCRs := partitionFailedChecks(cls.failed)

	for _, runID := range runIDs {
		link := &model.GitHubCheckLink{
			Owner:        prLink.Owner,
			Repo:         prLink.Repo,
			ActionsRunID: runID,
		}
		if summary, buildErr := g.buildActionsRunSummary(link); buildErr == nil {
			sb.WriteString("\n")
			sb.WriteString(summary)
		} else {
			log.Printf("Failed to build Actions run summary for run %s: %v", runID, buildErr)
		}
	}

	for _, cr := range externalCRs {
		link := &model.GitHubCheckLink{
			Owner:      prLink.Owner,
			Repo:       prLink.Repo,
			CheckRunID: fmt.Sprintf("%d", cr.ID),
		}
		if summary, buildErr := g.buildCheckRunSummary(link); buildErr == nil {
			sb.WriteString("\n")
			sb.WriteString(summary)
		} else {
			log.Printf("Failed to build check run summary for %s (id=%d): %v", cr.Name, cr.ID, buildErr)
		}
	}

	return sb.String(), nil
}

// buildCheckRunSummary fetches a check run and formats its details as a markdown summary.
func (g *GitHubClient) buildCheckRunSummary(link *model.GitHubCheckLink) (string, error) {
	apiURL := fmt.Sprintf("%s/repos/%s/%s/check-runs/%s", githubAPIBaseURL, link.Owner, link.Repo, link.CheckRunID)
	log.Printf("Fetching GitHub check run details: %s", apiURL)

	checkRun, err := apiGetJSON[model.GitHubCheckRunResponse](g, apiURL)
	if err != nil {
		return "", fmt.Errorf("failed to fetch check run details: %w", err)
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

	annotations := g.fetchCheckRunAnnotations(link.Owner, link.Repo, checkRun.ID)
	if len(annotations) > 0 {
		sb.WriteString("### Annotations\n")
		writeAnnotations(&sb, annotations)
	}

	return sb.String(), nil
}

// buildActionsRunSummary fetches an Actions workflow run and formats its details as a markdown summary.
func (g *GitHubClient) buildActionsRunSummary(link *model.GitHubCheckLink) (string, error) {
	runURL := fmt.Sprintf("%s/repos/%s/%s/actions/runs/%s", githubAPIBaseURL, link.Owner, link.Repo, link.ActionsRunID)
	log.Printf("Fetching GitHub Actions run details: %s", runURL)

	run, err := apiGetJSON[model.GitHubActionsRunResponse](g, runURL)
	if err != nil {
		return "", fmt.Errorf("failed to fetch actions run details: %w", err)
	}

	var sb strings.Builder
	sb.WriteString(fmt.Sprintf("## GitHub Actions Run: %s\n", run.Name))
	sb.WriteString(fmt.Sprintf("- **Status**: %s\n", run.Status))
	sb.WriteString(fmt.Sprintf("- **Conclusion**: %s\n", run.Conclusion))
	sb.WriteString(fmt.Sprintf("- **URL**: %s\n\n", run.HTMLURL))

	jobsURL := fmt.Sprintf("%s/repos/%s/%s/actions/runs/%s/jobs", githubAPIBaseURL, link.Owner, link.Repo, link.ActionsRunID)
	jobsList, err := apiGetJSON[model.GitHubJobsListResponse](g, jobsURL)
	if err != nil {
		log.Printf("Failed to fetch jobs for actions run: %v", err)
		return sb.String(), nil
	}

	sb.WriteString(fmt.Sprintf("### Jobs (%d total)\n", jobsList.TotalCount))

	for _, job := range jobsList.Jobs {
		sb.WriteString(fmt.Sprintf("\n#### Job: %s\n", job.Name))
		sb.WriteString(fmt.Sprintf("- **Status**: %s, **Conclusion**: %s\n", job.Status, job.Conclusion))
		if job.Conclusion == conclusionFailure && len(job.Steps) > 0 {
			sb.WriteString("Steps:\n")
			writeSteps(&sb, job.Steps)
		}
	}

	// Fetch run-level summary artifact (written via $GITHUB_STEP_SUMMARY)
	if summary := g.fetchRunSummaryArtifact(link.Owner, link.Repo, link.ActionsRunID); summary != "" {
		sb.WriteString(fmt.Sprintf("\n### Run Summary\n%s\n", summary))
	}

	return sb.String(), nil
}

// buildJobLogsSummary fetches job details and logs, and formats them as a markdown summary.
func (g *GitHubClient) buildJobLogsSummary(link *model.GitHubCheckLink) (string, error) {
	jobURL := fmt.Sprintf("%s/repos/%s/%s/actions/jobs/%s", githubAPIBaseURL, link.Owner, link.Repo, link.JobID)
	log.Printf("Fetching GitHub job details: %s", jobURL)

	job, err := apiGetJSON[model.GitHubJobResponse](g, jobURL)
	if err != nil {
		return "", fmt.Errorf("failed to fetch job details: %w", err)
	}

	var sb strings.Builder
	sb.WriteString(fmt.Sprintf("## GitHub Actions Job: %s\n", job.Name))
	sb.WriteString(fmt.Sprintf("- **Status**: %s\n", job.Status))
	sb.WriteString(fmt.Sprintf("- **Conclusion**: %s\n", job.Conclusion))

	if len(job.Steps) > 0 {
		sb.WriteString("\n**Steps**:\n")
		writeSteps(&sb, job.Steps)
	}

	logs, dlErr := g.downloadJobLogs(link)
	if dlErr != nil {
		log.Printf("Failed to download job logs: %v", dlErr)
		sb.WriteString(fmt.Sprintf("\n*Failed to fetch logs: %v*\n", dlErr))
	} else {
		sb.WriteString(fmt.Sprintf("\n**Logs**:\n```\n%s\n```\n", logs))
	}

	return sb.String(), nil
}

// =====================================================================
// Internal: check run classification and formatting helpers
// =====================================================================

// checkRunClassification holds the classified results of a set of check runs.
type checkRunClassification struct {
	failed  []model.GitHubCheckRunResponse
	passed  int
	skipped int
	pending int
}

// classifyCheckRuns categorises check runs into failed, passed, skipped, and pending buckets.
func classifyCheckRuns(checkRuns []model.GitHubCheckRunResponse) checkRunClassification {
	var c checkRunClassification
	for _, cr := range checkRuns {
		switch {
		case cr.Status != checkStatusCompleted:
			c.pending++
		case cr.Conclusion == conclusionFailure || cr.Conclusion == conclusionActionRequired ||
			cr.Conclusion == conclusionTimedOut || cr.Conclusion == conclusionCancelled:
			c.failed = append(c.failed, cr)
		case cr.Conclusion == conclusionSkipped || cr.Conclusion == conclusionNeutral:
			c.skipped++
		default:
			c.passed++
		}
	}
	return c
}

// IsSpecRepo reports whether owner/repo is one of the Azure REST API spec repositories.
// Spec-only behavior (the "Next Steps to Merge" comment and ARM/breaking-change labels)
// is gated on this.
func IsSpecRepo(owner, repo string) bool {
	o := strings.ToLower(owner)
	r := strings.ToLower(repo)
	return o == "azure" && (r == "azure-rest-api-specs" || r == "azure-rest-api-specs-pr")
}

// nextStepsMarker identifies the automated spec-repo merge-guidance comment.
const nextStepsMarker = "Next Steps to Merge"

// specBlockingLabels are labels that block a spec PR from merging (normalized: lowercase,
// spaces removed). Only applied for spec repos.
var specBlockingLabels = map[string]bool{
	"armreview":                    true,
	"breakingchangereviewrequired": true,
	"waitforarmfeedback":           true,
}

// commonBlockingLabels block merging in any repository.
var commonBlockingLabels = map[string]bool{
	"do-not-merge": true,
	"donotmerge":   true,
}

// normalizeLabel lowercases a label and removes spaces for tolerant matching.
func normalizeLabel(l string) string {
	return strings.ReplaceAll(strings.ToLower(l), " ", "")
}

// HasBlockingLabels returns the labels on the PR that block merging, applying spec-only
// labels only when owner/repo is a spec repo.
func HasBlockingLabels(owner, repo string, labels []string) (bool, []string) {
	spec := IsSpecRepo(owner, repo)
	var blocking []string
	for _, l := range labels {
		key := normalizeLabel(l)
		if commonBlockingLabels[key] || (spec && specBlockingLabels[key]) {
			blocking = append(blocking, l)
		}
	}
	return len(blocking) > 0, blocking
}

// BuildReviewerSet returns the deduplicated set of responsible reviewers for a PR: the
// union of currently requested reviewers and users who have submitted a meaningful review
// (approved, requested changes, or dismissed). A reviewer drops off requested_reviewers
// once they submit a review, so the union is required to capture both.
func BuildReviewerSet(requested []model.GitHubUser, reviews []model.GitHubReview) []model.GitHubUser {
	seen := make(map[string]bool)
	var out []model.GitHubUser
	add := func(u model.GitHubUser) {
		login := strings.ToLower(u.Login)
		if login == "" || seen[login] {
			return
		}
		seen[login] = true
		out = append(out, u)
	}
	for _, u := range requested {
		add(u)
	}
	for _, rv := range reviews {
		switch strings.ToUpper(rv.State) {
		case "APPROVED", "CHANGES_REQUESTED", "DISMISSED":
			add(rv.User)
		}
	}
	return out
}

// LatestReviewDecision aggregates per-user latest review state into an overall decision:
// "changes_requested" if any reviewer's latest state requests changes, otherwise
// "approved" if at least one approval exists, otherwise "review_required".
// Reviews from the GitHub API are chronological, so the last state seen per user wins.
func LatestReviewDecision(reviews []model.GitHubReview) string {
	latest := make(map[string]string)
	for _, rv := range reviews {
		st := strings.ToUpper(rv.State)
		if st == "APPROVED" || st == "CHANGES_REQUESTED" || st == "DISMISSED" {
			latest[strings.ToLower(rv.User.Login)] = st
		}
	}
	changes := false
	approvals := 0
	for _, st := range latest {
		switch st {
		case "CHANGES_REQUESTED":
			changes = true
		case "APPROVED":
			approvals++
		}
	}
	if changes {
		return "changes_requested"
	}
	if approvals > 0 {
		return "approved"
	}
	return "review_required"
}

// MergeReadiness describes whether a PR looks ready for final review/approval, and if not,
// the blockers preventing it.
type MergeReadiness struct {
	Ready   bool
	Reasons []string
}

// AssessMergeReadiness conservatively determines whether a PR is ready. It is ready only
// when there are no failing checks, no pending checks, no requested changes, and no
// blocking labels.
func AssessMergeReadiness(failedChecks, pendingChecks int, decision string, blockingLabels []string) MergeReadiness {
	var reasons []string
	if failedChecks > 0 {
		reasons = append(reasons, fmt.Sprintf("%d failing check(s)", failedChecks))
	}
	if pendingChecks > 0 {
		reasons = append(reasons, fmt.Sprintf("%d pending check(s)", pendingChecks))
	}
	if decision == "changes_requested" {
		reasons = append(reasons, "a reviewer requested changes")
	}
	if len(blockingLabels) > 0 {
		reasons = append(reasons, "blocking label(s): "+strings.Join(blockingLabels, ", "))
	}
	return MergeReadiness{Ready: len(reasons) == 0, Reasons: reasons}
}

// partitionFailedChecks separates failed check runs into deduplicated GitHub Actions
// run IDs and external (non-Actions) check runs.
func partitionFailedChecks(failed []model.GitHubCheckRunResponse) (runIDs []string, externalCRs []model.GitHubCheckRunResponse) {
	seenRunIDs := make(map[string]bool)
	for _, cr := range failed {
		runID := extractActionsRunID(cr)
		if runID == "" {
			externalCRs = append(externalCRs, cr)
			continue
		}
		if !seenRunIDs[runID] {
			seenRunIDs[runID] = true
			runIDs = append(runIDs, runID)
		}
	}
	return
}

// extractActionsRunID returns the Actions run ID from a GitHub Actions check run,
// or empty string if the check run is not a GitHub Actions job.
func extractActionsRunID(cr model.GitHubCheckRunResponse) string {
	if cr.App.Name != githubActionsAppName {
		return ""
	}
	if matches := actionsRunRegex.FindStringSubmatch(cr.HTMLURL); len(matches) >= 4 {
		return matches[3]
	}
	return ""
}

// =====================================================================
// Internal: GitHub App authentication
// =====================================================================

// getToken returns a cached or fresh GitHub App installation token.
func (g *GitHubClient) getToken() (string, error) {
	g.tokenMu.Lock()
	defer g.tokenMu.Unlock()

	if g.token != "" && time.Now().Before(g.tokenExp.Add(-5*time.Minute)) {
		return g.token, nil
	}

	if config.AppConfig == nil {
		return "", fmt.Errorf("app configuration not initialized")
	}
	if config.Credential == nil {
		return "", fmt.Errorf("azure credential not initialized")
	}

	appID := config.AppConfig.GITHUB_APP_ID
	keyName := config.AppConfig.GITHUB_APP_KEY_NAME
	vaultURL := config.AppConfig.GITHUB_APP_KEYVAULT_URL
	owner := config.AppConfig.GITHUB_APP_INSTALLATION_OWNER

	if appID == "" || keyName == "" || vaultURL == "" {
		return "", fmt.Errorf("GitHub App configuration incomplete: GITHUB_APP_ID, GITHUB_APP_KEY_NAME, and GITHUB_APP_KEYVAULT_URL are required")
	}
	if owner == "" {
		owner = defaultInstallationOwner
	}

	jwt, err := g.createAppJWT(vaultURL, keyName, appID)
	if err != nil {
		return "", fmt.Errorf("failed to create GitHub App JWT: %w", err)
	}

	installationID, err := g.findInstallationID(jwt, owner)
	if err != nil {
		return "", fmt.Errorf("failed to find installation ID for owner '%s': %w", owner, err)
	}
	log.Printf("GitHub App installation ID for '%s': %d", owner, installationID)

	token, expiresAt, err := g.createInstallationToken(jwt, installationID)
	if err != nil {
		return "", fmt.Errorf("failed to create installation token: %w", err)
	}

	g.token = token
	g.tokenExp = expiresAt
	log.Printf("GitHub App installation token obtained, expires at %s", expiresAt.Format(time.RFC3339))
	return token, nil
}

func (g *GitHubClient) createAppJWT(vaultURL, keyName, appID string) (string, error) {
	header := map[string]string{"alg": "RS256", "typ": "JWT"}
	now := time.Now().Unix()
	payload := map[string]interface{}{
		"iat": now - 10,
		"exp": now + 600,
		"iss": appID,
	}

	headerJSON, err := json.Marshal(header)
	if err != nil {
		return "", fmt.Errorf("failed to marshal JWT header: %w", err)
	}
	payloadJSON, err := json.Marshal(payload)
	if err != nil {
		return "", fmt.Errorf("failed to marshal JWT payload: %w", err)
	}

	encodedHeader := base64URLEncode(headerJSON)
	encodedPayload := base64URLEncode(payloadJSON)
	unsignedToken := encodedHeader + "." + encodedPayload

	digest := sha256.Sum256([]byte(unsignedToken))

	cred, err := azidentity.NewManagedIdentityCredential(&azidentity.ManagedIdentityCredentialOptions{
		ID: azidentity.ClientID(config.GetBotClientID()),
	})
	if err != nil {
		return "", fmt.Errorf("failed to create managed identity credential: %w", err)
	}
	client, err := azkeys.NewClient(vaultURL, cred, nil)
	if err != nil {
		return "", fmt.Errorf("failed to create Key Vault keys client: %w", err)
	}

	algo := azkeys.SignatureAlgorithmRS256
	signResult, err := client.Sign(context.TODO(), keyName, "", azkeys.SignParameters{
		Algorithm: &algo,
		Value:     digest[:],
	}, nil)
	if err != nil {
		return "", fmt.Errorf("failed to sign JWT with Key Vault: %w", err)
	}

	signature := base64URLEncode(signResult.Result)
	return unsignedToken + "." + signature, nil
}

func (g *GitHubClient) findInstallationID(jwt, owner string) (int64, error) {
	req, err := http.NewRequest("GET", githubAPIBaseURL+"/app/installations", nil)
	if err != nil {
		return 0, fmt.Errorf("failed to create request: %w", err)
	}
	req.Header.Set("Authorization", "Bearer "+jwt)
	req.Header.Set("Accept", "application/vnd.github+json")
	req.Header.Set("User-Agent", g.userAgent)
	req.Header.Set("X-GitHub-Api-Version", githubAPIVersion)

	resp, err := g.httpClient.Do(req)
	if err != nil {
		return 0, fmt.Errorf("failed to list installations: %w", err)
	}
	defer func() { _ = resp.Body.Close() }()

	if resp.StatusCode != http.StatusOK {
		body, _ := io.ReadAll(resp.Body)
		return 0, fmt.Errorf("GitHub API returned status %d listing installations: %s", resp.StatusCode, string(body))
	}

	var installations []struct {
		ID      int64 `json:"id"`
		Account struct {
			Login string `json:"login"`
		} `json:"account"`
	}
	if err := json.NewDecoder(resp.Body).Decode(&installations); err != nil {
		return 0, fmt.Errorf("failed to decode installations response: %w", err)
	}

	for _, inst := range installations {
		if strings.EqualFold(inst.Account.Login, owner) {
			return inst.ID, nil
		}
	}
	return 0, fmt.Errorf("no GitHub App installation found for owner '%s'", owner)
}

func (g *GitHubClient) createInstallationToken(jwt string, installationID int64) (string, time.Time, error) {
	url := fmt.Sprintf("%s/app/installations/%d/access_tokens", githubAPIBaseURL, installationID)
	req, err := http.NewRequest("POST", url, strings.NewReader("{}"))
	if err != nil {
		return "", time.Time{}, fmt.Errorf("failed to create request: %w", err)
	}
	req.Header.Set("Authorization", "Bearer "+jwt)
	req.Header.Set("Accept", "application/vnd.github+json")
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("User-Agent", g.userAgent)
	req.Header.Set("X-GitHub-Api-Version", githubAPIVersion)

	resp, err := g.httpClient.Do(req)
	if err != nil {
		return "", time.Time{}, fmt.Errorf("failed to request installation token: %w", err)
	}
	defer func() { _ = resp.Body.Close() }()

	if resp.StatusCode != http.StatusCreated {
		body, _ := io.ReadAll(resp.Body)
		return "", time.Time{}, fmt.Errorf("GitHub API returned status %d creating installation token: %s", resp.StatusCode, string(body))
	}

	var result struct {
		Token     string `json:"token"`
		ExpiresAt string `json:"expires_at"`
	}
	if err = json.NewDecoder(resp.Body).Decode(&result); err != nil {
		return "", time.Time{}, fmt.Errorf("failed to decode token response: %w", err)
	}

	if result.Token == "" {
		return "", time.Time{}, fmt.Errorf("empty token in GitHub response")
	}

	expiresAt, err := time.Parse(time.RFC3339, result.ExpiresAt)
	if err != nil {
		expiresAt = time.Now().Add(1 * time.Hour)
	}
	return result.Token, expiresAt, nil
}

// =====================================================================
// Small helpers
// =====================================================================

func base64URLEncode(data []byte) string {
	return strings.TrimRight(base64.URLEncoding.EncodeToString(data), "=")
}

// writeAnnotations appends formatted annotation lines to sb.
func writeAnnotations(sb *strings.Builder, annotations []model.GitHubAnnotation) {
	for _, a := range annotations {
		fmt.Fprintf(sb, "- **[%s]** %s (line %d", a.AnnotationLevel, a.Path, a.StartLine)
		if a.EndLine != a.StartLine {
			fmt.Fprintf(sb, "-%d", a.EndLine)
		}
		fmt.Fprintf(sb, "): %s", a.Message)
		if a.Title != "" {
			fmt.Fprintf(sb, " \u2014 %s", a.Title)
		}
		sb.WriteString("\n")
	}
}

// writeSteps appends formatted step lines to sb.
func writeSteps(sb *strings.Builder, steps []struct {
	Name       string `json:"name"`
	Status     string `json:"status"`
	Conclusion string `json:"conclusion"`
	Number     int    `json:"number"`
}) {
	for _, step := range steps {
		icon := stepIcon(step.Status, step.Conclusion)
		fmt.Fprintf(sb, "  %s %d. %s (%s)\n", icon, step.Number, step.Name, step.Conclusion)
	}
}

func stepIcon(status, conclusion string) string {
	switch {
	case conclusion == conclusionFailure:
		return "✗"
	case conclusion == conclusionSkipped:
		return "○"
	case status == checkStatusInProgress:
		return "●"
	default:
		return "✓"
	}
}
