"""Resolve a GraphRAG ``context_records`` payload back to source documents.

The LocalSearch mixed context builder returns a flat
``{table_name: DataFrame}`` dict. This module walks that payload, maps the
cited text-unit short IDs back to their original source documents, and
returns them as :class:`~models.knowledge.Reference` objects — the same
shape the KB tool produces, so KB and graph hits merge uniformly.

References are returned **ranked by graph relevance and capped at a top-K**,
each carrying a normalised ``score`` — mirroring the KB tool's
rerank-and-cap contract so the two reference sets the chat agent fuses share
ordering semantics and a comparable size.
"""

from __future__ import annotations

import logging
import math
import re
from typing import Any

from config.tenant_config import get_knowledge_source
from models.knowledge import Reference

logger = logging.getLogger(__name__)

# How many headers deep we keep, mirroring the KB tool's
# ``header1 | header2 | header3`` (3 levels).
_MAX_HEADER_DEPTH = 3
# Matches an ATX markdown header line (``#`` .. ``######`` followed by a
# space and the heading text). Trailing ``#`` characters (closed ATX
# headers) are stripped.
_HEADER_RE = re.compile(r"^(#{1,6})\s+(.*?)\s*#*\s*$")
# Fenced code-block delimiters (``` or ~~~). ``#`` lines inside a fence are
# comments / shell prompts, never markdown headers, so they are skipped.
_FENCE_RE = re.compile(r"^\s*(`{3,}|~{3,})")


