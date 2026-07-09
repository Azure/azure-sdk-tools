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
) -> list[Reference]:
    """Resolve a graph context payload into ranked source + synthesis references.

    Two kinds of reference are returned:

    * **Source text-unit references** — one :class:`Reference` per unique cited
      document, **ranked by graph relevance and capped at ``top_k``**. GraphRAG
      orders the cited text units by relevance (most-connected first); we
      preserve that order, keep the highest-ranked text unit as each document's
      representative excerpt, stamp a normalised ``score`` (1.0 = most relevant)
      on every reference, sort by that score, and return only the top ``top_k``.
      This mirrors the KB tool's rerank-and-cap contract.
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


def extract_relationships_from_context(
    dfs: "dict[str, Any]",
    context_records: Any,
    *,
    top_n: int = 10,
) -> list[Reference]:
    """Resolve a graph context payload into **multi-hop relationship** context.

    This is the graph's *unique* contribution versus the KB: not document
    references (the KB owns all direct citations) and not community synthesis,
    but the **connections between the concepts** the query touches — how
    entities relate across the corpus, which flat KB chunks cannot express.

    We take the in-context entities the local search surfaced, look up the
    relationship edges **among those entities** in ``dfs['relationships']``,
    rank them by graph degree (most-connected first), and return the top
    ``top_n`` as compact ``"<source> → <target>: <description>"`` statements.
    Each is a :class:`Reference` with ``source="graphrag_relationship"`` and no
    ``link`` — it is relational context to reason over, never a citable source.

    Returns an empty list when the relationships parquet is missing, no
    in-context entities were found, or no edges connect them.
    """
    if top_n <= 0:
        return []

    import pandas as pd

    rel = dfs.get("relationships")
    if rel is None or getattr(rel, "empty", True):
        return []
    if not {"source", "target", "description"}.issubset(rel.columns):
        return []

    entity_names: set[str] = set()

    def visit(node: Any) -> None:
        if isinstance(node, pd.DataFrame):
            if "entity" in node.columns:
                for value in node["entity"].astype(str).tolist():
                    if value:
                        entity_names.add(value.strip().upper())
            return
        if isinstance(node, dict):
            for v in node.values():
                visit(v)
        elif isinstance(node, (list, tuple, set)):
            for v in node:
                visit(v)

    visit(context_records)
    if not entity_names:
        return []

    src_up = rel["source"].astype(str).str.upper()
    tgt_up = rel["target"].astype(str).str.upper()
    edges = rel[src_up.isin(entity_names) & tgt_up.isin(entity_names)]
    if edges.empty:
        return []

    rank_col = "combined_degree" if "combined_degree" in edges.columns else "weight"
    if rank_col in edges.columns:
        edges = edges.sort_values(rank_col, ascending=False)

    refs: list[Reference] = []
    n = min(top_n, len(edges))
    denom = float(max(n, 1))
    for i in range(n):
        row = edges.iloc[i]
        src = str(row.get("source") or "").strip()
        tgt = str(row.get("target") or "").strip()
        desc = str(row.get("description") or "").strip()
        if not (src and tgt):
            continue
        content = f"{src} \u2192 {tgt}: {desc}" if desc else f"{src} \u2192 {tgt}"
        refs.append(
            Reference(
                title=f"{src} \u2192 {tgt}",
                source="graphrag_relationship",
                link="",
                content=content,
                score=max(0.0, (n - i) / denom),
            )
        )
    if refs:
        logger.info(
            "GraphRAG surfaced %d relationship edge(s) as relational context",
            len(refs),
        )
    return refs


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
