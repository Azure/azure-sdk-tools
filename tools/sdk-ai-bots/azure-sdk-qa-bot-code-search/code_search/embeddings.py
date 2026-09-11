from __future__ import annotations

import asyncio
import logging
import random

import cocoindex as coco
from openai import APIConnectionError, APITimeoutError, AsyncAzureOpenAI, RateLimitError

from .models import EmbeddingConfig

logger = logging.getLogger(__name__)

OPENAI_CLIENT = coco.ContextKey[AsyncAzureOpenAI]("azure-sdk-code-openai-client")
EMBEDDING_CONFIG = coco.ContextKey[EmbeddingConfig](
    "azure-sdk-code-embedding-config", detect_change=True
)
EMBEDDING_LIMITER = coco.ContextKey[asyncio.Semaphore](
    "azure-sdk-code-embedding-limiter"
)


@coco.fn(memo=True, version=1)
async def embed_content(content: str) -> tuple[float, ...]:
    client = coco.use_context(OPENAI_CLIENT)
    config = coco.use_context(EMBEDDING_CONFIG)
    limiter = coco.use_context(EMBEDDING_LIMITER)
    for attempt in range(10):
        try:
            async with limiter:
                response = await client.embeddings.create(
                    input=content,
                    model=config.deployment,
                    dimensions=config.dimensions,
                )
            vector = tuple(float(value) for value in response.data[0].embedding)
            if len(vector) != config.dimensions:
                raise RuntimeError(
                    f"embedding dimension mismatch: expected {config.dimensions}, "
                    f"got {len(vector)}"
                )
            return vector
        except (RateLimitError, APITimeoutError, APIConnectionError) as exc:
            if attempt == 9:
                raise
            delay = min(2**attempt, 30) + random.random()
            logger.warning(
                "embedding request failed with %s; retrying in %.1fs",
                type(exc).__name__,
                delay,
            )
            await asyncio.sleep(delay)
    raise RuntimeError("embedding retry loop exhausted")
