package test

import (
	"testing"

	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/utils"
	"github.com/stretchr/testify/assert"
)

func TestIsGitHubCheckLink(t *testing.T) {
	// Valid check/actions links
	assert.True(t, utils.IsGitHubCheckLink("https://github.com/Azure/azure-rest-api-specs/actions/runs/22387426697"))
	assert.True(t, utils.IsGitHubCheckLink("https://github.com/Azure/azure-rest-api-specs/actions/runs/22387426697/job/64801013267"))
	assert.True(t, utils.IsGitHubCheckLink("https://github.com/Azure/azure-rest-api-specs/runs/53495170336"))

	// Not check links
	assert.False(t, utils.IsGitHubCheckLink("https://github.com/Azure/azure-rest-api-specs/pull/40736"))
	assert.False(t, utils.IsGitHubCheckLink("https://github.com/Azure/azure-sdk-tools"))
	assert.False(t, utils.IsGitHubCheckLink("https://dev.azure.com/azure-sdk/internal/_build/results?buildId=5530426"))
	assert.False(t, utils.IsGitHubCheckLink(""))
}

func TestIsGitHubPRLink(t *testing.T) {
	// Valid PR links
	assert.True(t, utils.IsGitHubPRLink("https://github.com/Azure/azure-rest-api-specs/pull/12345"))
	assert.True(t, utils.IsGitHubPRLink("https://github.com/Azure/azure-sdk-for-python/pull/99/files"))

	// Not PR links
	assert.False(t, utils.IsGitHubPRLink("https://github.com/Azure/azure-rest-api-specs/actions/runs/18752237048"))
	assert.False(t, utils.IsGitHubPRLink("https://github.com/Azure/azure-sdk-tools/issues/999"))
	assert.False(t, utils.IsGitHubPRLink("https://github.com/Azure/azure-sdk-tools"))
	assert.False(t, utils.IsGitHubPRLink(""))
}

func TestIsCIRelatedIntention(t *testing.T) {
	// Should match CI-related intents
	assert.True(t, utils.IsCIRelatedIntention("ci-build", "What is happening?"))
	assert.True(t, utils.IsCIRelatedIntention("general", "Why is this check failing?"))
	assert.True(t, utils.IsCIRelatedIntention("sdk-develop", "My pipeline is failing"))
	assert.True(t, utils.IsCIRelatedIntention("", "I got a build error in my PR"))
	assert.True(t, utils.IsCIRelatedIntention("PIPELINE-CHECK", "Something"))

	// Should not match unrelated intents
	assert.False(t, utils.IsCIRelatedIntention("api-design", "How should I design my API?"))
	assert.False(t, utils.IsCIRelatedIntention("general", "What is the latest SDK version?"))
	assert.False(t, utils.IsCIRelatedIntention("", ""))
}

// ============================================================
// Temporary debug tests — call real GitHub API, delete after use
// ============================================================

// func TestDebug_FetchGitHubPRChecks(t *testing.T) {
// 	result, err := utils.GetGitHubClient().FetchPRChecks("https://github.com/Azure/azure-rest-api-specs/pull/40736")
// 	if err != nil {
// 		t.Fatalf("FetchPRChecks error: %v", err)
// 	}
// 	t.Logf("PR Checks result:\n%s", result)
// }

// func TestDebug_FetchCheckLogs_PRCheckRun(t *testing.T) {
// 	result, err := utils.GetGitHubClient().FetchCheckLogs("https://github.com/Azure/azure-rest-api-specs/pull/40770/checks?check_run_id=64935706870")
// 	if err != nil {
// 		t.Fatalf("FetchCheckLogs (PR check run) error: %v", err)
// 	}
// 	t.Logf("PR check run result:\n%s", result)
// }

// func TestDebug_FetchCheckLogs_ActionsRun(t *testing.T) {
// 	result, err := utils.GetGitHubClient().FetchCheckLogs("https://github.com/Azure/azure-rest-api-specs/actions/runs/22387426697")
// 	if err != nil {
// 		t.Fatalf("FetchCheckLogs (actions run) error: %v", err)
// 	}
// 	t.Logf("Actions run result:\n%s", result)
// }

// func TestDebug_FetchCheckLogs_Job(t *testing.T) {
// 	result, err := utils.GetGitHubClient().FetchCheckLogs("https://github.com/Azure/azure-rest-api-specs/actions/runs/22387426697/job/64801013267")
// 	if err != nil {
// 		t.Fatalf("FetchCheckLogs (job) error: %v", err)
// 	}
// 	t.Logf("Job result:\n%s", result)
// }

