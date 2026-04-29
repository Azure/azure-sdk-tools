---
name: markdown-token-optimizer
description: "Analyze markdown files for token efficiency and reduce context-window bloat. **UTILITY SKILL**. DO NOT USE FOR: code optimization, general file editing, non-markdown files. TRIGGERS: optimize markdown, reduce tokens, token count, token bloat, too many tokens, make concise, shrink file, file too large, optimize for AI, token efficiency, verbose markdown, reduce file size. INVOKES: waza CLI."
license: MIT
metadata:
  author: Microsoft
  version: "1.0.0"
compatibility: "Platforms: copilot-chat."
---

# Markdown Token Optimizer

Analyzes markdown files and suggests optimizations to reduce token consumption while maintaining clarity.

## When to Use

- Optimize markdown files for token efficiency
- Reduce SKILL.md file size or check for bloat
- Make documentation more concise for AI consumption

## Workflow

1. **Count** - Calculate tokens (~4 chars = 1 token), report totals
2. **Scan** - Find patterns: emojis, verbosity, duplication, large blocks
3. **Suggest** - Table with location, issue, fix, savings estimate
4. **Summary** - Current/potential/savings with top recommendations

See [ANTI-PATTERNS.md](references/ANTI-PATTERNS.md) for detection patterns and [OPTIMIZATION-PATTERNS.md](references/OPTIMIZATION-PATTERNS.md) for techniques.

## Rules

- Suggest only (no auto-modification)  
- Preserve clarity in all optimizations
- SKILL.md target: <500 tokens, references: <1000 tokens

## Examples

- "Optimize this SKILL.md for tokens"
- "Count tokens in references/SCORING.md"

## Troubleshooting

High token count? Check for emojis, repeated headings, or verbose tables.

## MCP Tools

| Tool | Purpose |
|------|---------|
| None | CLI-only; uses local file tools |

**CLI fallback:** Primary mode is CLI-based. No MCP servers required.

## References

- [OPTIMIZATION-PATTERNS.md](references/OPTIMIZATION-PATTERNS.md) - Optimization techniques
- [ANTI-PATTERNS.md](references/ANTI-PATTERNS.md) - Token-wasting patterns
