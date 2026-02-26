package test

import (
	"testing"

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
