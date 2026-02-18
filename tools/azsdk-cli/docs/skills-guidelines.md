# Skills and copilot instructions in Azure SDK cli

This document provides comprehensive guidelines for creating skills. GitHub Copilot uses skills defined in `.github/skills`. Each skill (which is a directory under `.github/skills`) includes a `SKILL.md` file which contains the metadata and instructions. You can also include examples and some scripts in the skill directory. Copilot identifies a skill based on its description and how it semantically matches the prompt in a given context. Each skill should define instruction, success criteria, list of tools and commands to be used by the skill, and an optional next prompt for the user so the agent can recommend the next step.

## When to Use Skill

| Component                | Used for                                      |
|--------------------------|-----------------------------------------------|
| **Skills**               | For workflows and multiple commands           |
| **MCP**                  | For deterministic tasks                       |
| **Copilot instructions** | For repository-wide conventions and behaviors |

Skills are for workflows/sets of instructions, while MCP tools are for deterministic tasks. You should ask the question before creating a skill: Does Copilot need to follow a set of instructions or commands/tools, or does it need to follow a workflow to achieve the goal for a given prompt? For example, the user prompt "Generate and release SDK from TypeSpec" is definitely not a deterministic atomic task. Copilot needs to run multiple steps in this case.

Skills should be used when you want Copilot to run multi-step instructions and workflows. It can also be used to build the workflows that can be automated. In case of Azure SDK tooling, following are a great examples to use skill.

- **TypeSpec authoring**: This includes multi-step tasks to guide the user to define/update TypeSpec definition. It will also validate the generated TypeSpec.
- **TypeSpec to SDK generation**: This connects various tasks to build the end to end workflow. This skill will use several modular skills. For e.g. sdk generation skill.
- **Generate, test and validate SDK**: run sdk generation, build and test. It can invoke TypeSpec customization skill if SDK generation fails. This skill also can include sample SDK generation failures and fixes.
- **Prepare SDK release**: Update package metadata, change log and read me, create pull request. This can also verify package approval and help user to get SDK ready for release.
- **TypeSpec customization**: runs TypeSpec changes and skill can refer various examples to help customize spec.

We should not write a skill for a task that can be completed by one MCP tool call. If an MCP tool can get the results for a prompt, then it should not be added as a skill.

Following are a few examples of **not** skill candidates:

- **Release SDK**: It's just one MCP tool call to release a package.
- Test a package
- Validate package
- Verify setup

When you are in doubt whether to write a skill or just leave the functionality in the MCP, create a skill. Skills use MCP tools or CLI commands.

## How to Define a Skill

Main component of the skill is markdown file names `SKILL.md` that contains the instructions. A skill can contain additional reference docs to help LLM make decision better. One usage is to create an `example.md` and include it in referencing document. Each skill is added as folder in `.github/skills` in the repo root. Following is the folder structure for a skill.

```
skill-name/
├── SKILL.md              # Required: main instructions
├── references/           # Optional: additional documentation
│   ├── examples.md
│   └── troubleshooting.md
```

**Skill name**: Azure SDK tools will have skills applicable for just one repo or shared across multiple repos. More details about skill names can be found in the "Where to Create the Skill" section below.

### Skill.md structure

Each skill contains `name`, `description`, and instructions. The description field determines the success factor of whether a skill is selected by the LLM for a given context and prompt. The LLM loads the description for all skills and makes a decision to choose a skill based on how it matches the prompt in a given context. Following is the skill template:

```yaml
---
name: skill-name
description: |
  One-line description of what the skill does.
  USE FOR: List various scenarios
  DO NOT USE FOR: List any possible conflicting scenarios if applicable.
  TOOLS/COMMANDS: List mcp tools and commands
---
```

**Skill Body Structure:**

```markdown

# Skill Title

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
```

## Where to Create the Skill

Skills that are specific to a particular language or repository should be stored within that repository.

**Example**: TypeSpec validation Skills specific to azure-rest-api-specs should be stored in the azure-rest-api-specs repository at `.github/skills/`.

### Multi-Repository Skills

Skills that apply to multiple repositories with the same instructions should be placed in the `.github/skills/common` directory within the azure-sdk-tools repository. This enables sharing of common automation patterns across all Azure SDK repositories.

### How to sync skills across the repos

Engineering system has a pipeline to sync all changes in the `eng/common` in azure-sdk-tools repo to `eng/common` in all repos. This can be enhanced to support the sync of skills to `.github/skills`.

To distribute Skills from azure-sdk-tools to individual Azure SDK repositories:

- submit a PR to create or edit a skill in `Azure/azure-sdk-tools`.
- Skills in `.github/skills/common` are synced to `.github/skills` in individual SDK repositories using engsys pipeline.
- Changes to the engineering systems common sync framework are required to enable this synchronization.
- The sync process ensures that all repositories benefit from centralized Skill updates.

## Service specific instruction

If a service has additional service specific guidelines for a particular workflow then it can be included as a markdown in the service folder and reference it from the skill. For e.g. if a team wants to have specific instructions as part of preparing package for a release.

## How to Test

We need to do mainly following two tests:

- **Skill selection accuracy**: This test ensures correct skill is loaded by LLM for a prompt in a given context.
Eval framework for azsdk cli currently does not support skills. This requires an enhancement to support evaluate how LLM matches a prompt to skill. A skill selection scenario should be added that contains a list of prompts and context and expected skill to be used.
Eval will verifies the scenario to check if skill is loaded as expected for a prompt.

- **Skill workflow completion test**: This test is required to make sure LLM uses the steps in the workflow as expected. A scenario for this test contains all the commands and mcp tools expected to be used for an end to end skill completion and corresponding context and expected steps to be completed. Eval test will make sure LLM completed all expected steps and executed all mcp tools/commands.

## Copilot Instructions

### When to Use Copilot Instructions

Copilot instructions should be used for repository-wide conventions and behaviors that apply broadly across most prompts and interactions. They define the general operating principles for agents working within the repository.

Copilot instructions are appropriate for:

- **Output format preferences**: Standardize how agents should structure their responses (e.g., "always provide code snippets in markdown format")
- **Interaction logging**: Require agents to maintain local log files of all interactions for debugging and auditing
- **Confirmation policies**: Establish when agents must request user confirmation before executing commands or making changes (e.g., "always confirm before running destructive operations")
- **General coding conventions**: Repository-wide style preferences that apply to all code (e.g., "use TypeScript strict mode", "follow Azure SDK design principles")
- **Communication patterns**: How agents should report progress, errors, or request clarification

### What Not to Put in Copilot Instructions

Copilot instructions must **NOT** contain:

- **Workflow-specific instructions**: Step-by-step procedures for particular tasks should be in Skills, not Copilot instructions
- **Task-specific commands**: Detailed command sequences belong in Skills
- **Conditional logic for specific scenarios**: Complex decision trees should be encapsulated in Skills
- **Detailed technical procedures**: In-depth implementation details should be documented in Skills

**Reason**: Including workflow or task-specific content in Copilot instructions causes them to be loaded in every prompt, increasing token costs unnecessarily. Only instructions that apply to most interactions should be in Copilot instructions.

### Design Principle

Keep Copilot instructions minimal and focused on cross-cutting concerns. When in doubt, create a Skill instead of adding to Copilot instructions.

## Telemetry

Telemetry for Skills is not yet implemented. Telemetry design and implementation will be covered in a forthcoming global telemetry design document that addresses:

- Skill invocation tracking
- Success/failure metrics
- Performance measurements
- Tool and command usage statistics
- Error pattern analysis

Check the global telemetry design document for updates on when and how telemetry will be integrated into the Skills framework.
