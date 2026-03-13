---
name: skill-authoring
description: "Write Agent Skills that comply with the agentskills.io specification. WHEN: \"create a skill\", \"new skill\", \"write a skill\", \"skill template\", \"skill structure\", \"review skill\", \"skill PR\", \"skill compliance\", \"SKILL.md format\", \"skill frontmatter\", \"skill best practices\". DO NOT USE FOR: improving existing skills (use sensei), general documentation. INVOKES: waza CLI."
license: MIT
metadata:
  author: Microsoft
  version: "1.0.0"
compatibility:
  platforms: "copilot-chat"
---

# Skill Authoring Guide

## Constraints

- `name`: 1-64 chars, lowercase + hyphens, match directory
- `description`: inline double-quoted, ≤60 words, ≤1024 chars
- Use `WHEN:` with quoted trigger phrases (preferred over `USE FOR:`)
- SKILL.md: <500 tokens (soft), <5000 (hard)
- references/*.md: <1000 tokens each

## Structure

- `SKILL.md` (required) — Frontmatter + instructions
- `references/` (optional) — Detailed docs, loaded on demand
- `scripts/` (optional) — Executable code

## Progressive Disclosure

Metadata loads at startup. SKILL.md on activation. References load when linked via `[text](references/file.md)`.

## Prerequisites

No MCP servers required. Uses waza CLI only.

## Quick Start

1. Create skill directory with `SKILL.md`
2. Add frontmatter: `name`, `description`, `license`
3. Run `waza check {skill-name}` to validate

## Examples

- "Create a new skill for code review"
- "Review my skill for compliance"

## Troubleshooting

If `waza check` reports broken links, verify reference file paths match exactly.

## References

- [Guidelines](references/guidelines/README.md) — Writing guidelines
- [Token Budgets](references/token-budgets.md) — Limits and splitting
- [Reference Loading](references/REFERENCE-LOADING.md) — How references load
- [Checklist](references/CHECKLIST.md) — Pre-submission checklist
- [Validation](references/validation/README.md) — Link and reference validation
