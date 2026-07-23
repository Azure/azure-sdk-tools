# APIView — User Guide

This guide is for SDK authors, architects, and reviewers who use APIView to review and approve API surfaces. For developer setup, see [CONTRIBUTING.md](../CONTRIBUTING.md). For architecture internals, see [overview.md](overview.md).

---

## What Is APIView?

APIView is the Azure SDK team's API review platform. It shows the public API surface of Azure SDK packages — classes, methods, properties, and signatures — so that architects can review, comment on, and approve APIs before release. For the full architecture and technical details, see [overview.md](overview.md).

---

## Key Concepts

**Review** — A group of all revisions for a package, regardless of revision type. For example, the library `Azure.Template` in .NET should have one review with all revisions grouped under it.

**API Revision** — Created every time there is a new API-level change.

**Types of API revisions** (based on how they are generated):

| Type | Description |
|------|-------------|
| **Automatic** | A daily CI pipeline checks for any new change in public API surface and creates a new revision if it finds a difference. These revisions cannot be deleted or updated manually — only comments and approvals are allowed. |
| **Pull Request** | Created automatically when a PR introduces an API surface change. |
| **Manual** | Created by uploading an artifact through the web UI. |

For details on when and why these revisions are created, see [ci-integration.md](ci-integration.md#types-of-api-revisions).

---

## Getting Access

To access APIView, you must:

1. Have a **GitHub account**
2. Be a member of the **Microsoft** or **Azure** GitHub organization
3. Set your organization membership to **PUBLIC** (this is the most common access issue):
   - Microsoft: https://github.com/orgs/Microsoft/people
   - Azure: https://github.com/orgs/Azure/people

---

## API Approvals

There are two types of approvals based on package version:

| Type | When Required | Purpose |
|------|---------------|---------|
| **First Release Approval** | Before releasing a preview version of a package that has never been GA'd | Ensures the package name is appropriate before the first beta release. Not required if an API revision was previously approved for the same package. |
| **GA Release Approval** | Before releasing a GA version | Ensures all APIs are reviewed and approved. Release builds submit the package to APIView to compare against the last approved revision — if API surface differs, a new revision is created and the pipeline fails. |

For details on when releases are blocked and the enforcement logic, see [ci-integration.md](ci-integration.md#release-enforcement-logic). For the full approval workflow including prerequisites, carry-forward mechanics, and release gating endpoints, see [release-approval.md](release-approval.md).

### Who Can Approve?

API revision approvers are the architects and deputy architects for each language. For the current list of approvers by language, see the [APIView documentation page](https://eng.ms/docs/products/azure-developer-experience/support/apiview#who-can-approve-my-revision).

---

## Review Process

### Comment Severity

APIView supports comment severity levels to communicate the importance of feedback:

| Severity | Description |
|----------|-------------|
| **Question** | A clarifying question about the API design |
| **Suggestion** | A non-blocking recommendation for improvement |
| **ShouldFix** | An issue that should be addressed before merging |
| **MustFix** | A critical issue that must be resolved before merging |

- The **comment author** selects the severity when creating the comment
- Only the **comment author** can modify the severity
- Language approvers can also modify the severity of **`azure-sdk` bot comments** (not comments by other users)
- Severity cannot be changed on **diagnostic comments**

### Diagnostic Severity Mapping

When diagnostics are converted to comments, their severity is automatically mapped:

| Diagnostic Level | Comment Severity |
|-------------------|-----------------|
| Fatal | MustFix |
| Error | MustFix |
| Warning | ShouldFix |
| Info | Suggestion |

### Comment Source

Comments are tagged with a source to indicate how they were created:

| Source | Description |
|--------|-------------|
| **UserGenerated** | Created manually by human reviewers (default) |
| **AIGenerated** | Created by Copilot during automated API review |
| **Diagnostic** | Generated from API guideline diagnostics / linting rules |

---

## Navigating the Review Page

### Left Command Bar

The left command bar contains icon buttons for navigating the key sections of the review page:

| Button | Destination |
|--------|-------------|
| **API** | Default page showing the current API under review |
| **Revisions** | Opens the revisions panel |
| **Conversations** | Comment threads and discussions |
| **Samples** | Code sample attachments |

### Revisions Panel

The revisions panel allows you to browse, set, or switch active and diff revisions:

- Shows **API Revisions** when on the API page, and **Sample Revisions** when on the Samples page
- **Set active revision:** Check the **Make Active** button on the desired revision
- **Set diff revision:** Check the **Make Diff** button on the desired revision
- **Exit diff view:** Click **Clear Diff** on a revision that was previously set as diff
- **Close panel:** Click outside the panel — the page updates accordingly
- **Full revisions page:** Open the panel, then click the **Revisions** link at the top left of the panel
