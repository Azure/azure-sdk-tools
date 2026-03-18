"""Data models for knowledge retrieval and search."""

from __future__ import annotations

from pydantic import BaseModel


class KnowledgeChunk(BaseModel):
    """A single chunk returned from Azure AI Search."""

    chunk_id: str
    title: str
    content: str
    source: str
    link: str = ""
    header1: str = ""
    header2: str = ""
    header3: str = ""
    rerank_score: float = 0.0


class KnowledgeResult(BaseModel):
    """Processed knowledge item ready for prompt injection."""

    title: str
    source: str
    link: str
    content: str