// func TestDebug_FetchCheckLogs_CheckRun(t *testing.T) {
// 	result, err := utils.GetGitHubClient().FetchCheckLogs("https://github.com/Azure/azure-rest-api-specs/runs/53495170336")
// 	if err != nil {
// 		t.Fatalf("FetchCheckLogs (check run) error: %v", err)
// 	}
// 	t.Logf("Check run result:\n%s", result)
// }

// ============================================================
// PR review enrichment helpers
// ============================================================

func TestIsSpecRepo(t *testing.T) {
	assert.True(t, utils.IsSpecRepo("Azure", "azure-rest-api-specs"))
	assert.True(t, utils.IsSpecRepo("azure", "azure-rest-api-specs-pr"))
	assert.True(t, utils.IsSpecRepo("AZURE", "Azure-Rest-Api-Specs"))

	assert.False(t, utils.IsSpecRepo("Azure", "azure-sdk-for-python"))
	assert.False(t, utils.IsSpecRepo("Azure", "azure-sdk-tools"))
	assert.False(t, utils.IsSpecRepo("contoso", "azure-rest-api-specs"))
}

func TestHasBlockingLabels(t *testing.T) {
	// Spec-only labels apply in spec repos.
	blocked, labels := utils.HasBlockingLabels("Azure", "azure-rest-api-specs",
		[]string{"ARMReview", "Approved-OkToMerge"})
	assert.True(t, blocked)
	assert.Equal(t, []string{"ARMReview"}, labels)

	// Spec-only labels do NOT apply in non-spec repos.
	blocked, labels = utils.HasBlockingLabels("Azure", "azure-sdk-for-python",
		[]string{"ARMReview"})
	assert.False(t, blocked)
	assert.Empty(t, labels)

	// Common blocking labels apply everywhere (tolerant matching).
	blocked, labels = utils.HasBlockingLabels("Azure", "azure-sdk-tools",
		[]string{"Do Not Merge"})
	assert.True(t, blocked)
	assert.Equal(t, []string{"Do Not Merge"}, labels)

	// No blocking labels.
	blocked, labels = utils.HasBlockingLabels("Azure", "azure-rest-api-specs",
		[]string{"feature", "documentation"})
	assert.False(t, blocked)
	assert.Empty(t, labels)
}

func TestBuildReviewerSet(t *testing.T) {
	requested := []model.GitHubUser{
		{Login: "alice", HTMLURL: "https://github.com/alice"},
		{Login: "bob"},
	}
	reviews := []model.GitHubReview{
		{User: model.GitHubUser{Login: "carol"}, State: "APPROVED"},
		{User: model.GitHubUser{Login: "alice"}, State: "APPROVED"}, // dup of requested
		{User: model.GitHubUser{Login: "dave"}, State: "COMMENTED"}, // excluded (comment only)
	}

	got := utils.BuildReviewerSet(requested, reviews)

	logins := make([]string, 0, len(got))
	for _, u := range got {
		logins = append(logins, u.Login)
	}
	// Requested first (alice, bob), then meaningful reviewers (carol); dave excluded.
	assert.Equal(t, []string{"alice", "bob", "carol"}, logins)
}

func TestLatestReviewDecision(t *testing.T) {
	assert.Equal(t, "review_required", utils.LatestReviewDecision(nil))

	assert.Equal(t, "approved", utils.LatestReviewDecision([]model.GitHubReview{
		{User: model.GitHubUser{Login: "a"}, State: "APPROVED"},
		{User: model.GitHubUser{Login: "b"}, State: "COMMENTED"},
	}))

	// Any latest changes_requested dominates.
	assert.Equal(t, "changes_requested", utils.LatestReviewDecision([]model.GitHubReview{
		{User: model.GitHubUser{Login: "a"}, State: "APPROVED"},
		{User: model.GitHubUser{Login: "b"}, State: "CHANGES_REQUESTED"},
	}))

	// Latest state per user wins (chronological): a's later APPROVED supersedes earlier.
	assert.Equal(t, "approved", utils.LatestReviewDecision([]model.GitHubReview{
		{User: model.GitHubUser{Login: "a"}, State: "CHANGES_REQUESTED"},
		{User: model.GitHubUser{Login: "a"}, State: "APPROVED"},
	}))
}

func TestAssessMergeReadiness(t *testing.T) {
	ready := utils.AssessMergeReadiness(0, 0, "approved", nil)
	assert.True(t, ready.Ready)
	assert.Empty(t, ready.Reasons)

	// review_required (no approval yet) is still "ready" — checks/labels clean, just
	// needs sign-off; readiness only blocks on concrete blockers.
	ready = utils.AssessMergeReadiness(0, 0, "review_required", nil)
	assert.True(t, ready.Ready)

	notReady := utils.AssessMergeReadiness(2, 1, "changes_requested", []string{"ARMReview"})
	assert.False(t, notReady.Ready)
	assert.Len(t, notReady.Reasons, 4)
}
