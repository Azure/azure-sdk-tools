# Skills vs Tools vs Copilot SDK: Decision Guide

> **Purpose**: Help the team decide which approach to use for new AI-assisted capabilities.

## Quick Decision Matrix

| Scenario | Recommended Approach |
|----------|---------------------|
| Straightforward workflow with known steps | **Skill** |
| Complex logic, API calls, or data processing | **MCP Tool** |
| Multi-turn conversations, complex reasoning | **Copilot SDK Agent** |
| Existing CLI command needs AI discoverability | **MCP Tool** (wrap existing) |
| Team-specific guidance or checklists | **Skill** |

## Comparison Criteria

### 1. Discoverability ✅ Comparable

| Approach | How It Works | Evaluation Result |
|----------|--------------|-------------------|
| **MCP Tool** | Tool description loaded at server startup | 33% → 100% with optimized descriptions |
| **Skill** | SKILL.md matched against user prompt | ~100% with TRIGGERS keywords |

**Finding**: Both achieve similar discoverability when descriptions are well-written. Skills use `TRIGGERS:` keywords; tools need descriptive `description` fields.

### 2. Context Efficiency ✅ Skills Win

| Approach | Loading Behavior | Token Impact |
|----------|------------------|--------------|
| **MCP Tool** | Eager load - all tool descriptions loaded at startup | Fixed overhead per tool (~100-500 tokens each) |
| **Skill** | Lazy load - only loaded when prompt matches | Zero overhead until triggered |

**Finding**: Skills are more context-efficient because they lazy-load. With many tools, startup context can grow significantly.

### 3. Maintainability ✅ Skills Win

| Approach | Change Process | Complexity |
|----------|----------------|------------|
| **MCP Tool** | Code change → Build → Test → Deploy → Release | High (C#, CI/CD, versioning) |
| **Skill** | Edit markdown → Commit → Push | Low (natural language, no build) |

**Finding**: Skills are natural language and don't require deployment cycles. Faster iteration.

### 4. Portability ✅ Skills Win

| Approach | Distribution | Cross-repo |
|----------|--------------|------------|
| **MCP Tool** | Installed via extension or CLI | Single installation serves all repos |
| **Skill** | Copy `.github/skills/` folder | Must be copied to each target repo |

**Finding**: Skills are easier to customize per-repo but require manual copying. Tools are centrally deployed.

### 5. Correctness ⚖️ Tools Win

| Approach | Execution Model | Reliability |
|----------|-----------------|-------------|
| **MCP Tool** | Deterministic code execution | High - code does exactly what it's written to do |
| **Skill** | LLM interprets instructions | Variable - depends on model interpretation |

**Finding**: Tools guarantee correct execution. Skills rely on LLM following instructions accurately.

## Decision Flowchart

```
Start: "I need to add AI capability for X"
          │
          ▼
    ┌─────────────────────────────────────┐
    │ Does it require deterministic logic │
    │ (API calls, calculations, data)?    │
    └─────────────────────────────────────┘
          │
     Yes ─┼─ No
          │    │
          ▼    ▼
      MCP Tool  ┌─────────────────────────────────────┐
                │ Is it a multi-step workflow with    │
                │ known steps (checklist, guide)?     │
                └─────────────────────────────────────┘
                      │
                 Yes ─┼─ No
                      │    │
                      ▼    ▼
                   Skill   ┌─────────────────────────────────────┐
                           │ Does it need multi-turn dialogue    │
                           │ or complex reasoning?               │
                           └─────────────────────────────────────┘
                                 │
                            Yes ─┼─ No
                                 │    │
                                 ▼    ▼
                         Copilot SDK  Consider simpler
                           Agent      approach or combine
```

## Examples

### Use a Skill When...

- ✅ "Create a new TypeSpec project" → Known steps, checklist-style guidance
- ✅ "Review my API design" → Guidance based on best practices
- ✅ "Help me write a changelog" → Template + instructions

### Use an MCP Tool When...

- ✅ "Run AppCAT assessment" → Executes CLI, returns structured data
- ✅ "Build my Java project" → Invokes Maven/Gradle, parses errors
- ✅ "Search the knowledgebase" → API call with specific parameters

### Use Copilot SDK Agent When...

- ✅ Complex multi-turn debugging sessions
- ✅ Orchestrating multiple tools with reasoning
- ✅ Scenarios needing memory across interactions

## Hybrid Approach

Skills and tools can complement each other:

```
User: "Help me migrate my Spring Boot app"

1. Skill provides: High-level migration checklist, best practices
2. Tool executes: AppCAT assessment, build validation, CVE scanning
3. Skill guides: Interpretation of results, next steps
```

## Testing Your Choice

| Approach | Test Method |
|----------|-------------|
| **Skill** | `dotnet test --filter "Category=skills"` in `Azure.Sdk.Tools.Cli.Evaluations` |
| **MCP Tool** | `dotnet test --filter "Category=mcp"` in `Azure.Sdk.Tools.Cli.Evaluations` |

Both use the same `PromptToToolMatchEvaluator` to measure discoverability.

## References

- [Agent Skills Specification](https://agentskills.io/)
- [MCP Tool Documentation](../../tools/azsdk-cli/docs/mcp-tools.md)
- [Evaluation Framework](../../tools/azsdk-cli/Azure.Sdk.Tools.Cli.Evaluations/)
