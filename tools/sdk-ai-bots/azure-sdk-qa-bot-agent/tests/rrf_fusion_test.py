"""Offline unit tests for RRF hybrid fusion (no Azure backend required)."""

from __future__ import annotations

import asyncio
from dataclasses import dataclass, field

from models.knowledge import KnowledgeChunk
from utils.azure_ai_search import SearchClient, fuse_with_rrf


def _chunk(cid: str) -> KnowledgeChunk:
    return KnowledgeChunk(chunk_id=cid, title="t")


def test_rrf_boosts_chunks_ranked_by_both_retrievers():
    # vector: A,B,C  |  keyword: C,A,D
    vec = [_chunk("A"), _chunk("B"), _chunk("C")]
    kw = [_chunk("C"), _chunk("A"), _chunk("D")]
    fused = fuse_with_rrf([vec, kw])

    order = [c.chunk_id for c in fused]
    # A and C appear in both lists → they rank above single-list B and D.
    assert set(order[:2]) == {"A", "C"}
    assert set(order[2:]) == {"B", "D"}
    # Scores are the RRF sums, descending.
    scores = [c.rerank_score for c in fused]
    assert scores == sorted(scores, reverse=True)


def test_rrf_dedupes_by_chunk_id_keeping_first_metadata():
    vec = [_chunk("A"), _chunk("A")]  # duplicate id
    fused = fuse_with_rrf([vec])
    assert [c.chunk_id for c in fused] == ["A"]


def test_rrf_single_list_preserves_order():
    vec = [_chunk("A"), _chunk("B"), _chunk("C")]
    fused = fuse_with_rrf([vec])
    assert [c.chunk_id for c in fused] == ["A", "B", "C"]


def test_rrf_falls_back_to_header_key_without_chunk_id():
    a = KnowledgeChunk(chunk_id="", title="Doc", header1="H1")
    b = KnowledgeChunk(chunk_id="", title="Doc", header1="H2")
    fused = fuse_with_rrf([[a, b]])
    # Distinct header paths are treated as distinct chunks (not collapsed).
    assert len(fused) == 2


@dataclass
class _FakeSearchClient:
    """Records retriever calls; stands in for the Azure-backed SearchClient."""

    calls: list[tuple[str, str, str | None]] = field(default_factory=list)
    failing: set[str] = field(default_factory=set)
    candidate_top_k: int = 20

    async def _run(self, kind, query, source_filters, extra_filter):
        self.calls.append((kind, query, extra_filter))
        if kind in self.failing:
            raise RuntimeError(f"{kind} is down")
        return [_chunk(f"{kind}-{query}-1"), _chunk(f"{kind}-{query}-2")]

    async def agentic_search(self, query, source_filters, extra_filter=None):
        return await self._run("agentic", query, source_filters, extra_filter)

    async def vector_search(self, query, source_filters, top_k=None, extra_filter=None):
        return await self._run("vector", query, source_filters, extra_filter)

    async def keyword_search(self, query, source_filters, top_k=None, extra_filter=None):
        return await self._run("keyword", query, source_filters, extra_filter)

    def fused_search(self, *args, **kwargs):
        return SearchClient.fused_search(self, *args, **kwargs)  # type: ignore[arg-type]


def test_fused_search_runs_agentic_only_in_deep_mode():
    client = _FakeSearchClient()
    asyncio.run(client.fused_search(["q"], {"s": "context_id eq 's'"}))
    assert sorted({c[0] for c in client.calls}) == ["keyword", "vector"]

    client = _FakeSearchClient()
    asyncio.run(client.fused_search(["q"], {"s": "f"}, use_agentic=True))
    assert sorted({c[0] for c in client.calls}) == ["agentic", "keyword", "vector"]


def test_fused_search_applies_extra_filter_and_caps_queries():
    client = _FakeSearchClient()
    asyncio.run(
        client.fused_search(["a", "b", "c", "d"], {"s": "f"}, extra_filter="WIKI")
    )
    assert sorted({c[1] for c in client.calls}) == ["a", "b", "c"]
    assert {c[2] for c in client.calls} == {"WIKI"}


def test_fused_search_survives_a_failing_retriever():
    client = _FakeSearchClient(failing={"vector"})
    fused = asyncio.run(client.fused_search(["q"], {"s": "f"}))
    # Keyword results still come back, ordered by that retriever alone.
    assert [c.chunk_id for c in fused] == ["keyword-q-1", "keyword-q-2"]


class _OverlapSearchClient(_FakeSearchClient):
    async def vector_search(self, query, source_filters, top_k=None, extra_filter=None):
        return [_chunk("shared"), _chunk(f"vector-{query}")]

    async def keyword_search(self, query, source_filters, top_k=None, extra_filter=None):
        return [_chunk(f"keyword-{query}"), _chunk("shared")]


def test_fused_search_rewards_hits_retrieved_across_queries():
    client = _OverlapSearchClient()
    fused = asyncio.run(client.fused_search(["concrete", "abstract"], {"s": "f"}))
    assert fused[0].chunk_id == "shared"
