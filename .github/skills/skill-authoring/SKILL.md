---
name: skill-authoring
description: "Guide writing of Agent Skills compliant with agentskills.io specification. WHEN: \"create a skill\", \"new skill\", \"write a skill\", \"skill template\", \"skill structure\", \"review skill\", \"skill PR\", \"skill compliance\", \"SKILL.md format\", \"skill frontmatter\", \"skill best practices\". INVOKES: waza CLI."
license: MIT
metadata:
  author: Microsoft
  version: "1.0.0"
compatibility:
  platforms: "copilot-chat"
---

# Skill Authoring Guide

Write Agent Skills that comply with the [agentskills.io specification](https://agentskills.io/specification).

## Constraints

- `name`: 1-64 chars, lowercase + hyphens, match directory
- `description`: inline double-quoted string, ≤60 words, ≤1024 chars
- Use `WHEN:` with quoted trigger phrases (preferred over `USE FOR:`)
- SKILL.md: <500 tokens (soft), <5000 (hard)
- references/*.md: <1000 tokens each

## Structure

- `SKILL.md` (required) — Frontmatter + instructions
- `references/` (optional) — Detailed docs, loaded on demand
- `scripts/` (optional) — Executable code

## Progressive Disclosure

Metadata loads at startup. SKILL.md loads on activation. References load **only when explicitly linked** via `[text](references/file.md)` — not on activation. Keep SKILL.md lean.

## Validation

Run `waza check {skill-name}` to validate compliance, token budget, and links.

## Reference Documentation

- [Guidelines](references/guidelines/README.md) — Writing guidelines
- [Token Budgets](references/token-budgets.md) — Limits and splitting
- [Reference Loading](references/REFERENCE-LOADING.md) — How references load
- [Checklist](references/CHECKLIST.md) — Pre-submission checklist
- [Validation](references/validation/README.md) — Link and reference validation
