# Azure SDK Shared Copilot Skills

Copilot skills that guide AI agents through the Azure SDK development and TypeSpec-to-SDK release workflow.
These skills are consumed by GitHub Copilot (CLI, VS Code, and Coding Agent) when users work
with TypeSpec API specifications and Azure SDK generation.

Shared skills (those distributed to other Azure SDK repositories) are identified by
`distribution: shared` in their SKILL.md frontmatter metadata block and use the
`azsdk-common-` directory prefix. This prefix enables the `sync-.github-skills.yml`
pipeline to match and distribute them to all subscribed language SDK repos.

---

## Available Skills

### Workflow & Utility Skills

| Skill | Triggers | Description |
| ----- | -------- | ----------- |
| [azsdk-common-generate-sdk-locally](azsdk-common-generate-sdk-locally/SKILL.md) | "generate SDK locally", "build SDK", "run SDK tests" | Generate, build, and test Azure SDKs locally from TypeSpec |
| [azsdk-common-prepare-release-plan](azsdk-common-prepare-release-plan/SKILL.md) | "create release plan", "link SDK PR to plan" | Create and manage release plan work items |
| [azsdk-common-apiview-feedback-resolution](azsdk-common-apiview-feedback-resolution/SKILL.md) | "APIView comments", "resolve API review feedback" | Retrieve and resolve APIView review feedback |
| [azsdk-common-pipeline-troubleshooting](azsdk-common-pipeline-troubleshooting/SKILL.md) | "pipeline failed", "build failure", "CI check failing" | Diagnose and resolve SDK CI and generation pipeline failures |
| [azsdk-common-sdk-release](azsdk-common-sdk-release/SKILL.md) | "release SDK", "trigger release pipeline" | Check release readiness and trigger SDK releases |

### Development & Meta Skills

These skills help with skill development itself:

| Skill | Triggers | Description |
| ----- | -------- | ----------- |
| [sensei](sensei/SKILL.md) | "run sensei", "improve skill", "fix frontmatter" | Iteratively improve skill frontmatter compliance using the Ralph loop |
| [skill-authoring](skill-authoring/SKILL.md) | "create a skill", "new skill", "skill template" | Guidelines for writing Agent Skills per agentskills.io spec |
| [markdown-token-optimizer](markdown-token-optimizer/SKILL.md) | "optimize markdown", "reduce tokens", "token count" | Analyze markdown files for token efficiency |

### Skill Anatomy

Each skill lives in `<name>/` and contains:

```
<name>/
├── SKILL.md           # Skill definition: YAML frontmatter + steps + related skills
├── references/        # Detailed reference docs (offloaded to keep SKILL.md under 500 tokens)
│   └── *.md
└── evals/             # Evaluation definitions
    ├── eval.yaml          # Capability evals (graders, stimuli, tool-call checks)
    └── trigger.eval.yaml  # Trigger & anti-trigger evals (skill-invocation checks)
```


---

## Tooling

| Tool | Purpose | Install |
| ---- | ------- | ------- |
| [**vally**](https://literate-engine-r3wnl4v.pages.github.io/) | Run skill evals, grade trajectories | `npm install -g @microsoft/vally-cli` |

### Linting Evals

```bash
cd .github/skills

# Lint all eval files
vally lint .
```

### Running Evals

```bash
cd .github/skills

# Run all ci-gate evals
vally eval --tag "type=ci-gate"

# Run evals for a specific skill
vally eval --tag area="skill-authoring"

# Run with output directory for logs
vally eval --tag area="sensei" --output-dir ./results
```

The [skill-eval pipeline](https://dev.azure.com/azure-sdk/internal/_build?definitionId=8165&_a=summary) runs ci-gate evals automatically after PRs are merged. It can also be triggered manually from the same page.

---

## Project Configuration

- **`.vally.yaml`** — Eval paths, environments (MCP server configs), and result output
- **`.gitignore`** — Excludes eval output directories and temp files

## Further Reading

- [agentskills.io spec](https://agentskills.io) — Skill frontmatter specification
- [vally docs](https://literate-engine-r3wnl4v.pages.github.io/) — Eval runner and graders
