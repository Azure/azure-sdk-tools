---
name: skill-authoring
description: "Write Agent Skills that comply with the agentskills.io specification. WHEN: \"create a skill\", \"new skill\", \"write a skill\", \"skill template\", \"skill structure\", \"review skill\", \"skill PR\", \"skill compliance\", \"SKILL.md format\", \"skill frontmatter\", \"skill best practices\". DO NOT USE FOR: improving existing skills (use sensei), general documentation. INVOKES: waza CLI."
license: MIT
metadata:
  author: Microsoft
  version: "1.0.0"
compatibility: "copilot-chat"
---

# Skill Authoring Guide

This skill helps write Agent Skills that comply with the agentskills.io specification by defining valid frontmatter, structure, and routing patterns, while guiding authors toward compliant `SKILL.md` files, supporting references, and local validation with the waza CLI.

## Triggers

USE FOR: write Agent Skills that comply with the agentskills.io specification; skill template; skill structure; review skill; skill PR; skill compliance; SKILL.md format; skill frontmatter; skill best practices
WHEN: "create a skill", "new skill", "write a skill", "skill template", "skill structure", "review skill", "skill PR", "skill compliance", "SKILL.md format", "skill frontmatter", "skill best practices"
DO NOT USE FOR: improving existing skills (use sensei), general documentation

## Rules

- `name`: 1-64 chars, lowercase + hyphens, match directory
- `description`: inline double-quoted, ≤60 words, ≤1024 chars
- Use `WHEN:` with quoted trigger phrases (preferred over `USE FOR:`)
- SKILL.md: <500 tokens (soft), <5000 (hard)
- references/*.md: <1000 tokens each

## Structure

- `SKILL.md` (required) — Frontmatter + instructions
- `references/` (optional) — Detailed docs, loaded on demand
- `scripts/` (optional) — Executable code

## Formatting

- run `npm ci` in `.github/skills` to install dependencies
- run `npm run format` to auto-format markdown and yaml files using Prettier

## Progressive Disclosure

Metadata loads at startup. SKILL.md on activation. References load when linked via markdown link.

## MCP Tools

| Tool | Purpose |
|------|---------|
| None | CLI-only; uses local file tools |

**CLI fallback:** Primary mode is CLI-based. No MCP servers required.

## Steps

1. Create skill directory with `SKILL.md`
2. Add frontmatter: `name`, `description`, `license`
3. Run `waza check {skill-name}` to validate

## Examples

- "Create a new skill for code review"
- "Review my skill for compliance"

## Troubleshooting

If `waza check` reports broken links, verify reference file paths match exactly.

## References

- [Guidelines](references/guidelines.md) — Writing and structure
- [Validation](references/validation.md) — Checks and procedures
- [Reference Guide](references/reference-guide.md) — Budgets, loading, checklist