def extract_references_from_context(
    dfs: "dict[str, Any]",
    context_records: Any,
    *,
    top_k: int = 8,
    community_top_n: int = 2,
    query: "str | None" = None,
    embedder: Any = None,
) -> list[Reference]:
    """Resolve a graph context payload into ranked source + synthesis references.

    Two kinds of reference are returned:

    * **Source text-unit references** — one :class:`Reference` per unique cited
      document, **ranked and capped at ``top_k``**. GraphRAG orders the cited
      text units by *entity-graph connectivity* (most-connected first), which is
      not the same as query relevance — so the passage that actually answers the
      question often sits past ``top_k`` and is dropped. When ``query`` and
      ``embedder`` are supplied we therefore **semantically rerank the full
      candidate set by query-embedding cosine similarity before capping**
      (see :func:`_semantic_rerank`), so the most query-relevant passage rises
      into the small top-K instead of being buried by connectivity order. This
      makes the graph side truly symmetric with the KB tool's semantic reranker.
      Without ``query``/``embedder`` we fall back to the connectivity order. Each
      reference carries a normalised ``score`` (1.0 = most relevant, descending).
    * **Community-report references** (step 1.A) — up to ``community_top_n``
      GraphRAG *community reports*: cross-document syntheses that summarise how
      an entity cluster relates. These are the graph's differentiator — content
      the KB (which retrieves independent chunks) cannot produce — so they are
      appended with ``source="graphrag_community"`` and no ``link`` (they are
      synthesis, not a single citable document).

    Returns an empty list when the required parquets are missing or nothing
    matched. Community reports are only present when the context builder was run
    with ``community_prop > 0`` (see engine.py).
    """
    community_refs = _extract_community_reports(context_records, community_top_n)

    text_units_df = dfs.get("text_units")
    documents_df = dfs.get("documents")
    if text_units_df is None or documents_df is None:
        return community_refs

    ordered_short_ids = _collect_text_unit_short_ids(context_records)
    if not ordered_short_ids:
        return community_refs

    # ``ordered_short_ids`` is in graph relevance order (most-connected
    # text unit first). Map each id to its 0-based rank so the resolved
    # references can be scored and sorted by relevance instead of length.
    rank_by_id: dict[int, int] = {}
    for rank, sid in enumerate(ordered_short_ids):
        try:
            key = int(sid)
        except (TypeError, ValueError):
            continue
        rank_by_id.setdefault(key, rank)
    if not rank_by_id:
        return community_refs

    base_rank_count = len(rank_by_id)
    base_mask = text_units_df["human_readable_id"].isin(list(rank_by_id.keys()))
    matched_units = text_units_df[base_mask]
    if matched_units.empty:
        return community_refs

    doc_index = documents_df.drop_duplicates(subset=["id"]).set_index("id")
    # ``raw_data`` is the column GraphRAG persists from
    # ``TextDocument.raw_data``. The sync project's
    # ``SourceAwareMarkItDownFileReader`` writes
    # ``{"source_folder": "<kb-folder>", "source_path": "<full-path>"}``
    # into every row so the bot can attribute each graph reference to a
    # concrete KnowledgeSource and resolve its link — independent of
    # ``documents.title``. A snapshot without ``raw_data`` is treated as
    # incompatible rather than silently regressing to lossy matching.
    if "raw_data" not in doc_index.columns:
        logger.error(
            "documents.parquet is missing the 'raw_data' column — this "
            "snapshot is incompatible. Republish from the sync project to fix."
        )
        return community_refs

    # Rank each matched text unit by its graph-relevance position (its index
    # in ``ordered_short_ids``). Any unit without a rank sorts last.
    def _unit_rank(row: Any) -> int:
        try:
            hrid = int(row.get("human_readable_id"))
        except (TypeError, ValueError):
            return base_rank_count
        return rank_by_id.get(hrid, base_rank_count)

    ranked_units = matched_units.assign(
        _rank=[_unit_rank(row) for _, row in matched_units.iterrows()]
    ).sort_values("_rank", ascending=True)

    # Collapse to one reference per document, keeping the highest-ranked
    # (most relevant) text unit as that document's representative excerpt.
    references: list[Reference] = []
    seen_docs: set[str] = set()
    unattributed = 0
    denom = float(max(base_rank_count, 1))
    for _, row in ranked_units.iterrows():
        doc_id = row.get("document_id")
        if not doc_id or doc_id in seen_docs:
            continue
        seen_docs.add(doc_id)

        source_name, source_path = _read_doc_attribution(doc_index, doc_id)
        if not source_name:
            unattributed += 1
            source_name = "graphrag"
            knowledge_source = None
        else:
            knowledge_source = get_knowledge_source(source_name)

        # Derive the source-folder-relative, ``#``-encoded path straight
        # from ``raw_data.source_path`` (independent of documents.title).
        rel_title = _source_path_to_rel_title(source_path, source_name)
        display_path = rel_title.replace("#", "/")
        # Prefer the KnowledgeSource's URL resolver so the link matches the
        # KB tool's references (trim_format, suffix, custom link_fn). Fall
        # back to the plain path when the source folder is unregistered.
        if knowledge_source is not None and rel_title:
            link = knowledge_source.get_link(rel_title)
        else:
            link = display_path

        # Prefer a section-level ``h1 | h2 | h3`` heading path parsed from
        # the cited chunk (matching the KB tool's header-based titles);
        # fall back to the document path, then a synthetic id.
        chunk_title = _extract_chunk_header_path(str(row.get("text") or ""))
        ref_title = chunk_title or display_path or f"Document {doc_id[:12]}"

        # Normalised relevance score in (0, 1]: rank 0 -> 1.0, descending.
        rank = int(row.get("_rank", base_rank_count))
        score = max(0.0, (denom - rank) / denom)

        references.append(
            Reference(
                title=ref_title,
                source=source_name,
                link=link,
                content=str(row.get("text") or ""),
                score=score,
            )
        )

    if unattributed:
        logger.warning(
            "GraphRAG context referenced %d document(s) with no "
            "raw_data.source_folder — they were attributed to "
            "source='graphrag'. Rebuild the snapshot to fix.",
            unattributed,
        )

    # Semantically rerank the full candidate set by query relevance BEFORE the
    # top_k cap. GraphRAG hands us the passages in entity-connectivity order,
    # which routinely buries the query-answering passage past the cap; a
    # cosine rerank against the query embedding lifts it into the small top-K
    # (recall) without widening the returned set (precision — no agent-diluting
    # flood). Falls back to connectivity order if query/embedder are absent or
    # embedding fails.
    if query and embedder is not None and len(references) > 1:
        references = _semantic_rerank(references, query, embedder)

    # Cap at ``top_k`` so the graph contributes a focused, relevance-ranked
    # set comparable to the KB tool — not a ~20-ref flood that dilutes the
    # answer. References are already ordered best-first.
    if top_k > 0 and len(references) > top_k:
        logger.info(
            "GraphRAG references capped %d -> %d (top_k)", len(references), top_k
        )
        references = references[:top_k]

    # Append the community-report synthesis (step 1.A) after the doc-grounded
    # text-unit references, so specific source excerpts lead and the
    # cross-document synthesis supplements.
    return references + community_refs


