# GitHub Agent Workflow for Azure SDK Tooling

This document describes how to use [GitHub Agent Workflows (gh-aw)](https://github.github.com/gh-aw/setup/quick-start/) to build agentic automation. It covers architecture, authoring, development practices, known challenges, and current & planned workflows.

---

## Table of Contents

- [Overview](#overview)
- [What is a GitHub Agent Workflow?](#what-is-a-github-agent-workflow)
- [Architecture](#architecture)
  - [Markdown Workflow File (.md)](#markdown-workflow-file-md)
  - [Compiled Lock File (.lock.yml)](#compiled-lock-file-lockyml)
  - [Shared Imports](#shared-imports)
- [Creating a GitHub Agent Workflow](#creating-a-github-agent-workflow)
  - [Quick Start](#quick-start)
  - [Workflow Structure](#workflow-structure)
  - [Triggering Mechanisms](#triggering-mechanisms)
  - [Shared Imports](#shared-imports-1)
- [Development and Testing](#development-and-testing)
  - [Workflows Without Azure Authentication](#workflows-without-azure-authentication)
  - [Workflows With Azure Authentication](#workflows-with-azure-authentication)
- [Challenges and Known Issues](#challenges-and-known-issues)
  - [Azure Authentication](#azure-authentication)
  - [COPILOT_GITHUB_TOKEN Requirement](#copilot_github_token-requirement)
  - [Lock File Commit SHA Constraint](#lock-file-commit-sha-constraint)
- [Current Agent Workflows](#current-agent-workflows)
  - [SDK Generation Agent Workflow (POC)](#sdk-generation-agent-workflow-poc)
- [Planned Agent Workflows](#planned-agent-workflows)
  - [Update Release Plan on TypeSpec PR Creation](#update-release-plan-on-typespec-pr-creation)
  - [Run SDK Generation on TypeSpec PR Merge](#run-sdk-generation-on-typespec-pr-merge)

---

## Overview

GitHub Agent Workflows combine GitHub Actions infrastructure with AI-powered Copilot prompts to create **agentic workflows** — automations that can reason about context, make decisions, and perform multi-step tasks. By authoring workflows as markdown files with embedded AI prompts and frontmatter configuration, teams can build intelligent pipelines.

For the Azure SDK team, agent workflows can enable scenarios such as:

- Automatically generating SDKs from TypeSpec specifications.
- Auto update release plans with metadata extracted from TypeSpec projects.
- Auto create a release plan whenever a spec PR is merged with a new API version

---

## What is a GitHub Agent Workflow?

A GitHub Agent Workflow (`gh-aw`) is an automation framework built on top of GitHub Actions that:

1. **Accepts GitHub events** (issues, comments, PRs, manual dispatch) as triggers.
2. **Runs frontmatter steps** (standard GitHub Actions steps) for setup tasks like authentication, CLI installation, and environment preparation.
3. **Executes AI prompts** (workflow prompts written in Markdown) that instruct a Copilot agent to perform reasoning, API calls, and multi-step orchestration.
4. **Produces safe outputs** such as issue comments, status updates, and `noop` signals.

The combination of deterministic setup steps and AI-driven decision-making makes agent workflows well-suited for complex, context-dependent automation.

---

## Architecture

### Markdown Workflow File (.md)

The primary authoring format for agent workflows is a **Markdown file** placed in `.github/workflows/`. The file contains:

- **YAML frontmatter** — defines triggers (`on:`), permissions, environment variables, steps, imports, tools, and safe outputs.
- **Markdown body** — contains AI prompts and instructions that the Copilot agent follows at runtime.

Example: [sdk-generation-agent.md](https://github.com/Azure/azure-rest-api-specs/blob/main/.github/workflows/sdk-generation-agent.md)

### Compiled Lock File (.lock.yml)

GitHub compiles the `.md` file into a `.lock.yml` file using `gh aw compile`. The agent workflow is **triggered from the `.lock.yml` file**, not the `.md` file directly.

> **Important:** Both the `.md` file and the `.lock.yml` file must have the **same commit SHA**. If they are out of sync, the agent workflow will not execute the Copilot step. Always run `gh aw compile` after modifying the `.md` file and commit both files together.

### Shared Imports

Shared imports are reusable markdown fragments that can be shared across agent workflows(e.g., `.github/workflows/shared-github-aw-imports/`). They contain their own frontmatter steps and workflow prompts that get composed into the main workflow.

**Execution order:**

1. **Frontmatter steps** from shared imports execute first, in the order they are listed.
2. **Frontmatter steps** from the main workflow `.md` file execute next.
3. **Workflow prompts** from shared imports execute after all frontmatter steps have completed.
4. **Workflow prompts** from the main workflow `.md` file execute last.

This means all deterministic setup steps (from both imports and the main file) run before any AI prompts, and common setup tasks (authentication, CLI installation) can be abstracted into shared components.

---

## Creating a GitHub Agent Workflow

### Quick Start

Follow the [gh-aw Quick Start Guide](https://github.github.com/gh-aw/setup/quick-start/) to set up the `gh-aw` CLI and create your first workflow.

The general steps are:

1. **Install the `gh-aw` CLI extension:**

   ```bash
   gh extension install github/gh-aw
   ```

2. **Author the workflow** as a Markdown file in `.github/workflows/`.

   `gh-aw` provides a built-in template and prompt that can generate the workflow Markdown file for you via Copilot. Use the template to scaffold a new workflow, and Copilot will produce the frontmatter and prompt structure based on your description. See [Creating Workflows with VS Code / Claude / Codex / Copilot](https://github.github.com/gh-aw/setup/creating-workflows/#vscodeclaudecodexcopilot) for details.

3. **Compile the workflow** to generate the `.lock.yml` file:

   ```bash
   gh aw compile <markdown file>
   ```

4. **Commit both files** (`.md` and `.lock.yml`) in the same commit.

5. **Push to the repository** to make the workflow available.

### Workflow Structure

A typical agent workflow `.md` file contains the following sections:

| Section | Purpose |
|---|---|
| `description` | Short description of the workflow |
| `on` | GitHub event triggers (issues, issue_comment, pull_request, workflow_dispatch, etc.) |
| `if` | Conditional expression to filter events |
| `permissions` | GitHub token permissions required |
| `imports` | List of shared import Markdown files |
| `env` | Environment variables |
| `steps` | Frontmatter steps (standard GitHub Actions steps) |
| `tools` | GitHub Copilot tool configuration |
| `safe-outputs` | Allowed output actions (comments, noop, etc.) |
| `strict` | Whether to enforce strict mode |
| Markdown body | AI prompts and instructions for the Copilot agent |

### Triggering Mechanisms

GitHub Agent Workflows support various triggering mechanisms through the `on:` frontmatter field:

| Trigger | Description |
|---|---|
| `issues` | Triggered when an issue is opened, labeled, edited, etc. |
| `issue_comment` | Triggered when a comment is created on an issue or PR |
| `pull_request` | Triggered on PR events (opened, synchronized, merged, etc.) |
| `workflow_dispatch` | Manual trigger with optional input parameters |
| `push` | Triggered on pushes to specified branches |
| `schedule` | Triggered on a cron schedule |

Triggers can be combined with the `if:` condition to further filter events. For example, triggering only when a specific label is applied or when a comment contains a specific keyword.

### Shared Imports

To import shared workflow fragments, use the `imports` field in the frontmatter:

For e.g.

```yaml
imports:
  - shared-github-aw-imports/install_azsdk_cli_import.md
  - shared-github-aw-imports/global_networks_auth_import.md
```

Each imported file can define its own `steps`, `network` allowlists, environment variables, and Markdown prompts. These are composed into the final compiled workflow.

---

## Development and Testing

Development approach depends on whether the agent workflow requires Azure authentication.

### Workflows Without Azure Authentication

If the agent workflow **does not** require Azure authentication and only interacts with GitHub APIs:

1. **Fork the repository** to your personal GitHub account.
2. **Develop and test** the agent workflow entirely in your fork.
3. Iterate on the `.md` file, compile with `gh aw compile`, and trigger the workflow from your fork.
4. Once the workflow is validated, open a pull request to merge it upstream.

This approach keeps experimental workflows isolated and avoids any impact on the upstream repository.

### Workflows With Azure Authentication

If the agent workflow **requires** Azure authentication (e.g., managed identity, Azure Key Vault access):

1. **Create a baseline agent workflow** with a **noop** in the workflow step (i.e., the Copilot prompt simply calls the `noop` safe output without performing any real work).
2. **Merge the baseline workflow** to the upstream repository's `main` branch. This is necessary because Azure federated identity and OIDC token exchange only work from the upstream repository's configured identity.
3. Once merged, the agent workflow is available for **manual dispatch from any branch** in the upstream repository.
4. **Iterate on the workflow** by creating branches in the upstream repository, modifying the `.md` file, compiling, and triggering via `workflow_dispatch` from your branch.

This two-phase approach ensures that Azure authentication is properly configured from the start while still allowing iterative development.

---

## Challenges and Known Issues

### Azure Authentication

Azure authentication inside agent workflows requires additional setup:

- **Managed identity authentication** — The workflow must acquire an OIDC token and use it to authenticate with Azure via federated credentials. This involves `id-token: write` permission and explicit CLI login steps.
- **Network allowlisting** — The agent workflow runner must be able to reach Azure endpoints (`login.microsoftonline.com`, `azure.com`, `visualstudio.com`, etc.). These must be explicitly allowed in the workflow's `network.allowed` configuration.

To abstract this complexity, a **shared import workflow** is available currently in azure-rest-api-specs repo:

> **[global_networks_auth_import.md](https://github.com/Azure/azure-rest-api-specs/blob/main/.github/workflows/shared-github-aw-imports/global_networks_auth_import.md)**

This shared import handles:

- OIDC token acquisition via `actions/github-script`.
- Azure CLI login using workload identity federation.
- GitHub CLI installation and `gh-aw` extension setup.
- Fetching secrets from Azure Key Vault.
- Setting the `COPILOT_GITHUB_TOKEN` via `gh aw secrets set`.

Include it in your workflow's `imports` to avoid re-implementing authentication logic:

```yaml
imports:
  - shared-github-aw-imports/global_networks_auth_import.md
```

### COPILOT_GITHUB_TOKEN Requirement

Agent workflows currently require a `COPILOT_GITHUB_TOKEN` to be set as a GitHub Actions secret. This token is used for **Copilot request billing**.

- The fine grained PAT with `copilot-requests` permission must be provisioned and added as GitHub actions secret.

> **Note:** This is a **temporary limitation**. Per discussions with the gh-aw team, once **organization-wide billing** for Copilot requests is supported, the `COPILOT_GITHUB_TOKEN` PAT will no longer be required.

### azsdk MCP Server Limitation

GitHub Agent Workflows cannot host or run the azsdk MCP server inline, so MCP-based interactions are unavailable inside the Copilot step. However, the Copilot step can still issue any supported azsdk CLI command (via standard GitHub Actions tooling), so prompts may continue to reference azsdk CLI operations for current automation scenarios.

### Lock File Commit SHA Constraint

The compiled `.lock.yml` file and the source `.md` file must share the **same commit SHA**. If they diverge (e.g., the `.md` is updated without recompiling), the agent workflow will skip the Copilot step.

**Best practice:** Always run `gh aw compile` immediately after editing the `.md` file and commit both files together.

---

## Current Agent Workflows

### SDK Generation Agent Workflow (POC)

**SDK Generation Agent** automates SDK generation from TypeSpec specifications, which will be replacing the curent flow that relies on GitHub Coding Agent (which required creating a placeholder PR).

**Workflow file:** [sdk-generation-agent.md](https://github.com/Azure/azure-rest-api-specs/blob/main/.github/workflows/sdk-generation-agent.md)

#### How It Works

1. **Trigger:** The workflow is triggered when:
   - An issue is created or labeled with the `Run sdk generation` label.
   - A comment containing `Regenerate SDK` is posted on an issue with the `Run sdk generation` label.
   - A manual `workflow_dispatch` is invoked with an `issue_url` input.

2. **Validation:** The agent verifies that:
   - A release plan exists in the issue context.
   - TypeSpec project details (path, API version, languages) are present.
   - The triggering event meets the required conditions.

3. **SDK generation:** For each language mentioned in the issue context, the agent:
   - Calls `azsdk spec-workflow generate-sdk` with the TypeSpec path, API version, release type, and language.
   - Monitors pipeline status every 5 minutes.
   - Retrieves SDK pull request links from the pipeline run.

4. **Reporting:** The agent posts status updates and SDK PR links as comments on the originating issue, and links SDK pull requests to the release plan.

#### Shared Workflow Steps

The SDK generation agent uses two shared imports:

1. **[install_azsdk_cli_import.md](https://github.com/Azure/azure-rest-api-specs/blob/main/.github/workflows/shared-github-aw-imports/install_azsdk_cli_import.md)** — Checks out the repository and installs the `azsdk` CLI tool.
2. **[global_networks_auth_import.md](https://github.com/Azure/azure-rest-api-specs/blob/main/.github/workflows/shared-github-aw-imports/global_networks_auth_import.md)** — Handles Azure authentication (OIDC, Azure CLI login) and sets the `COPILOT_GITHUB_TOKEN`.

#### Improvement Over GitHub Coding Agent

Previously, SDK generation was triggered by assigning a GitHub issue to the GitHub Coding Agent. The Coding Agent would create a **placeholder pull request** solely to run the SDK generation pipeline. This added noise and extra steps to the process.

The GitHub Agent Workflow approach **eliminates the placeholder PR** entirely. The workflow runs directly from the GitHub issue, orchestrates SDK generation, and reports results back to the issue — resulting in a cleaner, more streamlined experience.

---

## Agent Workflows to be added

### Update Release Plan on TypeSpec PR Creation

**Goal:** Automatically update release plan details when a TypeSpec pull request is created for a TypeSpec project.

**Current state:** This is done manually by users, which is error-prone (e.g., invalid API versions, incorrect package names).

**Proposed behavior:**

1. Trigger on `pull_request` events for TypeSpec projects.
2. Check if an active release plan is in progress for the TypeSpec project.
3. Parse the `tspconfig.yaml` file in the TypeSpec project to extract:
   - Package name details per language.
   - API version information.
   - Other SDK metadata.
4. Automatically update the release plan with accurate, machine-parsed values.

**Benefits:**

- Eliminates manual data entry errors.
- Ensures release plan metadata is always consistent with the actual TypeSpec configuration.
- Reduces friction for service teams submitting TypeSpec specifications.

### Run SDK Generation on TypeSpec PR Merge

**Goal:** Automatically trigger SDK generation when a TypeSpec pull request is merged, so SDKs are ready for review before the user even checks.

**Proposed behavior:**

1. Trigger on `pull_request` merge events.
2. Check if an active release plan exists for the TypeSpec project in the merged PR.
3. If a release plan is found, automatically trigger SDK generation for all configured languages.
4. Monitor pipeline progress and link resulting SDK pull requests to the release plan.
5. When the user visits the release planner or prompts the agent, the SDKs are already generated and ready for review.

**Benefits:**

- Proactive SDK generation eliminates wait time for users.
- Ensures SDKs are always up-to-date with the latest merged TypeSpec changes.
- Reduces the manual steps required in the end-to-end SDK release process.

---

## References

- [gh-aw Quick Start Guide](https://github.github.com/gh-aw/setup/quick-start/)
- [SDK Generation Agent Workflow](https://github.com/Azure/azure-rest-api-specs/blob/main/.github/workflows/sdk-generation-agent.md)
- [Shared Import: Azure Authentication](https://github.com/Azure/azure-rest-api-specs/blob/main/.github/workflows/shared-github-aw-imports/global_networks_auth_import.md)
- [Shared Import: azsdk CLI Installation](https://github.com/Azure/azure-rest-api-specs/blob/main/.github/workflows/shared-github-aw-imports/install_azsdk_cli_import.md)
- [GitHub Coding Agent Instructions](https://github.com/Azure/azure-rest-api-specs/blob/main/.github/instructions/github-codingagent.instructions.md)
