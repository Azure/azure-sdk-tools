# API Spec Status Sync Solution

## Problem
The release planner for management plane SDK release app incorrectly shows API spec status as "not merged" even when the corresponding GitHub PR is in merged state.

## Root Cause
- The `Custom.APISpecApprovalStatus` field in Azure DevOps work items is only updated when someone manually runs the `check-api-readiness` command
- There is no automatic mechanism to update the DevOps work item when a GitHub PR gets merged
- The release planner relies solely on the DevOps work item field rather than checking GitHub PR status directly

## Solution Components

### 1. Sync Command (Implemented)
**Command**: `azsdk spec-workflow sync-api-spec-status`

**Purpose**: Manually sync API spec status for all open release plans by checking GitHub PR status

**How it works**:
1. Queries DevOps for all open release plans with active spec pull requests
2. For each release plan, extracts the PR number from the spec PR URL
3. Checks the current GitHub PR status (merged, open with approvals, etc.)
4. Updates the DevOps work item API spec status accordingly:
   - "Merged" for merged PRs
   - "Approved" for open PRs with required approval labels

**Usage**:
```bash
# Run manually to sync current status
azsdk spec-workflow sync-api-spec-status

# Can be scheduled via cron or GitHub Actions for regular syncing
```

### 2. GitHub Webhook Integration (Framework Added)
**Location**: `tools/github-event-processor/`

**Purpose**: Automatically update release plan status when spec PRs are merged in real-time

**How it works**:
1. GitHub webhook triggers when PR is closed/merged in azure-rest-api-specs repo
2. `UpdateReleasePlanApiSpecStatus` handler detects merged spec PRs
3. Queries DevOps for matching release plans
4. Updates their API spec status to "Merged"

**Status**: Framework added, needs DevOps service integration for full functionality

### 3. Enhanced Status Detection
**File**: `tools/azsdk-cli/Azure.Sdk.Tools.Cli/Tools/ReleasePlan/SpecWorkFlowTool.cs`

**Improvements**:
- Robust PR status detection (merged vs approved vs pending)
- Support for both management plane (ARM approval) and data plane (API stewardship) PRs
- Better error handling and logging
- Batch processing of multiple release plans

## Usage Instructions

### Manual Sync
Run the sync command periodically to ensure all release plans have up-to-date status:
```bash
azsdk spec-workflow sync-api-spec-status
```

### Automated Sync
Add to GitHub Actions workflow or cron job:
```yaml
# Example GitHub Actions workflow
- name: Sync API Spec Status
  run: |
    azsdk spec-workflow sync-api-spec-status
```

### Real-time Updates (Future)
Deploy the GitHub event processor to azure-rest-api-specs repository to get real-time updates when PRs are merged.

## Files Modified

### CLI Tool
- `Tools/ReleasePlan/SpecWorkFlowTool.cs` - Added sync command
- `Services/DevOpsService.cs` - Added method to query open release plans
- `Tests/Mocks/Services/MockDevOpsService.cs` - Updated mock

### GitHub Event Processor
- `EventProcessing/PullRequestProcessing.cs` - Added webhook handler
- `Constants/RulesConstants.cs` - Added new rule constant

## Benefits
1. **Accurate Status**: Release planner shows correct "merged" status for merged PRs
2. **Automated Sync**: No manual intervention needed to keep status up-to-date
3. **Real-time Updates**: Immediate status updates when PRs are merged (with webhook)
4. **Comprehensive**: Handles both management and data plane specifications
5. **Scalable**: Can process multiple release plans in batch

## Testing
Tests added to verify the sync functionality and demonstrate the bug fix:
- Tests for merged PR scenarios
- Tests for approved but not merged PRs
- Tests for various PR states and edge cases