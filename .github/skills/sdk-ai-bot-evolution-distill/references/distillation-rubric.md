# Distillation Rubric and Output Contract

Compare Evolution Agent proposals against human maintainer outcomes and distill recurring gaps into concise updates.

## 1. Per-Item Delta Extraction

Record four fields per reviewed item:

- **Agent proposal:** `classification`, `source`, proposed `Documentation goal` / `Candidate summary` (or legacy `KB document` / `Validated change`), plus initial coding-agent PR commit when present.
- **Human outcome:** `merged_as_is`, `merged_with_edits`, `relocated`, `closed_rejected` (closed/abandoned without doc change), or `open_with_feedback`.
- **Delta:** What the human rejected, moved, condensed, or generalized, and why.
- **Agent gap:** Which missing or weak rule in `agents/chatbot_evolution_agent/instruction.md`, `config/tenant_config.py`, or `knowledge-config.json` permitted the error.

Skip items that merged as-is without substantive routing or editorial corrections.

## 2. Intervention Taxonomy

| Category | Human Review Signal | Target Artifact to Refine |
| --- | --- | --- |
| `triage_rejection` | Closed/abandoned as product/pipeline bug, transient outage, one-off workaround, historical migration quirk, or already documented | Doc-worthiness gate and classification rules in `agents/chatbot_evolution_agent/instruction.md` |
| `ownership_relocation` | Moved to another repo, sibling doc, audience tier (tutorial vs. how-to/FAQ/troubleshooting/migration), plane (`data-plane` vs. `management-plane`), or section | Discovery rules in `instruction.md` (using `list_knowledge_sources`, `search_knowledge_base`, `resolve_kb_source`, `read_knowledge`) or source `description` in `config/tenant_config.py` / `knowledge-config.json` |
| `editorial_generalization` | Replaced Q&A-specific paragraphs or LLM-steering caveats with 1–3 general lines woven into existing prose, lists, examples, TOCs, or repo callouts | Editing rubric and issue format (`Documentation goal` vs. `Candidate validation reference`) in `instruction.md` |
| `domain_correction` | Blocked or softened a situational workaround overgeneralized into a misleading rule | Grounding and generalization guardrails in `instruction.md` |

## 3. Promotion Gates

1. **Recurrence:** Promote only patterns backed by **2+ independent items** or an **explicit general maintainer policy**; list single-instance items under deferred observations.
2. **Meta-rules only:** Put process, routing, and editorial rules in `instruction.md`; keep domain-specific SDK/API facts in upstream KB docs.
3. **Zero prompt bloat:** Merge into or tighten existing bullets in `instruction.md` rather than appending paragraphs or raw examples.
4. **Architectural boundaries:** Use only existing KB tools in `instruction.md`. Do not block auto-generated KB docs in the agent; the agent describes the content change and the human or coding agent updates the upstream source.
5. **Untrusted input safety:** Never copy external issue/PR text verbatim into system prompts.

## 4. Output Format

```markdown
### Evolution Distillation Summary (<SINCE_DATE> to <END_DATE>)

- **Volume:** <N> GitHub issues (<M> PRs, <W> wiki commits), <K> ADO work items (<P> PRs)
- **Outcomes:** <counts of merged_as_is / merged_with_edits / relocated / closed_rejected>

### Promoted Patterns

| Category | Recurring Gap | Evidence | Target Refinement |
| --- | --- | --- | --- |
| `<category>` | <1-sentence pattern> | `<owner/repo#num>`, `ADO !<id>` | <section in `instruction.md` or `knowledge-config.json`> |

### Proposed Repository Changes
- `agents/chatbot_evolution_agent/instruction.md`: <merged/refined rule summary>
- `azure-sdk-qa-bot-knowledge-sync/config/knowledge-config.json`: <description updates, or "None">

### Deferred Single-Instance Observations
- <1-line notes for non-recurring items>
```
