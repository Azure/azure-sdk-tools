"""Offline unit tests for RRF hybrid fusion (no Azure backend required)."""

from __future__ import annotations

import sys
from pathlib import Path

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from models.knowledge import KnowledgeChunk
from utils.azure_ai_search import fuse_with_rrf


def _chunk(cid: str) -> KnowledgeChunk:
    return KnowledgeChunk(chunk_id=cid, title="t")


def test_rrf_boosts_chunks_ranked_by_both_retrievers():
    # vector: A,B,C  |  keyword: C,A,D
    vec = [_chunk("A"), _chunk("B"), _chunk("C")]
    kw = [_chunk("C"), _chunk("A"), _chunk("D")]
    fused = fuse_with_rrf([(vec, 1.0), (kw, 1.0)], k=60)

    order = [c.chunk_id for c in fused]
    # A and C appear in both lists → they rank above single-list B and D.
    assert set(order[:2]) == {"A", "C"}
    assert set(order[2:]) == {"B", "D"}
    # Scores are the RRF sums, descending.
    scores = [c.rerank_score for c in fused]
    assert scores == sorted(scores, reverse=True)


def test_rrf_dedupes_by_chunk_id_keeping_first_metadata():
    vec = [_chunk("A"), _chunk("A")]  # duplicate id
    fused = fuse_with_rrf([(vec, 1.0)], k=60)
    assert [c.chunk_id for c in fused] == ["A"]


def test_rrf_respects_weights():
    vec = [_chunk("A"), _chunk("B")]
    kw = [_chunk("B"), _chunk("A")]
    # Heavily weight keyword → B (keyword rank 1) should win.
    fused = fuse_with_rrf([(vec, 0.1), (kw, 10.0)], k=60)
    assert fused[0].chunk_id == "B"


def test_rrf_single_list_preserves_order():
    vec = [_chunk("A"), _chunk("B"), _chunk("C")]
    fused = fuse_with_rrf([(vec, 1.0)], k=60)
    assert [c.chunk_id for c in fused] == ["A", "B", "C"]


def test_rrf_falls_back_to_header_key_without_chunk_id():
    a = KnowledgeChunk(chunk_id="", title="Doc", header1="H1")
    b = KnowledgeChunk(chunk_id="", title="Doc", header1="H2")
    fused = fuse_with_rrf([([a, b], 1.0)], k=60)
    # Distinct header paths are treated as distinct chunks (not collapsed).
    assert len(fused) == 2
