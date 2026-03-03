# Shared Azure SDK Skills

Shared skills are synced from [azure-sdk-tools](https://github.com/Azure/azure-sdk-tools) to all Azure SDK repositories via the `eng-skills-sync` pipeline. These skills apply across all repos and should not be modified directly in individual repositories — changes will be overwritten on the next sync.

## How Skills Are Used

AI tools use this index for **progressive disclosure** — they read this file first to discover what shared skills are available, then selectively read individual `SKILL.md` files for detailed instructions.

- **GitHub Copilot (VS Code / CLI):** Auto-discovers skills in `.github/skills/`. No additional configuration needed.
- **Claude:** Reference this file from your repo's `CLAUDE.md` (e.g., `See .github/skills/common/README.md for shared skills`).
- **Other tools:** Point your AI tool's configuration to this file.

## Adding a New Shared Skill

1. Create a new directory under `.github/skills/common/` in the [azure-sdk-tools](https://github.com/Azure/azure-sdk-tools) repo.
1. Add a `SKILL.md` file following the [skill template](./skill-template.md).
1. Submit a PR — the sync pipeline will distribute the skill to all repos.

For detailed guidelines, see [skills-guidelines.md](https://github.com/Azure/azure-sdk-tools/blob/main/tools/azsdk-cli/docs/skills-guidelines.md).
