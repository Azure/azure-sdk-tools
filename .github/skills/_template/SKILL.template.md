---
# SKILL.md Template - Copy this file to your skill folder
# Spec: https://agentskills.io/specification

name: "your-skill-name"
# Max 64 chars, lowercase letters/numbers/hyphens only
# Must match parent directory name

description: |
  Does X for Y scenarios. Use when working with X files,
  analyzing Y data, or when the user mentions Z operations.
# Max 1024 chars. Describe WHAT it does and WHEN to use it.
---

# Your Skill Name

## When to Use

- Scenario that should trigger this skill
- Another relevant use case
- Domain-specific task

## Quick Start

```python
# Minimal example showing the core workflow
example_code_here()
```

## Workflow

1. **Step 1**: Description
2. **Step 2**: Call `azsdk_your_tool` with parameters
3. **Step 3**: Verify output

## Related Tools

| Tool | Use When |
|------|----------|
| `azsdk_your_tool` | Execute the actual operation |

## Troubleshooting

**Common error**: Solution

See [references/](references/) for detailed scenarios.

---
*Spec limits: SKILL.md < 5000 tokens (~500 lines), description < 1024 chars*
