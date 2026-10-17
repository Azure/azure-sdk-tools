# APIView — How Reviews, Revisions, and Pipelines Work

This document explains how APIView integrates with SDK pull requests, CI pipelines, and releases. It clarifies when API revisions are created, when approvals are required, and why certain pipelines fail. For the detailed approval workflow and code references, see [release-approval.md](https://github.com/Azure/azure-sdk-tools/blob/main/src/dotnet/APIView/docs/release-approval.md). For the technical workflow details (endpoints, sequence diagrams, per-language parsing), see [overview.md](https://github.com/Azure/azure-sdk-tools/blob/main/src/dotnet/APIView/docs/overview.md#10-core-workflows).

---

## Background

Originally, API View reviews were entirely manual — SDK teams uploaded API surfaces, architects reviewed and approved, but there was no enforcement during release. A package could ship without any confirmed API approval.

To fix this, APIView was integrated into:
- **PR pipelines** — early detection of API changes
- **Scheduled CI pipelines** — source of truth for what will ship
- **Release pipelines** — enforce approval before GA release

---

## Key Concept: API Surface, Not Versions

APIView compares **API surfaces**, not versions. An approval is valid if there exists an APIView revision with the **exact same API surface**, regardless of when or how it was created (manual, PR, or scheduled). Version-only changes do not require new approvals.

---

## Types of API Revisions

### PR-Based Revisions

**When created:** Automatically, only if a PR introduces an API surface change. If a PR does not change the API surface (e.g., version-only changes), no revision is created.

**Why they exist:**
- Early detection of API changes
- Immediate feedback to SDK authors
- Allow architects to review before merge

**Key benefit:** If an architect approves the PR-based revision, that approval is automatically reused later. Architects do not need to approve twice.

### Automatic (Scheduled) Revisions

Created by the scheduled CI pipeline. These represent the API surface of what is currently on the `main` / release branch — the **source of truth** for what will ship.

**Why repeated builds do not create duplicate revisions:** The pipeline runs daily, and the same package version may be uploaded repeatedly. APIView handles revision identity separately from approval:

- Same package version and API surface: update the existing non-released revision's token file in place. This preserves approval and comments while retaining changes in documentation and `SkipDiff` regions.
- Same package version with a changed API surface: update the newest unapproved, unreleased, comment-free revision; otherwise create a revision so reviewed history is preserved.
- Different package version: create a revision even when the API surface is unchanged, then carry approval forward from a matching approved revision.
- Released exact match: return the released revision unchanged because released revisions are immutable.

---

## Release Enforcement Logic

### Release date is the trigger

Pipelines do **not** fail just because approval is missing. Failure happens only when **all** of the following are true:

1. The package is marked as ready for release
2. A **release date** is present in the changelog
3. The version is **GA**
4. There is **no approved** APIView revision with a matching API surface

If the release date is not set, approval status is ignored.

### GA vs. pre-release versions

| Version Type | API Approval Required |
|---|---|
| GA | Required |
| Beta | Not required (namespace approval still applies) |
| Alpha / Dev | Not enforced |

For the detailed version classification rules (how versions are parsed, Copilot review requirements, and auto-archive behavior by version type), see [release-approval.md](https://github.com/Azure/azure-sdk-tools/blob/main/src/dotnet/APIView/docs/release-approval.md#2-ga-vs-preview-version-classification).

This prevents surprise failures right before release, since scheduled pipelines surface issues early.

---

## Common Scenarios

See [troubleshooting.md](https://github.com/Azure/azure-sdk-tools/blob/main/src/dotnet/APIView/docs/troubleshooting.md) for common questions like "Why didn't my PR create an APIView revision?" and "My PR has no API changes but the release is blocked".

---

## Design Principles

- Do **not** require approvals for version-only changes
- Surface issues **early** via PR and scheduled pipelines
- Only **block** at release time, and only for GA versions
- **Namespace approval** is still required for Beta releases (first release of a new package)
- Reuse approvals automatically when API surface is unchanged
