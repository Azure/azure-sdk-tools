"""Critical-path tests for GraphRAG reference-title derivation."""

from __future__ import annotations

import sys
from pathlib import Path

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from utils.knowledge_graph import (  # noqa: E402
    _cosine_similarity,
    _extract_chunk_header_path,
    _semantic_rerank,
    _source_path_to_rel_title,
)
from models.knowledge import Reference  # noqa: E402


def test_nested_header_path():
    text = (
        "# API Versioning\n"
        "intro\n"
        "## Adding a Stable Version\n"
        "details\n"
        "### Step 1\n"
        "do this\n"
    )
    assert (
        _extract_chunk_header_path(text)
        == "API Versioning | Adding a Stable Version | Step 1"
    )


def test_source_path_to_rel_title_strips_folder():
    assert (
        _source_path_to_rel_title("typespec_docs/sub#file.md", "typespec_docs")
        == "sub#file.md"
    )


class _FakeEmbedder:
    """Returns a canned embedding per input text (query + passages).

    ``embedding(input=[...])`` mirrors the real ``LLMEmbedding`` contract:
    a response object exposing ``.embeddings`` as a list aligned with inputs.
    """

    def __init__(self, vectors: "dict[str, list[float]]"):
        self._vectors = vectors

    def embedding(self, *, input):  # noqa: A002 - mirror real kwarg name
        vecs = [self._vectors[t] for t in input]
        return type("_Resp", (), {"embeddings": vecs})()


def _ref(title: str, content: str, score: float) -> Reference:
    return Reference(
        title=title, source="graphrag", link=title, content=content, score=score
    )


def test_semantic_rerank_lifts_query_relevant_ref():
    # Connectivity order puts the on-topic ref LAST; rerank must lift it first.
    query = "how to approve the release stage"
    refs = [
        _ref("a", "unrelated connectivity-ranked chunk", 1.0),
        _ref("b", "another off-topic chunk", 0.66),
        _ref("c", "approve the release stage after the build completes", 0.33),
    ]
    vectors = {
        query: [1.0, 0.0],
        "unrelated connectivity-ranked chunk": [0.0, 1.0],
        "another off-topic chunk": [0.1, 1.0],
        "approve the release stage after the build completes": [1.0, 0.05],
    }
    out = _semantic_rerank(refs, query, _FakeEmbedder(vectors))
    assert out[0].title == "c"  # most query-similar rises to the top
    assert out[0].score == 1.0  # normalised descending score re-stamped
    assert out[-1].score < out[0].score


def test_semantic_rerank_falls_back_on_embedding_error():
    class _Boom:
        def embedding(self, *, input):  # noqa: A002
            raise RuntimeError("embedding endpoint down")

    refs = [_ref("a", "x", 1.0), _ref("b", "y", 0.5)]
    out = _semantic_rerank(refs, "q", _Boom())
    assert [r.title for r in out] == ["a", "b"]  # original order preserved


def test_cosine_similarity_orthogonal_and_parallel():
    assert _cosine_similarity([1.0, 0.0], [1.0, 0.0]) == 1.0
    assert _cosine_similarity([1.0, 0.0], [0.0, 1.0]) == 0.0
    assert _cosine_similarity([0.0, 0.0], [1.0, 1.0]) == 0.0
