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
3. **Add test prompts**: Create `tests/your-skill-name/prompts.json` (copy from existing)
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

## Resources

- [Agent Skills Specification](https://agentskills.io/)
- [Microsoft GitHub Copilot for Azure Skills](https://github.com/microsoft/GitHub-Copilot-for-Azure/tree/main/.github/skills)
