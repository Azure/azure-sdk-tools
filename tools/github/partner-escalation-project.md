# Partner Escalation — GitHub Systems Revamp

> Build on existing GitHub Event Processor, Issue Labeler, and MCP tooling to improve partner communication and issue triage, reduce manual handoffs, and introduce ICM only as a targeted escalation mechanism (post‑SLA breach).

**Project board:** [Issue Resolution & Partner Enablement](https://github.com/orgs/Azure/projects/937)
**Metrics reference:** [Azure/azure-sdk-pr#2550](https://github.com/Azure/azure-sdk-pr/issues/2550)

## Purpose

Ongoing working series to identify concrete pain points around GitHub triage, partner communication, and SLA‑based escalation into ICM, and converge on a small, clearly scoped Phase 1.

---

## Context & Problem Statement

- Pain around GitHub issue triage, ownership clarity, and partner communication
- Limited visibility into SLA status and upcoming breaches
- Manual back‑and‑forth between SDK and partner teams
- Escalation paths exist but are inconsistent or too late

---

## What Already Exists (Baseline)

### Issue Labeler Service ([tool docs](../issue-labeler/))

- **OpenAI Labeler** — RAG‑based labeling
- **Legacy ML.NET Labeler** — traditional ML approach
- **TriageRag** — semantic + vector search
- **Knowledge Agent** — answer generation

### GitHub Event Processor ([tool docs](../github-event-processor/README.md))

- GitHub Actions–based event‑driven issue triage
- Scheduled processing (stale issues, lifecycle rules)
- Service attention routing via CODEOWNERS

### Azure SDK MCP Server ([tool docs](../../eng/common/mcp/README.md))

- Existing tools: service label checks, CODEOWNERS lookup
- Opportunity to extend with partner‑facing tools

---

## Epics

### 1. Issue Labeling & Routing ([#14114](https://github.com/Azure/azure-sdk-tools/issues/14114))

Improve the accuracy and coverage of automated issue labeling across repos, establish baseline metrics for label correctness, and determine the right balance between shared labels (for consistent SLA reporting) and repo‑specific labels (for local workflows).

**Key deliverables:**
- Baseline labeling accuracy per repo using `Mcp.Evaluator`
- Improved label model for pilot repo (e.g., Java)
- Decision on shared vs. repo‑specific label taxonomy

**Proposed metrics:**

| Metric | Definition | Target |
|--------|-----------|--------|
| Triage backlog | Open issues labeled `needs-triage`, per repo | ↓ Decrease |
| Auto‑label rate | % of issues receiving a service label from automation | ↑ Increase |
| Manual correction rate | % of auto‑labeled issues where a human changes the label within 48h | ↓ Decrease |
| Offline model accuracy | Server + Tool label accuracy from `Mcp.Evaluator` against ground truth | ↑ Increase |
| Time to first service label | Minutes from issue creation → first service label | ↓ Decrease |

### 2. Partner Self‑Service Tooling ([#14115](https://github.com/Azure/azure-sdk-tools/issues/14115))

Build MCP/CLI tools that let partner and service teams independently check issue queues, look up ownership, and view SLA status — reducing the SDK team's role as intermediary.

**Key deliverables:**
- `azsdk_issue_status` MCP tool — partners can query their issue queue directly
- `azsdk_sla_report` MCP tool — proactive SLA visibility for partners
- Ownership lookup integrated into existing MCP server

**Proposed metrics:**

| Metric | Definition | Target |
|--------|-----------|--------|
| Service Attention backlog | Open issues labeled `Service Attention`, per repo | ↓ Decrease |
| Service Attention staleness | % of `Service Attention` issues with no update in 14+ days | ↓ Decrease |
| MCP tool adoption | Unique users invoking partner‑facing MCP tools per week | ↑ Increase |
| SDK‑as‑intermediary volume | Manual handoff actions by SDK team on behalf of partners | ↓ Decrease |

### 3. SLA Monitoring & Notifications ([#14116](https://github.com/Azure/azure-sdk-tools/issues/14116))

Automate detection of approaching and breached SLAs, and deliver actionable signals (GitHub comments, recurring summary emails, eventually Teams notifications) to replace underused manual dashboards.

**Key deliverables:**
- Automated SLA threshold detection integrated into Event Processor
- GitHub comment notifications on approaching/breached SLAs
- Recurring SLA summary emails (similar to existing Java team model)

**Proposed metrics:**

| Metric | Definition | Target |
|--------|-----------|--------|
| `needs-team-attention` dwell time (P50/P90) | Duration an issue carries `needs-team-attention` before removal | ↓ Decrease |
| `needs-team-attention` staleness | % of `needs-team-attention` issues with no update in 14+ days | ↓ Decrease |
| Issue age distribution (P50/P90) | Age (days) of currently open issues | ↓ Decrease |
| Stale issue rate | % of open issues with no activity in 90+ days | ↓ Decrease |
| Resolution time (P50/P90) | Days from open → close for issues in measurement period | ↓ Decrease |
| Notification delivery rate | % of SLA‑approaching issues that received automated notification | ↑ Increase |

### 4. SLA‑Based Escalation via ICM ([#14117](https://github.com/Azure/azure-sdk-tools/issues/14117))

Introduce ICM as a targeted escalation path triggered only after an SLA breach. Define the escalation contract (trigger criteria, required metadata, ownership) and ensure GitHub remains the system of record.

**Key deliverables:**
- Defined SLA breach threshold (e.g., "14 days in `Service Attention` with no partner response")
- Escalation contract: trigger criteria, required ICM metadata, ownership rules
- Integration with Event Processor scheduled processing

**Proposed metrics:**

| Metric | Definition | Target |
|--------|-----------|--------|
| SLA breach rate | % of issues exceeding SLA threshold without resolution | ↓ Decrease |
| ICM incidents created from GitHub | Count of auto‑created ICM incidents from SLA breaches | Controlled increase, then stabilize |
| ICM → resolution time | Days from ICM creation to GitHub issue close | ↓ Decrease |
| False escalation rate | % of ICM incidents closed without action (unnecessary) | ↓ Decrease |

### 5. Triage Infra & Repo Playbooks ([#14118](https://github.com/Azure/azure-sdk-tools/issues/14118))

Evaluate GitHub Event Processor vs. GitHub Actions for repo‑specific customization, and document per‑repo triage playbooks capturing each language repo's unique assumptions around ownership, escalation, and workflows.

**Key deliverables:**
- Assessment: Event Processor extensibility vs. GitHub Actions for repo‑specific rules
- Documented triage playbooks for each language repo
- Enhanced Event Processor escalation patterns (level 1‑2 escalation: nudge → team lead)

**Proposed metrics:**

| Metric | Definition | Target |
|--------|-----------|--------|
| Repos with documented playbooks | Count of language repos with a formalized triage playbook | ↑ Increase to 5 |
| Event Processor rule coverage | Number of active rules in `event-processor.config` per repo | Stable or growing |
| Repo‑specific customization requests | Requests from language teams for triage behavior changes | Track, then ↓ |
| Issue velocity by repo | Net new issues (opened − closed) per 30 days per repo | Net negative |

### 6. Copilot Coding Agent — Exploratory ([#14119](https://github.com/Azure/azure-sdk-tools/issues/14119))

Test whether AI‑powered agents can meaningfully assist with initial issue investigation, draft responses, or low‑complexity fixes by piloting one narrow use case before deciding on broader investment.

**Key deliverables:**
- Pilot: one narrow use case (issue summary / draft response)
- Decision framework for broader investment based on pilot results

**Proposed metrics:**

| Metric | Definition | Target |
|--------|-----------|--------|
| Agent‑assisted issue count | Issues where the Copilot agent provided investigation or draft response | ↑ Increase |
| Agent response acceptance rate | % of agent draft responses used (with or without edits) | ↑ Increase |
| Time to first response (agent vs. manual) | Compare first‑response time on agent‑handled vs. manual issues | Agent faster |

---

## Metrics Collection Tiers

| Tier | Description | Count | Effort |
|------|-------------|-------|--------|
| **Tier 1 — Snapshots** | GitHub Search API counts. No per‑issue calls. | 10 | Low — single scheduled script |
| **Tier 2 — Timeline Sampling** | Per‑issue Timeline API calls on a sample. | 9 | Medium — sampling script + rate‑limit handling |
| **Tier 3 — Instrumented** | Built into systems as they're delivered. | 7 | Baked into epic delivery |

---

## Repos in Scope

| Repo | Notes |
|------|-------|
| `Azure/azure-sdk-for-python` | High volume, large stale backlog |
| `Azure/azure-sdk-for-java` | Existing email‑based SLA summaries for reference |
| `Azure/azure-sdk-for-js` | Low incoming volume but high staleness |
| `Azure/azure-sdk-for-net` | Highest total volume, most Service Attention issues |
| `Azure/azure-sdk-for-go` | Smallest repo, mostly security ICMs |

---

## Ownership Table (DRIs)

| Area | DRI (Owner) | Why this owner | Concrete decision this owner drives |
|------|-------------|----------------|-------------------------------------|
| Improve Issue Labeling & Routing (Azure SDK repos) | Ronnie Geraghty | Deep history owning labeler + triage models; explicitly called out limits of current models and repo differences; discussed .NET/Java custom models vs general model | Decide whether improving labeling accuracy is a Phase‑1 lever, which repo to pilot (e.g., Java), and what metric matters (manual relabels, misroutes) |
| Partner Self‑Service Tooling (MCP / CLI) | Sameeksha | Raised SDK teams acting as intermediaries; proposed MCP/CLI surfaces; focused on reducing manual back‑and‑forth | Decide which 2–3 partner questions to support first (issue queue, owner, SLA) and what surface (MCP tool vs CLI vs GitHub command) |
| SLA Escalation via ICM | Maddy / Xiang | Explicitly raised .NET pain with ICM volume and SDKs being middlemen; shared ICM data context | Decide exact escalation contract: when SLA breach triggers ICM, what metadata is required, and confirm SDK is not intermediary |
| SLA Monitoring & Notifications | Sameeksha / Maddy | Called out service teams being unaware of SLA metrics and dashboards not being consumed | Decide what signal actually changes behavior (GitHub comment, summary notification), threshold, and what not to build (no new dashboards) |
| Repo‑specific differences / Playbooks | Language reps (Python: Xiang, .NET: Heaps, C/C++/Rust/Go: Anton Kolesnyk) | Each described materially different repo realities (Python issue growth, .NET labeler works but low service engagement, C/C++ mostly security ICMs) | Capture repo‑specific triage assumptions (GitHub vs ICM first, ownership, escalation) so automation doesn't assume one global flow |
| Copilot Coding Agent (Exploratory) | Ronnie Geraghty / Anton | Has historical context on past AI attempts and explicitly framed this as exploratory only | Decide whether to revisit later by testing 1 narrow use case (issue summary / draft response) |