# Cap on how much of each passage is embedded for reranking. The answering
# sentence is almost always near the top of a chunk, and embedding models
# truncate long inputs anyway, so this bounds latency/cost without hurting the
# similarity signal.
_RERANK_CHARS = 3000


def _semantic_rerank(
    references: list[Reference], query: str, embedder: Any
) -> list[Reference]:
    """Reorder candidate references by cosine similarity to the query.

    GraphRAG orders passages by entity-graph connectivity, not query relevance,
    so the passage that actually answers the question is frequently buried past
    the ``top_k`` cap. We embed the query and every candidate passage in a
    **single** batched call (``embedding(input=[query, *passages])``), rank the
    passages by cosine similarity to the query, and re-stamp a normalised
    descending ``score`` so the downstream contract (1.0 = most relevant) is
    preserved. On any failure the original connectivity order is returned
    unchanged — reranking is a best-effort refinement, never a hard dependency.
    """
    if len(references) <= 1:
        return references
    texts = [query] + [
        (ref.content or ref.title or "")[:_RERANK_CHARS] for ref in references
    ]
    try:
        vectors = embedder.embedding(input=texts).embeddings
    except Exception:
        logger.warning(
            "GraphRAG semantic rerank failed to embed; keeping connectivity order",
            exc_info=True,
        )
        return references
    if not vectors or len(vectors) != len(texts):
        logger.warning(
            "GraphRAG semantic rerank got %d embeddings for %d inputs; "
            "keeping connectivity order",
            len(vectors) if vectors else 0,
            len(texts),
        )
        return references

    query_vec = vectors[0]
    scored = [
        (_cosine_similarity(query_vec, vec), ref)
        for ref, vec in zip(references, vectors[1:])
    ]
    scored.sort(key=lambda pair: pair[0], reverse=True)

    denom = float(len(scored))
    reranked: list[Reference] = []
    for rank, (_, ref) in enumerate(scored):
        ref.score = max(0.0, (denom - rank) / denom)
        reranked.append(ref)
    logger.info(
        "GraphRAG semantically reranked %d references by query relevance",
        len(reranked),
    )
    return reranked


def _cosine_similarity(a: "list[float]", b: "list[float]") -> float:
    """Cosine similarity between two equal-length embedding vectors."""
    dot = 0.0
    norm_a = 0.0
    norm_b = 0.0
    for x, y in zip(a, b):
        dot += x * y
        norm_a += x * x
        norm_b += y * y
    if norm_a <= 0.0 or norm_b <= 0.0:
        return 0.0
    return dot / math.sqrt(norm_a * norm_b)


def _extract_community_reports(
    context_records: Any, top_n: int
) -> list[Reference]:
    """Lift GraphRAG community reports out of the context payload as references.

    Community reports are cross-document syntheses (``id, title, content``) that
    the LocalSearch context builder includes when run with ``community_prop > 0``.
    They are the graph's differentiator versus the KB's independent-chunk recall,
    so we surface the top ``top_n`` (the builder emits them relevance-ordered) as
    :class:`Reference` objects with ``source="graphrag_community"`` and no
    ``link`` — they are synthesis, not a single citable document. Returns an
    empty list when ``top_n <= 0`` or no reports table is present.
    """
    if top_n <= 0:
        return []

    import pandas as pd

    reports_df = None

    def visit(node: Any) -> None:
        nonlocal reports_df
        if reports_df is not None or node is None:
            return
        if isinstance(node, pd.DataFrame):
            if {"title", "content"}.issubset(node.columns) and "text" not in node.columns:
                reports_df = node
            return
        if isinstance(node, dict):
            for v in node.values():
                visit(v)
        elif isinstance(node, (list, tuple, set)):
            for v in node:
                visit(v)

    visit(context_records)
    if reports_df is None or reports_df.empty:
        return []

    refs: list[Reference] = []
    n = min(top_n, len(reports_df))
    for i in range(n):
        row = reports_df.iloc[i]
        content = str(row.get("content") or "")
        if not content:
            continue
        title = str(row.get("title") or "").strip() or f"Community {row.get('id')}"
        # Relevance-ordered by the builder; score descending from 1.0.
        score = max(0.0, (n - i) / n)
        refs.append(
            Reference(
                title=title,
                source="graphrag_community",
                link="",
                content=content,
                score=score,
            )
        )
    if refs:
        logger.info("GraphRAG surfaced %d community-report synthesis reference(s)", len(refs))
    return refs


