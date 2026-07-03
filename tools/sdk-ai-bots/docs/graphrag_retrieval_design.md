# GraphRAG Knowledge-Graph Retrieval — Design

## 1 Background

The Azure SDK QA Bot answers developer questions by grounding a chat agent on a curated knowledge corpus (TypeSpec docs, ARM/API guidelines, SDK repo docs, samples, resolved support threads). Until now the only retrieval path was **vector / agentic search over an Azure AI Search index** (the "KB path"). Vector search excels at single-concept, definitional, verbatim-rule lookups, but it retrieves each chunk independently and has no notion of how entities relate **across** documents — so it under-serves relational, multi-hop, cross-document, and troubleshooting questions where the answer depends on connecting facts that live in different chunks.

This design adds a second, complementary retrieval path built on **Microsoft GraphRAG** (entity-graph traversal) that runs **side-by-side** with the existing KB path. The agent issues both retrievers in parallel and synthesises one answer over the merged references.

> This document describes the overall design of the GraphRAG update and how it is used, and contrasts it with the original KB architecture. It complements [`agent_framework_and_memory_design.md`](agent_framework_and_memory_design.md).

### 1.1 Goals / non-goals

- **Goal** — add graph-grounded recall for relational/cross-document questions **without regressing** the KB path or the concise (150–200 word) answer style.
- **Goal** — keep the graph reference shape **identical** to KB references so the agent and UI treat both uniformly, and tenant-scope graph retrieval with the same semantics as the KB tool.
- **Non-goal** — replacing the KB path. GraphRAG is additive; the two are weighted per question type (§4).
- **Non-goal** — running GraphRAG's own answer generation. We use only its retrieval (context-building) stage and let the chat agent compose the answer.

---

## 2 Design overview

GraphRAG is added as a **second retriever over the same corpus**, not a replacement for the KB path. Three pieces make it work:

1. **An offline graph build** turns the existing markdown corpus into a knowledge graph (entities, relationships, communities) and publishes it as an immutable, versioned snapshot in blob storage.
2. **A warm backend retrieval service** loads the current snapshot and answers graph queries over HTTP, returning source references in the same shape as KB results.
3. **The chat agent** calls the KB tool and the graph tool in parallel, then merges and weights the two reference sets before composing a single grounded answer.

The rest of this section walks through each piece; §3 covers how snapshots stay fresh and tenant-scoped, §4 covers synthesis, and §6 lists the concrete differences from the original architecture.

### 2.1 The graph build (offline)

A separate sync project reads the **same** markdown the KB path indexes and runs the GraphRAG pipeline to extract entities and relationships and cluster them into communities. Every document keeps a **source attribution** tag so each graph hit can be traced back to a concrete knowledge source and resolve a link consistent with the KB path.

Each run is a **full rebuild** that writes a new, immutable snapshot (a set of graph artifacts) under a timestamped path, plus a small manifest file that points at the current snapshot. Full rebuilds keep snapshots reproducible and make activation atomic — nothing consumes a snapshot until the manifest flips to it. The build runs on a daily schedule.

### 2.2 The retrieval service (online)

Loading a graph and building its search context is expensive, so graph retrieval lives in the **backend as a warm, long-lived service** rather than being reconstructed inside each short-lived chat-agent sandbox. The backend loads the current snapshot once and serves graph queries over an HTTP endpoint; the chat agent reaches it through a `search_knowledge_graph` tool that mirrors the KB tool's interface and fails soft (an empty result never breaks a turn).

Crucially, the service runs **only the retrieval half** of GraphRAG: it embeds the query, finds the most relevant entities, expands one hop through their relationships, and resolves back to the **source text passages** those entities came from. It does **not** call an LLM to write an answer. The passages are returned as references — the same shape as KB chunks — and the chat agent composes the final answer over them, exactly as it does for KB results. This keeps a single, consistent answering path and preserves the concise answer style.

---

## 3 Freshness and tenant scoping

**Snapshot refresh (pull-based).** The backend periodically checks the manifest and hot-swaps to a newer snapshot only when the build id changes. Reloads are atomic with rollback on failure, so a bad build never leaves the service half-loaded. The bot *pulls* new knowledge on its own schedule rather than being pushed to.

**Tenant scoping (same semantics as KB).** Every tenant is limited to its own set of knowledge sources. Graph retrieval enforces this with two layers that mirror the KB tool: it restricts results to the tenant's allowed sources, and it further honours any per-source narrowing the tenant applies (e.g. a language- or title-based filter on a shared source). The net effect is that a question scoped to a tenant sees the same slice of the corpus through the graph as it does through the KB.

---

## 4 Hybrid synthesis — how the two paths combine

Both retrievers are **mandatory on every domain question** and are issued in the **same parallel batch**, so there is no added latency turn. The agent then merges the two reference sets and **de-duplicates by link**, with the KB (primary-source) hit winning when both return the same document.

The two sources are **weighted by question type** rather than blended equally (this weighting was tuned from case-level analysis of where each path helps or hurts):

- **Definitional / decorator / language-feature questions → KB is the backbone**, graph is confirmation-only, with an anti-dilution rule: don't pull in adjacent or legacy mechanisms the user didn't ask about, and prefer the KB answer if the graph contradicts it.
- **Process / workflow / permissions / CI / release / cross-team questions → graph is the backbone**, with KB grounding exact wording and links.

The answer-length target stays **150–200 words** regardless of which path leads.

---

## 5 Evaluation

- **Evaluations run with memory disabled.** The agent can inject historical Q&A from its memory store; because the eval datasets are built from prior Q&A, leaving memory on leaks ground-truth answers and biases the comparison. All evaluation is run with memory off.
- **Latest result (223-case perf set, memory off, concise answers):** KB-only **78.0 %** vs Graph RAG **81.2 %**. Case-level analysis shows Graph's advantage is concentrated in its designed strengths (relational / troubleshooting / versioning questions), with small dilutions on definitional ones — which the question-type weighting in §4 keeps in check. Remaining failures are dominated by **corpus gaps** (short, thread-specific facts that simply aren't in the indexed corpus); the next lever is curated corpus expansion rather than further retrieval or prompt tuning.

---

## 6 Key differences from the original architecture

| Dimension | Original KB path | GraphRAG path (this update) |
|---|---|---|
| Retrieval model | Vector / semantic + agentic search over document chunks | Entity-graph traversal: query → entities → 1-hop → source passages |
| Best at | Single-concept, definitional, verbatim rules, code snippets | Relational, multi-hop, cross-document, troubleshooting |
| Corpus build | TypeScript sync, **incremental** index updates | Python sync, **full rebuild** → versioned snapshots |
| Artefact | AI Search index (chunks + vectors) | Immutable graph snapshot + manifest in blob storage |
| Refresh | Incremental per changed file | Daily full rebuild; backend **pulls** the new snapshot |
| Where retrieval runs | Inline search client | Warm backend service behind an HTTP endpoint (avoids per-sandbox cold start) |
| Answer generation | Agent composes over chunks | Agent composes over graph refs — **no GraphRAG answer-generation step** |
| Tenant scoping | Source + per-source filter on the index | Same two-layer semantics applied to the graph |
| Reference shape | KB `Reference` | **Same** `Reference` shape and linking |
| Relationship to each other | — | **Additive & complementary**; merged, de-duped, weighted by question type |

**What did *not* change:** the KB path, the AI Search index, the agent framework, the memory design, the answer style, and the reference contract the UI renders. GraphRAG is a bolt-on second retriever that reuses the same corpus, the same tenant model, and the same reference shape — the bot simply has one more, complementary way to find grounding evidence.
