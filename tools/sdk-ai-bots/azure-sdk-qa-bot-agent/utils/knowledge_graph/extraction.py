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
    dfs: "dict[str, Any]", context_records: Any
) -> list[Reference]:
    """Resolve cited text-unit short IDs back to source-document references.

    Returns one :class:`Reference` per unique cited document (the largest
    text-unit chunk wins as the representative excerpt). Returns an empty
    list when the required parquets are missing or nothing matched.
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

    matched_units = text_units_df[
        text_units_df["human_readable_id"].isin(list(normalised_ids))
    ]
    if matched_units.empty:
        return []

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
