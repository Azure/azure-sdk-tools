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

## Anti-Patterns: When NOT to Use a Skill

Skills are **not** the right choice when:

### ❌ Execution is Required

| Bad Skill Candidate | Why | Use Instead |
|---------------------|-----|-------------|
| `verify_setup` | Needs to run `python --version`, `node --version`, etc. | MCP Tool |
| `build_code` | Must invoke Maven/Gradle and parse errors | MCP Tool |
| `run_tests` | Must execute test runner and report results | MCP Tool |
| `get_pipeline_status` | Requires API call to Azure DevOps | MCP Tool |

**Rule**: If it needs to **execute commands** or **call APIs**, it's a tool.

### ❌ Output Must Be Structured Data

| Bad Skill Candidate | Why | Use Instead |
|---------------------|-----|-------------|
| `list_packages` | Returns JSON array of packages | MCP Tool |
| `get_release_plan` | Returns structured release data | MCP Tool |
| `analyze_dependencies` | Returns dependency graph | MCP Tool |

**Rule**: If other tools or automation consume the output, it's a tool.

### ❌ Authentication is Required

| Bad Skill Candidate | Why | Use Instead |
|---------------------|-----|-------------|
| `create_pr` | Needs GitHub token | MCP Tool |
| `publish_package` | Needs npm/NuGet credentials | MCP Tool |
| `deploy_resources` | Needs Azure credentials | MCP Tool |

**Rule**: If it touches auth tokens or credentials, it's a tool.

### ❌ The Workflow is a Single Command

| Bad Skill Candidate | Why | Use Instead |
|---------------------|-----|-------------|
| "Run linter" | Just `npm run lint` | Tool or copilot-instructions |
| "Format code" | Just `dotnet format` | Tool or copilot-instructions |

**Rule**: If a single command does the job, a skill adds overhead without value.

## Evaluation Checklist

Before committing to Skill vs Tool, validate your choice:

### 1. Can It Be a Skill?

- [ ] **No code execution needed** - Skill can't run `python`, `npm`, `dotnet`
- [ ] **No API calls required** - Skill can't call REST APIs  
- [ ] **No authentication needed** - Skill has no access to tokens/creds
- [ ] **Output is guidance, not data** - Skill produces prose, not JSON

*If any checkbox is unchecked → Use a Tool*

### 2. Should It Be a Skill?

- [ ] **Workflow has 3+ steps** - Simple tasks don't need skills
- [ ] **Steps vary by context** - Same workflow, different paths
- [ ] **Guidance changes frequently** - Markdown is easier to update than C#
- [ ] **Multiple repos need customization** - Skills can be tailored per-repo

*If most checkboxes are unchecked → Consider if a skill adds value*

### 3. Skill Quality Checklist

- [ ] **Folder name** is lowercase, hyphenated (e.g., `typespec-new-project`)
- [ ] **`name:`** in frontmatter matches folder name exactly
- [ ] **`description:`** includes trigger phrases users would say
- [ ] **SKILL.md body** uses imperative form ("Do X", not "You should do X")
- [ ] **Referenced files** actually exist (`references/`, `scripts/`)
- [ ] **Tested** with prompts in `tests/your-skill/prompts.json`
- [ ] **Triggers correctly** when using listed phrases

### 4. Test Your Choice

| Test | How | Pass Criteria |
|------|-----|---------------|
| **Discoverability** | Run `dotnet test --filter "Category=skills"` | Prompts trigger the skill |
| **Effectiveness** | Manual test with real scenario | Workflow completes correctly |
| **No false triggers** | Test with unrelated prompts | Skill doesn't activate incorrectly |

## Evaluation Framework

### Current State: Discoverability Only ✅

We can measure: *"Does the prompt trigger the right skill/tool?"*

```
Prompt → PromptToToolMatchEvaluator → Pass/Fail
```

### Missing: Effectiveness ❓

We need to measure: *"Did using the skill lead to successful task completion?"*

| Eval Type | What It Measures | How to Test | Status |
|-----------|------------------|-------------|--------|
| **Discoverability** | Does prompt trigger skill/tool? | `PromptToToolMatchEvaluator` | ✅ Have |
| **Relevance** | Is triggered skill/tool appropriate? | Manual review or LLM-as-judge | ❌ Missing |
| **Completeness** | Did workflow complete? | End-to-end test with assertions | ❌ Missing |
| **Correctness** | Was output correct? | Golden file comparison | ❌ Missing |
| **Efficiency** | How many tokens/turns? | Measure conversation length | ❌ Missing |

### When to Invest in Effectiveness Evals

- **Now (1 skill)**: Manual testing is sufficient
- **3-5 skills**: Consider LLM-as-judge for relevance
- **10+ skills**: Invest in end-to-end automation

## References

- [Agent Skills Specification](https://agentskills.io/)
- [MCP Tool Documentation](../../tools/azsdk-cli/docs/mcp-tools.md)
- [Evaluation Framework](../../tools/azsdk-cli/Azure.Sdk.Tools.Cli.Evaluations/)
