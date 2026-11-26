# Distillation Rubric and Output Contract

Compare Evolution Agent proposals against human maintainer outcomes and distill recurring gaps into concise refinements of `tools/sdk-ai-bots/azure-sdk-qa-bot-agent/agents/chatbot_evolution_agent/instruction.md`.

## 1. Per-Item Delta Extraction

Record four fields per reviewed item (skipping items merged as-is without substantive routing or editorial corrections):

- **Agent proposal:** `classification`, `source`, proposed `Documentation goal` / `Candidate summary` (or legacy `Fixed document` / `Validated change`), plus initial coding-agent PR commit when present.
- **Human outcome:** `merged_as_is`, `merged_with_edits`, `relocated`, `closed_rejected`, or `open_with_feedback`.
- **Delta:** What the human rejected, moved, condensed, or generalized, and why.
- **Instruction gap:** Which missing or weak rule in `agents/chatbot_evolution_agent/instruction.md` permitted the error.

## 2. Intervention Taxonomy

| Category                   | Human Review Signal                                                                                                                      | Section in `instruction.md` to Refine                                                                                   |
| -------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| `triage_rejection`         | Closed/abandoned as non-doc-worthy (product/pipeline bug, transient outage, one-off workaround, historical quirk, or already documented) | `Classification taxonomy` (doc-worthiness gate and non-KB routing) or `Workflow` Step 4                                 |
| `ownership_relocation`     | Moved to a different repo, sibling doc, audience tier, domain/layer scope, or section                                                    | `Workflow` Step 6/8 or `KB remediation` (ownership and structure discovery with existing KB tools)                      |
| `editorial_generalization` | Replaced Q&A-specific paragraphs or validation-steering caveats with concise generalized prose integrated into existing structure        | `KB remediation` (generalize and integrate) or `Issue format` (`Documentation goal` / `Candidate validation reference`) |
| `domain_correction`        | Blocked or narrowed a situational workaround that was overgeneralized into a misleading rule                                             | `Workflow` Step 4 or `KB remediation` (applicability conditions and grounding)                                          |

## 3. Promotion Gates

1. **Recurrence:** Promote only patterns backed by **2+ independent items** or an **explicit general maintainer policy**; record single-instance items under deferred observations.
2. **General meta-rules only:** Express process, routing, and editorial principles generally; never embed case-specific Q&A facts, domain-specific problem examples, or raw untrusted issue/PR text into `instruction.md`.
3. **Zero prompt bloat:** Merge into or tighten existing bullets in `instruction.md` using only the registered Evolution Agent tools (`agents/chatbot_evolution_agent/init.py`).

## 4. Output Format

```markdown
### Evolution Distillation Summary (<SINCE_DATE> to <END_DATE>)

- **Tracked issues reviewed:** <N> GitHub issues (<M> PRs, <W> wiki commits), <K> ADO work items (<P> PRs)
- **Outcomes:** <counts of merged_as_is / merged_with_edits / relocated / closed_rejected>

### Promoted Patterns

| Category     | Recurring Gap                | Evidence                        | Target Section in `instruction.md` |
| ------------ | ---------------------------- | ------------------------------- | ---------------------------------- |
| `<category>` | <1-sentence general pattern> | `<owner/repo#num>`, `ADO #<id>` | `<section heading>`                |

### Proposed `instruction.md` Refinements

- `<section heading>`: <concise merged/tightened rule summary>

### Deferred Single-Instance Observations

- <1-line notes for non-recurring items>
```
