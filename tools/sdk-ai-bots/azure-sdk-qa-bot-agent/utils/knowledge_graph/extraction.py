"""Resolve a GraphRAG ``context_records`` payload back to source documents.

The LocalSearch mixed context builder returns a flat
``{table_name: DataFrame}`` dict. This module walks that payload, maps the
cited text-unit short IDs back to their original source documents, and
returns them as :class:`~models.knowledge.Reference` objects — the same
shape the KB tool produces, so KB and graph hits merge uniformly.
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
    expand_communities: bool = False,
    max_expansion_units: int = 40,
    two_hop: bool = False,
    max_two_hop_units: int = 40,
) -> list[Reference]:
    """Resolve cited text-unit short IDs back to source-document references.

    Returns one :class:`Reference` per unique cited document (the largest
    text-unit chunk wins as the representative excerpt). Returns an empty
    list when the required parquets are missing or nothing matched.

    When ``expand_communities`` is set (optimization #2), the directly-cited
    text units are augmented with additional units drawn from the **community
    membership** of the cited entities: the query's matched entities → the
    communities they belong to → those communities' other member text units.
    This surfaces topically-associated source documents from the same
    knowledge cluster that the token-limited local context did not select,
    bounded by ``max_expansion_units``.
    """
    text_units_df = dfs.get("text_units")
    documents_df = dfs.get("documents")
    if text_units_df is None or documents_df is None:
        return []

    short_ids = _collect_text_unit_short_ids(context_records)
    if not short_ids:
        return []

    normalised_ids: set[int] = set()
    for sid in short_ids:
        try:
            normalised_ids.add(int(sid))
        except (TypeError, ValueError):
            continue
    if not normalised_ids:
        return []

    base_mask = text_units_df["human_readable_id"].isin(list(normalised_ids))
    base_units = text_units_df[base_mask]
    if base_units.empty:
        return []

    extra_ids: set[str] = set()
    if expand_communities:
        extra_ids |= _community_expansion_unit_ids(dfs, base_units, max_expansion_units)
    if two_hop:
        extra_ids |= _two_hop_expansion_unit_ids(dfs, base_units, max_two_hop_units)

    matched_units = base_units
    if extra_ids and "id" in text_units_df.columns:
        expand_mask = text_units_df["id"].astype(str).isin(extra_ids)
        matched_units = text_units_df[base_mask | expand_mask]
        logger.info(
            "GraphRAG ref expansion: +%d associated text units "
            "(base=%d, total=%d, community=%s, two_hop=%s)",
            max(0, len(matched_units) - int(base_mask.sum())),
            int(base_mask.sum()),
            len(matched_units),
            expand_communities,
            two_hop,
        )

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
        return []

    # Group text units by document so each citation carries a single
    # representative chunk excerpt (largest chunk wins).
    sorted_units = matched_units.assign(
        _len=[len(str(v)) for v in matched_units["text"]]
    ).sort_values("_len", ascending=False)

    references: list[Reference] = []
    seen_docs: set[str] = set()
    unattributed = 0
    for _, row in sorted_units.iterrows():
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

        references.append(
            Reference(
                title=ref_title,
                source=source_name,
                link=link,
                content=str(row.get("text") or ""),
            )
        )

    if unattributed:
        logger.warning(
            "GraphRAG context referenced %d document(s) with no "
            "raw_data.source_folder — they were attributed to "
            "source='graphrag'. Rebuild the snapshot to fix.",
            unattributed,
        )

    return references


def _flatten_ids(value: Any) -> list[str]:
    """Coerce a cell holding id(s) into a flat list of string ids.

    GraphRAG stores list-valued id columns (``entity_ids``, ``community_ids``,
    ``text_unit_ids``) as python lists / numpy arrays, but older snapshots may
    store a single scalar. This normalises both.
    """
    if value is None:
        return []
    if isinstance(value, (list, tuple, set)):
        return [str(v) for v in value if v is not None and str(v) != ""]
    try:
        import numpy as np

        if isinstance(value, np.ndarray):
            return [str(v) for v in value.tolist() if v is not None and str(v) != ""]
    except Exception:  # noqa: BLE001
        pass
    text = str(value).strip()
    return [text] if text else []


def _community_expansion_unit_ids(
    dfs: "dict[str, Any]", matched_units: Any, cap: int
) -> set[str]:
    """Text-unit ids from the communities of the cited entities (optimization #2).

    Chain: cited text units → their ``entity_ids`` → those entities'
    ``community_ids`` → the communities' ``text_unit_ids``. All hops use stable
    parquet columns and are guarded — any missing column disables expansion
    (returns an empty set) rather than raising. The result is capped at ``cap``
    to keep the reference list bounded.
    """
    entities_df = dfs.get("entities")
    communities_df = dfs.get("communities")
    if entities_df is None or communities_df is None:
        return set()
    if "entity_ids" not in matched_units.columns:
        return set()
    if "id" not in entities_df.columns or "community_ids" not in entities_df.columns:
        return set()
    if "id" not in communities_df.columns or "text_unit_ids" not in communities_df.columns:
        return set()

    try:
        # 1) entities appearing in the cited text units
        entity_ids: set[str] = set()
        for cell in matched_units["entity_ids"]:
            entity_ids.update(_flatten_ids(cell))
        if not entity_ids:
            return set()

        # 2) the communities those entities belong to
        ent_rows = entities_df[entities_df["id"].astype(str).isin(entity_ids)]
        community_ids: set[str] = set()
        for cell in ent_rows["community_ids"]:
            community_ids.update(_flatten_ids(cell))
        if not community_ids:
            return set()

        # 3) the member text units of those communities (bounded)
        comm_rows = communities_df[communities_df["id"].astype(str).isin(community_ids)]
        unit_ids: set[str] = set()
        for cell in comm_rows["text_unit_ids"]:
            unit_ids.update(_flatten_ids(cell))
            if len(unit_ids) >= cap:
                break
        return set(list(unit_ids)[:cap])
    except Exception:  # noqa: BLE001
        logger.warning(
            "GraphRAG community expansion failed; falling back to base refs.",
            exc_info=True,
        )
        return set()


def _two_hop_expansion_unit_ids(
    dfs: "dict[str, Any]",
    matched_units: Any,
    cap: int,
) -> set[str]:
    """Text-unit ids reachable within two relationship hops (optimization #3).

    Chain: cited text units → their ``entity_ids`` (hop-0 entities) → the
    ``relationships`` incident to those entities give hop-1 neighbor entities
    (and the edges' own ``text_unit_ids``) → the relationships incident to the
    hop-1 neighbors give hop-2 edges' ``text_unit_ids``. Entities are matched by
    both ``id`` and ``title`` because relationship ``source``/``target`` columns
    reference entity titles. All hops are guarded and the result is capped at
    ``cap`` so the reference list stays bounded.
    """
    entities_df = dfs.get("entities")
    relationships_df = dfs.get("relationships")
    if entities_df is None or relationships_df is None:
        return set()
    if "entity_ids" not in matched_units.columns:
        return set()
    rel_cols = relationships_df.columns
    if "source" not in rel_cols or "target" not in rel_cols:
        return set()
    if "text_unit_ids" not in rel_cols:
        return set()

    try:
        # hop-0: entities appearing in the cited text units.
        entity_ids: set[str] = set()
        for cell in matched_units["entity_ids"]:
            entity_ids.update(_flatten_ids(cell))
        if not entity_ids:
            return set()

        # Build the set of matchable names (ids + titles) for hop-0 entities.
        def _names_for(ids: set[str]) -> set[str]:
            names = set(ids)
            if "id" in entities_df.columns and "title" in entities_df.columns:
                rows = entities_df[entities_df["id"].astype(str).isin(ids)]
                names.update(rows["title"].astype(str).tolist())
            return names

        def _edge_hop(names: set[str]) -> tuple[set[str], set[str]]:
            """Return (edge text_unit_ids, neighbor endpoint names)."""
            src = relationships_df["source"].astype(str)
            tgt = relationships_df["target"].astype(str)
            mask = src.isin(names) | tgt.isin(names)
            edges = relationships_df[mask]
            unit_ids: set[str] = set()
            for cell in edges["text_unit_ids"]:
                unit_ids.update(_flatten_ids(cell))
            neighbors = set(edges["source"].astype(str).tolist())
            neighbors |= set(edges["target"].astype(str).tolist())
            neighbors -= names
            return unit_ids, neighbors

        names0 = _names_for(entity_ids)
        collected: set[str] = set()

        # hop-1 edges.
        hop1_units, hop1_neighbors = _edge_hop(names0)
        collected |= hop1_units
        if len(collected) < cap and hop1_neighbors:
            # hop-2 edges from the hop-1 neighbor entities.
            hop2_units, _ = _edge_hop(hop1_neighbors)
            collected |= hop2_units

        return set(list(collected)[:cap])
    except Exception:  # noqa: BLE001
        logger.warning(
            "GraphRAG two-hop expansion failed; falling back to base refs.",
            exc_info=True,
        )
        return set()
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


def _collect_text_unit_short_ids(context_records: Any) -> set[str]:
    """Recursively collect every text-unit short id in a context payload.

    The LocalSearch mixed context builder returns a flat
    ``{table_name: DataFrame}`` dict whose exact shape varies across
    graphrag versions, so we treat any DataFrame carrying both ``id`` and
    ``text`` columns as a candidate "sources" table.
    """
    import pandas as pd

    found: set[str] = set()

    def visit(node: Any) -> None:
        if node is None:
            return
        if isinstance(node, pd.DataFrame):
            if {"id", "text"}.issubset(node.columns):
                for value in node["id"].astype(str).tolist():
                    if value:
                        found.add(value)
            return
        if isinstance(node, dict):
            for v in node.values():
                visit(v)
            return
        if isinstance(node, (list, tuple, set)):
            for v in node:
                visit(v)

    visit(context_records)
    return found


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
