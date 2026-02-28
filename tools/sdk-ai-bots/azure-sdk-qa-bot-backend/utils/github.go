package utils

import (
	"archive/zip"
	"bytes"
	"compress/gzip"
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
			httpClient: &http.Client{Timeout: 30 * time.Second},
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
		return g.fetchCheckRunDetails(link)
	}
	if link.JobID != "" {
		return g.fetchJobLogs(link)
	}
	if link.ActionsRunID != "" {
		return g.fetchActionsRunDetails(link)
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
	return g.fetchPRCheckRuns(prLink)
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

// apiGet makes an authenticated GET request to the GitHub API.
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

	resp, err := g.httpClient.Do(req)
	if err != nil {
		return nil, fmt.Errorf("request failed: %w", err)
	}
	defer func() { _ = resp.Body.Close() }()

	if resp.StatusCode != http.StatusOK {
		body, _ := io.ReadAll(resp.Body)
		return nil, fmt.Errorf("GitHub API returned status %d for %s: %s", resp.StatusCode, url, string(body))
	}
	return io.ReadAll(resp.Body)
}

// =====================================================================
// Internal: check / PR / job fetchers
// =====================================================================

func (g *GitHubClient) fetchPRCheckRuns(prLink *model.GitHubPRLink) (string, error) {
	prURL := fmt.Sprintf("%s/repos/%s/%s/pulls/%s", githubAPIBaseURL, prLink.Owner, prLink.Repo, prLink.PRNumber)
	log.Printf("Fetching GitHub PR details: %s", prURL)

	body, err := g.apiGet(prURL)
	if err != nil {
		return "", fmt.Errorf("failed to fetch PR details: %w", err)
	}

	var pr model.GitHubPRResponse
	if err = json.Unmarshal(body, &pr); err != nil {
		return "", fmt.Errorf("failed to parse PR response: %w", err)
	}

	var sb strings.Builder
	sb.WriteString(fmt.Sprintf("## GitHub PR #%d: %s\n", pr.Number, pr.Title))
	sb.WriteString(fmt.Sprintf("- **State**: %s\n", pr.State))
	sb.WriteString(fmt.Sprintf("- **URL**: %s\n", pr.HTMLURL))

	runsURL := fmt.Sprintf("%s/repos/%s/%s/actions/runs?head_sha=%s&per_page=100",
		githubAPIBaseURL, prLink.Owner, prLink.Repo, pr.Head.SHA)
	log.Printf("Fetching actions runs for SHA %s", pr.Head.SHA)

	body, err = g.apiGet(runsURL)
	if err != nil {
		log.Printf("Failed to fetch actions runs: %v", err)
		sb.WriteString("*Failed to fetch actions runs*\n")
		return sb.String(), nil
	}

	var runsList model.GitHubActionsRunsListResponse
	if err := json.Unmarshal(body, &runsList); err != nil {
		log.Printf("Failed to parse actions runs response: %v", err)
		sb.WriteString("*Failed to parse actions runs*\n")
		return sb.String(), nil
	}

	// Deduplicate workflow runs by name, keeping only the latest (highest run_number).
	latestByName := make(map[string]model.GitHubActionsRunResponse)
	for _, run := range runsList.WorkflowRuns {
		if existing, ok := latestByName[run.Name]; !ok || run.RunNumber > existing.RunNumber {
			latestByName[run.Name] = run
		}
	}

	var failed, succeeded, pending []model.GitHubActionsRunResponse
	for _, run := range latestByName {
		switch {
		case run.Conclusion == "failure" || run.Conclusion == "action_required" ||
			run.Conclusion == "timed_out" || run.Conclusion == "cancelled":
			failed = append(failed, run)
		case run.Status == "completed" && (run.Conclusion == "success" ||
			run.Conclusion == "neutral" || run.Conclusion == "skipped"):
			succeeded = append(succeeded, run)
		default:
			pending = append(pending, run)
		}
	}

	sb.WriteString(fmt.Sprintf("### Actions Runs Summary: %d unique workflows, %d failed, %d passed, %d pending\n\n",
		len(latestByName), len(failed), len(succeeded), len(pending)))

	if len(failed) > 0 {
		sb.WriteString("### Failed Actions Runs\n")
		for _, run := range failed {
			link := &model.GitHubCheckLink{
				Owner:        prLink.Owner,
				Repo:         prLink.Repo,
				ActionsRunID: fmt.Sprintf("%d", run.ID),
			}
			details, err := g.fetchActionsRunDetails(link)
			if err != nil {
				log.Printf("Failed to fetch details for run %d: %v", run.ID, err)
				sb.WriteString(fmt.Sprintf("\n#### %s\n", run.Name))
				sb.WriteString(fmt.Sprintf("- **Conclusion**: %s\n", run.Conclusion))
				sb.WriteString(fmt.Sprintf("- **URL**: %s\n", run.HTMLURL))
				continue
			}
			sb.WriteString(details)
		}
	}

	return sb.String(), nil
}

