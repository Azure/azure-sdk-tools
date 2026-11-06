---
name: code-review
description: "Review azsdk-cli behavior and Python maintainability in PRs and diffs. WHEN: 'GitHub Copilot code review for azsdk-cli', 'CCR review of an azsdk-cli PR', 'Copilot review of tools/azsdk-cli changes', 'review Python code', 'Python maintainability review'."
license: MIT
metadata:
  author: Microsoft
  version: "1.0.0"
compatibility: "GitHub Copilot code review, copilot-chat, @microsoft/vally-cli 0.7.0"
---

# Code Review

Apply the azsdk-cli rules to changes under `tools/azsdk-cli/**` and the Python
guidance to Python changes. Do not apply azsdk-cli-specific rules to other tools.

## Process

1. Read the pull request intent, changed files, and applicable repository
   instructions. For azsdk-cli changes, read `tools/azsdk-cli/AGENTS.md`.
2. Identify the affected subsystem and trace changed behavior through its
   callers, registrations, response models, tests, mocks, and applicable
   design spec.
3. For azsdk-cli, apply the relevant [review rules](references/review-rules.md).
   For Python, also check [maintainability](references/python-maintainability.md).
4. Apply the [finding contract](references/finding-contract.md), keeping
   maintainability suggestions separate from behavioral findings.

Prefer no finding over a speculative or analyzer-owned comment.
