"""Offline unit tests for MMR diversity selection."""

from __future__ import annotations

from models.knowledge import KnowledgeChunk
from utils.azure_ai_search import _jaccard, _token_set, mmr_select


def _mk(cid: str, title: str, content: str, score: float) -> KnowledgeChunk:
    c = KnowledgeChunk(chunk_id=cid, title=title)
    c.content = content
    c.rerank_score = score
    return c


def _pool():
    return [
        _mk("A", "A", "versioning added removed decorator", 5.0),
        _mk("A2", "A2", "versioning added removed decorator", 4.9),  # near-dup of A
        _mk("A3", "A3", "versioning added removed decorator", 4.8),  # near-dup
        _mk("B", "B", "pagination nextlink pageable list", 4.0),
        _mk("C", "C", "longrunning operation status monitor", 3.5),
    ]


def test_jaccard_basic():
    a = _token_set(_mk("x", "", "added removed", 1))
    b = _token_set(_mk("y", "", "added removed", 1))
    assert _jaccard(a, b) == 1.0
    c = _token_set(_mk("z", "", "pagination list", 1))
    assert _jaccard(a, c) == 0.0


def test_top_pick_is_most_relevant():
    sel = mmr_select(_pool(), 3, lambda_=0.7)
    assert sel[0].chunk_id == "A"


def test_low_lambda_diversifies_over_near_dups():
    # diversity-weighted: after A, the diverse B/C beat the near-dup A2/A3
    sel = mmr_select(_pool(), 3, lambda_=0.3)
    ids = {c.chunk_id for c in sel}
    assert "A" in ids
    assert "B" in ids and "C" in ids
    assert "A2" not in ids and "A3" not in ids


def test_high_lambda_keeps_relevance_order():
    # relevance-weighted: near-dups retained
    sel = mmr_select(_pool(), 3, lambda_=0.9)
    assert [c.chunk_id for c in sel] == ["A", "A2", "A3"]


def test_returns_all_when_pool_small():
    pool = _pool()[:2]
    assert mmr_select(pool, 5, lambda_=0.7) == pool
