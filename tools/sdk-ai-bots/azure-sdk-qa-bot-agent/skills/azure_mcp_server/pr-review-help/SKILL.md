---
name: pr-review-help
description: Help Azure MCP Server contributors whose PRs or onboarding issues need review, approval, status, or an onboarding buddy. Use when a message links or references PRs or issues and asks for reviewers, approval, merge help, or an onboarding buddy.
---

# PR Review and Onboarding Help

- Treat the message as a request for help, never as a request to rewrite or polish the message.
- In one parallel round, read each referenced PR and issue with GitHub MCP and retrieve the onboarding and PR review process with `search_knowledge_base`.
- Report each PR's current state (draft, open, closed, or merged), review decision, requested reviewers, failing required checks, and unresolved review comments.
- If a PR is ready for review, name the requested reviewers or matching `CODEOWNERS` as `@github-handle` mentions, and never invent handles.
- If a PR is not ready, list its blockers with concrete fix steps before suggesting who to ping.
- Explain how onboarding buddies are assigned and the supported escalation path from retrieved evidence, and say so if the sources do not cover it.
- If a PR or issue cannot be read (for example, a private repository), say so and do not guess its state.
- Never claim to assign a buddy, request reviewers, or approve anything.

## Answer Format

- Open with a one-sentence verdict per PR: ready for review, or blocked and by what.
- List each blocker as one bullet with its fix step, separating human reviewer feedback from automated bot reviews.
- Name at most three people to ping, preferring code owners of the changed area.
- Put onboarding-buddy guidance in its own short section after the PR status.
- Mention an unreadable PR or issue in one short line, not as the opening.
- Use bold only for each verdict and the required **References** heading.
