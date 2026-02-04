# Azure SDK tools guidelines to use skills

This document provides comprehensive guidelines for creating skills.GitHub copilot uses skills defined in `.github/skills`. Each skill9which is a directory under `github/skills`) includes a `SKILL.md` which contains the metadata and instructions. You can also include examples and some scripts in the skill directory. Copilot identifies a skill based on its description and how semantically matches the prompt in a given context.  Each skill should define instruction, success criteria, list of tools and commands to be used by the skill and an optional next prompt for the user so agent can recommend the next step.

## When to Use

Instructions for copilot/agent can be either included in copilot-instructions.md or as a skill. Instructions in the copilot instruction file are included as part of every prompt with the LLM and this makes it costly token use when some of these  instructions are task specific and not applicable globally for all prompts. 
Skill helps to reduce the token usage by using the instructions in the skill only on demand basis. Skill can use a combination of instruction, mcp tool and commands to run a series of tasks.


| Skill | Copilot Instructions |
|-------|---------------------|
| Specific tasks and workflows | Repository-wide conventions, General coding conventions |
| Detailed workflow instructions | Output format preferences |
| Automate repeatable workflows | Interaction logging |
| Build modular automation | Confirmation policies |
| Provide consistent execution | Communication patterns |
| Task-specific commands and procedures | Cross-cutting concerns |

### Use Skills for Specific Tasks and Workflows

Skills should be used when you need to:

- **create a detailed workflow instruction that connects various tasks**: A complete set of instructions in order to integrate various tasks, tools and commands to guide a user through complete workflow.  (e.g TypeSpec to SDK release workflow, Package generation to Prepare release workflow).
- **Provide detailed instructions applicable for a task**: A set of instructions to be completed by agent to achieve a goal. (e.g. Analyze pipeline failure. Create a release plan, verify setup)
- **Automate repeatable workflows**: Tasks that follow a consistent pattern across multiple scenarios (e.g., TypeSpec validation, package generation, package test and validation, release planning)
- **Build modular automation**: Create granular Skills that can be combined to construct larger workflow Skills
- **Provide consistent execution**: Ensure that tasks are performed the same way regardless of who or what triggers them

### Examples of Skill Usage

- **TypeSpec Validation**: Automate the process of validating TypeSpec projects against schema and configuration requirements
- **Package Generation**: Standardize the steps for generating SDK packages from TypeSpec definitions
- **Package Test & Validation**: Ensure packages meet quality standards through automated testing and validation
- **Release Planner**: Coordinate the release planning process across multiple packages and languages

### Granular Skills vs. Workflow Skills

- **Granular Skills**: Focus on single, atomic operations (e.g., "validate TypeSpec schema", "compile package", "run unit tests")
- **Workflow Skills**: Combine multiple granular Skills to accomplish end-to-end processes (e.g., "complete package validation" might reference validation, build, and test Skills)

## Where to store the skills

GitHub copilot requires all skills in `.github/skills` directory. Some skills are language or repo specific and some skills are common across all repos. A repo specific skill can be created in `'github/skills` in the repo itself. If a skill is applicable for more than one language and if instructions are same then skill should be created in `Azure/azure-sdk-tools` repo and sync them to all repos. One challenge is to avoid naming collision between local skill and centrally stored skill. A global skill should have a suffix `global` and local skill can have suffix `local` to avoid the name collision. For e.g. package-generate-skill-global.

### Language-Dependent and Repository-Specific Skills

Skills that are specific to a particular language or repository should be stored within that repository.

**Example**: TypeSpec validation Skills specific to azure-rest-api-specs should be stored in the azure-rest-api-specs repository at `.github/skills/`.

### Multi-Repository Skills

Skills that apply to multiple repositories with the same instructions should be placed in the `eng/common/.github/skills` directory within the azure-sdk-tools repository. This enables sharing of common automation patterns across all Azure SDK repositories.

### How to sync skills across the repos

Engineering system has a pipeline to sync all changes in the `eng/common` in azure-sdk-tools repo to `eng/common` in all repos. This can be enhanced to support the sync of skills to `github/skills`.

To distribute Skills from azure-sdk-tools to individual Azure SDK repositories:

- submit a PR to create or edit a skill in `Azure/azure-sdk-tools.
- Skills in `eng/common/.github/skills` are synced to `.github/skills` in individual SDK repositories using engsys pipeline.
- Changes to the engineering systems common sync framework are required to enable this synchronization
- The sync process ensures that all repositories benefit from centralized Skill updates

## What to Include in Skill Instructions

### Prefer Language-Independent Commands

When writing Skill instructions:

1. **Use azsdk-cli commands** or **MCP tools** that abstract language-specific implementations
2. Only use language-specific commands when no CLI or MCP wrapper exists
3. This approach ensures Skills remain portable and easier to maintain across all repos.

**Example**: Prefer `azsdk pkg validate --package-path ./sdk/storage` over language-specific commands like `dotnet test` or `pytest`.

### Define Success Criteria

Every Skill must clearly define what success means for its execution. Include:

- Expected outputs or artifacts
- Validation checkpoints
- Exit conditions (both success and failure)
- Next steps after successful completion

**Example**: For a package build Skill, success means: compiled artifacts exist in the expected output directory, no build errors occurred, and all dependencies resolved successfully.

### Include Sample Errors and Fixes

To improve agent effectiveness and reduce iteration cycles, include:

- **Common errors**: Document frequently encountered errors with clear descriptions
- **Possible causes**: Explain why these errors typically occur
- **Resolution steps**: Provide specific actions to resolve each error

**Example for TypeSpec Validation**:
```
Common Error: "Unknown decorator @example"
Possible Cause: Missing or incorrect import statement
Fix: Add `import "@azure-tools/typespec-azure-core";` to main.tsp
```

**Example for Test Failures**:
```
Common Error: "Test timeout after 30 seconds"
Possible Causes: Network issues, service unavailable, incorrect test configuration
Fix: Check service endpoint availability, verify authentication, increase timeout in test configuration
```

### Define a section to list Tool and Commands used by skill.

Each Skill should maintain a list of:

- MCP tools used
- CLI commands invoked
- External dependencies required

This information will support evaluation frameworks and helps identify any gaps in skill goal for various prompts.

## How to test

### Current Evaluation Framework Limitations

The current evaluation framework primarily focuses on testing Copilot instructions and has no support for Skill-specific testing. Enhancement is needed to provide comprehensive Skill validation.

### Required Testing Capabilities

To properly test Skills, the evaluation framework should support:

1. **Skill Selection Testing**
   - Verify the correct Skill is selected for a given task or prompt
   - Test disambiguation when multiple Skills could apply
   - Validate Skill matching accuracy

2. **Conflict and Ambiguity Detection**
   - Identify when multiple Skills provide contradictory instructions
   - Detect overlapping Skill responsibilities
   - Test resolution strategies for ambiguous scenarios

3. **Scenario-Based Execution Verification**
   - Validate that Skills execute the correct tools and commands
   - Verify proper parameter passing to tools
   - Ensure correct sequencing of operations in workflow Skills
   - Test error handling and recovery paths

4. **Integration Testing**
   - Test Skills that reference other Skills
   - Verify data flow between composed Skills
   - Validate end-to-end workflow execution

### Manual Testing

Until automated evaluation is enhanced, Skills should be tested manually by:

- Running the Skill in representative scenarios
- Verifying all commands execute successfully
- Checking that success criteria are met
- Testing error scenarios and recovery paths

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
