---
name: sensei
description: "**WORKFLOW SKILL** — Iteratively improve skill frontmatter compliance using the Ralph loop pattern. WHEN: \"run sensei\", \"sensei help\", \"improve skill\", \"fix frontmatter\", \"skill compliance\", \"frontmatter audit\", \"score skill\", \"check skill tokens\". INVOKES: waza CLI, git. FOR SINGLE OPERATIONS: use waza check directly."
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

Use `--skip-integration` for faster iteration (trigger tests only).

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

## References

- [SCORING.md](references/SCORING.md) — Scoring criteria
- [LOOP.md](references/LOOP.md) — Detailed workflow
- [EXAMPLES.md](references/EXAMPLES.md) — Before/after examples
- [TOKEN-INTEGRATION.md](references/TOKEN-INTEGRATION.md) — Token budgets

## Related Skills

- **markdown-token-optimizer** — Token optimization
- **skill-authoring** — Skill writing guidelines
