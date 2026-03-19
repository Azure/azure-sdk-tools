"""Data models for knowledge retrieval and search."""

from __future__ import annotations

from dataclasses import dataclass, field
from pydantic import BaseModel


# ---------------------------------------------------------------------------
# Knowledge source definition
# ---------------------------------------------------------------------------

@dataclass(frozen=True)
class KnowledgeSource:
    """A searchable knowledge source in Azure AI Search.

    Attributes:
        name:        Unique identifier used as the index / source filter value.
        description: Human-readable description so the LLM knows *when* to
                     query this source.
        filter:      Optional OData filter expression applied when searching
                     this source (e.g. ``search.ismatch('python_*', 'title')``).
    """

    name: str
    description: str
    filter: str = ""

    def to_display_dict(self) -> dict[str, str]:
        """Return a minimal dict suitable for showing the LLM."""
        return {"name": self.name, "description": self.description}


# ---------------------------------------------------------------------------
# Search result models
# ---------------------------------------------------------------------------

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
