---
name: sensei
description: "Improve skill frontmatter compliance iteratively using the Ralph loop pattern. **WORKFLOW SKILL**. WHEN: \"run sensei\", \"sensei help\", \"improve skill\", \"fix frontmatter\", \"skill compliance\", \"frontmatter audit\", \"score skill\", \"check skill tokens\". INVOKES: waza CLI, git."
license: MIT
metadata:
  author: Microsoft
  version: "1.0.0"
compatibility:
  platforms: "copilot-chat"
---

# Sensei

Automates skill frontmatter improvement via the Ralph loop — iteratively improving skills until they pass waza check compliance.

## Usage

```
Run sensei on <skill-name>
Run sensei on <skill1>, <skill2>
Run sensei on all skills
```


## The Ralph Loop

For each skill, repeat until compliant (max 5 iterations):

1. **READ** — Load SKILL.md and current token count
2. **SCORE** — Run `waza check {skill-name}` for compliance
3. **FIX** — Address issues: tokens, broken links, frontmatter
4. **VERIFY** — Re-run `waza check`; loop if issues remain
5. **COMMIT** — `sensei: improve {skill-name} frontmatter`

Target: High compliance, ≤500 tokens, all links valid.

## Frontmatter Rules

- Use inline double-quoted `description` (not `>-` folded scalars)
- Lead with action verb + domain; add `WHEN:` trigger phrases
- Keep ≤60 words, ≤1024 chars
- Prefer `WHEN:` over `USE FOR:` for cross-model reliability

## Examples

- "Run sensei on pipeline-troubleshooting"
- "Run sensei on all skills"

## Prerequisites

No MCP servers required. Uses waza CLI and git only.

## Troubleshooting

If waza check fails to run, verify waza is installed and you are in the skills directory.

## References

- [SCORING.md](references/SCORING.md) — Scoring criteria
- [LOOP.md](references/LOOP.md) — Detailed workflow
- [EXAMPLES.md](references/EXAMPLES.md) — Before/after examples
- [TOKEN-INTEGRATION.md](references/TOKEN-INTEGRATION.md) — Token budgets

