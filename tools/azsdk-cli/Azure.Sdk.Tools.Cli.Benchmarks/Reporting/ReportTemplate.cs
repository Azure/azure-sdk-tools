// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Benchmarks.Reporting;

/// <summary>
/// Contains the report template used by the LLM to generate benchmark reports.
/// </summary>
public static class ReportTemplate
{
    /// <summary>
    /// The default model used for report generation.
    /// </summary>
    public const string DefaultReportModel = "claude-sonnet-4.5";

    /// <summary>
    /// System prompt that instructs the LLM on how to generate the report.
    /// </summary>
    public const string SystemPrompt = """
        You are a benchmark report generator. You will receive JSON data from benchmark test runs
        and a report template. Your job is to analyze the data and fill in the template accurately.

        Rules:
        - Be precise with numbers and statistics. Do not hallucinate data.
        - If data is missing or incomplete, clearly indicate "N/A" or "Data not available".
        - For narrative sections, base your analysis strictly on the data provided.
        - Use the exact template structure provided. Do not add or remove sections.
        - For tool call analysis, group by tool name and MCP server when applicable.
        - For areas of improvement, only cite issues that are directly evidenced in the data.
        """;

    /// <summary>
    /// The markdown template for the benchmark report.
    /// Placeholders are described in curly braces for the LLM to fill in.
    /// </summary>
    public const string Template = """
        # Benchmark Report

        **Test Run:** `{test-run-name}`
        **Date:** {test-date}
        **Report Generated:** {report-date}
        **Model Used:** `{model-name}`

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
        """;
}
