# Skill Writing Guidelines

Best practices for writing Agent Skills per the [Agent Skills Specification](https://agentskills.io/specification).

## Quick Reference

| Constraint | Limit | Notes |
|------------|-------|-------|
| `name` field | 1-64 chars | Lowercase, hyphens only, no consecutive hyphens |
| `description` field | 1-1024 chars | Describe what AND when to use |
| SKILL.md body | < 5000 tokens | ~500 lines max |
| Reference files | < 1000 tokens | Load on demand |

## Frontmatter

### Required Fields

```yaml
---
name: my-skill-name
description: A clear description of what this skill does and when to use it.
---
```

**`name` rules:**
- 1-64 characters, lowercase letters, numbers, hyphens only
- Must not start/end with `-` or contain `--`
- Must match parent directory name

**`description` rules:**
- 1-1024 characters
- Should describe BOTH what the skill does AND when to use it
- Include keywords that help agents identify relevant tasks

### Optional Fields

```yaml
license: Apache-2.0
compatibility: Requires az CLI and docker
metadata:
  author: your-org
  version: "1.0"
```

### Activation Triggers

Use `WHEN:` with distinctive quoted trigger phrases (preferred for cross-model compatibility):

```yaml
# Good - WHEN: with quoted phrases (preferred)
description: "Perform Azure compliance assessments using azqr. WHEN: \"check compliance\", \"assess Azure resources\"."
```

> ⚠️ **Do NOT add "DO NOT USE FOR:" clauses.** They cause keyword contamination on Claude Sonnet and similar models.

## Directory Structure

```
my-skill/
├── SKILL.md              # Required: main instructions
├── references/           # Optional: detailed documentation
│   ├── api-reference.md
│   └── examples.md
├── templates/            # Optional: output templates
├── scripts/              # Optional: executable code
└── assets/               # Optional: templates, data files
```

| Folder | Purpose | Naming |
|--------|---------|--------|
| `references/` | Detailed docs, loaded on-demand | `lowercase-hyphens.md` |
| `templates/` | Output format templates | `lowercase-hyphens.md` |
| `scripts/` | Executable code | Language conventions |
| `assets/` | Data files, configs | File type conventions |

### Recipes Pattern

For skills with multiple implementation approaches, use `references/recipes/` with selective loading so only the chosen recipe loads.

### Services Pattern

For skills working with multiple cloud services, use `references/services/` with self-contained service files.

## Writing Effective Skills

### Progressive Disclosure

| Tier | Content | When Loaded |
|------|---------|-------------|
| **Metadata** | `name` + `description` | Startup (all skills) |
| **Instructions** | SKILL.md body | On activation |
| **Resources** | `references/`, `scripts/` | On demand |

**Key principle:** Keep SKILL.md lean, move details to references.

### How References Load

References load **only when explicitly linked** — NOT when the skill activates:

- ✅ `See [error guide](references/errors.md)` → Agent loads file
- ❌ "Error docs are in references/" → Agent won't find files

Per [agentskills Issue #97](https://github.com/agentskills/agentskills/issues/97):
- References are re-read each time (no caching)
- Write each reference as a **self-contained unit**
- The **entire file** loads when referenced (not sections)

### DO: Use Tables for Dense Information

### DON'T: Use Token-Wasting Patterns

- ❌ Decorative emojis throughout text
- ❌ Repeated headers with same content
- ❌ Verbose explanations for simple concepts
- ❌ Link text that duplicates the path
- ❌ Separate "Reference" columns with just hyperlinked paths

### File References

Use relative paths from the skill root with descriptive link text. Keep references one level deep.

## Resources

- [Agent Skills Specification](https://agentskills.io/specification)
- [What Are Skills](https://agentskills.io/what-are-skills)
- [Reference Loading Clarification (Issue #97)](https://github.com/agentskills/agentskills/issues/97)
- [Skill Token Limits (Issue #1130)](https://github.com/github/copilot-cli/issues/1130)
