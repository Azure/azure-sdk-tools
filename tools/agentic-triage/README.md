# Agentic Issue Triage

Reference example and triage rules for GitHub Agentic Triage across Azure SDK language repos. Zero ML training needed — just a markdown file and CODEOWNERS.

## Architecture

Each repository owns a fully self-contained workflow file at `.github/workflows/issue-triage.md` containing frontmatter (triggers, permissions, jobs) and triage instructions inline. There is no shared-file sync — each repo copies the example and customizes freely.

```
tools/agentic-triage/
├── triage-rules.md                                   ← Triage rules spec (human-readable)
├── examples/java-issue-triage.md                     ← Reference example (copy, customize, compile, run)
└── README.md                                         ← This file

your-repo/
└── .github/workflows/issue-triage.md                 ← Your repo's workflow (fully self-contained)
```

The **[triage-rules.md](triage-rules.md)** is the human-readable specification of all triage rules. The **[example workflow](examples/java-issue-triage.md)** is the reference implementation that embeds these rules as agent instructions.

## Quick Start

1. Copy the Java example (or adapt for your language):
   ```bash
   cp tools/agentic-triage/examples/java-issue-triage.md  /path/to/your-repo/.github/workflows/issue-triage.md
   ```

2. Update language-specific sections (package naming, search examples, docs URLs, `allowed-repos`)

3. Compile & push:
   ```bash
   cd /path/to/your-repo
   gh aw compile .github/workflows/issue-triage.md
   git add .github/workflows/issue-triage.md .github/workflows/issue-triage.lock.yml
   git commit -m "Add agentic issue triage workflow"
   git push
   ```

**Prerequisites:** `gh` CLI, `gh aw` extension (`gh extension install github/gh-aw`), `COPILOT_GITHUB_TOKEN` repo secret, `copilot-setup-steps.yml` in `.github/workflows/`.

## Current State

| Repo | Status | Notes |
|------|--------|-------|
| **azure-sdk-for-rust** | ✅ Live | Reference implementation |
| **azure-sdk-for-net** | ✅ Live | Most advanced — NuGet deprecation checks, `mention_owners` job |
| **azure-sdk-for-js** | ✅ Live | npm deprecation checks, multiple agent workflows |
| **azure-sdk-for-java** | ❌ Missing | |
| **azure-sdk-for-python** | ❌ Missing | |

## What to Customize Per Repo

When copying the example, update these sections for your language:

| Section | What to change |
|---------|---------------|
| Frontmatter `description` | Language name |
| Frontmatter `allowed-repos` | Your repo path |
| Language-Specific Context | Package naming, search examples, docs/API URLs, troubleshooting guide paths |
| Deprecation checks | Add language-specific checks (e.g. NuGet for .NET, npm for JS) |
| `mention_owners` job | Include if your repo needs @mention routing; remove if not needed |

## Relationship to Existing Tools

| Tool | Role | What changes |
|------|------|-------------|
| `tools/github-event-processor` | Canonical label/routing rules, lifecycle transitions | **No change** — agentic triage aligns with its rules |
| `tools/issue-labeler` | ML-based label prediction | **Replaced** by agentic triage (no training data needed) |

## Process Changes

When triage rules change:
1. Update the reference example in this repo
2. Open an issue in each language repository with the exact changes needed
3. Repository owners update their workflow and recompile

## Maintenance

- **Rule changes** → update the example here, propagate via issues to each repo
- **Per-repo changes** → edit `.github/workflows/issue-triage.md` in the language repo directly
- **Recompile** (`gh aw compile`) after ANY change to the workflow
- **CODEOWNERS changes** → no workflow change needed (read dynamically)