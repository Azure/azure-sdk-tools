# Spec: [Stage Number and Name] - [Tool Name]

## Table of Contents

- [Definitions](#definitions)
- [Background / Problem Statement](#background--problem-statement)
- [Goals and Exceptions/Limitations](#goals-and-exceptionslimitations)
- [Design Proposal](#design-proposal)
- [Alternatives Considered](#alternatives-considered-optional) _(optional)_
- [Open Questions](#open-questions)
- [Success Criteria](#success-criteria)
- [Agent Prompts](#agent-prompts)
- [CLI Commands](#cli-commands)
- [Implementation Plan](#implementation-plan)
- [Testing Strategy](#testing-strategy)
- [Documentation Updates](#documentation-updates)
- [Metrics/Telemetry](#metricstelemetry)

---

## Definitions

_Define any terms that might be ambiguous or interpreted differently across teams. Establish shared understanding before diving into the design._

**Example:**

- **Generate SDK**: In this context, "generate SDK" means...
- **Environment Setup**: Refers to...
- **Tool**: A discrete unit of functionality that...

**Your definitions:**

- **[Term]**: [Clear definition]
- **[Term]**: [Clear definition]

---

## Background / Problem Statement

_Describe the problem you're solving and why it's important._

### Current State

[What is the current situation? What pain points exist? Describe the current state for all languages (.NET, Java, JavaScript, Python, Go) and for both data plane and management plane if they differ.]

### Why This Matters

[Why should we invest time in solving this? What's the impact if we don't? Consider how users will have to switch between using the agent and the old way of doing things.]

---

## Goals and Exceptions/Limitations

### Goals

What are we trying to achieve with this design?

- [ ] Goal 1
- [ ] Goal 2
- [ ] Goal 3

### Exceptions and Limitations

_Known cases where this approach doesn't work or has limitations._

#### Exception 1: [Scenario]

**Description:**
[When does this exception occur?]

**Impact:**
[What's the consequence?]

**Workaround:**
[Is there a workaround? If not, why is this acceptable?]

#### Language-Specific Limitations

| Language   | Limitation | Impact | Workaround |
|------------|------------|--------|------------|
| .NET       | [If any]   | [Impact] | [Workaround or "None"] |
| Java       | [If any]   | [Impact] | [Workaround or "None"] |
| JavaScript | [If any]   | [Impact] | [Workaround or "None"] |
| Python     | [If any]   | [Impact] | [Workaround or "None"] |
| Go         | [If any]   | [Impact] | [Workaround or "None"] |

---

## Design Proposal

_Provide a detailed explanation of your proposed solution._

### Overview

[High-level description of the approach]

### Detailed Design

[Detailed explanation with diagrams, code samples, or workflows as appropriate]

#### Component 1: [Name]

[Description]

```text
[Code sample, diagram, or example]
```

#### Component 2: [Name]

[Description]

---

### Cross-Language Considerations

_How does this design work across different SDK languages?_

| Language   | Approach | Notes |
|------------|----------|-------|
| .NET       | [How it works in .NET] | [Any specific considerations] |
| Java       | [How it works in Java] | [Any specific considerations] |
| JavaScript | [How it works in JS] | [Any specific considerations] |
| Python     | [How it works in Python] | [Any specific considerations] |
| Go         | [How it works in Go] | [Any specific considerations] |

### User Experience

[How will developers interact with this? Show examples of commands, outputs, or workflows]

```bash
# Example usage
azsdk some-command --option value
```

### Architecture Diagram

```text
[Add diagram here - can be ASCII art, mermaid, or link to image]

┌─────────────┐
│  Component  │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│  Component  │
└─────────────┘
```

---

## Alternatives Considered _(optional)_

_What other approaches did you evaluate? Why was this design chosen?_

### Alternative 1: [Name]

**Description:**
[What is this alternative?]

**Pros:**

- [Pro 1]
- [Pro 2]

**Cons:**

- [Con 1]
- [Con 2]

**Why not chosen:**

[Reasoning]

---

### Alternative 2: [Name]

[Same structure as Alternative 1]

---

## Open Questions

_Unresolved items that need discussion and input from reviewers._

- [ ] **Question 1**: [Description of what needs to be decided]
  - Context: [Why is this uncertain?]
  - Options: [What are the choices?]
  
- [ ] **Question 2**: [Description]
  - Context: [Why is this uncertain?]
  - Options: [What are the choices?]

---

## Success Criteria

_Measurable criteria that define when this feature/tool is complete and working as intended._

This feature/tool is complete when:

- [ ] [Criterion 1: e.g., All five languages successfully execute the workflow]
- [ ] [Criterion 2: e.g., Agent prompts produce expected behavior]
- [ ] [Criterion 3: e.g., CLI commands work with all documented options]
- [ ] [Criterion 4: e.g., Documentation is complete and accurate]

---

## Agent Prompts

_Natural language prompts that users can provide to the AI agent (GitHub Copilot) to execute this tool or workflow. Include both simple and complex scenarios._

### [Scenario Name 1]

**Prompt:**

```text
[Example natural language prompt that a user would type]
```

**Expected Agent Activity:**

1. [First action the agent should take]
2. [Second action the agent should take]
3. [Third action the agent should take]
4. [Final action or report to user]

### [Scenario Name 2]

**Prompt:**

```text
[Another example prompt for a different use case]
```

**Expected Agent Activity:**

1. [Action 1]
2. [Action 2]
3. [Report results]

---

## CLI Commands

_Direct command-line interface usage showing exact commands, options, and expected outputs._

### [Command Name 1]

**Command:**

```bash
azsdk [command-name] --option1 value1 --option2 value2
```

**Options:**

- `--option1 <value>`: Description of what this option does (required/optional)
- `--option2 <value>`: Description of what this option does (default: some-value)
- `--flag`: Description of this boolean flag

**Expected Output:**

```text
[Example of what the command outputs when successful]

✓ Action completed successfully
✓ Files generated: 5
✓ All checks passed

Summary: [Brief summary of what was accomplished]
```

**Error Cases:**

```text
[Example of error output when something goes wrong]

✗ Error: Missing required option --option1
  
Usage: azsdk [command-name] --option1 <value> [options]
```

### [Command Name 2]

**Command:**

```bash
azsdk [another-command] --required-param value
```

**Options:**

- `--required-param <value>`: [Description]
- `--optional-param <value>`: [Description] (optional)

**Expected Output:**

```text
[Expected output example]
```

---

## Implementation Plan

_If this is a large effort, break down the implementation into phases._

### Phase 1: [Name]

- Milestone: [What will be delivered?]
- Timeline: [Estimated timeframe]
- Dependencies: [What must be done first?]

### Phase 2: [Name]

[Same structure]

### Phase 3: [Name]

[Same structure]

---

## Testing Strategy

_How will this design be validated?_

### Unit Tests

[What unit tests are needed?]

### Integration Tests

[What integration tests are needed?]

### Manual Testing

[What manual testing is needed?]

### Cross-Language Validation

[How will we ensure this works across all SDK languages?]

---

## Metrics/Telemetry

_What data should we collect to measure success or diagnose issues?_

### Metrics to Track

| Metric Name | Description | Purpose |
|-------------|-------------|---------|
| [Metric 1]  | [What it measures] | [Why we need it] |
| [Metric 2]  | [What it measures] | [Why we need it] |

### Privacy Considerations

[How will we ensure no sensitive data is collected?]

---

## Documentation Updates

_What documentation needs to be created or updated?_

- [ ] README updates
- [ ] Developer guides
- [ ] API documentation
- [ ] Examples/samples |
