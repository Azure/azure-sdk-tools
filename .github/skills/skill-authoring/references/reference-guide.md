# Reference Guide: Budgets, Loading & Checklist

## Token Budget Guidelines

| File Type | Soft Limit | Hard Limit | Action if Exceeded |
|-----------|------------|------------|-------------------|
| SKILL.md | 500 tokens | 5000 tokens | Split into references |
| references/*.md | 1000 tokens | 2000 tokens | Split into folder with README.md |
| docs/*.md | 1500 tokens | 3000 tokens | Restructure |

### Splitting Large References

When a reference exceeds 1000 tokens, convert to a folder structure:

```
references/large-guide.md    # 1500 tokens → split into:
references/large-guide/
├── README.md                # Overview + links (~200 tokens)
├── section-1.md             # (~400 tokens)
└── section-2.md             # (~400 tokens)
```

### Why Token Limits Matter

> **Units note:** Limits are measured in **tokens** (cl100k_base tokenizer), not words. 5000 tokens ≈ 3,750 words.

- **Metadata (~100 tokens)**: Loads at startup for ALL skills
- **SKILL.md (<5000 tokens)**: Loads entirely on activation
- **References**: Load only when explicitly linked

## Reference Loading Behavior

### Just-In-Time (JIT) Loading

Reference files are **NOT** loaded when a skill activates. They load **only when explicitly referenced** via a markdown link.

```markdown
<!-- This triggers a load -->
See [the guide](references/guide.md) for details.

<!-- This does NOT trigger a load -->
Documentation is available in the references folder.

<!-- This does NOT work - folder links don't load content -->
See [recipes](references/recipes/) for options.
```

### Link to Files, Not Folders

| ❌ Won't Load | ✅ Will Load |
|---------------|--------------|
| `[Languages](references/languages/)` | `[Languages](references/languages/README.md)` |
| `[Python](references/languages/python)` | `[Python](references/languages/python/README.md)` |

### No Caching Between Requests

Per [agentskills Issue #97](https://github.com/agentskills/agentskills/issues/97): reference files are fully loaded each time they are referenced.

Implications:
- Write each reference as a **self-contained unit**
- The **entire file** loads when referenced (not sections)
- Split large topics into separate files, each < 1,000 tokens

### Token Efficiency: Selective Loading

```markdown
## SDK Release Workflow
Which language SDK are you releasing?
- [Python](references/languages/python/README.md) - PyPI release
- [JavaScript](references/languages/javascript/README.md) - npm release
<!-- Only the chosen language guide loads (~300 tokens), not all -->
```

### Skill Visibility Limits

From [GitHub Copilot CLI Issue #1130](https://github.com/github/copilot-cli/issues/1130): with many skills installed, not all appear in the available skills list. Keep `description` concise but keyword-rich.

## Submission Checklist

### Frontmatter
- [ ] `name`: present, 1-64 chars, lowercase + hyphens, matches directory, no reserved prefixes
- [ ] `description`: present, 1-1024 chars, ≤60 words, explains WHAT and WHEN
- [ ] `description` uses `WHEN:` with quoted trigger phrases
- [ ] No XML angle brackets in frontmatter

### Token Budget
- [ ] SKILL.md under 500 tokens (soft) / 5000 tokens (hard)
- [ ] Reference files each under 1000 tokens
- [ ] Run `npm run tokens -- check` from `scripts/`

### Structure
- [ ] SKILL.md exists in skill root
- [ ] File references use relative paths, no deep chains
- [ ] All links point to files (not folders), no broken links, no orphans

### Content Quality
- [ ] Action-oriented instructions with examples
- [ ] Tables for dense information
- [ ] No decorative emojis, no repeated content
- [ ] Complex details in `references/`

### Azure-Specific
- [ ] Prefers azsdk MCP/CLI tools over direct CLI
- [ ] Lists relevant MCP tools in "Tools Used" section
- [ ] Includes troubleshooting section

### Final Steps

```bash
cd scripts
npm run references        # Validate all skill links
npm run tokens -- check   # Check token limits
```
