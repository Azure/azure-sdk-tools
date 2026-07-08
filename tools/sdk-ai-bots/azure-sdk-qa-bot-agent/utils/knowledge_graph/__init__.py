"""Knowledge graph retrieval package (Microsoft GraphRAG).

Public surface:

* :class:`KnowledgeGraphService` / :func:`get_knowledge_graph_service` —
  the warm singleton the backend ``/graph/query`` endpoint drives.
* :func:`parse_title_filter_terms` — translate a KB ``source_filter`` OData
  clause into terms the graph file-level filter matches on source_path.

Internal helpers (``_extract_chunk_header_path``, ``_source_path_to_rel_title``)
are re-exported for unit tests.
"""

from __future__ import annotations

from utils.knowledge_graph.extraction import (
    _cosine_similarity,
    _extract_chunk_header_path,
    _semantic_rerank,
    _source_path_to_rel_title,
)
from utils.knowledge_graph.filtering import parse_title_filter_terms
from utils.knowledge_graph.service import (
    KnowledgeGraphService,
    get_knowledge_graph_service,
)

__all__ = [
    "KnowledgeGraphService",
    "get_knowledge_graph_service",
    "parse_title_filter_terms",
    "_cosine_similarity",
    "_extract_chunk_header_path",
    "_semantic_rerank",
    "_source_path_to_rel_title",
]