func (g *GitHubClient) fetchCheckRunAnnotations(owner, repo string, checkRunID int64) []model.GitHubAnnotation {
	apiURL := fmt.Sprintf("%s/repos/%s/%s/check-runs/%d/annotations?per_page=50",
		githubAPIBaseURL, owner, repo, checkRunID)
	log.Printf("Fetching annotations for check run %d: %s", checkRunID, apiURL)

	body, err := g.apiGet(apiURL)
	if err != nil {
		log.Printf("Failed to fetch annotations for check run %d: %v", checkRunID, err)
		return nil
	}

	var annotations []model.GitHubAnnotation
	if err := json.Unmarshal(body, &annotations); err != nil {
		log.Printf("Failed to parse annotations for check run %d: %v", checkRunID, err)
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

	body, err := g.apiGet(artifactsURL)
	if err != nil {
		log.Printf("Failed to fetch artifacts for run %s: %v", actionsRunID, err)
		return ""
	}

	var artifactsList model.GitHubArtifactsListResponse
	if err := json.Unmarshal(body, &artifactsList); err != nil {
		log.Printf("Failed to parse artifacts response: %v", err)
		return ""
	}

	for _, a := range artifactsList.Artifacts {
		if a.Name == "job-summary" {
			log.Printf("Found job-summary artifact (id=%d, size=%d), downloading...", a.ID, a.SizeInBytes)
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

func (g *GitHubClient) fetchCheckRunDetails(link *model.GitHubCheckLink) (string, error) {
	apiURL := fmt.Sprintf("%s/repos/%s/%s/check-runs/%s", githubAPIBaseURL, link.Owner, link.Repo, link.CheckRunID)
	log.Printf("Fetching GitHub check run details: %s", apiURL)

	body, err := g.apiGet(apiURL)
	if err != nil {
		return "", fmt.Errorf("failed to fetch check run details: %w", err)
	}

	var checkRun model.GitHubCheckRunResponse
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

	annotations := g.fetchCheckRunAnnotations(link.Owner, link.Repo, checkRun.ID)
	if len(annotations) > 0 {
		sb.WriteString("### Annotations\n")
		writeAnnotations(&sb, annotations)
	}

	return sb.String(), nil
}

func (g *GitHubClient) fetchActionsRunDetails(link *model.GitHubCheckLink) (string, error) {
	runURL := fmt.Sprintf("%s/repos/%s/%s/actions/runs/%s", githubAPIBaseURL, link.Owner, link.Repo, link.ActionsRunID)
	log.Printf("Fetching GitHub Actions run details: %s", runURL)

	body, err := g.apiGet(runURL)
	if err != nil {
		return "", fmt.Errorf("failed to fetch actions run details: %w", err)
	}

	var run model.GitHubActionsRunResponse
	if err = json.Unmarshal(body, &run); err != nil {
		return "", fmt.Errorf("failed to parse actions run response: %w", err)
	}

	var sb strings.Builder
	sb.WriteString(fmt.Sprintf("## GitHub Actions Run: %s\n", run.Name))
	sb.WriteString(fmt.Sprintf("- **Status**: %s\n", run.Status))
	sb.WriteString(fmt.Sprintf("- **Conclusion**: %s\n", run.Conclusion))
	sb.WriteString(fmt.Sprintf("- **URL**: %s\n\n", run.HTMLURL))

	jobsURL := fmt.Sprintf("%s/repos/%s/%s/actions/runs/%s/jobs", githubAPIBaseURL, link.Owner, link.Repo, link.ActionsRunID)
	body, err = g.apiGet(jobsURL)
	if err != nil {
		log.Printf("Failed to fetch jobs for actions run: %v", err)
		return sb.String(), nil
	}

	var jobsList model.GitHubJobsListResponse
	if err = json.Unmarshal(body, &jobsList); err != nil {
		log.Printf("Failed to parse jobs response: %v", err)
		return sb.String(), nil
	}

	sb.WriteString(fmt.Sprintf("### Jobs (%d total)\n", jobsList.TotalCount))

	for _, job := range jobsList.Jobs {
		sb.WriteString(fmt.Sprintf("\n#### Job: %s\n", job.Name))
		sb.WriteString(fmt.Sprintf("- **Status**: %s, **Conclusion**: %s\n", job.Status, job.Conclusion))
		if job.Conclusion == "failure" && len(job.Steps) > 0 {
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

func (g *GitHubClient) fetchJobLogs(link *model.GitHubCheckLink) (string, error) {
	jobURL := fmt.Sprintf("%s/repos/%s/%s/actions/jobs/%s", githubAPIBaseURL, link.Owner, link.Repo, link.JobID)
	log.Printf("Fetching GitHub job details: %s", jobURL)

	body, err := g.apiGet(jobURL)
	if err != nil {
		return "", fmt.Errorf("failed to fetch job details: %w", err)
	}

	var job model.GitHubJobResponse
	if err = json.Unmarshal(body, &job); err != nil {
		return "", fmt.Errorf("failed to parse job response: %w", err)
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

func (g *GitHubClient) downloadJobLogs(link *model.GitHubCheckLink) (string, error) {
	logsURL := fmt.Sprintf("%s/repos/%s/%s/actions/jobs/%s/logs", githubAPIBaseURL, link.Owner, link.Repo, link.JobID)
	log.Printf("Downloading job logs: %s", logsURL)

	req, err := http.NewRequest("GET", logsURL, nil)
	if err != nil {
		return "", fmt.Errorf("failed to create request: %w", err)
	}

	req.Header.Set("Accept", "application/vnd.github.v3+json")
	req.Header.Set("User-Agent", g.userAgent)

	if token, tokenErr := g.getToken(); tokenErr == nil && token != "" {
		req.Header.Set("Authorization", "token "+token)
	}

	noRedirectClient := &http.Client{
		Timeout: 30 * time.Second,
		CheckRedirect: func(req *http.Request, via []*http.Request) error {
			return http.ErrUseLastResponse
		},
	}

	resp, err := noRedirectClient.Do(req)
	if err != nil {
		return "", fmt.Errorf("failed to request logs: %w", err)
	}
	defer func() { _ = resp.Body.Close() }()

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
		_ = resp.Body.Close()

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
		owner = "Azure"
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
	req.Header.Set("X-GitHub-Api-Version", "2022-11-28")

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
	req.Header.Set("X-GitHub-Api-Version", "2022-11-28")

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
	case conclusion == "failure":
		return "✗"
	case conclusion == "skipped":
		return "○"
	case status == "in_progress":
		return "●"
	default:
		return "✓"
	}
}