def _read_doc_attribution(doc_index: Any, doc_id: str) -> tuple[str, str]:
    """Return ``(source_folder, source_path)`` for a document id."""
    if doc_id not in doc_index.index:
        return "", ""
    doc_row = doc_index.loc[doc_id]
    # ``.loc`` returns a DataFrame for duplicate ids; dedup upstream
    # guards against that but be defensive.
    if getattr(doc_row, "ndim", 1) > 1:
        doc_row = doc_row.iloc[0]
    rd = doc_row.get("raw_data")
    if isinstance(rd, dict):
        return str(rd.get("source_folder") or ""), str(rd.get("source_path") or "")
    return "", ""


def _collect_text_unit_short_ids(context_records: Any) -> list[str]:
    """Recursively collect text-unit short ids **in relevance order**.

    The LocalSearch mixed context builder returns a flat
    ``{table_name: DataFrame}`` dict whose exact shape varies across
    graphrag versions, so we treat any DataFrame carrying both ``id`` and
    ``text`` columns as a candidate "sources" table. GraphRAG orders that
    table by relevance (most-connected text units first), so we preserve
    first-seen order and de-duplicate — the position of each id is its
    relevance rank, which the caller turns into a per-reference score.
    """
    import pandas as pd

    ordered: list[str] = []
    seen: set[str] = set()

    def visit(node: Any) -> None:
        if node is None:
            return
        if isinstance(node, pd.DataFrame):
            if {"id", "text"}.issubset(node.columns):
                for value in node["id"].astype(str).tolist():
                    if value and value not in seen:
                        seen.add(value)
                        ordered.append(value)
            return
        if isinstance(node, dict):
            for v in node.values():
                visit(v)
            return
        if isinstance(node, (list, tuple, set)):
            for v in node:
                visit(v)

    visit(context_records)
    return ordered


def _source_path_to_rel_title(source_path: str, source_folder: str) -> str:
    """Return the source-folder-relative, ``#``-encoded document path.

    The sync reader stores ``raw_data.source_path`` as the full input path
    (``typespec_docs/sub#file.md``). The KB-style link/display contract
    works on the folder-relative encoded path (``sub#file.md``) because
    each ``KnowledgeSource``'s ``base_url`` already covers the folder, so
    we drop the leading ``{source_folder}/`` segment. Any remaining path
    separators are ``#``-encoded to match the KB tool's
    ``title.replace("#", "/")`` link contract.

    Returns an empty string when ``source_path`` is missing. The synthetic
    ``"graphrag"`` fallback source name is never a real prefix, so it is
    left untouched.
    """
    path = (source_path or "").strip()
    if not path:
        return ""
    if source_folder and source_folder != "graphrag":
        prefix = f"{source_folder}/"
        if path.startswith(prefix):
            path = path[len(prefix):]
    return path.replace("/", "#")


def _extract_chunk_header_path(text: str) -> str:
    """Derive a ``h1 | h2 | h3`` heading path from a text-unit chunk.

    GraphRAG splits documents into fixed-size token windows that — unlike
    the KB tool's header-aware chunks — carry no header metadata. We
    recover a section-level title by tracking the most recent heading at
    each level (1-3) in document order and joining them with `` | `` (the
    same shape as the KB tool's ``_build_reference_title``).

    Lines inside fenced code blocks are skipped so shell prompts and
    comments (``# do this``) are not mistaken for headers. Returns an
    empty string when the chunk contains no usable heading.
    """
    if not text:
        return ""

    headings: list[str | None] = [None, None, None]
    in_fence = False
    for line in text.splitlines():
        if _FENCE_RE.match(line):
            in_fence = not in_fence
            continue
        if in_fence:
            continue
        m = _HEADER_RE.match(line)
        if not m:
            continue
        depth = len(m.group(1))
        heading_text = m.group(2).strip()
        if not heading_text or depth > _MAX_HEADER_DEPTH:
            continue
        idx = depth - 1
        headings[idx] = heading_text
        for deeper in range(idx + 1, _MAX_HEADER_DEPTH):
            headings[deeper] = None

    return " | ".join(h for h in headings if h)
