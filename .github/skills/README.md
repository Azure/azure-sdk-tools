# Azure SDK Agent Skills

> **Note**: This is the **development and testing location** for skills. Skills are authored and tested here, then deployed to target repositories where users work (e.g., `azure-rest-api-specs`, `azure-sdk-for-*`).

## When to Use Skills vs Tools

See **[DECISION-GUIDE.md](DECISION-GUIDE.md)** for the full comparison. Quick summary:

| Use **Skill** when... | Use **MCP Tool** when... |
|-----------------------|--------------------------|
| Straightforward workflow with known steps | Complex logic, API calls, data processing |
| Guidance, checklists, best practices | Deterministic execution required |
| Fast iteration needed (no deployment) | Needs to wrap existing CLI commands |

## Development vs Production

| Location | Purpose |
|----------|---------|
| `azure-sdk-tools/.github/skills/` | **Development** - Author, test, validate skills |
| `azure-rest-api-specs/.github/skills/` | **Production** - TypeSpec-related skills |
| `azure-sdk-for-*/.github/skills/` | **Production** - SDK-specific skills |

Skills are discovered from the repo where the user is working.

## How to Add a New Skill

1. **Copy the template**: `cp -r _template/SKILL.template.md your-skill-name/SKILL.md`
2. **Edit SKILL.md**: Fill in name, description, and content per [agentskills.io spec](https://agentskills.io/specification)
3. **Add test prompts**: Create `tests/your-skill-name/prompts.json` (see [Testing](#testing))
4. **Validate**: Run `dotnet test --filter "Category=skills"` from `tools/azsdk-cli/Azure.Sdk.Tools.Cli.Evaluations`
5. **Deploy**: Copy to target repo's `.github/skills/`

## Example

See [typespec-new-project/](typespec-new-project/) for a complete example.

## Token Budgets

| Field | Limit |
|-------|-------|
| `name` | 1-64 chars, lowercase + hyphens |
| `description` | 1-1024 chars |
| SKILL.md body | < 5000 tokens |

## Testing

The eval framework validates that skills are correctly matched to user prompts using embedding-based similarity. See also: [skills-guidelines.md](../../tools/azsdk-cli/docs/skills-guidelines.md) for full design details.

### Test Prompts Format

Each skill must have a `tests/{skill-name}/prompts.json` file with the following structure:

```json
{
  "skillName": "your-skill-name",
  "source": {
    "repo": "Azure/azure-rest-api-specs",
    "path": ".github/skills/your-skill-name/SKILL.md"
  },
  "shouldTrigger": [
    "Prompt that should trigger this skill",
    "Another prompt that should match"
  ],
  "shouldNotTrigger": [
    "Prompt that should NOT trigger this skill",
    "Another prompt that should match a different skill or no skill"
  ]
}
```

| Field | Required | Purpose |
|-------|----------|---------|
| `skillName` | Yes | Must match the `name` in SKILL.md frontmatter |
| `source` | Yes (external) | Points to the SKILL.md in its source repo. The framework fetches the live description at test time. |
| `shouldTrigger` | Yes | Prompts that **should** match this skill (positive cases) |
| `shouldNotTrigger` | Yes | Prompts that **should NOT** match this skill (negative cases) |

> **Note:** For local skills (with SKILL.md in this repo), `source` is not needed — the description is read directly from the frontmatter.

### Testing Skills from Other Repos

Skills live in their respective repos (e.g., `azure-typespec-author` in `azure-rest-api-specs`). To test them here, add a `source` field that points to the SKILL.md — the eval framework **fetches the live description from GitHub at test time**, so it's always up-to-date with no risk of stale cached data:

```json
{
  "skillName": "azure-typespec-author",
  "source": {
    "repo": "Azure/azure-rest-api-specs",
    "path": ".github/skills/azure-typespec-author/SKILL.md"
  },
  "shouldTrigger": ["Add a new ARM resource to my TypeSpec project"],
  "shouldNotTrigger": ["Generate SDK from my TypeSpec project"]
}
```

**How it works:**
1. At test time, the framework fetches `SKILL.md` from `raw.githubusercontent.com/{repo}/main/{path}`
2. Parses the YAML frontmatter to extract the current description
3. If the fetch fails, the test **fails with a clear error** — no silent fallback to stale data

The test framework merges skills from two sources:
1. **Local skills** — Loaded from `.github/skills/*/SKILL.md` (skills that live in this repo)
2. **External skills** — Fetched from GitHub via `source` in `tests/*/prompts.json` (skills in other repos)

### What the Tests Validate

| Test | What it checks |
|------|---------------|
| **ShouldTrigger** | Each positive prompt matches the expected skill in top-3 results with ≥40% confidence (embedding similarity) |
| **ShouldNotTrigger** | Each negative prompt does NOT match the skill — verifies the skill isn't triggered by unrelated prompts |
| **AllSkillsHaveTestPrompts** | Every skill (local SKILL.md + external with description) has a prompts.json with both shouldTrigger and shouldNotTrigger arrays |

### Writing Good Test Prompts

**shouldTrigger** — include diverse prompt variations:
- Direct requests ("Create a new TypeSpec project")
- Indirect/natural language ("I want to onboard my service to TypeSpec")
- Partial/vague prompts that should still match ("Set up TypeSpec for my API")

**shouldNotTrigger** — include prompts for related but different tasks:
- Prompts that should match a *different* skill ("Generate SDK from TypeSpec" for a project-init skill)
- Prompts that should match an MCP tool instead ("Validate my TypeSpec project")
- General prompts that shouldn't trigger any skill ("Build my SDK package")

### Running Tests

```bash
# From tools/azsdk-cli/Azure.Sdk.Tools.Cli.Evaluations
dotnet test --filter "Category=skills"
```

Required environment variables:
- `AZURE_OPENAI_ENDPOINT` — Azure OpenAI endpoint for embeddings
- `AZURE_OPENAI_MODEL_DEPLOYMENT_NAME` — Model deployment name
- `REPOSITORY_NAME` — Repository name (e.g., `Azure/azure-sdk-tools`)
- `COPILOT_INSTRUCTIONS_PATH_MCP_EVALS` — Path to copilot instructions file

## Resources

- [Agent Skills Specification](https://agentskills.io/)
- [Microsoft GitHub Copilot for Azure Skills](https://github.com/microsoft/GitHub-Copilot-for-Azure/tree/main/.github/skills)
- [Skills Guidelines](../../tools/azsdk-cli/docs/skills-guidelines.md)
