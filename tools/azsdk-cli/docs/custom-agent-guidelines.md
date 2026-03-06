# Custom Agents in azsdk-cli

This document provides guidance on when and how to use the different types of agents available in the azsdk-cli ecosystem.

## Types of Agents

There are two distinct types of agents used in the Azure SDK tools:

| Type | Location | Purpose | Requires |
|------|----------|---------|----------|
| **Copilot SDK Agents** | `CopilotAgents/` in azsdk-cli | Programmatic agents embedded in CLI tools | GitHub Copilot CLI |
| **Custom Agents** | `.github/agents/*.agent.md` | User-facing interactive agents in VS Code | GitHub Copilot |

## When to Use Each Type

### Decision Flowchart

```
Need guaranteed loading (not probabilistic skill selection)?
└── YES → Custom Agent (.github/agents/)

Language-specific prompts or complexity beyond validation?
└── YES → MCP Tool + Copilot SDK Agent

Otherwise → Skill (.github/skills/)
```

### Skills vs Programmatic Agents: Key Distinctions

The primary factors for choosing between skills and programmatic agents are:

1. **User in the loop vs headless execution**
   - **Skill**: The user is interacting with Copilot, can see/approve changes, agent uses its built-in tools (e.g. TypeSpec Authoring skill)
   - **Programmatic agent**: Running headless in a CLI command or CI pipeline, no user interaction

2. **Language-agnostic vs language-specific (for shared scenarios)**
   - **Skill**: The same instructions work regardless of SDK language
   - **Programmatic agent**: When a shared scenario (e.g., "generate samples") needs different prompts or context per language. A language-aware programmatic agent serves as a single entry point that saves context and provides a unified experience.

3. **Validation-only vs complex workflows**
   - **Skill**: Primarily guidance with validation steps
   - **Programmatic agent**: Multi-step workflows, batching, conditional execution

**Recommendation:** Start with a skill if it can be language-agnostic. If testing shows poor results or you need language-specific behavior, graduate to an MCP tool with a Copilot SDK agent.

### Use Skills Instead of Agents When

Skills (`.github/skills/`) are the **default choice** for user-facing guidance when the task can be written in a language-agnostic way. Skills are simpler than programmatic agents and can be invoked with a CLI coding agent (e.g. copilot cli).

**Advantages of Skills:**
- **Bundled resources:** Can include reference files (templates, examples, checklists)
- **Simpler maintenance:** Easier to update than programmatic agents
- **Leverage built-in tools:** The main Copilot agent handles file editing, terminal commands, etc.
- **User sees changes:** Diffs and modifications are shown to the user for review

**Use Skills when:**
- The task is user-facing (user interacting with Copilot)
- Instructions can be written language-agnostically
- Complexity is primarily around validation or guidance
- The main agent's built-in tools are sufficient

**Graduate to MCP Tool + Copilot SDK Agent when:**
- Testing shows the skill produces poor/inconsistent results
- You discover the task needs language-specific prompts or context
- The workflow is too complex for declarative guidance (e.g. requires loops or branching logic)

**Example migration path:**
1. Start with a skill: "Generate a README for this package using the template"
2. If it struggles with language-specific conventions → Create an MCP tool
3. If the tool needs LLM reasoning → Add a Copilot SDK agent inside the tool

### Use Copilot SDK Agents When

Copilot SDK agents are the **recommended approach** for programmatic agent development in azsdk-cli.

**Use when:**
- **Headless/automated execution**: CLI commands, CI pipelines, no user in the loop
- **Language-specific shared scenarios**: A single scenario (e.g., "generate samples") that needs different prompts or context depending on the SDK language—use one MCP tool instead of multiple language-specific skills
- **Complex workflows**: Multi-step processes with batching, conditional execution, or iteration
- **Embedded in MCP tools**: The agent is one step in a larger programmatic workflow

**Examples:**
- TypeSpec customization service (part of a larger workflow)
- Sample generation/translation (same scenario, but Java/Python/JS need different prompts)

### Use Custom Agents When

Custom agents are markdown-defined agents that GitHub Copilot can invoke in VS Code.

> **Note:** In most cases, **Skills are preferred over Custom Agents**. Skills have the advantage of bundling reference files (templates, examples, etc.).

**Use a Custom Agent only when:**

1. **Users want to manually select a specialized mode.** Custom agents appear in the agent picker, allowing users to explicitly choose them. This is similar to "forcing" a skill to be loaded.

## Summary: Choosing the Right Approach

| Scenario | Recommendation |
|----------|----------------|
| Headless/automated execution (CLI, CI) | **Copilot SDK Agent** |
| Language-specific prompts or logic | **Copilot SDK Agent** |
| User-facing, language-agnostic guidance | **skill** (start here, graduate if needed) |
| Users must manually select a mode | **Custom Agent** (.github/agents/) |

## Related Documentation

- [Skills](skills-guidelines.md) - Procedural instructions for GitHub Copilot
