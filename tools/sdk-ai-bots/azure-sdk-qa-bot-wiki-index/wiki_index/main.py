"""CLI for building and maintaining the generated wiki page set.

Usage::

    python -m wiki_index.main
"""

from __future__ import annotations

import argparse
import asyncio
import logging
from contextlib import suppress

from azure.core.exceptions import ResourceExistsError
from azure.identity.aio import DefaultAzureCredential as AsyncDefaultAzureCredential
from azure.storage.blob.aio import BlobServiceClient

from .config import get as cfg, load as load_config
from .llm import ChatLLM, build_azure_openai_client
from .reader import read_blob_container
from .reconcile import reconcile

logger = logging.getLogger(__name__)


def _make_blob_service_client(credential) -> BlobServiceClient:
    return BlobServiceClient(
        account_url=cfg("STORAGE_BLOB_ENDPOINT") or cfg("STORAGE_BASE_URL"),
        credential=credential,
    )


async def _read_corpus(credential) -> list[tuple[str, str]]:
    container = cfg("STORAGE_KNOWLEDGE_CONTAINER", "knowledge")
    blob_service = _make_blob_service_client(credential)
    async with blob_service:
        cc = blob_service.get_container_client(container)
        return await read_blob_container(cc)


async def _run(args: argparse.Namespace) -> int:
    async with AsyncDefaultAzureCredential() as credential:
        await load_config(credential)
        corpus = await _read_corpus(credential)
        if not corpus:
            logger.warning("no markdown found in knowledge container")
            return 0

        aoai = build_azure_openai_client(cfg("AZURE_OPENAI_ENDPOINT"))
        llm = ChatLLM(aoai, cfg("WIKI_SYNTHESIS_DEPLOYMENT", "gpt-5.6-sol"))

        # Incremental reconcile against the wiki container (durable + rebuildable).
        wiki_container = cfg("STORAGE_WIKI_OUTPUT_CONTAINER", "wiki")
        blob_service = _make_blob_service_client(credential)
        async with blob_service:
            cc = blob_service.get_container_client(wiki_container)
            with suppress(ResourceExistsError):
                await cc.create_container()
            stats = await reconcile(cc, corpus, llm, min_docs=args.min_docs)
        logger.info(
            "done: %d pages written, %d soft-deleted (container %r)",
            stats.pages_written, stats.pages_deleted, wiki_container,
        )
    return 0


def main() -> None:
    parser = argparse.ArgumentParser(description="Build the wiki page set.")
    parser.add_argument(
        "--min-docs",
        type=int,
        default=2,
        help="min source docs for an entity/concept page",
    )
    parser.add_argument("--log-level", default="INFO")
    args = parser.parse_args()

    logging.basicConfig(
        level=getattr(logging, args.log_level.upper(), logging.INFO),
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )
    raise SystemExit(asyncio.run(_run(args)))


if __name__ == "__main__":
    main()
