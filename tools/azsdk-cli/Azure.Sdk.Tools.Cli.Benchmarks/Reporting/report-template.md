# Benchmark Report

**Test Run:** `{runName}`
**Date:** {testDate}
**Report Generated:** {reportDate}
**Model Used:** `{model}`

---

### Scenarios Executed

List every scenario that was run.

| # | Scenario Name | Description | Tags | Runs |
|---|---------------|-------------|------|------|
| {n} | {scenario-name} | {scenario-description} | {tags} | {run-count} |
| ... | ... | ... | ... | ... |

---

## 📊 Overall Statistics

| Metric | Value |
|--------|-------|
| **Total Scenarios** | {total-scenarios} |
| **Total Individual Runs** | {total-runs} |
| **Overall Pass Rate** | **{rate}%** ({passed}/{total}) |
| **Average Duration** | **{avg-duration}** |
| **Total Duration** | **{total-duration}** |

---

## 🔍 Per-Scenario Results

For each scenario, provide a narrative summary of what happened during execution,
what went well, and what went wrong. One subsection per scenario.

### Scenario {n}: {scenario-name}

**Description:** {scenario-description}
**Tags:** {tags}
**Prompt:** "{prompt-used}"
**Pass Rate:** {rate}% ({passed}/{total}) | **Duration:** {duration}

**Validation Results:**

| Validator | Result | Message |
|-----------|--------|---------|
| {validator-name} | ✅ Pass / ❌ Fail | {message} |
| ... | ... | ... |

**What Happened:**
{Narrative description of the scenario execution flow. Describe the key steps the agent took,
which tools were called, and the final outcome. Be specific — reference actual agent behavior
observed in the logs.}

**✅ What Went Well:**
- {Positive observation}
- ...

**❌ What Went Wrong:**
- {Negative observation — if any}
- ...

*(Repeat this subsection for each scenario)*

---

## 🔧 Tool Usage Summary

Aggregated tool call statistics across all scenarios.

### Tool Call Frequency

| Tool Name | MCP Server | Total Calls | Avg Duration (ms) | Scenarios Used In |
|-----------|------------|-------------|-------------------|-------------------|
| {tool-name} | {mcp-server or "Built-in"} | {count} | {avg-ms} | {scenario-list} |
| ... | ... | ... | ... | ... |

### Tool Call Timeline

For each scenario, list the sequence of tool calls made.

| Scenario | Tool Calls (in order) | Total Tool Calls |
|----------|-----------------------|------------------|
| {scenario-name} | {tool1} → {tool2} → ... | {count} |
| ... | ... | ... |

---

## 📈 Duration Report

| # | Scenario Name | Duration | Pass/Fail |
|---|---------------|----------|-----------|
| {n} | {scenario-name} | {duration} | ✅ / ❌ |
| ... | ... | ... | ... |

### Aggregate Duration Summary

| Metric | Value |
|--------|-------|
| **Total Duration (all scenarios)** | **{total}** |
| **Longest Scenario** | {value} ({scenario-name}) |
| **Shortest Scenario** | {value} ({scenario-name}) |
| **Average Per Scenario** | {value} |

---

## 🪙 Token Usage

### Per-Scenario Token Usage

| # | Scenario Name | Input Tokens | Output Tokens | Cache Read | Cache Write | Total Tokens |
|---|---------------|-------------|---------------|------------|-------------|-------------|
| {n} | {scenario-name} | {input} | {output} | {cache-read} | {cache-write} | {total} |
| ... | ... | ... | ... | ... | ... | ... |

### Aggregate Token Usage

| Metric | Value |
|--------|-------|
| **Total Input Tokens** | {total-input} |
| **Total Output Tokens** | {total-output} |
| **Total Cache Read Tokens** | {total-cache-read} |
| **Total Cache Write Tokens** | {total-cache-write} |
| **Grand Total Tokens** | **{grand-total}** |

---

## 🔑 Areas for Improvement

Actionable suggestions based on problems discovered during testing. Each item should
identify the problem, cite supporting evidence from test results, and propose a concrete
fix or investigation.

1. **{area-title}** — {Description of the problem. Reference specific scenarios, pass rates,
   or agent behaviors that surfaced this issue. Suggest what could be changed to address it.}
2. **{area-title}** — {Description and suggestion}
3. ...

---

*Report generated on {report-date} — {total-scenarios} scenario(s) across {total-runs} total run(s).*
