# Agentic API Review with GitHub Copilot SDK

**Issue:** [Azure/azure-sdk-tools#14678](https://github.com/Azure/azure-sdk-tools/issues/14678)

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Goals](#2-goals)
3. [Non-Goals](#3-non-goals)
4. [Current Architecture (Deterministic Pipeline)](#4-current-architecture-deterministic-pipeline)
5. [Proposed Architecture (Agentic)](#5-proposed-architecture-agentic)
6. [Migration Mapping: Pipeline Stages → Agentic Equivalents](#6-migration-mapping-pipeline-stages--agentic-equivalents)
7. [Chunking Strategy for Large APIs](#7-chunking-strategy-for-large-apis)
8. [MVP Plan](#8-mvp-plan)
9. [Key Design Decisions](#9-key-design-decisions)
10. [Risk Assessment](#10-risk-assessment)
11. [File Structure (New/Modified)](#11-file-structure-newmodified)
12. [Success Criteria](#12-success-criteria)
13. [Open Questions](#13-open-questions)

---

## 1. Executive Summary

Replace AVC's deterministic 9-stage review pipeline with an agentic process powered by the GitHub Copilot Python SDK (`github-copilot-sdk`). The agent receives the API surface as input and uses **skills** — sourced directly from the Azure SDK design guideline markdown files and accumulated review memories — to inject language-specific, service-specific, and cross-cutting review knowledge. **Tools** provide dynamic access to the specific review being conducted (existing comments, API outline). The Copilot runtime handles planning, tool orchestration, and multi-turn reasoning — eliminating the hand-coded pipeline stages.

Critically, the skills are plain `SKILL.md` / `.instructions.md` files compatible with both the Copilot SDK (server-side) and VS Code Agent Mode / GitHub Copilot CLI (developer-side). This means the same review knowledge surfaces whether a review runs through the AVC endpoint or a developer invokes it locally.

---

## 2. Goals

1. **Eliminate Cosmos DB + Azure AI Search as the guideline/example knowledge base.** Guidelines and examples currently live as markdown in the [azure-sdk](https://github.com/Azure/azure-sdk) repo (e.g., `docs/python/design.md`) and are manually curated into Cosmos DB. The agentic approach should source these directly as skills — the markdown files *are* the knowledge base.

2. **Support service-specific and service×language-specific guidelines.** Today, review knowledge is scoped only by language. Services like Azure Functions, Key Vault, or Event Hubs have their own patterns, conventions, and approved exceptions. The skill hierarchy must allow scoping at `{language}`, `{service}`, and `{service}/{language}` levels.

3. **Reusable skills across surfaces.** The skills that encode review knowledge should be consumable by:
   - The **AVC FastAPI endpoint** (via GitHub Copilot SDK, server-side, automated reviews)
   - **VS Code Agent Mode** (developer loads skills into a Copilot chat session for interactive review)
   - **GitHub Copilot CLI** (developer runs a review from the terminal)
   
   This means skills must be self-contained markdown files with no dependency on AVC-specific tooling.

4. **Simplify the Azure resource footprint.** Removing Cosmos DB (guidelines + examples + memories containers), Azure AI Search (all 3 indexers), and the manual curation workflow significantly reduces operational complexity.

5. **Eliminate Cosmos DB memories.** Memories (learned review decisions from thread resolutions and @mention feedback) overlap heavily with service-specific skills. Rather than maintaining a separate dynamic knowledge base, memories should be stored as text — either individual markdown files or distilled into service-specific `SKILL.md` files — aligned with the skill-based architecture. This eliminates the `memories` Cosmos DB container, the memories AI Search indexer, and the `SearchManager` dependency for review-time knowledge retrieval.

6. **Maintain or improve review quality** compared to the current deterministic pipeline.

## 3. Non-Goals

1. **Eliminating Cosmos DB entirely.** The `review-jobs`, `metrics`, and `evals` containers serve operational purposes (job tracking, telemetry, evaluation storage) and are not candidates for removal in this work. Memories *are* in scope — see Goal 5.

2. **Replacing the existing deterministic pipeline immediately.** The agentic mode will be an experimental parallel mode. The existing pipeline remains the default until the agentic mode is validated.

3. **Migrating the @mention or thread-resolution agent workflows.** These use Azure AI Agent Service and are out of scope.

4. **Building a general-purpose code review agent.** This is specifically for Azure SDK API surface review in APIView format.

5. **Automating guideline markdown authoring.** Teams still write and maintain their guideline markdown files in the azure-sdk repo. This proposal consumes them; it doesn't generate them.

---

## 4. Current Architecture (Deterministic Pipeline)

```
API Text → Section → [3 Parallel LLM Calls/Section] → Filter Generic → Deduplicate
→ Hard Filter → Filter Preexisting → Judge Score → Assign Correlation IDs → Output
```

**9 fixed stages**, each a separate prompt template (`.prompty` file) with rigid inputs/outputs:

| Stage | Prompt | Purpose |
|-------|--------|---------|
| 1. Generate (guideline) | `guidelines_review.prompty` | RAG with all language guidelines |
| 2. Generate (context) | `context_review.prompty` | RAG with semantic search (memories/examples) |
| 3. Generate (generic) | `generic_review.prompty` | Custom rules + design principles |
| 4. Filter generic | `filter_generic_comment.prompty` | Validate generic comments against KB |
| 5. Deduplicate | `merge_comments.prompty` | Merge same-line comments |
| 6. Hard filter | `filter_comment_with_metadata.prompty` | Apply language exceptions + outline |
| 7. Filter preexisting | `filter_existing_comment.prompty` | Remove duplicates of human comments |
| 8. Judge score | `judge_comment_confidence.prompty` | Confidence + severity scoring |
| 9. Correlate | `generate_correlation_ids.prompty` | Group similar comments |

**Pain points:**
- 9+ LLM round trips per section, most doing narrow filtering tasks
- Rigid pipeline — can't skip stages or adapt based on content
- Each stage loses context from previous stages (stateless prompt calls)
- Parallel-but-fixed: 3 prompts/section regardless of content complexity
- Heavy post-processing (5 filter stages) to compensate for generation quality

---

## 5. Proposed Architecture (Agentic)

```
API Text → Copilot Session (system prompt + skills + tools) → Structured Output
```

The agent has full context (guidelines, memories, exceptions, outline) injected via **skills** and accessed on-demand via **tools**. It reasons about the API surface in a multi-turn loop, deciding what to investigate and when to stop.

### Core Components

#### 1. Skills (Loaded into Context)

Skills replace both the `.prompty` system prompts **and** the Cosmos DB guideline/example knowledge base. Each is a markdown file whose content is injected into the session context automatically. The same files work across three surfaces:

| Surface | How Skills Are Loaded |
|---------|----------------------|
| **AVC endpoint** (Copilot SDK) | `skill_directories=[...]` in `create_session()` |
| **VS Code Agent Mode** | Skills folder referenced via `AGENTS.md` or workspace settings |
| **GitHub Copilot CLI** | `copilot --skill-directories ./skills/...` |

##### Skill Hierarchy

Domain skills are distributed via the **`eng/common/` sync mechanism** — the existing pipeline that authors content in `azure-sdk-tools/eng/common/` and automatically syncs it to every language repo (`azure-sdk-for-python`, `azure-sdk-for-java`, `azure-sdk-for-net`, etc.) and related repos like `azure-rest-api-specs`. This means any developer working in their language repo already has the review skills locally — no extra cloning required.

There are already `eng/common/instructions/` (for `.instructions.md` files) and `eng/common/knowledge/` folders in this sync tree. Review skills extend this pattern.

**Domain skills — live in `azure-sdk-tools/eng/common/skills/`** (synced to all repos):

```
eng/common/skills/
├── api-review/
│   └── SKILL.md              # Core review methodology, output format,
│                              # severity taxonomy. Always loaded.
├── diff-review/
│   └── SKILL.md              # Additional instructions for diff-mode reviews.
│
├── general/
│   └── SKILL.md              # Cross-language guidelines (curated from
│                              # azure-sdk/docs/general/)
│
├── python/
│   ├── SKILL.md              # Python design guidelines (curated from
│   │                          # azure-sdk/docs/python/design.md)
│   └── services/
│       ├── keyvault/
│       │   └── SKILL.md      # Key Vault Python-specific patterns/exceptions
│       └── eventhubs/
│           └── SKILL.md      # Event Hubs Python-specific patterns/exceptions
│
├── java/
│   ├── SKILL.md              # Java design guidelines
│   └── services/
│       └── ...               # Service-specific Java skills
│
├── dotnet/
│   ├── SKILL.md              # .NET design guidelines
│   └── services/
│       └── ...
│
├── (one per language...)
│
└── services/
    ├── keyvault/
    │   └── SKILL.md          # Cross-language Key Vault patterns/exceptions
    └── eventhubs/
        └── SKILL.md          # Cross-language Event Hubs patterns/exceptions
```

Since `eng/common/` syncs everywhere, a developer working in `azure-sdk-for-python` sees these skills at `eng/common/skills/` and can reference them from their workspace `AGENTS.md` or VS Code settings. The AVC endpoint in `azure-sdk-tools` has them in the same repo — no cross-repo fetch needed.

**Scoping at review time:** When reviewing a Python API for, say, `azure-keyvault-keys`, the session loads:
1. `eng/common/skills/api-review/` — always
2. `eng/common/skills/general/` — always
3. `eng/common/skills/python/` — language match
4. `eng/common/skills/services/keyvault/` — service match (cross-language)
5. `eng/common/skills/python/services/keyvault/` — service×language match

Plus `skills/diff-review/` if in diff mode.

##### Sourcing and Authorship

The **language guideline** skills are curated from the design docs in `azure-sdk` (e.g., `docs/python/design.md`). These docs were written by architects and are rarely updated. A build script or manual process extracts the most review-relevant sections and assembles the `SKILL.md` files. These are essentially static snapshots.

The **service-specific** skills are a different story — no human is writing these. They are **derived artifacts**, distilled from AVC's accumulated review memories. When AVC learns from feedback (mention responses, thread resolutions), those memories are scoped by language and often by service. A periodic AVC process can distill high-confidence memories for a given service into a `SKILL.md` file, creating service-specific review knowledge that would never otherwise be authored.

| Current | Agentic |
|---------|--------|
| `azure-sdk/docs/python/design.md` → manual curation → Cosmos DB → AI Search → RAG | `azure-sdk/docs/python/design.md` → curated extract → `eng/common/skills/python/SKILL.md` → loaded into agent context |
| (nothing — no service-specific knowledge) | AVC memories → periodic distillation → `eng/common/skills/python/services/keyvault/SKILL.md` |
| Memories in Cosmos DB → AI Search → RAG tool call at review time | Memories → text files in skill hierarchy → loaded into agent context |

##### Memories as Skills

Today, memories live in Cosmos DB and are retrieved at review time via Azure AI Search (RAG). In the agentic architecture, memories become **text artifacts in the skill hierarchy**, eliminating the Cosmos DB `memories` container and the memories AI Search indexer entirely.

**How it works:**

1. **Existing memories are distilled into service-specific skills.** A one-time migration process (`scripts/distill_memories.py`) reads all memories from Cosmos DB, groups them by language and service, and generates `SKILL.md` files under the appropriate `{language}/services/{service}/` directory. High-confidence memories become documented patterns and exceptions in the skill content.

2. **New feedback flows produce text files, not database records.** When the thread resolution or mention workflows would previously create a `Memory` object in Cosmos DB, they instead produce a structured markdown file committed to the skill hierarchy:

   ```
   eng/common/skills/python/services/keyvault/memories/
   ├── 2026-03-15-vault-url-parameter-convention.md
   ├── 2026-04-01-key-rotation-async-pattern.md
   └── ...
   ```

   Each memory file follows a standard template:
   ```markdown
   ---
   title: vault_url parameter convention
   language: python
   service: keyvault
   is_exception: true
   source: thread_resolution
   related_guidelines: [python_design.html#client-constructor]
   created: 2026-03-15
   ---
   Key Vault Python clients use `vault_url` instead of `endpoint` as the
   primary constructor parameter. This is an approved exception to the
   general naming guideline.
   ```

3. **Periodic distillation rolls individual memory files into the parent `SKILL.md`.** As individual memory files accumulate for a service, a periodic process (manual or automated) consolidates them into the service's `SKILL.md` — combining related memories, resolving contradictions, and producing a coherent narrative. The individual files can then be archived or removed.

4. **The commit-based workflow provides versioning and review.** Unlike Cosmos DB records (which are created silently), text files committed to `eng/common/skills/` go through the standard PR process — architects can review, edit, and approve new memories before they affect future reviews. This adds a quality gate that the current write-to-Cosmos flow lacks.

**Creation flow options for new feedback:**

| Option | Mechanism | Pros | Cons |
|--------|-----------|------|------|
| **A. Direct PR** | Thread resolution / mention workflows create a branch and open a PR with the new memory file | Full review gate; works with eng/common sync | Latency — memory isn't active until PR merges and syncs |
| **B. Local file store + periodic sync** | AVC writes memory files to a local/blob staging area; a scheduled job opens batched PRs | Lower latency for creation; batched review | Staging area adds operational complexity |
| **C. Git-backed store** | AVC has write access to a dedicated memories branch; auto-merges non-exception memories, PRs for exceptions | Fast for non-controversial memories; review gate for exceptions | Requires branch management; auto-merge trust model |

**Recommendation:** Start with **Option A** (direct PR) for simplicity, accepting the latency tradeoff. The current memory creation rate (a few per week) is low enough that PR latency is acceptable. Revisit if volume increases.

The conversion for language skills could be:
- **Direct copy**: Guideline markdown used as-is in `SKILL.md` (if small enough for context)
- **Curated extract**: A build script extracts the most review-relevant sections and assembles `SKILL.md`
- **Reference + fetch**: `SKILL.md` contains summaries; a tool fetches full guideline text on-demand

**Recommendation:** Start with curated extracts. The full `python_design.md` is ~4000 lines — too large for context. Extract the sections most relevant to API surface review (naming, types, parameters, error handling, etc.) and aggregate with the filter exceptions from `metadata/{language}/filter.yaml`.

##### Skill Content

**Language skills** contain curated extracts from the corresponding azure-sdk design docs (naming conventions, client design, type guidance, error handling, etc.) combined with the filter exceptions from `metadata/{language}/filter.yaml`.

**Service-specific skills** contain approved patterns and known exceptions distilled from AVC memories for that service (e.g., Key Vault's `vault_url` parameter convention, or Event Hubs' partition-based client model). These are generated by an AVC process, not hand-authored.

##### Reuse Across Surfaces

Because skills are plain markdown files with optional YAML frontmatter, and because `eng/common/` is synced to every Azure SDK repo, they work natively across all three consumption surfaces:

- **VS Code Agent Mode** — Developer working in `azure-sdk-for-python` (or any language repo, or `azure-rest-api-specs`) already has `eng/common/skills/` synced locally. They configure skill directories in `AGENTS.md` or workspace settings and run interactive reviews in chat.
- **GitHub Copilot CLI** — Developer passes `--skill-directories` pointing to their local `eng/common/skills/` to load review knowledge for a terminal-based review
- **AVC endpoint** — Skills live in the same `azure-sdk-tools` repo as AVC. The `AgenticApiViewReview` class resolves the skill directories relative to the repo root.

The key design principle: language and service skills contain **only domain knowledge** (guidelines, patterns, exceptions). AVC-specific instructions (output format, tool usage, scoring rubric) live in the `api-review` skill only. This separation means language/service skills are reusable anywhere without modification.

#### 2. Tools (On-Demand Knowledge Access)

With guidelines, examples, and memories moved into skills (loaded into context), tools serve a narrow role: accessing **live data** about the specific review at hand. The agent is given the following tools:

| Tool | Purpose |
|------|---------|
| **get_existing_comments** | Check if humans have already commented on a specific line, to avoid duplicating existing feedback |
| **get_api_outline** | Retrieve the high-level outline/structure of the API being reviewed |
| **submit_comment** | Submit a review comment with line number, bad code, suggestion, explanation, guideline citations, and severity. This is the primary output mechanism — each comment is captured as a tool call, avoiding the need to parse free-form JSON |

Note: With memories stored as text in the skill hierarchy (see Goal 5 and Skill Hierarchy below), there is no `search_memories` tool. Past decisions — exceptions, approved patterns, feedback-derived learnings — are loaded directly into the agent's context via service-specific and language-specific skills, the same way guidelines are. This eliminates a tool-call round trip and ensures the agent always has the full precedent context without needing to decide what to search for.

#### 3. Focused Review Passes (Multi-Agent Pattern)

The current pipeline runs three parallel prompts per section: **guideline review**, **context review** (RAG-based), and **generic review**. This maps naturally to a multi-agent pattern where focused reviewers each bring different expertise and their outputs are merged. The question is whether this pattern works across all three consumption surfaces.

**The answer is: the orchestration mechanism differs per surface, but the concept of focused passes applies everywhere.**

| Surface | How Focused Passes Work |
|---------|------------------------|
| **AVC endpoint (Copilot SDK)** | True sub-agents. The SDK supports custom sub-agents with scoped tools and prompts. An orchestrator agent spawns a **guideline reviewer** (loaded with language/service skills, focused on design guideline compliance), a **precedent reviewer** (loaded with service-specific memory skills, focused on past decisions and known exceptions), and a **design reviewer** (focused on API usability and consistency). The orchestrator merges their `submit_comment` outputs, deduplicates, and returns the combined result. This is fully automatic — no human in the loop. |
| **VS Code Agent Mode** | Separate `.agent.md` files. Each focused reviewer becomes an agent definition (e.g., `guideline-reviewer.agent.md`, `precedent-reviewer.agent.md`) that a developer can invoke independently. A composite `api-reviewer.agent.md` could also exist that loads all skills for a single-pass review. The developer chooses: run one focused agent or the composite. There is **no automatic orchestration** — the developer drives which agents to invoke. |
| **GitHub Copilot CLI** | Single-pass with all skills. The CLI doesn't support spawning sub-agents. A developer runs one invocation with all relevant skill directories loaded and gets a unified review. Multiple focused passes would require running the CLI multiple times with different `--skill-directories` sets — possible but manual. |

The implication for skill design: each focused reviewer's knowledge must live in **separate, composable skills** so that different surfaces can combine them differently. The `api-review/SKILL.md` (core methodology) is always loaded. The guideline reviewer loads `{language}/SKILL.md` + `services/`. The precedent reviewer loads `{language}/services/{service}/memories/` (the text-based memory files). The design reviewer loads `general/SKILL.md` for cross-cutting principles.

**Recommendation:** Design skills as composable units from the start. The AVC endpoint implements true sub-agent orchestration in Phase 2. VS Code and CLI surfaces get the focused-pass benefit through skill composition, even without automatic orchestration. Evaluate whether the sub-agent split meaningfully improves quality vs. a single agent with all skills — the current pipeline uses the 3-pass pattern because a single prompt couldn't hold all context, but skills-in-context may make a single-pass agent sufficient.

---

## 6. Migration Mapping: Pipeline Stages → Agentic Equivalents

| Current Stage | Agentic Equivalent | How |
|---------------|-------------------|-----|
| **Sectioning** | Reuse `SectionedDocument` | Same chunking logic. One agent session per section, deduplicate across sections. |
| **3× parallel prompts** | Single agent or sub-agent split | Single-pass: agent has all skills + tools in one session. Multi-pass (SDK only): orchestrator delegates to guideline/precedent/design sub-agents. Evaluate both. |
| **Filter generic comments** | Built into agent reasoning | All guideline knowledge is in context via skills. Agent self-filters because it can directly verify claims against loaded guidelines. |
| **Deduplication** | Agent maintains state | Agent sees its own submitted comments. Tool can reject duplicates. |
| **Hard filtering** | Skill-injected exceptions | Language skill includes filter exceptions. Agent avoids them naturally. |
| **Filter preexisting** | `get_existing_comments` tool | Agent checks before commenting. |
| **Judge scoring** | Agent self-scores | Prompt instructs agent to assign severity + confidence with each comment inline. |
| **Correlation IDs** | Post-processing (kept) | Lightweight deterministic grouping stays outside the agent. |
| **Sort + return** | Post-processing (kept) | Trivial. |

---

## 7. Chunking Strategy for Large APIs

The existing `SectionedDocument` logic carries over unchanged. The review requires complete coverage of every line — context compaction (which summarizes earlier turns) is not applicable here because the agent needs to see and reason about every line of the API surface.

One agent session runs per section, with post-processing to deduplicate across sections, same as today. For the sub-agent pattern (Section 3), the orchestrator can assign sections to focused reviewer sub-agents in parallel — this is just the existing chunking combined with the multi-agent split.

---

## 8. MVP Plan

The goal of the MVP is to validate the core hypothesis: **a single agent session with skills-in-context produces review quality comparable to the 9-stage pipeline.** We want to answer this question as fast as possible — before investing in SDK integration, pipeline wiring, or infrastructure changes. The MVP is static skill files + a manual test harness, nothing more.

### Scope

**One language. One API view. Side-by-side comparison.**

Pick Python (most mature guidelines, most review data). Pick a recent API review where the pipeline produced known-good output as the comparison baseline.

### Steps

1. **Author the minimum skill set** — hand-written markdown files, not wired to any pipeline or deployment:
   - `skills/api-review/SKILL.md` — review methodology, output format, severity taxonomy (extracted from the existing prompty system prompts: `guidelines_review.prompty`, `context_review.prompty`, `judge_comment_confidence.prompty`)
   - `skills/general/SKILL.md` — cross-language design principles (extracted from `azure-sdk/docs/general/`)
   - `skills/python/SKILL.md` — Python design guidelines + filter exceptions (extracted from `azure-sdk/docs/python/design.md` + `metadata/python/filter.yaml`)
   - `skills/python/services/keyvault/SKILL.md` — one service-specific skill with distilled memories (manually exported from Cosmos DB for Key Vault Python memories)

2. **Test in Copilot Agent Mode** — no code, no SDK, no new modules:
   - Open a workspace with the skill files and a saved API view text file
   - Point VS Code Agent Mode at the `skills/` directory (via workspace `AGENTS.md` or settings)
   - In Copilot chat, paste or reference the API view text and ask for a review
   - The agent has the skills loaded as context — it *is* the agentic reviewer, just running interactively instead of programmatically
   - Capture the output (comments, citations, reasoning)

3. **Compare output:**
   - Run the existing pipeline on the same API view via `avc review generate`
   - Manual side-by-side comparison of both outputs: comment relevance, false positive rate, missed issues, guideline citation accuracy
   - Note where the agent's reasoning differs — does it catch things the pipeline misses? Does it hallucinate? Does it handle filter exceptions that the pipeline needs 5 post-processing stages for?

4. **Iterate on skills:**
   - If the agent misses issues → refine skill content (add more guideline sections, sharpen instructions)
   - If the agent hallucinates → add constraints to `api-review/SKILL.md`
   - If context is too large → trim skill files, measure what fits
   - Repeat steps 2-3 until quality is comparable

### What this proves

- Whether skills-in-context can replace RAG (guidelines + examples + memories all loaded statically)
- Whether a single agent pass can replace the 9-stage generate-then-filter pipeline
- What skill content quality is needed to match the pipeline
- Whether the context window can hold skills + a code section simultaneously

### What this doesn't require

| Not needed for MVP | Why |
|--------------------|-----|
| `github-copilot-sdk` | VS Code Agent Mode *is* the Copilot SDK — same runtime, no code to write |
| New Python modules (`_agentic_reviewer.py`, etc.) | No programmatic integration yet |
| CLI or FastAPI changes | Not wiring into `avc` or the web service |
| `SectionedDocument` chunking | Test with one section or a small API; chunking is a known-solved problem |
| Tools (`submit_comment`, `get_existing_comments`) | Agent outputs comments as text; structured tool output is a later concern |
| Cosmos DB / AI Search changes | Existing pipeline untouched |
| `scripts/sync_skills.py` or `distill_memories.py` | Hand-author everything |
| Multi-agent orchestration | Single agent first |
| Telemetry, streaming, deployment | Zero infrastructure |

### Success gate

The MVP succeeds if the agent-with-skills produces **comparable or better** comments on the test API view — assessed by manual review. If it does, invest in the programmatic integration (Copilot SDK, `AgenticApiViewReview` class, CLI/FastAPI wiring). If not, iterate on skill content before writing any code.

---

## 9. Key Design Decisions

### 1. BYOK vs. GitHub Auth
**Recommendation: BYOK.** AVC already has an Azure AI Foundry endpoint. The Copilot SDK supports custom providers ("Bring Your Own Key") with Azure OpenAI endpoints. Using BYOK avoids coupling to GitHub Copilot billing and lets us use the same models/quotas.

### 2. Tool Granularity
**Recommendation: Fine-grained tools + `submit_comment` accumulator.** Let the agent decide when to check existing comments and when to stop.

### 3. All Knowledge as Skills, Not a Database
**Recommendation: Guideline, example, *and memory* content is loaded via skills (static context), not searched via tools.** This eliminates the Cosmos DB `guidelines`, `examples`, and `memories` containers and all three AI Search indexers. The agent always has the full guideline + precedent context via skills. No tool-call round trips for knowledge retrieval. Dynamic, review-specific data (existing human comments, API outline) is still accessed via tools.

### 4. Service-Specific Scoping
**Recommendation: Hierarchical skill directories.** Skills are organized as `{language}/`, `services/{service}/`, and `{language}/services/{service}/`. The session loader computes which skill directories to include based on the language and (when known) the service being reviewed.

### 5. Dual-Surface Skill Reuse
**Recommendation: Language/service skills contain only domain knowledge (guidelines, patterns, exceptions). AVC-specific instructions (output format, `submit_comment` tool usage, scoring rubric) live in the `api-review` skill only.** This separation means language/service skills are reusable in VS Code Agent Mode and Copilot CLI without modification.

### 6. Output Extraction
**Recommendation: `submit_comment` tool as primary output mechanism.** Each comment is captured as a tool call, avoiding the need to parse free-form JSON from the final response. The final message can be the summary.

### 7. Backward Compatibility
**Recommendation: Parallel implementation.** Keep `ApiViewReview` intact. New `AgenticApiViewReview` class shares `ReviewResult` model and `CommentGrouper`. Feature-flagged in the API.

---

## 10. Risk Assessment

| Risk | Mitigation |
|------|-----------|
| Agent hallucinates guidelines | Guidelines are in context via skills — agent can quote directly rather than guess IDs. `submit_comment` can validate citations. |
| Agent misses issues the pipeline would catch | Eval suite comparison; can run both modes and union results |
| Context window too small for large APIs | Existing `SectionedDocument` chunking; monitor section size limits. Memory files increase context — monitor total token budget. |
| Copilot SDK is in public preview | Keep deterministic pipeline as fallback; abstract behind interface |
| Latency higher than parallel pipeline | Agent loop may need fewer total LLM calls; measure empirically |
| Cost per review changes | Track token usage in telemetry; compare with existing pipeline |
| New memories have latency before becoming active | PR-based flow means memories aren't active until merged and synced. Acceptable at current creation rate (few per week). Revisit if volume increases. |
| Memory quality without real-time search | Memories in context (not searched on-demand) means every memory for a service is loaded, including potentially stale or low-relevance ones. Periodic distillation and curation mitigates this. |

---

## 11. File Structure (New/Modified)

In `azure-sdk-tools/eng/common/skills/` (synced to all language repos):

```
eng/common/skills/
├── api-review/
│   └── SKILL.md                 # NEW: Core review methodology
├── diff-review/
│   └── SKILL.md                 # NEW: Diff-mode instructions
├── general/
│   └── SKILL.md                 # NEW: Cross-language design guidelines
├── python/
│   ├── SKILL.md                 # NEW: Python guidelines (curated from
│   │                            #       azure-sdk/docs/python/design.md)
│   └── services/
│       └── keyvault/
│           ├── SKILL.md         # NEW: Key Vault + Python patterns
│           │                    #       (distilled from AVC memories)
│           └── memories/        # NEW: Individual memory files (accumulated
│               ├── 2026-03-15-vault-url-convention.md  #  from feedback)
│               └── ...          # Periodically rolled into SKILL.md
├── java/
│   ├── SKILL.md                 # NEW: Java guidelines
│   └── services/
│       └── .../
├── (one per language...)
├── services/
│   ├── keyvault/
│   │   ├── SKILL.md             # NEW: Cross-language Key Vault patterns
│   │   └── memories/            # NEW: Cross-language Key Vault memories
│   └── .../
```

In `azure-sdk-tools/.../apiview-copilot`:

```
src/
├── _agentic_reviewer.py        # NEW: AgenticApiViewReview class
├── _agentic_tools.py            # NEW: @define_tool functions (existing comments,
│                                #       outline, submit_comment)
├── _skill_loader.py             # NEW: Resolves skill directories for a given
│                                #       language + service, incl. memory files
├── _apiview_reviewer.py         # UNCHANGED (existing pipeline)
├── _models.py                   # UNCHANGED (shared ReviewResult, Comment)
├── _search_manager.py           # DEPRECATED: all search methods removed once
│                                #             agentic mode is validated
├── _github_manager.py           # MODIFIED: new method to create memory file PRs
scripts/
├── sync_skills.py               # NEW: Curates SKILL.md files from azure-sdk
│                                #       guideline markdown
├── distill_memories.py          # NEW: One-time migration of Cosmos DB memories
│                                #       to text files in the skill hierarchy
```

---

## 12. Success Criteria

1. **Comment quality metrics improved relative to current pipeline** — measured via production `comment_quality` buckets (`upvoted`, `downvoted`, `deleted`, `implicit_good`, `implicit_bad`). Upvoted and implicit-good rates should increase; downvoted, deleted, and implicit-bad rates should decrease. These metrics already capture both overall quality and false-positive rate. Baseline captured from the deterministic pipeline before agentic mode goes live.
2. **Wall-clock time lower than current pipeline** — the current pipeline makes 9+ sequential LLM calls per section. A single-session agent should need fewer round trips by combining generation, filtering, and scoring in one pass.
3. **Cosmos DB `guidelines`, `examples`, and `memories` containers eliminated** — all review knowledge served entirely via skills
4. **All three Azure AI Search indexers eliminated** — no RAG dependency for review-time knowledge retrieval
5. **Feedback flows produce text files** — thread resolution and mention workflows create memory markdown files via PRs, not database records
6. **Skills usable outside AVC** — a developer working in any Azure SDK language repo can load `eng/common/skills/python/` in VS Code Agent Mode and get useful review guidance without the AVC endpoint

---

## 13. Open Questions

1. **Context window limits** — Skills (language guidelines, filter exceptions, memory files) are loaded into context alongside each section's API text. Need to measure combined token usage (skills + memories + section) to ensure it fits within BYOK model limits. Large guideline skills may need to be trimmed or split. Memory files add to context size — services with many accumulated memories may exceed budget.
2. **Copilot CLI bundling** — The SDK bundles the Copilot CLI; is this acceptable for server deployment on Azure App Service?
3. **Rate limiting** — How does BYOK mode interact with Azure AI Foundry throttling? The tool-call loop makes multiple model calls per session.
4. **Streaming to APIView** — Can we stream comments to the APIView UI as the agent finds them (via `submit_comment` tool events)?
5. **Cost model** — BYOK uses our own Azure tokens. Need to compare total token usage vs. the fixed 9-stage pipeline.
6. **Guideline markdown size** — Some language design docs (e.g., `python_design.md`) are ~4000 lines. Do we extract subsections, or can modern context windows handle the full text? Measure token counts per language.
7. **Service detection** — How do we determine which service an API belongs to for service-specific skill loading? Parse the package name? Accept as input parameter? Both?
8. **Skill sync mechanism** — Skills live in `eng/common/skills/` in `azure-sdk-tools` and get synced automatically to language repos. But AVC deployment on App Service may need skills packaged differently (the deployed app won't have the full `azure-sdk-tools` repo). How do we get skills into the deploy artifact? Copy step in CI? Relative path from the apiview-copilot package root to `eng/common/`?
9. **Memory file creation permissions** — The feedback flows (thread resolution, mention) need to create PRs against `azure-sdk-tools`. Does the AVC service identity have write access to create branches and PRs? Or does this flow through a separate GitHub App / bot account?
10. **Memory distillation cadence** — When individual memory files accumulate under a service's `memories/` directory, they need periodic consolidation into the parent `SKILL.md`. What triggers this — a threshold count, a scheduled job, a manual review? Who reviews the consolidated output?
11. **Memory migration completeness** — The one-time migration from Cosmos DB to text files needs to handle: memories with no service scope (apply broadly), memories that contradict each other, memories linked to specific guideline IDs that may change format. How do we validate migration quality?
12. **Evaluation framework** — The existing eval suite (`evals/`) tests individual `.prompty` files with recorded inputs/outputs. The agentic mode replaces per-stage prompts with a single agent session, making these evals inapplicable. Need to work with the AI eval team (Juan) to define an appropriate evaluation methodology — likely end-to-end golden-set comparisons (same API input → compare review output) rather than per-prompt unit tests.
