---
# SKILL.md Template - Copy this file to your skill folder
# Spec: https://agentskills.io/specification

name: <skill name>
# name: Max 64 characters. Lowercase letters, numbers, and hyphens only. Must not start or end with a hyphen
# Must match parent directory name
description: |
    One-line description of what the skill does.
    USE FOR: List various scenarios
    DO NOT USE FOR: List any possible conflicting scenarios if applicable.
    TOOLS/COMMANDS: List mcp tools and commands
# Max 1024 chars in description
---

# Skill title

## MCP Tools Used

| MCP Tool | Purpose |
|----------|---------|
| `azsdk_xxx` | Reason to use |
| `azsdk_yyy` | Reason to use |

## CLI commands used

| CLI command | Purpose |
|----------|---------|
| `azsdk command` | Reason to use |
| `azsdk command` | Reason to use |

## Steps

### Step 1: Action Name

Invoke `azsdk_xxx` MCP tool or CLI command.

### Step 2: Process the output and invoke `azsdk_yyy`

### Step 3: Use skill package generation skill if step 2 completed successfully.


## Related Skills

- For X: `package generation`
- For Y: `prepare SDK release`

## References

List of reference files names within this skill for examples and commands
