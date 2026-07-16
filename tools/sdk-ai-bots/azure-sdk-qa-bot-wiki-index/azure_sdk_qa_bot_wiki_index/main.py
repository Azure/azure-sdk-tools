"""CLI — generate knowledge wiki pages and push them into the shared KB index.

Reads the same markdown corpus the KB indexer ingests from blob storage,
synthesises knowledge pages (per-document summary cards, cross-document entity
and concept pages), embeds them with the index's own embedding model, and
upserts them into the Azure AI Search index alongside the raw chunks.

Usage::

    python -m azure_sdk_qa_bot_wiki_index.main --pages summary --prefix typespec_docs/
    python -m azure_sdk_qa_bot_wiki_index.main --pages summary,entity,concept
    python -m azure_sdk_qa_bot_wiki_index.main --purge          # delete all wiki docs

Config (environment variables)::

    AI_SEARCH_BASE_URL             Azure AI Search endpoint
    AI_SEARCH_INDEX                target index (shared with the KB)
    STORAGE_BLOB_ENDPOINT          blob account endpoint
    STORAGE_KNOWLEDGE_CONTAINER    knowledge container name (default: knowledge)
    AZURE_OPENAI_ENDPOINT          Azure OpenAI endpoint
    WIKI_SYNTHESIS_DEPLOYMENT      chat deployment (default: gpt-5.4)
    WIKI_EMBEDDING_DEPLOYMENT      embedding deployment (default: text-embedding-ada-002)
"""

from __future__ import annotations

import argparse
import asyncio
import logging
import os

from azure.identity.aio import DefaultAzureCredential as AsyncDefaultAzureCredential
from azure.search.documents.aio import SearchClient as AzureSearchClient
from azure.storage.blob.aio import BlobServiceClient

from .build import (
    build_graph_pages,
    build_summary_cards,
    embed_docs,
)
from .documents import WikiDoc, delete_all_wiki_docs, push_docs
from .reader import read_blob_container
from .synthesis import Embedder, Synthesizer, build_azure_openai_client

logger = logging.getLogger(__name__)


def _env(name: str, default: str = "") -> str:
    return os.environ.get(name, default)


def _make_search_client(credential) -> AzureSearchClient:
    return AzureSearchClient(
        endpoint=_env("AI_SEARCH_BASE_URL").rstrip("/"),
        index_name=_env("AI_SEARCH_INDEX"),
        credential=credential,
    )


def _make_blob_service_client(credential) -> BlobServiceClient:
    endpoint = _env("STORAGE_BLOB_ENDPOINT")
    return BlobServiceClient(account_url=endpoint, credential=credential)


async def _run(args: argparse.Namespace) -> int:
    pages = {p.strip() for p in args.pages.split(",") if p.strip()}
    async with AsyncDefaultAzureCredential() as credential:
        search_client = _make_search_client(credential)
        async with search_client:
            if args.purge:
                removed = await delete_all_wiki_docs(search_client)
                logger.info("purged %d wiki docs", removed)
                if not pages:
                    return 0

            container = _env("STORAGE_KNOWLEDGE_CONTAINER", "knowledge")
            prefixes = [p.strip() for p in args.prefix.split(",") if p.strip()] or [""]
            blob_service = _make_blob_service_client(credential)
            corpus: list[tuple[str, str]] = []
            async with blob_service:
                container_client = blob_service.get_container_client(container)
                for pfx in prefixes:
                    corpus += await read_blob_container(container_client, prefix=pfx)
            if not corpus:
                logger.warning("no markdown found under prefixes %r", prefixes)
                return 0
            if args.limit and args.limit > 0:
                corpus = corpus[: args.limit]
                logger.info("limited corpus to first %d docs", len(corpus))

            aoai = build_azure_openai_client(_env("AZURE_OPENAI_ENDPOINT"))
            synth = Synthesizer(aoai, _env("WIKI_SYNTHESIS_DEPLOYMENT", "gpt-5.4"))
            embedder = Embedder(
                aoai, _env("WIKI_EMBEDDING_DEPLOYMENT", "text-embedding-ada-002")
            )

            docs: list[WikiDoc] = []
            if "summary" in pages:
                docs += build_summary_cards(corpus, synth)
            if "entity" in pages or "concept" in pages:
                docs += build_graph_pages(
                    corpus,
                    synth,
                    want_entity="entity" in pages,
                    want_concept="concept" in pages,
                )

            if not docs:
                logger.warning("no wiki docs generated")
                return 0

            embed_docs(docs, embedder)

            if args.dry_run:
                logger.info("dry-run: generated %d docs, not pushing", len(docs))
                for d in docs[:3]:
                    logger.info("sample [%s] %s\n%s", d.page_type, d.header_1, d.chunk[:400])
                return 0

            pushed = await push_docs(search_client, docs)
            logger.info("done: %d/%d wiki docs pushed", pushed, len(docs))
    return 0


def main() -> None:
    parser = argparse.ArgumentParser(description="Build KB wiki pages into Azure AI Search.")
    parser.add_argument(
        "--pages",
        default="summary",
        help="comma-separated page types to build: summary,entity,concept",
    )
    parser.add_argument("--prefix", default="", help="comma-separated blob name prefixes (e.g. typespec_docs/,typespec_azure_docs/)")
    parser.add_argument("--limit", type=int, default=0, help="cap number of source docs (0 = all)")
    parser.add_argument("--purge", action="store_true", help="delete all existing wiki docs first")
    parser.add_argument("--dry-run", action="store_true", help="generate + embed but do not push")
    parser.add_argument("--log-level", default="INFO")
    args = parser.parse_args()

    logging.basicConfig(
        level=getattr(logging, args.log_level.upper(), logging.INFO),
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )
    raise SystemExit(asyncio.run(_run(args)))


if __name__ == "__main__":
    main()
