# Skill Validation Procedures

Run these validations when reviewing or authoring skills.

## Automated Validation

```bash
cd scripts
npm run references              # Check broken links and cross-skill escapes
npm run tokens -- check         # Check token limits
```

## Validation Checks

| # | Check | Description |
|---|-------|-------------|
| 1 | Broken Links | Verify all markdown links point to existing files (not folders) |
| 2 | Orphaned References | Find unreferenced files in `references/` |
| 3 | Token Splitting | Split references exceeding 1000 tokens |
| 4 | Duplicate Content | Consolidate repeated content |
| 5 | Out-of-Place Guidance | Find misplaced service-specific content |

## Quick Checklist

| Check | Action if Failed |
|-------|------------------|
| Broken links | Fix path, add `/README.md` for folder links, or delete |
| Folder links | Change to file links (e.g., `recipes/` → `recipes/README.md`) |
| Orphaned references | Delete, add link, or user decision |
| References >1000 tokens | Split into folder with README.md |
| Duplicate content | Consolidate into shared reference |
| Out-of-place guidance | Extract, create skill, add section, or user decision |

## Broken Link Verification

### Always Link to Files, Not Folders

| ❌ Invalid | ✅ Valid |
|------------|----------|
| `[Recipes](references/recipes/)` | `[Recipes](references/recipes/README.md)` |

### Use Descriptive Link Text, Not Paths

| ❌ Wasteful | ✅ Concise |
|-------------|------------|
| `[references/guide.md](references/guide.md)` | `[Guide](references/guide.md)` |

### Automated Check

```bash
cd scripts
npm run references              # Validate all skills
npm run references <skill-name> # Validate a single skill
```

This checks:
1. Every local markdown link resolves to an existing file
2. No link escapes the skill's own directory (cross-skill links)
3. Ignores external URLs, `mailto:`, and fragment-only links

When broken link found, ask user: fix reference, delete link, or something else.

## Orphaned Reference Detection

1. List all files in `references/` directory (recursively)
2. Collect all link targets from SKILL.md and linked references
3. Compare: any file not in the link targets is orphaned

When orphaned reference found, ask user: delete reference, add link, or something else.

## Duplicate Content Consolidation

Indicators: same code blocks in multiple files, identical troubleshooting steps, repeated tables.

Procedure:
1. Identify duplicate content (3+ lines repeated in 2+ files)
2. Extract to a new reference file
3. Replace duplicates with links to the consolidated reference

## Out-of-Place Guidance Detection

Indicators of misplaced content:
- Generic workflow steps with service-specific workarounds embedded
- Platform-agnostic instructions mentioning a particular Azure service
- General commands followed by special handling for one service

| Context | Misplaced Content | Why It's Misplaced |
|---------|-------------------|-------------------|
| Generic `azd up` workflow | "Note: For Cosmos DB, add `--no-prompt` flag" | Service-specific workaround in generic flow |
| General authentication docs | "If using Azure SQL, also grant db_owner role" | SQL-specific step in auth overview |

When found, ask user: extract to service-specific reference, create new skill, add conditional section, keep as-is, or something else.

## Token Splitting

References exceeding 1000 tokens should be split:

```
# Before
references/large-guide.md          # 1500 tokens

# After
references/large-guide/
├── README.md           # Overview + links
├── section-1.md
└── section-2.md
```

Update original links: `[guide](references/large-guide.md)` → `[guide](references/large-guide/README.md)`
