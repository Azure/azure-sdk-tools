# Partner Escalation — GitHub Systems Revamp

> Build on existing GitHub Event Processor, Issue Labeler, and MCP tooling to improve partner communication and issue triage, reduce manual handoffs, and introduce ICM only as a targeted escalation mechanism (post‑SLA breach).

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

### Issue Labeler Service

- **OpenAI Labeler** — RAG‑based labeling
- **Legacy ML.NET Labeler** — traditional ML approach
- **TriageRag** — semantic + vector search
- **Knowledge Agent** — answer generation

### GitHub Event Processor

- Webhook‑based issue triage
- Scheduled processing (stale issues, lifecycle rules)
- Service attention routing via CODEOWNERS

### Azure SDK MCP Server

- Existing tools: service label checks, CODEOWNERS lookup
- Opportunity to extend with partner‑facing tools

---

## Proposed Areas to Explore

### 1. Improve Issue Labeling & Routing

- Extend existing labeler approach to Azure SDK repos
- Establish baseline metrics (accuracy, manual corrections)

### 2. Partner Self‑Service Tooling

MCP / CLI tools for:

- Issue queue visibility
- Ownership lookup
- SLA status (on track / approaching / breached)

### 3. SLA‑Based Escalation via ICM

- GitHub remains system of record
- ICM created only after SLA breach
- Leverage existing scheduled event processing

### 4. SLA Monitoring & Notifications

- Automated detection of approaching / breached SLAs
- Replace manual dashboards with lightweight signals
- GitHub comments first; Teams notifications later

### 5. Copilot Coding Agent (Exploratory)

- Initial issue investigation
- Draft responses or low‑complexity fixes
- Agent workflow integration

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

---

## Feedback & Questions

Comments from Ronnie Geraghty shared in our earlier sync:

- **Labeling & triage work together** — While the current labeling infrastructure functions, it doesn't work equally well across all repos or scenarios. Improving one piece (e.g., labeling) without considering routing, escalation, and reporting together may limit impact.

- **Event Processor vs GitHub Actions** — The current GitHub Event Processor (C# + Octokit) is powerful but not easily customizable. Some teams have asked whether GitHub Actions–based approaches would allow more repo‑specific customization.

- **Shared vs repo‑specific labeling** — Today's auto‑triage assumes SDK‑style labels (plane + service). Other repos have different needs and may want custom label models. This creates a tradeoff:
  - Flexibility for individual repos
  - vs consistent SLA reporting across repos

  A possible direction is a mix of shared labels (for reporting) and repo‑specific labels (for local workflows).

- **Recurring SLA visibility** — In addition to dashboards or tools, recurring SLA/issue summary emails (similar to what exists for the Java team) may be a simple, high‑value way to improve visibility for Eng and PM owners tied to service tree IDs.
