---
name: code-review
description: "Guide GitHub Copilot code review (CCR) for PRs and diffs that change tools/azsdk-cli, focusing on high-confidence behavioral and integration defects beyond analyzers. WHEN: 'GitHub Copilot code review for azsdk-cli', 'CCR review of an azsdk-cli PR', 'Copilot review of tools/azsdk-cli changes'."
license: MIT
metadata:
  author: Microsoft
  version: "1.0.0"
compatibility: "GitHub Copilot code review, copilot-chat, @microsoft/vally-cli 0.7.0"
---

# azsdk-cli Code Review

Apply this skill to GitHub Copilot code review when changed behavior is under
`tools/azsdk-cli/**`. For other changes, do not apply azsdk-cli-specific
guidance.

## Process

1. Read the pull request intent, changed files, and
   `tools/azsdk-cli/AGENTS.md`.
2. Identify the affected subsystem and trace changed behavior through its
   callers, registrations, response models, tests, mocks, and applicable
   design spec.
3. Load and apply only the relevant [review rules](references/review-rules.md).
4. Before reporting anything, enforce the
   [finding contract](references/finding-contract.md).

Prefer no finding over a speculative or analyzer-owned comment.
