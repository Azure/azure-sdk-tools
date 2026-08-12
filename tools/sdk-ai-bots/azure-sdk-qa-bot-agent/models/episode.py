"""Data models for expert experience episodes.

An **episode** is a structured problem-solution pair extracted from an
expert-resolved conversation thread.  It captures not just the answer but
the *reasoning chain* — the diagnostic steps an expert followed to reach
the resolution.

Episodes are stored in Cosmos DB with vector embeddings for similarity
search, giving full control over what is persisted and retrieved (no
Foundry memory-store double-extraction).
"""

from __future__ import annotations

import uuid
from datetime import datetime, timezone
from typing import Optional

from pydantic import BaseModel, Field


# ------------------------------------------------------------------
# Structured output from episode extraction
# ------------------------------------------------------------------

class Episode(BaseModel):
    """Structured episode extracted from an expert-resolved conversation thread.

    This is the schema the LLM is instructed to return as JSON.
    """

    trigger: str = Field(
        ...,
        description="The symptom or question that started the thread — what the user observed or asked.",
    )
    symptoms: list[str] = Field(
        default_factory=list,
        description="Observable signs of the problem (error messages, unexpected behavior, etc.).",
    )
    reasoning_chain: list[str] = Field(
        ...,
        min_length=2,
        description="Step-by-step diagnostic process the expert followed, in order.",
    )
    resolution: str = Field(
        ...,
        description="What ultimately fixed the problem or answered the question.",
    )
    key_insight: str = Field(
        ...,
        description="The generalizable takeaway — a principle that applies beyond this specific case.",
    )
    confidence: float = Field(
        default=1.0,
        ge=0.0,
        le=1.0,
        description="Agent's confidence that the extraction is accurate (0-1).",
    )


# JSON schema exposed to the LLM as the response_format
EPISODE_JSON_SCHEMA = Episode.model_json_schema()


# ------------------------------------------------------------------
# Cosmos DB document
# ------------------------------------------------------------------

class EpisodeDocument(BaseModel):
    """An episode persisted in the ``experience-episodes`` Cosmos DB container.

    Extends :class:`Episode` with storage-level fields: identifiers,
    provenance, timestamps, and a vector embedding for similarity search.

    The ``id`` is deterministic — derived from ``tenant_id`` and
    ``source_thread_id`` — so that upserts replace previous extractions
    as the thread grows and reaches resolution.
    """

    id: str = Field(..., description="Deterministic ID: episode-{tenant_id}-{source_thread_id}.")
    tenant_id: str = Field(..., description="Tenant the episode belongs to (partition key).")

    # Episode payload
    trigger: str
    symptoms: list[str] = Field(default_factory=list)
    reasoning_chain: list[str]
    resolution: str
    key_insight: str
    confidence: float = 1.0

    # Provenance
    source_thread_id: str = Field(
        ..., description="conversation_id of the source thread."
    )

    # Provenance metadata
    message_count: int = Field(
        default=0, description="Number of messages in the thread at extraction time.",
    )

    # Vector embedding (populated before storage)
    embedding: Optional[list[float]] = Field(
        default=None,
        description="Vector embedding of trigger + symptoms for similarity search.",
    )

    # Timestamps
    created_at: str = Field(
        default_factory=lambda: datetime.now(timezone.utc).isoformat(),
    )
    updated_at: str = Field(
        default_factory=lambda: datetime.now(timezone.utc).isoformat(),
    )

    @classmethod
    def from_episode(
        cls,
        episode: Episode,
        *,
        tenant_id: str,
        source_thread_id: str,
        embedding: list[float] | None = None,
        message_count: int = 0,
    ) -> EpisodeDocument:
        """Create a Cosmos DB document from a Memory-Agent episode."""
        doc_id = f"episode-{tenant_id}-{source_thread_id}"
        return cls(
            id=doc_id,
            tenant_id=tenant_id,
            trigger=episode.trigger,
            symptoms=list(episode.symptoms),
            reasoning_chain=list(episode.reasoning_chain),
            resolution=episode.resolution,
            key_insight=episode.key_insight,
            confidence=episode.confidence,
            source_thread_id=source_thread_id,
            message_count=message_count,
            embedding=embedding,
        )

    def to_searchable_text(self) -> str:
        """Return the text used to generate the vector embedding."""
        parts = [self.trigger]
        if self.symptoms:
            parts.extend(self.symptoms)
        return "\n".join(parts)
