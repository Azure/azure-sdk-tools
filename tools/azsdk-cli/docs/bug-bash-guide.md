# Azure SDK Tools Agent Bug Bash Guide

This guide provides comprehensive instructions for organizing and executing a bug bash for the Azure SDK Tools Agent (azsdk-cli). Use this guide to plan effective testing sessions that help identify issues and improve the agent's capabilities.

## Table of Contents

- [Overview](#overview)
- [Scope and Goals](#scope-and-goals)
- [Participant Invite List](#participant-invite-list)
- [Prerequisites and Setup](#prerequisites-and-setup)
- [Testing Scenarios](#testing-scenarios)
- [Feedback Capture](#feedback-capture)
- [Post-Bash Triage Workflow](#post-bash-triage-workflow)
- [Tips for Organizers](#tips-for-organizers)

## Overview

A bug bash is a focused testing event where participants exercise the Azure SDK Tools Agent across diverse scenarios to discover bugs, usability issues, and unexpected behaviors. The agent provides both CLI commands and MCP (Model Context Protocol) tools for Azure SDK development workflows.

**Agent Capabilities:**
- TypeSpec project operations (conversion, generation, updates)
- SDK package management (build, release, versioning)
- Release plan creation and management
- APIView integration and automated reviews
- Pipeline analysis and troubleshooting
- CODEOWNERS management
- Test result analysis

## Scope and Goals

### What's Being Tested

The bug bash focuses on the Azure SDK Tools Agent in both operational modes:

1. **CLI Mode** - Direct command-line usage (`azsdk <command>`)
2. **MCP Server Mode** - Agent integration via GitHub Copilot or other MCP clients
3. **GitHub Coding Agent Integration** - Usage within GitHub Actions workflows

### Testing Boundaries

**In Scope:**
- Command correctness and output quality
- MCP tool invocations and responses
- Error handling and recovery
- Integration with Azure SDK repos (azure-sdk-for-net, azure-sdk-for-java, etc.)
- TypeSpec workflow end-to-end (convert → generate → build → test)
- Release plan workflows
- APIView integration
- Pipeline diagnostics
- Documentation clarity and completeness

**Out of Scope:**
- Underlying Azure services (APIView backend, Azure DevOps infrastructure)
- Language-specific SDK runtime behavior (unless directly caused by generated code)
- Non-agent eng/common tooling

### Success Criteria

A successful bug bash achieves:

- **Coverage**: Each major tool/command exercised at least 3 times by different participants
- **Quality Issues Identified**: 10+ actionable bugs or improvement suggestions logged
- **Documentation Gaps**: All unclear or missing documentation noted
- **Usability Insights**: 5+ UX friction points documented
- **CI/CD Validation**: All workflows tested in realistic scenarios (not just happy paths)

### Metrics to Track

- Total participants
- Hours of testing time
- Issues filed (by severity/category)
- Commands/tools exercised (coverage map)
- Time to complete key scenarios
- Success rate per scenario

## Participant Invite List

### Internal Participants (Microsoft Employees)

**Required Roles:**
- **Azure SDK Engineers** (2-4 per language) - Familiar with SDK development workflows
- **Service Team Representatives** (1-2) - Users of TypeSpec and SDK generation
- **Engineering Systems Team** (1-2) - Pipeline and automation expertise
- **Product Managers** (1-2) - User experience perspective

**Recommended Team Distribution:**
- .NET SDK team members
- Java SDK team members
- JavaScript/TypeScript SDK team members
- Python SDK team members
- Azure REST API Specs reviewers
- EngSys/tools maintainers

**How to Invite Internal:**
1. Send calendar invite with:
   - Bug bash timeframe (recommend 2-4 hour window or full day)
   - Link to this guide
   - Link to feedback form/template
   - Slack/Teams channel for coordination
2. Share in relevant Teams channels:
   - `Azure SDK` team channels
   - `azure-sdk-tools` repository discussions
3. Post in internal Azure SDK coordination meetings

### External Participants (Customers/Partners)

**Target Profiles:**
- Service teams actively using TypeSpec for new APIs
- Early adopters of the agent/MCP tools
- Azure SDK contributors (community members)
- Partners building on Azure SDKs

**How to Invite External:**
1. GitHub repository announcements (if public bug bash)
2. Azure SDK blog post or newsletter
3. Direct outreach to known service team contacts
4. Office hours sessions converted to bug bash participation

**Note:** For external participants, ensure:
- They have appropriate access to test resources
- Clear NDA/confidentiality boundaries if testing unreleased features
- Separate feedback channels (avoid exposing internal-only information)

### Team Size Recommendations

- **Minimum Viable**: 5-8 participants (ensures scenario coverage)
- **Optimal**: 15-20 participants (good diversity without coordination overhead)
- **Maximum Practical**: 30 participants (requires dedicated coordinator)

## Prerequisites and Setup

Participants should complete these steps **before** the bug bash begins.

### Environment Requirements

**Operating System:**
- Windows 10/11, macOS 12+, or Linux (Ubuntu 20.04+)
- Bash/PowerShell/Zsh shell access

**Software Prerequisites:**
- [.NET 8.0 SDK or later](https://dotnet.microsoft.com/download)
- [Node.js 18+ and npm](https://nodejs.org/) (for TypeSpec and JavaScript SDK testing)
- [Git](https://git-scm.com/)
- [GitHub CLI (`gh`)](https://cli.github.com/)
- [VS Code](https://code.visualstudio.com/) with [GitHub Copilot extension](https://marketplace.visualstudio.com/items?itemName=GitHub.copilot) (for MCP testing)

**Optional but Recommended:**
- Docker Desktop (for isolated testing environments)
- Azure CLI (`az`) for Azure-authenticated scenarios
- Java 11+ (for Java SDK testing)
- Python 3.8+ (for Python SDK testing)

### Account Setup

**GitHub:**
- GitHub account with access to:
  - `Azure/azure-sdk-tools` (contributor or read access)
  - At least one Azure SDK language repo (`Azure/azure-sdk-for-*`)
  - `Azure/azure-rest-api-specs` (for TypeSpec testing)
- Personal access token (PAT) with `repo` and `workflow` scopes

**Azure DevOps:**
- Access to `azure-sdk` Azure DevOps organization
- Permissions to view pipeline runs

**Internal Microsoft Users:**
- Azure SDK Architecture Board access (for release plan testing)
- APIView access

### Agent Installation

**Option 1: Standalone CLI Installation**

```pwsh
# Clone azure-sdk-tools
git clone https://github.com/Azure/azure-sdk-tools.git
cd azure-sdk-tools

# Run installation script
./eng/common/mcp/azure-sdk-mcp.ps1 -UpdatePathInProfile

# Verify installation
azsdk --version
```

**Option 2: MCP Server Setup (for GitHub Copilot testing)**

1. Create or update `.vscode/mcp.json` in your test workspace:

```json
{
  "servers": {
    "azure-sdk": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "<path-to-azure-sdk-tools>/tools/azsdk-cli/Azure.Sdk.Tools.Cli/Azure.Sdk.Tools.Cli.csproj",
        "--",
        "mcp"
      ],
      "env": {
        "AZURE_DEVOPS_PAT": "<your-ado-pat>",
        "GITHUB_TOKEN": "<your-github-token>"
      }
    }
  }
}
```

2. Reload VS Code window
3. Verify MCP connection in Copilot Chat (type `@workspace` and confirm `azure-sdk` tools available)

**Option 3: GitHub Coding Agent Testing**

For testing in GitHub Actions context:
1. Fork an Azure SDK language repo
2. Verify `.vscode/mcp.json` is present (pre-configured in SDK repos)
3. Invoke GitHub Coding Agent on an issue in your fork

### Test Workspace Setup

Create a dedicated testing directory:

```bash
mkdir ~/azsdk-bugbash-workspace
cd ~/azsdk-bugbash-workspace

# Clone a test Azure SDK repo
gh repo clone Azure/azure-sdk-for-net

# Clone TypeSpec repo for conversion testing
gh repo clone Azure/azure-rest-api-specs

# Clone azure-sdk-tools for tool development testing
gh repo clone Azure/azure-sdk-tools
```

### Verification Checklist

Before starting testing, confirm:

- [ ] `azsdk --version` returns version number
- [ ] `azsdk --help` displays command list
- [ ] GitHub CLI authenticated: `gh auth status`
- [ ] Can access at least one Azure SDK repo
- [ ] MCP server connects in VS Code (if testing MCP mode)
- [ ] Feedback submission mechanism ready (see [Feedback Capture](#feedback-capture))

## Testing Scenarios

These scenarios represent real-world Azure SDK development workflows. Each participant should attempt **3-5 scenarios** minimum, prioritizing areas relevant to their expertise.

### Scenario Categories

1. [TypeSpec Operations](#typespec-operations)
2. [SDK Package Management](#sdk-package-management)
3. [Release Planning](#release-planning)
4. [Pipeline Diagnostics](#pipeline-diagnostics)
5. [APIView Integration](#apiview-integration)
6. [CODEOWNERS Management](#codeowners-management)
7. [Agent Integration (MCP/GitHub Coding Agent)](#agent-integration-mcpgithub-coding-agent)

---

### TypeSpec Operations

#### Scenario 1: Convert Swagger to TypeSpec

**Objective:** Convert an existing Azure service Swagger definition to TypeSpec.

**Steps:**
1. Identify a service with Swagger but no TypeSpec (e.g., older Azure service)
2. Run: `azsdk tsp convert --swagger-path <path-to-swagger> --output-dir <output-path>`
3. Validate the generated TypeSpec compiles
4. Compare output structure with hand-written TypeSpec

**Success Criteria:**
- Conversion completes without errors
- Generated TypeSpec follows Azure conventions
- README or conversion report is generated

**Known Complexity Areas:**
- Complex inheritance hierarchies
- Custom x-ms extensions
- Discriminated unions

---

#### Scenario 2: Generate SDK from TypeSpec

**Objective:** Generate a language SDK from a TypeSpec project.

**Steps:**
1. Navigate to a TypeSpec project (or use one from Scenario 1)
2. Run: `azsdk tsp client generate --language <lang> --typespec-project <path>`
3. Verify SDK code is generated in expected location
4. Build the generated SDK

**Languages to Test:** .NET, Java, Python, JavaScript

**Success Criteria:**
- Generation completes successfully
- Generated code follows language SDK conventions
- Build succeeds (or fails with actionable error)

**Variations:**
- Generate from a brand-new TypeSpec project
- Regenerate existing SDK (incremental update scenario)
- Generate with custom emitter options

---

#### Scenario 3: Customized Code Update

**Objective:** Apply customizations to generated SDK and rebuild.

**Steps:**
1. Generate SDK for a service (use Scenario 2)
2. Manually modify generated code (add custom method or comment)
3. Run: `azsdk tsp client customized-update --language <lang>`
4. Verify customizations are preserved
5. Build and confirm compilation

**Success Criteria:**
- Customizations are not overwritten
- Regeneration succeeds
- Build output includes customized code

**Known Edge Cases:**
- Conflicts between customizations and spec changes
- Java-specific patch application

---

#### Scenario 4: Find Modified TypeSpec Projects

**Objective:** Identify which TypeSpec projects changed in a branch.

**Setup:**
1. Create a test branch in azure-rest-api-specs
2. Modify 1-2 TypeSpec projects (change model, add operation)
3. Commit changes

**Steps:**
1. Run: `azsdk tsp project modified-projects`
2. Verify output lists only modified projects

**Success Criteria:**
- Correct projects identified
- No false positives/negatives
- Output format is parseable

---

### SDK Package Management

#### Scenario 5: Check API Spec Readiness

**Objective:** Validate that a TypeSpec spec is ready for SDK generation.

**Setup:**
1. Create PR in azure-rest-api-specs with TypeSpec changes
2. Note PR number

**Steps:**
1. Run: `azsdk release-plan check-api-readiness --pr <number> --tsp-config <path>`
2. Review readiness report

**Success Criteria:**
- Validation runs without errors
- Clear actionable feedback if spec not ready
- Validation passes for complete specs

**Failure Mode Testing:**
- Incomplete TypeSpec (missing operations)
- Incorrectly configured tspconfig.yaml
- Spec with breaking changes

---

#### Scenario 6: Get Failed Test Cases

**Objective:** Analyze test failures from a .trx file.

**Setup:**
1. Run SDK tests that produce failures
2. Locate generated .trx file

**Steps:**
1. Run: `azsdk pkg test results --trx-file <path>`
2. Review list of failed tests
3. Get details for a specific failure (use follow-up command if needed)

**Success Criteria:**
- Failed test names displayed
- Stack traces and error messages accessible
- Output helps identify root cause

**Edge Cases:**
- TRX with no failures
- Malformed TRX file
- Large TRX file (100+ tests)

---

### Release Planning

#### Scenario 7: Create Release Plan

**Objective:** Create a release plan for a new TypeSpec service.

**Prerequisites:** Internal Microsoft access to Architecture Board.

**Steps:**
1. Identify a TypeSpec project ready for SDK release
2. Run: `azsdk release-plan create --typespec-project <path> --service-id <id> --product-id <id>`
3. Verify release plan created in Azure DevOps

**Success Criteria:**
- Release plan work item created
- Contains correct service/product metadata
- Links to relevant specs/repos

**Variations:**
- Create with only TypeSpec project (service/product auto-discovered)
- Create for existing service (updated release plan)

---

#### Scenario 8: Check Release Plan Status

**Objective:** Query status of an existing release plan.

**Setup:** Use a release plan from Scenario 7 or identify existing plan ID.

**Steps:**
1. Run: `azsdk release-plan status --id <plan-id>`
2. Review status details

**Success Criteria:**
- Status displays correctly (Draft, In Progress, Released, Abandoned)
- Metadata matches Azure DevOps work item
- Links to related items work

---

#### Scenario 9: Abandon Release Plan

**Objective:** Mark a release plan as abandoned.

**Setup:** Create or identify a test release plan.

**Steps:**
1. Run: `azsdk release-plan abandon --id <plan-id>`
2. Confirm status updated in Azure DevOps

**Success Criteria:**
- Status changes to "Abandoned"
- Confirmation message displayed
- Audit trail preserved

---

### Pipeline Diagnostics

#### Scenario 10: Analyze Pipeline Failure

**Objective:** Diagnose why a pipeline run failed.

**Setup:**
1. Find a recent failed pipeline run in Azure DevOps
2. Note the build ID

**Steps:**
1. Run: `azsdk azp analyze --build-id <id>`
2. Review failure analysis

**Success Criteria:**
- Root cause identified (or hypotheses provided)
- Relevant log excerpts surfaced
- Actionable recommendations given

**Good Test Cases:**
- Compilation failure
- Test failure
- Timeout/infrastructure issue
- Configuration error

---

#### Scenario 11: Analyze Log File

**Objective:** Analyze a downloaded log file for errors.

**Setup:**
1. Download a log file from failed pipeline run

**Steps:**
1. Run: `azsdk azp log analyze --log-file <path>`
2. Review error analysis

**Success Criteria:**
- Errors and warnings extracted
- Context around errors provided
- Duplicates deduplicated

---

#### Scenario 12: Get Pipeline Status

**Objective:** Check current status of a running or completed pipeline.

**Steps:**
1. Run: `azsdk azp status --build-id <id>`
2. Observe status output

**Success Criteria:**
- Status displayed (Queued, Running, Completed, Failed)
- Duration shown if completed
- Summary of results included

---

#### Scenario 13: Download Test Results

**Objective:** Retrieve test artifacts from a pipeline run.

**Steps:**
1. Run: `azsdk azp test-results --build-id <id>`
2. Verify artifacts downloaded to local directory

**Success Criteria:**
- Artifacts downloaded successfully
- File paths displayed
- Contents match Azure DevOps artifacts

---

### APIView Integration

#### Scenario 14: Get APIView Review URL

**Objective:** Find the APIView review link for a package.

**Steps:**
1. Run: `azsdk apiview get-review-url --package-name <name> --language <lang>`
2. Open returned URL in browser
3. Verify it navigates to correct APIView review

**Success Criteria:**
- URL returned quickly
- URL is valid and accessible
- Package displayed in APIView

---

#### Scenario 15: Get APIView Comments

**Objective:** Retrieve reviewer feedback from APIView.

**Steps:**
1. Identify a package with existing APIView review comments
2. Run: `azsdk apiview get-comments --package-name <name> --language <lang>`
3. Review returned comments

**Success Criteria:**
- Comments retrieved
- Comment text, author, and timestamp shown
- Resolved vs. unresolved status indicated

---

#### Scenario 16: Request Copilot Review

**Objective:** Submit API surface for automated Copilot review.

**Steps:**
1. Prepare API surface text (or use APIView URL)
2. Run: `azsdk apiview request-copilot-review --api-text <text-or-url>`
3. Note job ID
4. Poll for results: `azsdk apiview get-copilot-review --job-id <id>`

**Success Criteria:**
- Review job accepted
- Job ID returned
- Results available within reasonable time
- Review comments returned in structured format

**Edge Cases:**
- Very large API surface
- Malformed API surface text
- Unsupported language

---

### CODEOWNERS Management

#### Scenario 17: View CODEOWNERS Associations

**Objective:** Look up ownership information.

**Steps:**
1. Run: `azsdk config codeowners view --package <package-name>`
2. Review owners, labels, and paths

**Success Criteria:**
- Owners listed with GitHub handles
- Labels associated with package shown
- Paths covered displayed

**Variations:**
- Query by GitHub user: `--github-user <username>`
- Query by label: `--label <label-name>`
- Query by path: `--path <file-path>`

---

#### Scenario 18: Update CODEOWNERS Cache

**Objective:** Refresh CODEOWNERS cache after making changes.

**Steps:**
1. Run: `azsdk config codeowners update-cache`
2. Confirm pipeline triggered in Azure DevOps
3. Wait for completion and verify cache updated

**Success Criteria:**
- Pipeline successfully triggered
- Status reported
- Subsequent queries reflect updated data

---

### Agent Integration (MCP/GitHub Coding Agent)

#### Scenario 19: MCP Tool Invocation in VS Code

**Objective:** Use agent tools via GitHub Copilot in VS Code.

**Setup:** MCP server configured (see [Prerequisites](#prerequisites-and-setup)).

**Steps:**
1. Open Copilot Chat in VS Code
2. Ask: "What TypeSpec projects were modified in this branch?"
3. Verify agent invokes `azsdk_get_modified_typespec_projects`
4. Review response quality

**Success Criteria:**
- Tool invoked automatically by Copilot
- Results returned to chat
- Response is accurate and helpful

**More Test Prompts:**
- "Analyze the latest pipeline failure for this repo"
- "Create a release plan for the TypeSpec project at specification/foo/bar"
- "Get APIView comments for package Azure.Foo"

---

#### Scenario 20: GitHub Coding Agent in Actions

**Objective:** Invoke agent within a GitHub Actions workflow.

**Setup:**
1. Fork an Azure SDK repo
2. Create an issue in your fork

**Steps:**
1. Tag the issue for GitHub Coding Agent (add agent-invoke label or command)
2. Wait for workflow to run
3. Review agent's work (commits, comments)

**Success Criteria:**
- Agent activates in response to issue
- Uses azsdk MCP tools as appropriate
- Produces useful results or asks clarifying questions

**Example Scenarios:**
- "Generate SDK for the TypeSpec project at specs/foo/Foo.Management"
- "Analyze why the latest CI run failed"
- "Add CODEOWNERS entry for package Azure.Foo.Bar with owner @username"

---

#### Scenario 21: Multi-Step Agent Workflow

**Objective:** Test agent across multi-command workflow.

**Workflow Example:** TypeSpec → SDK → PR
1. Ask agent: "Convert the Swagger at specification/foo/stable/2023-01-01/swagger.json to TypeSpec"
2. After conversion: "Generate .NET SDK from the new TypeSpec"
3. After generation: "Build the SDK and report any compilation errors"
4. If errors: "Fix the compilation errors"
5. After fixes: "Create a PR with these changes"

**Success Criteria:**
- Agent successfully chains commands
- Intermediate results used in subsequent steps
- Final PR created with correct changes
- Agent handles errors gracefully mid-workflow

---

### Stress and Edge Case Scenarios

#### Scenario 22: Concurrent Operations

**Objective:** Test agent behavior under concurrent usage.

**Steps:**
1. Open multiple terminal windows
2. Simultaneously run different azsdk commands in each
3. Observe for crashes, hangs, or corrupted output

**Success Criteria:**
- No crashes or hangs
- Output not interleaved between commands
- Each command completes successfully

---

#### Scenario 23: Large Input Handling

**Objective:** Test with large files or datasets.

**Test Cases:**
- Analyze a very large log file (>50 MB)
- Convert a complex Swagger file (100+ operations)
- Generate SDK for a service with 50+ models

**Success Criteria:**
- Commands complete (or fail gracefully with clear error)
- No memory exhaustion
- Performance is acceptable (or timeout is reasonable)

---

#### Scenario 24: Invalid Input Handling

**Objective:** Verify error handling for bad inputs.

**Test Cases:**
- Non-existent file paths
- Malformed TypeSpec
- Invalid build IDs
- Missing required parameters
- Incorrect parameter types

**Success Criteria:**
- Clear error messages
- No stack traces exposed to user (unless verbose mode)
- Suggested corrections where applicable
- Command exits with non-zero code

---

#### Scenario 25: Offline/Disconnected Testing

**Objective:** Test behavior when network resources unavailable.

**Steps:**
1. Disconnect network or block specific endpoints
2. Run commands requiring network (e.g., APIView, pipeline analysis)
3. Observe error messages

**Success Criteria:**
- Timeouts are reasonable (not excessive)
- Error messages clearly indicate network issue
- Commands that can work offline still function

---

## Feedback Capture

Effective bug bash feedback is **specific, actionable, and categorized**. Use the mechanisms below to report findings.

### GitHub Issues

**Primary Feedback Mechanism:** File issues in the [Azure/azure-sdk-tools](https://github.com/Azure/azure-sdk-tools/issues) repository.

**Issue Template:**

```markdown
## Bug Bash Feedback - [Category]

**Scenario:** [Name or number from this guide]

**Description:**
[Clear description of the issue or observation]

**Steps to Reproduce:**
1. [First step]
2. [Second step]
3. [...]

**Expected Behavior:**
[What you expected to happen]

**Actual Behavior:**
[What actually happened]

**Environment:**
- OS: [Windows 11 / macOS 14 / Ubuntu 22.04]
- Agent Version: [Output of `azsdk --version`]
- Mode: [CLI / MCP Server / GitHub Coding Agent]
- Language SDK: [.NET / Java / Python / JavaScript / N/A]

**Severity:**
- [ ] Critical (blocks testing or causes data loss)
- [ ] High (major functionality broken)
- [ ] Medium (workaround exists but inconvenient)
- [ ] Low (cosmetic or minor issue)

**Category:**
- [ ] Bug (incorrect behavior)
- [ ] Usability (confusing UX)
- [ ] Performance (slow response)
- [ ] Documentation (missing or unclear docs)
- [ ] Feature Request (new capability needed)

**Logs/Screenshots:**
[Attach relevant output, screenshots, or log files]

**Additional Context:**
[Any other relevant information]
```

### Required Labels

Apply these labels to bug bash issues:

- `bug-bash-2025` (or appropriate date)
- `azsdk-cli` (component)
- Severity: `severity:critical`, `severity:high`, `severity:medium`, `severity:low`
- Type: `bug`, `enhancement`, `documentation`, `question`

### Real-Time Coordination Channel

For live bug bash events, use a dedicated communication channel:

**Internal Microsoft:**
- Teams channel: [Create dedicated channel for event]
- Tag issues with `@azure-sdk-tools` maintainers for urgent items

**External/Open:**
- GitHub Discussions: Use [Azure/azure-sdk-tools Discussions](https://github.com/Azure/azure-sdk-tools/discussions)
- Tag discussion with `bug-bash` topic

### Feedback Form (Optional)

For structured data collection, create a form (Microsoft Forms, Google Forms, etc.) with fields:

- Participant name/alias
- Scenarios attempted (checklist)
- Scenarios completed successfully
- Scenarios with issues
- Most confusing aspect
- Most impressive capability
- Overall satisfaction (1-5 scale)
- Would you use this in your daily workflow? (Yes/No/Maybe)
- Additional comments

### Anonymous Feedback

Provide an option for anonymous feedback (e.g., dedicated email alias or form) to capture candid usability observations.

## Post-Bash Triage Workflow

After the bug bash concludes, organizers follow this process to review, prioritize, and address findings.

### Immediate Actions (Within 24 Hours)

1. **Thank Participants:**
   - Send follow-up message with summary statistics
   - Recognize top contributors

2. **Collect All Issues:**
   - Query GitHub for all `bug-bash-2025` labeled issues
   - Export to spreadsheet for analysis

3. **Critical Bug Triage:**
   - Identify severity:critical issues
   - Assign to maintainers for immediate investigation
   - Communicate ETA for fixes to participants

### First Week Actions

4. **Categorize and Deduplicate:**
   - Group similar issues
   - Mark duplicates and cross-reference
   - Tag issues by component (TypeSpec, Release Plan, Pipeline, etc.)

5. **Prioritize Bugs:**
   - Use severity + impact matrix:
     - **P0 (Critical):** Blocks core scenarios, data loss, security issues
     - **P1 (High):** Major functionality broken, no workaround
     - **P2 (Medium):** Significant inconvenience, workaround exists
     - **P3 (Low):** Minor issues, cosmetic problems

6. **Assign Owners:**
   - Each issue assigned to a maintainer or team
   - Set milestone targets (next release, backlog)

7. **Documentation Issues:**
   - Group documentation feedback
   - Assign to PM or documentation owner
   - Target for rapid update (within 1 sprint)

### First Sprint Actions

8. **Fix High-Priority Bugs:**
   - P0/P1 bugs addressed in next release
   - Regression tests added for fixed bugs

9. **Update Documentation:**
   - Address documentation gaps identified
   - Add clarifications for confusing areas
   - Update this bug bash guide based on learnings

10. **Feature Requests:**
    - Review enhancement suggestions
    - Add to roadmap if aligned with strategy
    - Provide rationale in issue if declining

### Follow-Up Communication

11. **Publish Results:**
    - Blog post or repo discussion summarizing:
      - Participation statistics
      - Top issues found
      - Issues fixed
      - Roadmap updates based on feedback
    - Thank participants publicly

12. **Close the Loop:**
    - Comment on every issue with disposition (fixed, planned, deferred, won't fix)
    - Provide links to PRs that addressed issues
    - Ask original reporters to verify fixes

### Metrics Analysis

13. **Evaluate Bug Bash Effectiveness:**
    - Review success criteria (see [Scope and Goals](#scope-and-goals))
    - Calculate:
      - Issue discovery rate (issues per participant-hour)
      - Fix rate (% fixed within 1 sprint)
      - Scenario coverage (% scenarios attempted)
      - Participant satisfaction (from feedback forms)
    - Document lessons learned for future bug bashes

### Continuous Improvement

14. **Update This Guide:**
    - Incorporate feedback on the bug bash process itself
    - Add newly discovered "good" test scenarios
    - Refine prerequisites based on setup pain points
    - Update tool list as agent capabilities expand

15. **Schedule Next Bug Bash:**
    - Recommend quarterly bug bashes for active development
    - Pre-release bug bash for major version milestones

## Tips for Organizers

### Before the Bug Bash

- **Send Reminders:** 1 week and 1 day before, with setup checklist
- **Pre-Test:** Run through all scenarios yourself to identify broken setup instructions
- **Prepare Resources:** Have sample TypeSpec projects, test repos, example commands ready
- **Assign Coordinators:** Have 1-2 people available during event to unblock participants

### During the Bug Bash

- **Monitor Channels:** Watch for common blockers and address proactively
- **Encourage Sharing:** Participants share interesting findings in real-time
- **Celebrate Discoveries:** Recognize great bug reports publicly
- **Be Available:** Quick response to questions maintains momentum

### After the Bug Bash

- **Don't Let Issues Languish:** Triage within 48 hours
- **Communicate Progress:** Update participants on fixes weekly
- **Document Patterns:** If multiple participants hit the same issue, it's a priority

### Recommended Frequency

- **Pre-Release:** Always before major version releases
- **Quarterly:** For actively developed features
- **After Major Changes:** When new tools or workflows added

## Appendix: Command Reference

For a complete list of all azsdk commands and MCP tools, see [mcp-tools.md](./mcp-tools.md).

**Quick Command Summary:**

| Category | Example Commands |
|----------|------------------|
| TypeSpec | `azsdk tsp convert`, `azsdk tsp client generate`, `azsdk tsp project modified-projects` |
| Release Plan | `azsdk release-plan create`, `azsdk release-plan status`, `azsdk release-plan abandon` |
| APIView | `azsdk apiview get-review-url`, `azsdk apiview get-comments`, `azsdk apiview request-copilot-review` |
| Pipeline | `azsdk azp analyze`, `azsdk azp status`, `azsdk azp log analyze`, `azsdk azp test-results` |
| CODEOWNERS | `azsdk config codeowners view`, `azsdk config codeowners update-cache` |
| Package/Test | `azsdk pkg test results` |

---

**Document Version:** 1.0  
**Last Updated:** 2025-01-24  
**Maintained By:** Azure SDK Tools PM Team

For questions or suggestions about this guide, file an issue in [Azure/azure-sdk-tools](https://github.com/Azure/azure-sdk-tools/issues) with the `documentation` label.
