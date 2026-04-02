"""Data models for knowledge retrieval and search."""

from __future__ import annotations

from dataclasses import dataclass, field
from collections.abc import Callable
from pydantic import BaseModel, Field, field_validator


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
        base_url:    Base URL prefix for resolving documentation links.
        trim_format: Whether to strip .md/.mdx suffix and ``docs/`` prefix
                     from the title before appending to *base_url*.
        suffix:      String appended to the path after trimming (e.g. ".tsp").
        link_fn:     Optional callable ``(title) -> str`` for sources that need
                     custom link logic (overrides *base_url* when set).
    """

    name: str
    description: str
    base_url: str = ""
    trim_format: bool = False
    suffix: str = ""
    link_fn: "Callable[[str], str] | None" = field(default=None, repr=False)

    def get_link(self, title: str) -> str:
        """Resolve the documentation URL for a chunk title."""
        if title.startswith("version-release-notes-index"):
            return "Please reference link from document content"
        if self.link_fn is not None:
            return self.link_fn(title)
        if not self.base_url:
            return ""
        path = title.replace("#", "/")
        if self.trim_format:
            path = _trim_file_format(path)
        return self.base_url + path + self.suffix

    def to_display_dict(self) -> dict[str, str]:
        """Return a minimal dict suitable for showing the LLM."""
        return {"name": self.name, "description": self.description}


def _trim_file_format(path: str) -> str:
    """Strip .md / .mdx suffix and leading ``docs/`` prefix."""
    for ext in (".md", ".mdx"):
        if path.endswith(ext):
            path = path[: -len(ext)]
    if path.startswith("docs/"):
        path = path[5:]
    return path


# ---------------------------------------------------------------------------
# Search result models
# ---------------------------------------------------------------------------

class KnowledgeChunk(BaseModel):
    """A single chunk returned from Azure AI Search.

    Field aliases allow direct construction from the search index
    ``source_data`` dict via ``model_validate(source_data)``.
    """

    model_config = {"populate_by_name": True}

    chunk_id: str = ""
    title: str = ""
    content: str = Field(default="", validation_alias="chunk")
    source: str = Field(default="", validation_alias="context_id")
    link: str = ""
    header1: str = Field(default="", validation_alias="header_1")
    header2: str = Field(default="", validation_alias="header_2")
    header3: str = Field(default="", validation_alias="header_3")
    rerank_score: float = Field(default=0.0, validation_alias="@search.reranker_score")

    @field_validator("rerank_score", mode="before")
    @classmethod
    def _coerce_rerank_score(cls, v: object) -> float:
        return float(v) if v is not None else 0.0


class KnowledgeResult(BaseModel):
    """Processed knowledge item ready for prompt injection."""

    title: str
    source: str
    link: str
    content: str

class Reference(BaseModel):
    """A reference to a document used to generate the answer."""
    title: str
    source: str
    link: str
    content: str = ""

    @field_validator("title", mode="after")
    @classmethod
    def _strip_trailing_pipes(cls, v: str) -> str:
        """Strip trailing pipe characters the LLM copies from search index titles."""
        return v.strip().rstrip("| ").strip()

class SearchKnowledgeBaseResult(BaseModel):
    """Output of the search_knowledge_base tool call."""
    results: list[Reference] = []
